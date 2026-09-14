#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MemorialArchive.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Replaces scene-owned panel objects with clean instances of the UI
    /// prefabs, removing stale scene overrides while preserving hierarchy.
    /// </summary>
    public static class PrefabPanelSceneNormalizer
    {
        private static readonly Dictionary<string, string> PanelPrefabPaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "MainMenuPanel", "Assets/Prefabs/UI/MainMenuPanel.prefab" },
                { "NewGameConfirmPanel", "Assets/Prefabs/UI/NewGameConfirmPanel.prefab" },
                { "SystemPanel", "Assets/Prefabs/UI/SystemPanel.prefab" },
                { "SettingsPanel", "Assets/Prefabs/UI/SettingsPanel.prefab" },
                { "SavePanel", "Assets/Prefabs/UI/SavePanel.prefab" },
                { "LoadPanel", "Assets/Prefabs/UI/LoadPanel.prefab" },
                { "GameplayHUD", "Assets/Prefabs/UI/GameplayHUD.prefab" },
                { "InventoryPanel", "Assets/Prefabs/UI/InventoryPanel.prefab" },
                { "DiaryPanel", "Assets/Prefabs/UI/DiaryPanel.prefab" },
                { "MapPanel", "Assets/Prefabs/UI/MapPanel.prefab" },
                { "GuideOverlayPanel", "Assets/Prefabs/UI/GuideOverlayPanel.prefab" },
                { "NarrativePanel", "Assets/Prefabs/UI/NarrativePanel.prefab" },
                { "OpeningDialoguePanel", "Assets/Prefabs/UI/OpeningDialoguePanel.prefab" },
                { "ContainerPanel", "Assets/Prefabs/UI/ContainerPanel.prefab" },
                { "ShortcutBarPanel", "Assets/Prefabs/UI/ShortcutBarPanel.prefab" },
                { "StairTravelPanel", "Assets/Prefabs/UI/StairTravelPanel.prefab" },
                { "BlackScreenStoryPanel", "Assets/Prefabs/UI/BlackScreenStoryPanel.prefab" }
            };

        [MenuItem("Tools/Memorial Archive/Normalize Scene Panel Prefab Instances")]
        public static void NormalizeAllScenePanels()
        {
            AssetDatabase.Refresh();
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            var changedScenes = 0;

            try
            {
                for (var i = 0; i < sceneGuids.Length; i++)
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    if (NormalizeScene(scenePath)) changedScenes++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Normalized UI panel prefab instances in {changedScenes} scene(s).");
        }

        private static bool NormalizeScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) return false;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var candidates = FindPanelCandidates(scene);
            if (candidates.Count == 0) return false;

            var bindings = CaptureRegistryBindings(scene);
            var replacements = new Dictionary<int, BasePanel>();

            foreach (var oldPanel in candidates)
            {
                var oldObject = oldPanel.gameObject;
                var parent = oldObject.transform.parent;
                var siblingIndex = oldObject.transform.GetSiblingIndex();
                var prefabPath = PanelPrefabPaths[oldObject.name];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Panel prefab was not found: {prefabPath}");
                }

                var oldInstanceId = oldPanel.GetInstanceID();
                UnityEngine.Object.DestroyImmediate(oldObject);

                var replacement = parent == null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                replacement.name = prefab.name;
                replacement.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, replacement.transform.parent == null
                    ? scene.rootCount - 1
                    : replacement.transform.parent.childCount - 1));

                var newPanel = replacement.GetComponent<BasePanel>();
                if (newPanel == null)
                {
                    throw new InvalidOperationException($"Panel prefab has no BasePanel component: {prefabPath}");
                }

                replacements[oldInstanceId] = newPanel;
            }

            RestoreRegistryBindings(bindings, replacements);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static List<BasePanel> FindPanelCandidates(Scene scene)
        {
            var candidates = new List<BasePanel>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var panel in root.GetComponentsInChildren<BasePanel>(true))
                {
                    if (!PanelPrefabPaths.ContainsKey(panel.gameObject.name)) continue;

                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
                    var isPrefabInstance = string.Equals(prefabPath, PanelPrefabPaths[panel.gameObject.name], StringComparison.Ordinal);
                    var isUnpackedPanel = string.IsNullOrEmpty(prefabPath);
                    if (isPrefabInstance || isUnpackedPanel) candidates.Add(panel);
                }
            }

            return candidates;
        }

        private sealed class RegistryBinding
        {
            public ScenePanelRegistry Registry;
            public int Index;
            public int OldPanelInstanceId;
        }

        private static List<RegistryBinding> CaptureRegistryBindings(Scene scene)
        {
            var bindings = new List<RegistryBinding>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var registry in root.GetComponentsInChildren<ScenePanelRegistry>(true))
                {
                    var serialized = new SerializedObject(registry);
                    var panels = serialized.FindProperty("panels");
                    if (panels == null) continue;

                    for (var i = 0; i < panels.arraySize; i++)
                    {
                        var panel = panels.GetArrayElementAtIndex(i).objectReferenceValue as BasePanel;
                        if (panel == null) continue;
                        bindings.Add(new RegistryBinding
                        {
                            Registry = registry,
                            Index = i,
                            OldPanelInstanceId = panel.GetInstanceID()
                        });
                    }
                }
            }

            return bindings;
        }

        private static void RestoreRegistryBindings(List<RegistryBinding> bindings,
            Dictionary<int, BasePanel> replacements)
        {
            foreach (var binding in bindings)
            {
                if (!replacements.TryGetValue(binding.OldPanelInstanceId, out var replacement)) continue;

                var serialized = new SerializedObject(binding.Registry);
                var panels = serialized.FindProperty("panels");
                if (panels == null || binding.Index >= panels.arraySize) continue;
                panels.GetArrayElementAtIndex(binding.Index).objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
