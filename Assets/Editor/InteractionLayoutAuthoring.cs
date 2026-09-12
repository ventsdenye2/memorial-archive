using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemorialArchive.Gameplay.Interaction.View;

namespace MemorialArchive.EditorTools
{
    /// <summary>Final placement pass over existing authored objects; does not regenerate scenes.</summary>
    public static class InteractionLayoutAuthoring
    {
        private const string ManifestPath = "Tools/SceneRebuild/interaction_layout.json";
        private const float WalkY = -5.2f;
        private const float CenterY = -4.6f;
        private const float Height = 2.4f;
        private const float MinimumGap = .25f;

        private sealed class Row
        {
            public string scene, id;
            public bool virtualPoint;
            public float x, width, minX, maxX, minY, maxY;
        }

        private static InteractionPointView[] ActivePoints(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<InteractionPointView>(false))
            .Where(p => p.enabled && p.gameObject.activeInHierarchy).ToArray();

        private static void VisitScenes(JObject manifest, Action<Scene, JArray> action)
        {
            var original = SceneManager.GetActiveScene();
            try
            {
                foreach (var sn in manifest["scenes"])
                {
                    var name = (string)sn["scene"];
                    var scene = SceneManager.GetSceneByName(name);
                    var wasLoaded = scene.IsValid() && scene.isLoaded;
                    if (!wasLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
                    try { action(scene, (JArray)sn["points"]); }
                    finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally { if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); }
        }

        [MenuItem("Tools/Memorial Archive/Apply Interaction Layout")]
        public static void Apply()
        {
            Guard();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits before applying interaction layout.");
            var manifest = LoadManifest();
            // Preflight every scene before saving any scene, so missing or unsupported components cannot cause a partial apply.
            VisitScenes(manifest, (scene, entries) =>
            {
                var points = ActivePoints(scene);
                foreach (var p in points)
                    if (entries.Count(e => (string)e["id"] == p.InteractionId && (bool?)e["virtual"] != true) != 1)
                        throw new InvalidOperationException(scene.name + ": missing or duplicate manifest entry " + p.InteractionId);
                foreach (var e in entries.Where(e => (bool?)e["virtual"] != true))
                {
                    var matches = points.Where(p => p.InteractionId == (string)e["id"]).ToArray();
                    if (matches.Length != 1 || !(matches[0].GetComponent<Collider2D>() is BoxCollider2D || matches[0].GetComponent<Collider2D>() is CircleCollider2D))
                        throw new InvalidOperationException(scene.name + ": expected one active point with box/circle trigger: " + e["id"]);
                    var scale = matches[0].transform.lossyScale;
                    if (Mathf.Abs(scale.x) < .001f || Mathf.Abs(scale.y) < .001f || (float)e["width"] <= 0)
                        throw new InvalidOperationException(scene.name + ": invalid scale/width: " + e["id"]);
                }
            });
            VisitScenes(manifest, (scene, entries) =>
            {
                var points = ActivePoints(scene).ToDictionary(p => p.InteractionId);
                foreach (var e in entries.Where(e => (bool?)e["virtual"] != true))
                {
                    var p = points[(string)e["id"]];
                    var t = p.transform;
                    t.position = new Vector3((float)e["x"], t.position.y, t.position.z);
                    var collider = p.GetComponent<Collider2D>();
                    var scale = t.lossyScale;
                    if (collider is BoxCollider2D box)
                    {
                        box.size = new Vector2((float)e["width"] / Mathf.Abs(scale.x), Height / Mathf.Abs(scale.y));
                        box.offset = new Vector2(0, (CenterY - t.position.y) / scale.y);
                    }
                    else if (collider is CircleCollider2D circle)
                    {
                        circle.radius = (float)e["width"] / (2 * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
                        circle.offset = new Vector2(0, (WalkY - t.position.y) / scale.y);
                    }
                    collider.isTrigger = true;
                    collider.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    EditorUtility.SetDirty(t);
                    EditorUtility.SetDirty(collider);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            });
            MemorialArchive.Editor.GuidePresentationBuilder.SyncInteractionLayout();
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Tools/Memorial Archive/Validate Interaction Layout")]
        public static void Validate()
        {
            Guard();
            var errors = new List<string>();
            var rows = new List<Row>();
            VisitScenes(LoadManifest(), (scene, entries) =>
            {
                Physics2D.SyncTransforms();
                var points = ActivePoints(scene);
                var local = new List<Row>();
                foreach (var p in points)
                    if (entries.Count(e => (string)e["id"] == p.InteractionId && (bool?)e["virtual"] != true) != 1)
                        errors.Add(scene.name + ": manifest match " + p.InteractionId);
                foreach (var e in entries)
                {
                    string id = (string)e["id"];
                    float x = (float)e["x"], width = (float)e["width"];
                    bool virt = (bool?)e["virtual"] == true;
                    var row = new Row { scene = scene.name, id = id, virtualPoint = virt, x = x, width = width,
                        minX = x - width / 2, maxX = x + width / 2, minY = CenterY - Height / 2, maxY = CenterY + Height / 2 };
                    if (!virt)
                    {
                        var matches = points.Where(p => p.InteractionId == id).ToArray();
                        if (matches.Length != 1) { errors.Add(scene.name + ": missing/duplicate active point " + id); continue; }
                        var p = matches[0];
                        var c = p.GetComponent<Collider2D>();
                        if (c == null) { errors.Add(scene.name + "/" + id + ": collider missing"); continue; }
                        if (!c.isTrigger || !c.isActiveAndEnabled) errors.Add(scene.name + "/" + id + ": trigger disabled");
                        var bounds = c.bounds;
                        row.x = p.transform.position.x;
                        row.width = bounds.size.x;
                        row.minX = bounds.min.x; row.maxX = bounds.max.x;
                        row.minY = bounds.min.y; row.maxY = bounds.max.y;
                        if (Mathf.Abs(row.x - x) > .01f || Mathf.Abs(row.width - width) > .01f)
                            errors.Add(scene.name + "/" + id + ": position/width differs from manifest");
                        if (row.minY > WalkY || row.maxY < WalkY) errors.Add(scene.name + "/" + id + ": walking height unreachable");
                    }
                    local.Add(row);
                }
                var sorted = local.OrderBy(r => r.minX).ToArray();
                for (int i = 0; i < sorted.Length; i++)
                    for (int j = i + 1; j < sorted.Length && sorted[j].minX - sorted[i].maxX < MinimumGap - .001f; j++)
                        errors.Add(scene.name + ": interaction ranges too close: " + sorted[i].id + " / " + sorted[j].id);
                var confiners = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider2D>(false))
                    .Where(c => c.name == "CameraConfiner" && c.isActiveAndEnabled && c.gameObject.activeInHierarchy).ToArray();
                if (confiners.Length != 1 && scene.name != "SampleScene") errors.Add(scene.name + ": expected one active confiner");
                if (confiners.Length == 1)
                    foreach (var row in local)
                        if (row.minX < confiners[0].bounds.min.x - .01f || row.maxX > confiners[0].bounds.max.x + .01f)
                            errors.Add(scene.name + "/" + row.id + ": outside confiner");
                rows.AddRange(local);
            });
            Directory.CreateDirectory("Temp/InteractionAudit");
            File.WriteAllText("Temp/InteractionAudit/after.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new { points = rows, errors, count = rows.Count }, Newtonsoft.Json.Formatting.Indented));
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("Interaction layout validation passed: " + rows.Count + " points (including virtual guide props).");
        }

        private static JObject LoadManifest() => JObject.Parse(File.ReadAllText(ManifestPath));
        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        }
    }
}
