using System;
using System.Collections.Generic;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Guide.View;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>Builds the authored guide presentation asset and its full-screen overlay prefab.</summary>
    public static class GuidePresentationBuilder
    {
        private const string ConfigPath = "Assets/Resources/Guide/Presentation.asset";
        private const string PrefabPath = "Assets/Prefabs/UI/GuideOverlayPanel.prefab";
        private const string ArtworkRoot = "Assets/Art/UI/Imported_UI2.0/UI2.0";
        private const string SupplementRoot = "Assets/Art/UI/Imported_UI2_Supplement";
        private const string ObstaclePath = "Assets/Art/Scenes/馆长办公室素材/馆长办公室素材/椅子 Scene_15_Background_Far.png";
        private const string DiaryPath = "Assets/Art/Imported/SceneRebuild0909/前厅素材/线索 副本.png";
        private const string DiaryInteractionPath = "Assets/GameConfigs/Guide/FirstDiary.asset";
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";

        [MenuItem("Tools/Memorial Archive/Build Full Page Guides")]
        public static void Build()
        {
            EnsureFolders();
            BuildPresentation();
            BuildOverlayPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Full page guide presentation and overlay prefab built.");
        }

        private static void BuildPresentation()
        {
            var config = AssetDatabase.LoadAssetAtPath<GuidePresentationConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GuidePresentationConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var pages = new List<GuidePage>
            {
                Page("movement", SpriteAt($"{ArtworkRoot}/教程页2/蒙版.png"), false, KeyCode.None, string.Empty),
                Page("inventory", SpriteAt($"{SupplementRoot}/教程页5/将背包物品拖拽至快捷栏，或选中物品点击装备按钮即可完成装备。.png"), true, KeyCode.None, string.Empty),
                Page("light", SpriteAt($"{ArtworkRoot}/教程页3/蒙版.png"), true, KeyCode.None, string.Empty),
                Page("systems", SpriteAt($"{SupplementRoot}/教程页4/组 44.png"), true, KeyCode.None, string.Empty),
                Page("combat", SpriteAt($"{ArtworkRoot}/教程页1/蒙版.png"), true, KeyCode.None, string.Empty),
                Page("dodge", null, true, KeyCode.Space, "按空格闪避"),
                Page("equip", null, true, KeyCode.None, "打开背包将武器装备至快捷栏并选中")
            };
            config.pages = pages.ToArray();
            config.closeHint = SpriteAt($"{ArtworkRoot}/教程页1/按下任意键关闭.png");
            config.obstacleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ObstaclePath);
            config.diarySprite = AssetDatabase.LoadAssetAtPath<Sprite>(DiaryPath);
            config.diaryInteraction = BuildFirstDiaryInteraction();
            config.encounterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Stage1MeleeMonster.prefab");
            config.overlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            config.encounterScene = "Floor_3F";
            config.treatmentScene = "Room_TreatmentA";
            SyncInteractionLayout(config);
            EditorUtility.SetDirty(config);
            ConfigureSprites(pages, config.closeHint);
        }

        private static GuidePage Page(string id, Sprite artwork, bool pause, KeyCode dismissKey, string message)
        {
            return new GuidePage { id = id, artwork = artwork, pause = pause, dismissKey = dismissKey, message = message };
        }

        private static void BuildOverlayPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) throw new InvalidOperationException("Guide overlay prefab could not be loaded: " + PrefabPath);
            try
            {
                var panel = root.GetComponent<GuideOverlayPanel>();
                if (panel == null) throw new InvalidOperationException("GuideOverlayPanel component is missing from the prefab root.");
                ResetRootRect(root.transform as RectTransform);
                foreach (var layout in root.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(layout);
                foreach (var fitter in root.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(fitter);
                var canvas = root.GetComponent<Canvas>();
                if (canvas == null) canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 500;
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
                while (root.transform.childCount > 0)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);

                var shield = Child(root.transform, "InputShield");
                Stretch(shield);
                var shieldImage = shield.gameObject.AddComponent<Image>();
                shieldImage.color = new Color(0f, 0f, 0f, 0f);
                shieldImage.raycastTarget = true;
                var canvasGroup = shield.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;

                var artwork = Child(root.transform, "Artwork");
                Stretch(artwork);
                var artworkImage = artwork.gameObject.AddComponent<Image>();
                artworkImage.preserveAspect = true;
                artworkImage.raycastTarget = false;

                var closeHint = Child(root.transform, "CloseHint");
                var closeRect = closeHint.GetComponent<RectTransform>();
                closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, .5f);
                closeRect.anchoredPosition = new Vector2(0f, -350f);
                closeRect.sizeDelta = new Vector2(541f, 118f);
                var closeImage = closeHint.gameObject.AddComponent<Image>();
                closeImage.preserveAspect = true;
                closeImage.raycastTarget = false;

                var message = Child(root.transform, "Message");
                var messageRect = message.GetComponent<RectTransform>();
                messageRect.anchorMin = new Vector2(.5f, .5f);
                messageRect.anchorMax = new Vector2(.5f, .5f);
                messageRect.anchoredPosition = new Vector2(0f, -220f);
                messageRect.sizeDelta = new Vector2(1600f, 180f);
                var text = message.gameObject.AddComponent<Text>();
                text.font = FindProjectFont();
                text.fontSize = 32;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;

                var panelSo = new SerializedObject(panel);
                SetObject(panelSo, "artwork", artworkImage);
                SetObject(panelSo, "closeHint", closeImage);
                SetObject(panelSo, "message", text);
                SetObject(panelSo, "inputShield", canvasGroup);
                SetEnum(panelSo, "panelId", PanelId.GuideOverlay);
                SetBool(panelSo, "startClosed", true);
                panelSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Stretch(Transform t)
        {
            var r = t.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void ResetRootRect(RectTransform rect)
        {
            if (rect == null) throw new InvalidOperationException("Guide overlay prefab root must have a RectTransform.");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static Font FindProjectFont()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BlackScreenStoryPanel.prefab");
            var text = prefab == null ? null : prefab.GetComponentInChildren<Text>(true);
            return text != null && text.font != null ? text.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite SpriteAt(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.maxTextureSize != 2048 || importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Guide artwork is missing or could not be imported as Sprite: " + path);
            return sprite;
        }

        private static void ConfigureSprites(IEnumerable<GuidePage> pages, Sprite closeHint)
        {
            foreach (var page in pages) ConfigureSprite(page.artwork);
            ConfigureSprite(closeHint);
        }

        private static void ConfigureSprite(Sprite sprite)
        {
            if (sprite == null) return;
            var path = AssetDatabase.GetAssetPath(sprite);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.maxTextureSize != 2048 || importer.npotScale != TextureImporterNPOTScale.None || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void SetObject(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
        private static void SetBool(SerializedObject so, string name, bool value) => so.FindProperty(name).boolValue = value;
        private static void SetEnum(SerializedObject so, string name, Enum value) => so.FindProperty(name).enumValueIndex = Convert.ToInt32(value);

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Guide")) AssetDatabase.CreateFolder("Assets/Resources", "Guide");
            if (!AssetDatabase.IsValidFolder("Assets/GameConfigs/Guide")) AssetDatabase.CreateFolder("Assets/GameConfigs", "Guide");
        }

        public static void SyncInteractionLayout(GuidePresentationConfig config = null)
        {
            if (config == null) config = AssetDatabase.LoadAssetAtPath<GuidePresentationConfig>(ConfigPath);
            if (config == null) return;
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Tools/SceneRebuild/interaction_layout.json"));
            foreach (var scene in manifest["scenes"])
            {
                if ((string)scene["scene"] != "FrontHall") continue;
                foreach (var point in scene["points"])
                {
                    var pixels = ((float)point["x"] - config.frontHallLeft) * 100;
                    switch ((string)point["id"])
                    {
                        case "light_fronthall_special": config.specialLampPixels = pixels; break;
                        case "guide_obstacle":
                            config.equipmentGatePixels = pixels;
                            config.obstacleInteractionWidth = (float)point["width"];
                            break;
                        case "guide_first_diary":
                            config.diaryPixels = pixels;
                            config.diaryInteractionWidth = (float)point["width"];
                            break;
                    }
                }
            }
            EditorUtility.SetDirty(config);
        }

        private static InteractionConfig BuildFirstDiaryInteraction()
        {
            var interaction = AssetDatabase.LoadAssetAtPath<InteractionConfig>(DiaryInteractionPath);
            if (interaction == null)
            {
                interaction = ScriptableObject.CreateInstance<InteractionConfig>();
                AssetDatabase.CreateAsset(interaction, DiaryInteractionPath);
            }
            var so = new SerializedObject(interaction);
            so.FindProperty("interactionId").stringValue = "guide_first_diary";
            so.FindProperty("interactionType").enumValueIndex = (int)InteractionType.NotePickup;
            so.FindProperty("displayName").stringValue = "灯下的日记";
            so.FindProperty("sceneId").stringValue = "FrontHall";
            so.FindProperty("noteId").stringValue = "guide_first_diary";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interaction);

            var database = AssetDatabase.LoadAssetAtPath<MemorialArchive.Framework.Config.GameConfigDatabase>(DatabasePath);
            if (database == null) throw new InvalidOperationException("GameConfigDatabase is missing: " + DatabasePath);
            var values = new List<InteractionConfig>();
            foreach (var existing in database.Interactions ?? Array.Empty<InteractionConfig>())
                if (existing != null && existing.InteractionId != "guide_first_diary") values.Add(existing);
            values.Add(interaction);
            var dbSo = new SerializedObject(database);
            var array = dbSo.FindProperty("interactions");
            array.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return interaction;
        }
    }
}
