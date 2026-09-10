using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Lighting.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.EditorTools
{
    /// <summary>Synchronizes only ordinary light nodes from the reviewed artwork coordinates.</summary>
    public static class OrdinaryLightLayoutSynchronizer
    {
        [Serializable] private sealed class Layout { public SceneLayout[] scenes; }
        [Serializable] private sealed class SceneLayout
        {
            public string scene;
            public float left;
            public Position[] lights;
        }
        [Serializable] private sealed class Position { public float x; public float y; }

        private const string ConfigFolder = "Assets/GameConfigs/SceneLayout/Lights/";

        [MenuItem("Tools/Memorial Archive/Apply Ordinary Light Positions")]
        public static void Apply() => Process(true);

        [MenuItem("Tools/Memorial Archive/Validate Ordinary Light Positions")]
        public static void Validate() => Process(false);

        private static void Process(bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before synchronizing ordinary lights.");

            var plan = JsonUtility.FromJson<Layout>(File.ReadAllText("Tools/SceneRebuild/layout.json"));
            foreach (var entry in plan.scenes)
            {
                var open = SceneManager.GetSceneByName(entry.scene);
                if (open.IsValid() && open.isLoaded && open.isDirty)
                    throw new InvalidOperationException("Save scene changes first: " + entry.scene);
            }

            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            var configs = new List<LightSourceConfig>();
            var managedPrefixes = new List<string>();
            var total = 0;
            foreach (var entry in plan.scenes)
            {
                var scene = SceneManager.GetSceneByName(entry.scene);
                var wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + entry.scene + ".unity", OpenSceneMode.Additive);
                try
                {
                    var parent = scene.GetRootGameObjects().Single(r => r.name == "SceneLayout0909").transform.Find("NormalLights");
                    var prefix = "light_" + entry.scene.ToLowerInvariant() + "_ordinary_";
                    managedPrefixes.Add(prefix);
                    var template = parent.GetComponentsInChildren<LightSourceView>(true).First();
                    var templateConfig = AssetDatabase.LoadAssetAtPath<LightSourceConfig>(ConfigFolder + prefix + "01.asset");
                    var expected = new HashSet<string>();
                    for (var index = 0; index < entry.lights.Length; index++)
                    {
                        var id = prefix + (index + 1).ToString("00");
                        expected.Add(id);
                        var node = parent.Find(id);
                        if (node == null && apply)
                        {
                            node = UnityEngine.Object.Instantiate(template.gameObject, parent).transform;
                            node.name = id;
                            SetString(node.GetComponent<LightSourceView>(), "lightId", id);
                            SetString(node.GetComponent<InteractionPointView>(), "interactionId", id);
                        }
                        if (node == null) throw new InvalidOperationException("Missing ordinary light: " + id);
                        var position = new Vector3(entry.left + entry.lights[index].x / 100f, 5.4f - entry.lights[index].y / 100f, 0);
                        var trigger = node.GetComponent<BoxCollider2D>();
                        if (apply)
                        {
                            node.position = position;
                            // Move the visible origin to the wall lamp while keeping F reachable from the floor.
                            trigger.offset = new Vector2(trigger.offset.x, -3.4f - position.y);
                        }
                        if ((node.position - position).sqrMagnitude > 0.000001f)
                            throw new InvalidOperationException("Ordinary light differs from artwork coordinates: " + id);
                        if (!trigger.isTrigger || trigger.bounds.min.y > -5.2f || trigger.bounds.max.y < -5.2f)
                            throw new InvalidOperationException("Ordinary light trigger is not reachable: " + id);

                        var path = ConfigFolder + id + ".asset";
                        var config = AssetDatabase.LoadAssetAtPath<LightSourceConfig>(path);
                        if (config == null && apply)
                        {
                            config = UnityEngine.Object.Instantiate(templateConfig);
                            config.name = id;
                            SetString(config, "lightId", id);
                            AssetDatabase.CreateAsset(config, path);
                        }
                        if (config == null || config.IsSpecial || config.LightId != id)
                            throw new InvalidOperationException("Invalid ordinary light config: " + id);
                        configs.Add(config);
                        total++;
                    }

                    foreach (var node in parent.GetComponentsInChildren<LightSourceView>(true))
                    {
                        if (expected.Contains(node.name)) continue;
                        if (!apply) throw new InvalidOperationException("Extra ordinary light: " + node.name);
                        UnityEngine.Object.DestroyImmediate(node.gameObject);
                    }
                    if (apply)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            }

            if (apply)
            {
                var serialized = new SerializedObject(database);
                var array = serialized.FindProperty("lightSources");
                var retained = database.LightSources.Where(c => c != null && !managedPrefixes.Any(p => c.LightId.StartsWith(p, StringComparison.Ordinal))).ToList();
                retained.AddRange(configs);
                array.arraySize = retained.Count;
                for (var i = 0; i < retained.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = retained[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            else if (configs.Any(c => !database.LightSources.Contains(c)))
                throw new InvalidOperationException("An ordinary light config is missing from GameConfigDatabase.");

            Debug.Log($"Ordinary light positions {(apply ? "applied" : "validated")}: {total} across {plan.scenes.Length} scenes.");
        }

        private static void SetString(UnityEngine.Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
