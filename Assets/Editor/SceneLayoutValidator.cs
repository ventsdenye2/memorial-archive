using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.EditorTools
{
    /// <summary>Read-only audit of the authored gameplay layout and registered resources.</summary>
    public static class SceneLayoutValidator
    {
        [MenuItem("Tools/Memorial Archive/Validate Scene Layout")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before validating scene assets.");
            var names = new[] { "FrontHall", "Room_Office", "Room_Reception", "Room_ArchiveA", "Room_ArchiveB", "Room_ArchiveC", "Room_Director", "Room_TreatmentA", "Room_TreatmentB", "Room_Toilet", "Room_Terrace", "Floor_1F", "Floor_2F", "Floor_3F", "Floor_4F" };
            var db = AssetDatabase.LoadAssetAtPath<MemorialArchive.Framework.Config.GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            var rows = new System.Collections.Generic.List<object>();
            var errors = new System.Collections.Generic.List<string>();
            var spawnsByScene = new System.Collections.Generic.Dictionary<string, string[]>();
            var routes = new System.Collections.Generic.List<string[]>();
            foreach (var name in names)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
                var wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    spawnsByScene[name] = roots.SelectMany(r => r.GetComponentsInChildren<MemorialArchive.Framework.Scene.SceneSpawnPoint>(false))
                        .Where(p => p.gameObject.activeInHierarchy)
                        .Select(p => new SerializedObject(p).FindProperty("pointId").stringValue).ToArray();
                    var points = roots.SelectMany(r => r.GetComponentsInChildren<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>(false)).Where(c=>c.gameObject.activeInHierarchy).ToArray();
                    var bounds = roots.SelectMany(r=>r.GetComponentsInChildren<Collider2D>(false)).Where(c=>c.name=="CameraConfiner" && c.enabled).ToArray();
                    var missing = roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                    var renderers = roots.SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(false)).Where(c=>c.gameObject.activeInHierarchy).ToArray();
                    var lights = roots.SelectMany(r=>r.GetComponentsInChildren<MemorialArchive.Gameplay.Lighting.View.LightSourceView>(false)).Where(c=>c.gameObject.activeInHierarchy).ToArray();
                    var monsters = roots.SelectMany(r=>r.GetComponentsInChildren<MemorialArchive.Gameplay.Monster.View.MonsterSpawnPointView>(false)).Where(c=>c.gameObject.activeInHierarchy).ToArray();
                    if(missing>0)errors.Add(name+": missing scripts "+missing);
                    if(bounds.Length!=1)errors.Add(name+": camera bounds count "+bounds.Length);
                    foreach(var p in points)
                    {
                        if(p.GetComponent<Collider2D>()==null)errors.Add(name+": trigger missing "+p.InteractionId);
                        if(p.InteractionType!=MemorialArchive.Gameplay.Interaction.Data.InteractionType.LightSource && !db.Interactions.Any(c=>c!=null && c.InteractionId==p.InteractionId))errors.Add(name+": config missing "+p.InteractionId);
                        if(bounds.Length==1 && (p.transform.position.x<bounds[0].bounds.min.x || p.transform.position.x>bounds[0].bounds.max.x))errors.Add(name+": point outside bounds "+p.InteractionId);
                        var trigger = p.GetComponent<Collider2D>();
                        if (trigger != null && (!trigger.isTrigger || trigger.bounds.min.y > -5.2f || trigger.bounds.max.y < -5.2f))
                            errors.Add(name + ": trigger does not cover walking height " + p.InteractionId);
                        var config = db.Interactions.FirstOrDefault(c => c != null && c.InteractionId == p.InteractionId);
                        if (config != null)
                        {
                            if (!string.IsNullOrEmpty(config.TransitionSceneId)) routes.Add(new[] { name, p.InteractionId, config.TransitionSceneId, config.TransitionSpawnPointId });
                            if (!string.IsNullOrEmpty(config.StairUpSceneId)) routes.Add(new[] { name, p.InteractionId, config.StairUpSceneId, config.StairUpSpawnPointId });
                            if (!string.IsNullOrEmpty(config.StairDownSceneId)) routes.Add(new[] { name, p.InteractionId, config.StairDownSceneId, config.StairDownSpawnPointId });
                        }
                    }
                    foreach(var group in points.GroupBy(p=>p.InteractionId).Where(g=>g.Count()>1))errors.Add(name+": duplicate point "+group.Key);
                    foreach(var r in renderers)
                    {
                        if(r.sprite==null)errors.Add(name+": sprite missing "+r.name);
                        if(r.transform.IsChildOf(roots.First(t=>t.name=="SceneLayout0909").transform) && r.name=="Background" && Mathf.Abs(r.transform.lossyScale.x-1)>0.001f)
                            errors.Add(name+": background stretched "+r.name);
                    }
                    foreach(var spawn in roots.SelectMany(r=>r.GetComponentsInChildren<MemorialArchive.Framework.Scene.SceneSpawnPoint>(false)).Where(c=>c.gameObject.activeInHierarchy))
                        if(bounds.Length==1 && (spawn.transform.position.x<bounds[0].bounds.min.x || spawn.transform.position.x>bounds[0].bounds.max.x))errors.Add(name+": spawn outside bounds "+spawn.name);
                    foreach(var l in lights)
                    {
                        var id = new SerializedObject(l).FindProperty("lightId").stringValue;
                        if(!db.LightSources.Any(c=>c!=null && c.LightId==id))errors.Add(name+": light config missing "+id);
                    }
                    foreach(var m in monsters)if(m.MonsterPrefab==null)errors.Add(name+": monster prefab missing "+m.SpawnPointId);
                    rows.Add(new {scene=name,points=points.Length,saves=points.Count(p=>p.InteractionType==MemorialArchive.Gameplay.Interaction.Data.InteractionType.SavePoint),notes=points.Count(p=>p.InteractionType==MemorialArchive.Gameplay.Interaction.Data.InteractionType.NotePickup),lights=lights.Length,monsters=monsters.Length,renderers=renderers.Length,bounds=bounds.Select(c=>new {center=c.bounds.center.ToString(),size=c.bounds.size.ToString()}).ToArray()});
                }
                finally { if (!wasLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
            }
            foreach (var route in routes)
                if (!spawnsByScene.TryGetValue(route[2], out var targets) || !targets.Contains(route[3]))
                    errors.Add("Missing transition destination: " + string.Join(" -> ", route));
            var result = new { scenes=rows, routes=routes, errors=errors };
            System.IO.Directory.CreateDirectory("Temp/SceneRebuildAudit");
            System.IO.File.WriteAllText("Temp/SceneRebuildAudit/unity_scene_audit.json", Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            if(errors.Count>0) throw new System.InvalidOperationException(string.Join("\n",errors));
            Debug.Log("Scene layout validation passed: "+rows.Count+" scenes. Report: Temp/SceneRebuildAudit/unity_scene_audit.json");
        }
    }
}
