#if UNITY_EDITOR
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// One-shot editor migration for the UI2.0 inventory artwork. The prefab
    /// remains fully authored after Apply; runtime code only refreshes its
    /// slot contents and visibility.
    /// </summary>
    public static class UI2InventoryMigration
    {
        private const string InventoryPrefabPath = "Assets/Prefabs/UI/InventoryPanel.prefab";
        private const string SlotPrefabPath = "Assets/Prefabs/UI/InventorySlot.prefab";
        private const string ShortcutPrefabPath = "Assets/Prefabs/UI/ShortcutBarPanel.prefab";
        private const string ArtRoot = "Assets/Art/UI/Imported_UI2.0/UI2.0/背包/";
        private const string LegacyArtRoot = "Assets/Art/UI/背包页/";

        [MenuItem("Tools/Memorial Archive/Apply UI2.0 Inventory Layout")]
        public static void Apply()
        {
            ApplyInventoryPanel();
            ApplyInventorySlot();
            ApplyShortcutBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("UI2.0 inventory, slot, and shortcut prefabs applied.");
        }

        private static void ApplyInventoryPanel()
        {
            var root = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                var panel = root.GetComponent<InventoryPanel>();
                if (panel == null)
                {
                    throw new System.InvalidOperationException("InventoryPanel component is missing from its prefab.");
                }

                var serialized = new SerializedObject(panel);
                SetObject(serialized, "maskSprite", LoadSprite(LegacyArtRoot + "蒙版图层(背包页）.png"));
                SetObject(serialized, "scenePanelSprite", LoadSprite(ArtRoot + "物品.png"));
                SetObject(serialized, "backpackPanelSprite", LoadSprite(ArtRoot + "背包.png"));
                SetObject(serialized, "slotSprite", LoadSprite(ArtRoot + "背包格子.png"));
                SetObject(serialized, "selectedSlotSprite", LoadSprite(ArtRoot + "背包格（选中）.png"));
                SetObject(serialized, "hudSlotSprite", LoadSprite(ArtRoot + "装备格子.png"));
                SetObject(serialized, "hudSelectedSlotSprite", LoadSprite(ArtRoot + "装备格子（选中）.png"));
                // The parchment is baked into 背包.png in UI2.0.
                SetObject(serialized, "descriptionSprite", null);
                SetObject(serialized, "closeSprite", LoadSprite(ArtRoot + "退出键.png"));
                SetObject(serialized, "selectSprite", LoadSprite(ArtRoot + "装备.png"));
                SetObject(serialized, "selectHighlightedSprite", LoadSprite(ArtRoot + "装备选中.png"));
                SetObject(serialized, "cancelSprite", LoadSprite(ArtRoot + "取消.png"));
                SetObject(serialized, "cancelHighlightedSprite", LoadSprite(ArtRoot + "取消（选中）.png"));
                SetObject(serialized, "sceneSlotSprite", LoadSprite(ArtRoot + "物品格.png"));
                SetObject(serialized, "sceneSelectedSlotSprite", LoadSprite(ArtRoot + "物品格（选中）.png"));
                SetObject(serialized, "backpackSlotSprite", LoadSprite(ArtRoot + "背包格子.png"));
                SetObject(serialized, "backpackSelectedSlotSprite", LoadSprite(ArtRoot + "背包格（选中）.png"));
                SetObject(serialized, "equipmentSlotSprite", LoadSprite(ArtRoot + "装备格子.png"));
                SetObject(serialized, "equipmentSelectedSlotSprite", LoadSprite(ArtRoot + "装备格子（选中）.png"));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                panel.RebuildPrefabLayoutForEditor();
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, InventoryPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyInventorySlot()
        {
            var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
            try
            {
                var image = root.GetComponent<Image>();
                var normal = LoadSprite(ArtRoot + "背包格子.png");
                var selected = LoadSprite(ArtRoot + "背包格（选中）.png");
                image.sprite = normal;
                image.color = Color.white;
                image.raycastTarget = true;
                var rect = root.transform as RectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(129f, 108f);

                var serialized = new SerializedObject(root.GetComponent<InventorySlotView>());
                SetObject(serialized, "normalSprite", normal);
                SetObject(serialized, "selectedSprite", selected);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyShortcutBar()
        {
            var root = PrefabUtility.LoadPrefabContents(ShortcutPrefabPath);
            try
            {
                var rootRect = root.transform as RectTransform;
                for (var i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }

                var mountObject = new GameObject("ShortcutMount", typeof(RectTransform));
                mountObject.transform.SetParent(root.transform, false);
                var mount = mountObject.GetComponent<RectTransform>();
                mount.anchorMin = mount.anchorMax = new Vector2(0.5f, 0.5f);
                mount.anchoredPosition = Vector2.zero;
                mount.sizeDelta = Vector2.zero;

                var normal = LoadSprite(ArtRoot + "装备格子.png");
                for (var i = 0; i < 4; i++)
                {
                    var slotObject = new GameObject(i == 3 ? "OffhandSlot" : "ShortcutSlot_" + (i + 1),
                        typeof(RectTransform), typeof(Image));
                    slotObject.transform.SetParent(mount, false);
                    var rect = slotObject.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(520.5f + i * 91f, -475.5f);
                    rect.sizeDelta = new Vector2(91f, 75f);
                    var image = slotObject.GetComponent<Image>();
                    image.sprite = normal;
                    image.color = Color.white;
                    image.raycastTarget = false;

                    var keyObject = new GameObject("KeyHint", typeof(RectTransform), typeof(Text));
                    keyObject.transform.SetParent(slotObject.transform, false);
                    var keyRect = keyObject.GetComponent<RectTransform>();
                    keyRect.anchorMin = new Vector2(0f, 1f);
                    keyRect.anchorMax = new Vector2(0f, 1f);
                    keyRect.pivot = new Vector2(0f, 1f);
                    keyRect.anchoredPosition = new Vector2(7f, -4f);
                    keyRect.sizeDelta = new Vector2(22f, 20f);
                    var key = keyObject.GetComponent<Text>();
                    key.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    key.fontSize = 14;
                    key.fontStyle = FontStyle.Bold;
                    key.color = new Color(0.25f, 0.18f, 0.13f, 1f);
                    key.raycastTarget = false;
                    key.text = i == 3 ? "副" : (i + 1).ToString();
                }

                // Keep the stretch root authored by the scene instance; only
                // the mount and its four slots are replaced by this migration.
                if (rootRect != null)
                {
                    rootRect.anchorMin = Vector2.zero;
                    rootRect.anchorMax = Vector2.one;
                    rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
                }

                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, ShortcutPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Missing UI2.0 sprite: " + path);
            }

            return sprite;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException("InventoryPanel field is missing: " + propertyName);
            }

            property.objectReferenceValue = value;
        }
    }
}
#endif
