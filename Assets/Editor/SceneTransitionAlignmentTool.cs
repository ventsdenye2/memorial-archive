#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Audits bidirectional physical scene transitions and aligns each arrival spawn
    /// with the reciprocal exit in the destination scene.
    /// </summary>
    public static class SceneTransitionAlignmentTool
    {
        private const float PositionTolerance = 0.001f;

        private sealed class SceneData
        {
            public string Name;
            public string Path;
            public readonly Dictionary<string, Vector3> Spawns = new Dictionary<string, Vector3>();
            public readonly List<TransitionEdge> Edges = new List<TransitionEdge>();
        }

        private sealed class TransitionEdge
        {
            public string InteractionId;
            public string SourceScene;
            public Vector3 SourcePosition;
            public string TargetScene;
            public string TargetSpawnId;
        }

        private sealed class Alignment
        {
            public string TargetScenePath;
            public string TargetScene;
            public string TargetSpawnId;
            public Vector3 CurrentPosition;
            public Vector3 ExpectedPosition;
            public string ReciprocalInteractionId;
        }

        private sealed class AuditResult
        {
            public readonly List<Alignment> Mismatches = new List<Alignment>();
            public readonly List<string> Errors = new List<string>();
            public int EdgeCount;
            public int AlignedCount;
        }

        [MenuItem("Tools/Memorial Archive/Scene Transitions/Audit Alignment")]
        public static void Audit()
        {
            var result = Analyze();
            LogResult("Scene transition audit", result);
        }

        public static string GetAuditReport()
        {
            var result = Analyze();
            return BuildReport("Scene transition audit", result);
        }

        [MenuItem("Tools/Memorial Archive/Scene Transitions/Align Arrival Spawns")]
        public static void AlignArrivalSpawns()
        {
            var initial = Analyze();
            if (initial.Errors.Count > 0)
            {
                LogResult("Scene transition alignment stopped", initial);
                throw new InvalidOperationException("Scene transition graph contains unresolved errors; no spawn points were changed.");
            }

            foreach (var group in initial.Mismatches.GroupBy(item => item.TargetScenePath))
            {
                ApplySceneAlignments(group.Key, group.ToArray());
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var final = Analyze();
            LogResult("Scene transition alignment complete", final);
            if (final.Errors.Count > 0 || final.Mismatches.Count > 0)
            {
                throw new InvalidOperationException("Scene transition alignment did not converge. Check the Unity console report.");
            }
        }

        private static AuditResult Analyze()
        {
            var configs = LoadInteractionConfigs();
            var scenes = LoadSceneData(configs);
            var scenesByName = scenes.ToDictionary(scene => scene.Name, StringComparer.Ordinal);
            var result = new AuditResult();

            foreach (var sourceScene in scenes)
            {
                foreach (var edge in sourceScene.Edges)
                {
                    result.EdgeCount++;
                    if (!scenesByName.TryGetValue(edge.TargetScene, out var targetScene))
                    {
                        result.Errors.Add($"{edge.SourceScene}/{edge.InteractionId}: target scene '{edge.TargetScene}' was not found.");
                        continue;
                    }

                    if (!targetScene.Spawns.TryGetValue(edge.TargetSpawnId, out var targetSpawnPosition))
                    {
                        result.Errors.Add($"{edge.SourceScene}/{edge.InteractionId}: spawn '{edge.TargetSpawnId}' was not found in {edge.TargetScene}.");
                        continue;
                    }

                    var reciprocals = targetScene.Edges
                        .Where(candidate => string.Equals(candidate.TargetScene, edge.SourceScene, StringComparison.Ordinal))
                        .ToArray();
                    if (reciprocals.Length == 0)
                    {
                        result.Errors.Add($"{edge.SourceScene}/{edge.InteractionId}: {edge.TargetScene} has no reciprocal exit back to {edge.SourceScene}.");
                        continue;
                    }

                    if (reciprocals.Length > 1)
                    {
                        result.Errors.Add($"{edge.SourceScene}/{edge.InteractionId}: {edge.TargetScene} has {reciprocals.Length} reciprocal exits to {edge.SourceScene}; pairing is ambiguous.");
                        continue;
                    }

                    var reciprocal = reciprocals[0];
                    if (Vector3.Distance(targetSpawnPosition, reciprocal.SourcePosition) <= PositionTolerance)
                    {
                        result.AlignedCount++;
                        continue;
                    }

                    result.Mismatches.Add(new Alignment
                    {
                        TargetScenePath = targetScene.Path,
                        TargetScene = targetScene.Name,
                        TargetSpawnId = edge.TargetSpawnId,
                        CurrentPosition = targetSpawnPosition,
                        ExpectedPosition = reciprocal.SourcePosition,
                        ReciprocalInteractionId = reciprocal.InteractionId
                    });
                }
            }

            return result;
        }

        private static Dictionary<string, InteractionConfig> LoadInteractionConfigs()
        {
            var result = new Dictionary<string, InteractionConfig>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:InteractionConfig", new[] { "Assets/GameConfigs/Interaction" }))
            {
                var config = AssetDatabase.LoadAssetAtPath<InteractionConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null || string.IsNullOrEmpty(config.InteractionId))
                {
                    continue;
                }

                if (result.ContainsKey(config.InteractionId))
                {
                    throw new InvalidOperationException($"Duplicate InteractionConfig id: {config.InteractionId}");
                }

                result.Add(config.InteractionId, config);
            }

            return result;
        }

        private static List<SceneData> LoadSceneData(IReadOnlyDictionary<string, InteractionConfig> configs)
        {
            var result = new List<SceneData>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                var scene = SceneManager.GetSceneByPath(scenePath);
                var wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                try
                {
                    var data = new SceneData { Name = scene.name, Path = scenePath };
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var spawn in root.GetComponentsInChildren<SceneSpawnPoint>(true))
                        {
                            if (string.IsNullOrEmpty(spawn.PointId))
                            {
                                continue;
                            }

                            if (data.Spawns.ContainsKey(spawn.PointId))
                            {
                                throw new InvalidOperationException($"Duplicate spawn id '{spawn.PointId}' in {scene.name}.");
                            }

                            data.Spawns.Add(spawn.PointId, spawn.transform.position);
                        }

                        foreach (var point in root.GetComponentsInChildren<InteractionPointView>(true))
                        {
                            if (!configs.TryGetValue(point.InteractionId, out var config) || config.InteractionType != InteractionType.SceneExit)
                            {
                                continue;
                            }

                            AddConfiguredEdges(data, point, config);
                        }
                    }

                    result.Add(data);
                }
                finally
                {
                    if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            return result;
        }

        private static void AddConfiguredEdges(SceneData scene, InteractionPointView point, InteractionConfig config)
        {
            AddEdge(scene, point, config.TransitionSceneId, config.TransitionSpawnPointId);
            AddEdge(scene, point, config.StairUpSceneId, config.StairUpSpawnPointId);
            AddEdge(scene, point, config.StairDownSceneId, config.StairDownSpawnPointId);
        }

        private static void AddEdge(SceneData scene, InteractionPointView point, string targetScene, string targetSpawn)
        {
            if (string.IsNullOrEmpty(targetScene) && string.IsNullOrEmpty(targetSpawn))
            {
                return;
            }

            scene.Edges.Add(new TransitionEdge
            {
                InteractionId = point.InteractionId,
                SourceScene = scene.Name,
                SourcePosition = point.transform.position,
                TargetScene = targetScene,
                TargetSpawnId = targetSpawn
            });
        }

        private static void ApplySceneAlignments(string scenePath, IReadOnlyList<Alignment> alignments)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                var spawns = new Dictionary<string, SceneSpawnPoint>(StringComparer.Ordinal);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var spawn in root.GetComponentsInChildren<SceneSpawnPoint>(true))
                    {
                        if (!string.IsNullOrEmpty(spawn.PointId))
                        {
                            spawns[spawn.PointId] = spawn;
                        }
                    }
                }

                foreach (var alignment in alignments)
                {
                    if (!spawns.TryGetValue(alignment.TargetSpawnId, out var spawn))
                    {
                        throw new InvalidOperationException($"Spawn disappeared during alignment: {scene.name}/{alignment.TargetSpawnId}");
                    }

                    Undo.RecordObject(spawn.transform, "Align scene arrival spawn");
                    spawn.transform.position = alignment.ExpectedPosition;
                    EditorUtility.SetDirty(spawn.transform);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void LogResult(string title, AuditResult result)
        {
            var message = BuildReport(title, result);
            if (result.Errors.Count > 0)
            {
                Debug.LogError(message);
            }
            else if (result.Mismatches.Count > 0)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static string BuildReport(string title, AuditResult result)
        {
            var lines = new List<string>
            {
                $"[{title}] edges={result.EdgeCount}, aligned={result.AlignedCount}, mismatches={result.Mismatches.Count}, errors={result.Errors.Count}"
            };
            lines.AddRange(result.Mismatches.Select(item =>
                $"MISMATCH {item.TargetScene}/{item.TargetSpawnId}: {item.CurrentPosition} -> {item.ExpectedPosition} (exit {item.ReciprocalInteractionId})"));
            lines.AddRange(result.Errors.Select(error => "ERROR " + error));
            return string.Join("\n", lines);
        }
    }
}
#endif
