#if UNITY_EDITOR
using System;
using MemorialArchive.Framework.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Applies the UI2.0 diary and map artwork while keeping the existing
    /// panel, toggle, and close-button objects. The supplied map export is a
    /// complete 1920x1080 composition; no room-to-pixel marker mapping is
    /// inferred here.
    /// </summary>
    public static class UI2DiaryMapMigration
    {
        private const string Ui2Root = "Assets/Art/UI/Imported_UI2.0/UI2.0";
        private const string PrefabRoot = "Assets/Prefabs/UI";
        private const float PixelsPerUnit = 100f;

        // Coordinates are normalized against the 1920x1080 UI2.0 map
        // composition with the RectTransform lower-left origin. They come
        // from the supplied scene_layout.png and 场景地图1草图.png references;
        // no coordinate is inferred at runtime.
        private readonly struct MapMarkerPlacement
        {
            public MapMarkerPlacement(string sceneId, Vector2 normalizedPosition)
            {
                SceneId = sceneId;
                NormalizedPosition = normalizedPosition;
            }

            public string SceneId { get; }
            public Vector2 NormalizedPosition { get; }
        }

        private static readonly MapMarkerPlacement[] MapMarkerPlacements =
        {
            new MapMarkerPlacement("Floor_4F", new Vector2(0.424f, 0.570f)),
            new MapMarkerPlacement("Floor_3F", new Vector2(0.424f, 0.450f)),
            new MapMarkerPlacement("Floor_2F", new Vector2(0.424f, 0.360f)),
            new MapMarkerPlacement("Floor_1F", new Vector2(0.591f, 0.250f)),
            new MapMarkerPlacement("FrontHall", new Vector2(0.464f, 0.250f)),
            new MapMarkerPlacement("Room_Director", new Vector2(0.365f, 0.450f)),
            new MapMarkerPlacement("Room_Office", new Vector2(0.365f, 0.360f)),
            new MapMarkerPlacement("Room_ArchiveC", new Vector2(0.484f, 0.450f)),
            new MapMarkerPlacement("Room_TreatmentA", new Vector2(0.561f, 0.450f)),
            new MapMarkerPlacement("Room_TreatmentB", new Vector2(0.641f, 0.450f)),
            new MapMarkerPlacement("Room_ArchiveA", new Vector2(0.484f, 0.360f)),
            new MapMarkerPlacement("Room_ArchiveB", new Vector2(0.561f, 0.360f)),
            new MapMarkerPlacement("Room_Reception", new Vector2(0.641f, 0.360f)),
            new MapMarkerPlacement("Room_Toilet", new Vector2(0.651f, 0.250f))
        };

        [MenuItem("Tools/Memorial Archive/Apply UI2.0 Diary and Map")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            ConfigureSourceSprites();

            ApplyPrefab($"{PrefabRoot}/DiaryPanel.prefab", ApplyDiaryPanel);
            ApplyPrefab($"{PrefabRoot}/MapPanel.prefab", ApplyMapPanel);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied UI2.0 diary and map artwork to DiaryPanel and MapPanel.");
        }

        private static void ConfigureSourceSprites()
        {
            foreach (var path in new[]
            {
                P("笔记", "透明度蒙版 拷贝 4.png"),
                P("笔记", "笔记本背景.png"),
                P("笔记", "前一页.png"),
                P("笔记", "后一页.png"),
                P("笔记", "日记.png"),
                P("笔记", "日记（选中）.png"),
                P("笔记", "纸条.png"),
                P("笔记", "纸条（选中）.png"),
                P("地图", "地图弹窗背景.png"),
                P("地图", "实时位置.png"),
                P("地图", "退出键.png"),
                P("地图", "退出键 （选中）.png")
            })
            {
                ConfigureSprite(path);
            }
        }

        private static string P(string folder, string file) => $"{Ui2Root}/{folder}/{file}";

        private static void ConfigureSprite(string path)
        {
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
        }

        private static void ApplyPrefab(string path, Action<GameObject> apply)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException($"Prefab could not be opened: {path}");
            }

            try
            {
                apply(root);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyDiaryPanel(GameObject root)
        {
            if (root.GetComponent<DiaryPanel>() == null)
            {
                throw new InvalidOperationException("DiaryPanel component is missing from its prefab.");
            }

            var mask = Find(root, "Mask");
            if (mask == null) throw new InvalidOperationException("DiaryPanel is missing Mask.");
            var maskImage = RequireImage(mask, "Mask");
            SetSprite(maskImage, P("笔记", "透明度蒙版 拷贝 4.png"), false, true);
            Stretch(mask.GetComponent<RectTransform>());
            mask.transform.SetAsFirstSibling();

            var book = Find(root, "Bg");
            if (book == null) throw new InvalidOperationException("DiaryPanel is missing Bg.");
            var bookImage = RequireImage(book, "Bg");
            var bookSprite = LoadSprite(P("笔记", "笔记本背景.png"));
            SetSprite(bookImage, bookSprite, false, false);
            SetNativeRect(book.GetComponent<RectTransform>(), bookSprite, Vector2.zero);
            book.transform.SetSiblingIndex(1);

            ConfigureDiaryTab(
                Find(root, "DiaryTab"),
                P("笔记", "日记.png"),
                P("笔记", "日记（选中）.png"),
                new Vector2(-70f, -170f),
                true);
            ConfigureDiaryTab(
                Find(root, "NoteTab"),
                P("笔记", "纸条.png"),
                P("笔记", "纸条（选中）.png"),
                new Vector2(-70f, -255f),
                false);

            ConfigureDiaryArrow(
                Find(root, "LeftArrow"),
                P("笔记", "前一页.png"),
                new Vector2(-90f, 0f),
                new Vector2(0f, 0.5f));
            ConfigureDiaryArrow(
                Find(root, "RightArrow"),
                P("笔记", "后一页.png"),
                new Vector2(90f, 0f),
                new Vector2(1f, 0.5f));

            ConfigureDisabledArrow(Find(root, "LeftArrowDisabled"), P("笔记", "前一页.png"));
            ConfigureDisabledArrow(Find(root, "RightArrowDisabled"), P("笔记", "后一页.png"));

            // The book background already contains the UI2.0 close artwork.
            // Keep a matching clickable overlay so CommonPanelActions.CloseOwner
            // remains intact without changing the panel controller.
            ConfigureCloseButton(
                Find(root, "CloseButton"),
                new Vector2(-101f, -86f),
                new Vector2(1f, 1f));
        }

        private static void ApplyMapPanel(GameObject root)
        {
            if (root.GetComponent<MapPanel>() == null)
            {
                throw new InvalidOperationException("MapPanel component is missing from its prefab.");
            }

            // This source is already an opaque 1920x1080 composition with the
            // parchment, room labels, entrance, and supplied fixed marker.
            var mapImage = RequireImage(root, "MapPanel");
            SetSprite(mapImage, P("地图", "地图弹窗背景.png"), false, true);
            Stretch(root.GetComponent<RectTransform>());

            var title = Find(root, "Title");
            if (title != null) title.SetActive(false);

            ConfigureCloseButton(
                Find(root, "CloseButton"),
                new Vector2(-230f, -180f),
                new Vector2(1f, 1f));

            var closeLabel = Find(root, "Label");
            if (closeLabel != null) closeLabel.SetActive(false);

            ConfigureMapMarker(root);
        }

        private static void ConfigureMapMarker(GameObject root)
        {
            var mapPanel = root.GetComponent<MapPanel>();
            var mapRect = root.GetComponent<RectTransform>();
            var markerObject = FindDirectChild(root, "PlayerMarker");
            if (markerObject == null)
            {
                markerObject = new GameObject("PlayerMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                markerObject.transform.SetParent(root.transform, false);
            }

            var markerImage = RequireImage(markerObject, "PlayerMarker");
            var markerSprite = LoadSprite(P("地图", "实时位置.png"));
            SetSprite(markerImage, markerSprite, false, false);
            SetNativeRect(markerObject.GetComponent<RectTransform>(), markerSprite, Vector2.zero);
            markerObject.SetActive(false);

            var close = Find(root, "CloseButton");
            if (close != null)
            {
                markerObject.transform.SetSiblingIndex(close.transform.GetSiblingIndex());
            }

            var serialized = new SerializedObject(mapPanel);
            SetObject(serialized, "mapRect", mapRect);
            SetObject(serialized, "markerImage", markerImage);
            var entries = serialized.FindProperty("markerPlacements");
            entries.arraySize = MapMarkerPlacements.Length;
            for (var i = 0; i < MapMarkerPlacements.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sceneId").stringValue = MapMarkerPlacements[i].SceneId;
                entry.FindPropertyRelative("roomId").stringValue = string.Empty;
                entry.FindPropertyRelative("normalizedPosition").vector2Value = MapMarkerPlacements[i].NormalizedPosition;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDiaryTab(
            GameObject tab,
            string normalPath,
            string selectedPath,
            Vector2 position,
            bool selected)
        {
            if (tab == null) throw new InvalidOperationException("DiaryPanel is missing a tab object.");
            var normal = LoadSprite(normalPath);
            var selectedSprite = LoadSprite(selectedPath);
            var background = RequireImage(FindDirectChild(tab, "Background"), $"{tab.name}/Background");
            var checkmark = RequireImage(Find(tab, "Checkmark"), $"{tab.name}/Checkmark");

            SetSprite(background, normal, false, true);
            SetNativeRect(background.rectTransform, normal, Vector2.zero);
            SetSprite(checkmark, selectedSprite, false, false);
            SetNativeRect(checkmark.rectTransform, selectedSprite, Vector2.zero);

            var tabRect = tab.GetComponent<RectTransform>();
            tabRect.anchorMin = tabRect.anchorMax = new Vector2(1f, 0.5f);
            tabRect.anchoredPosition = position;
            tabRect.sizeDelta = normal.rect.size;
            tabRect.localScale = Vector3.one;

            var toggle = tab.GetComponent<Toggle>();
            if (toggle == null) throw new InvalidOperationException($"Toggle component was not found on {tab.name}.");
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.SetIsOnWithoutNotify(selected);
        }

        private static void ConfigureDiaryArrow(GameObject arrow, string spritePath, Vector2 position, Vector2 anchor)
        {
            if (arrow == null) throw new InvalidOperationException("DiaryPanel is missing an arrow object.");
            var sprite = LoadSprite(spritePath);
            var image = RequireImage(arrow, arrow.name);
            SetSprite(image, sprite, false, true);
            SetNativeRect(arrow.GetComponent<RectTransform>(), sprite, position, anchor);

            var button = arrow.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException($"Button component was not found on {arrow.name}.");
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.highlightedSprite = sprite;
            state.pressedSprite = sprite;
            state.selectedSprite = sprite;
            state.disabledSprite = null;
            button.spriteState = state;
        }

        private static void ConfigureDisabledArrow(GameObject arrow, string spritePath)
        {
            if (arrow == null) return;
            var sprite = LoadSprite(spritePath);
            var image = RequireImage(arrow, arrow.name);
            SetSprite(image, sprite, false, false);
            SetNativeRect(arrow.GetComponent<RectTransform>(), sprite, Vector2.zero);
            image.color = new Color(1f, 1f, 1f, 0.45f);
        }

        private static void ConfigureCloseButton(GameObject close, Vector2 position, Vector2 anchor)
        {
            if (close == null) throw new InvalidOperationException("Panel is missing CloseButton.");
            var normal = LoadSprite(P("地图", "退出键.png"));
            var selected = LoadSprite(P("地图", "退出键 （选中）.png"));
            var image = RequireImage(close, "CloseButton");
            SetSprite(image, normal, false, true);
            SetNativeRect(close.GetComponent<RectTransform>(), normal, position, anchor);

            var button = close.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Button component was not found on CloseButton.");
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.highlightedSprite = selected;
            state.pressedSprite = selected;
            state.selectedSprite = selected;
            state.disabledSprite = null;
            button.spriteState = state;
        }

        private static Image RequireImage(GameObject go, string label)
        {
            if (go == null) throw new InvalidOperationException($"Missing UI object: {label}");
            var image = go.GetComponent<Image>();
            if (image == null) throw new InvalidOperationException($"Missing Image component: {label}");
            return image;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property was not found: {propertyName}");
            }

            property.objectReferenceValue = value;
        }

        private static GameObject FindDirectChild(GameObject parent, string name)
        {
            if (parent == null) return null;
            var child = parent.transform.Find(name);
            return child != null ? child.gameObject : null;
        }

        private static void SetSprite(Image image, Sprite sprite, bool preserveAspect, bool raycastTarget)
        {
            if (image == null) throw new InvalidOperationException("Cannot configure a null Image.");
            image.sprite = sprite;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.color = Color.white;
            image.raycastTarget = raycastTarget;
        }

        private static void SetSprite(Image image, string path, bool preserveAspect, bool raycastTarget)
        {
            SetSprite(image, LoadSprite(path), preserveAspect, raycastTarget);
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Sprite could not be loaded: {path}");
            return sprite;
        }

        private static void SetNativeRect(RectTransform rect, Sprite sprite, Vector2 position)
        {
            SetNativeRect(rect, sprite, position, new Vector2(0.5f, 0.5f));
        }

        private static void SetNativeRect(RectTransform rect, Sprite sprite, Vector2 position, Vector2 anchor)
        {
            if (rect == null) throw new InvalidOperationException("Expected a RectTransform.");
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.sizeDelta = sprite.rect.size;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) throw new InvalidOperationException("Expected a RectTransform.");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static GameObject Find(GameObject root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root.transform)
            {
                var found = Find(child.gameObject, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
