#if UNITY_EDITOR
using System;
using MemorialArchive.Framework.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Writes the UI2.0 authored hierarchy and sprite references into the save
    /// and load prefabs. Runtime panels only bind this authored surface; they do
    /// not create slot UI at runtime.
    /// </summary>
    public static class UI2SaveMigration
    {
        private const string Ui2Root = "Assets/Art/UI/Imported_UI2.0/UI2.0";
        private const string PrefabRoot = "Assets/Prefabs/UI";
        private const float PixelsPerUnit = 100f;

        private sealed class SaveArt
        {
            public Sprite Mask;
            public Sprite Chest;
            public Sprite[] NormalSlots;
            public Sprite[] SelectedSlots;
            public Sprite Cancel;
        }

        [MenuItem("Tools/Memorial Archive/Apply UI2.0 Save Layout")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var art = LoadSaveArt();

            ApplyPrefab($"{PrefabRoot}/SavePanel.prefab", root => ConfigureSavePanel(root, art));
            ApplyPrefab($"{PrefabRoot}/LoadPanel.prefab", root => ConfigureLoadPanel(root, art));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied UI2.0 authored save/load layout to SavePanel and LoadPanel.");
        }

        private static SaveArt LoadSaveArt()
        {
            return new SaveArt
            {
                Mask = LoadSprite("存档页", "透明度蒙版.png"),
                Chest = LoadSprite("存档页", "箱子.png"),
                NormalSlots = new[]
                {
                    LoadSprite("存档页", "存档一.png"),
                    LoadSprite("存档页", "存档2.png"),
                    LoadSprite("存档页", "存档三.png"),
                    LoadSprite("存档页", "存档四.png")
                },
                SelectedSlots = new[]
                {
                    LoadSprite("存档页", "存档一 (选中）.png"),
                    LoadSprite("存档页", "存档2 （选中）.png"),
                    LoadSprite("存档页", "存档三 （选中）.png"),
                    LoadSprite("存档页", "存档四 （选中）.png")
                },
                Cancel = LoadSprite("存档页", "取消键.png")
            };
        }

        private static Sprite LoadSprite(string folder, string file)
        {
            var path = $"{Ui2Root}/{folder}/{file}";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                throw new InvalidOperationException($"UI2.0 asset was not found: {path}");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer was not found: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Sprite could not be loaded: {path}");
            }

            return sprite;
        }

        private static void ApplyPrefab(string path, Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException($"Prefab could not be opened: {path}");
            }

            try
            {
                configure(root);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureSavePanel(GameObject root, SaveArt art)
        {
            var panel = root.GetComponent<SavePanel>();
            if (panel == null)
            {
                throw new InvalidOperationException("SavePanel component is missing from its prefab.");
            }

            AssignArt(panel, art);
            panel.RebuildPrefabLayoutForEditor();
            ValidateAuthoredLayout(root, "SavePanel");
        }

        private static void ConfigureLoadPanel(GameObject root, SaveArt art)
        {
            var panel = root.GetComponent<LoadPanel>();
            if (panel == null)
            {
                throw new InvalidOperationException("LoadPanel component is missing from its prefab.");
            }

            AssignArt(panel, art);
            panel.RebuildPrefabLayoutForEditor();
            ValidateAuthoredLayout(root, "LoadPanel");
        }

        private static void AssignArt(UnityEngine.Object panel, SaveArt art)
        {
            var serialized = new SerializedObject(panel);
            SetObject(serialized, "maskSprite", art.Mask);
            SetObject(serialized, "chestSprite", art.Chest);
            SetArray(serialized, "slotNormalSprites", art.NormalSlots);
            SetArray(serialized, "slotSelectedSprites", art.SelectedSlots);
            SetObject(serialized, "cancelSprite", art.Cancel);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized field was not found: {serialized.targetObject.name}.{propertyName}");
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string propertyName, Sprite[] values)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException($"Serialized sprite array was not found: {serialized.targetObject.name}.{propertyName}");
            }

            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void ValidateAuthoredLayout(GameObject root, string panelName)
        {
            var surface = root.transform.Find("SaveSlotSurface");
            if (surface == null)
            {
                throw new InvalidOperationException($"{panelName} migration did not create SaveSlotSurface.");
            }

            for (var slotIndex = 1; slotIndex <= 3; slotIndex++)
            {
                var slot = surface.Find("Slot_" + slotIndex);
                if (slot == null || slot.GetComponent<Button>() == null ||
                    slot.Find("SlotLabel")?.GetComponent<Text>() == null)
                {
                    throw new InvalidOperationException($"{panelName} migration did not create authored Slot_{slotIndex}.");
                }
            }

            var reservedSlot = surface.Find("Slot_4");
            if (reservedSlot == null || reservedSlot.GetComponent<Image>() == null ||
                reservedSlot.Find("SlotLabel")?.GetComponent<Text>() == null)
            {
                throw new InvalidOperationException($"{panelName} migration did not create authored Slot_4.");
            }

            if (surface.Find("Status")?.GetComponent<Text>() == null)
            {
                throw new InvalidOperationException($"{panelName} migration did not create Status.");
            }

            if (surface.Find("CancelButton")?.GetComponent<Button>() == null)
            {
                throw new InvalidOperationException($"{panelName} migration did not create CancelButton.");
            }
        }
    }
}
#endif
