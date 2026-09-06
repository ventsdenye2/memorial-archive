#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Applies the UI2.0 menu art to the four menu prefabs without replacing
    /// their existing controller/button event wiring. Run from
    /// Tools/Memorial Archive/Apply UI2.0 Menu.
    /// </summary>
    public static class UI2MenuMigration
    {
        private const string Ui2Root = "Assets/Art/UI/Imported_UI2.0/UI2.0";
        private const string PrefabRoot = "Assets/Prefabs/UI";
        private const float PixelsPerUnit = 100f;

        [MenuItem("Tools/Memorial Archive/Apply UI2.0 Menu")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            ConfigureSourceSprites();

            ApplyPrefab($"{PrefabRoot}/MainMenuPanel.prefab", ApplyMainMenu);
            ApplyPrefab($"{PrefabRoot}/NewGameConfirmPanel.prefab", ApplyNewGameConfirm);
            ApplyPrefab($"{PrefabRoot}/SystemPanel.prefab", ApplySystemPanel);
            ApplyPrefab($"{PrefabRoot}/SettingsPanel.prefab", ApplySettingsPanel);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied UI2.0 art and layout to MainMenuPanel, NewGameConfirmPanel, SystemPanel, and SettingsPanel.");
        }

        private static void ConfigureSourceSprites()
        {
            foreach (var path in new[]
            {
                P("封面", "背景.png"), P("封面", "红绳（此图层置于所有选项图层上）.png"),
                P("封面", "新游戏.png"), P("封面", "新游戏（选中）.png"),
                P("封面", "继续游戏.png"), P("封面", "继续游戏(选中）.png"),
                P("封面", "读取存档.png"), P("封面", "读取存档 （选中）.png"),
                P("封面", "设置.png"), P("封面", "设置 （选中）.png"),
                P("封面", "退出游戏.png"), P("封面", "退出游戏 （选中）.png"),
                P("开始游戏弹窗", "透明度蒙版 拷贝.png"), P("开始游戏弹窗", "弹窗.png"),
                P("开始游戏弹窗", "是的.png"), P("开始游戏弹窗", "是的（选中）.png"),
                P("开始游戏弹窗", "取消_.png"), P("开始游戏弹窗", "取消（选中）.png"),
                P("设置一级弹窗", "背景.png"), P("设置一级弹窗", "红绳（置于所有图层上）.png"),
                P("设置一级弹窗", "继续游戏.png"), P("设置一级弹窗", "继续游戏（选中）.png"),
                P("设置一级弹窗", "读取存档.png"), P("设置一级弹窗", "读取存档（选中）.png"),
                P("设置一级弹窗", "设置.png"), P("设置一级弹窗", "设置（选中）.png"),
                P("设置一级弹窗", "返回主菜单.png"), P("设置一级弹窗", "返回主菜单（选中）.png"),
                P("游戏设置弹窗", "设置弹窗背景.png"), P("游戏设置弹窗", "全屏模式.png"),
                P("游戏设置弹窗", "全屏模式 未选中.png"), P("游戏设置弹窗", "窗口模式.png"),
                P("游戏设置弹窗", "窗口模式 (未选中）.png"), P("游戏设置弹窗", "下一项.png"),
                P("游戏设置弹窗", "退出键.png"), P("游戏设置弹窗", "退出键 （选中）.png"),
                P("游戏设置弹窗", "音量底框.png"), P("游戏设置弹窗", "音量（可变滑条）.png"),
                P("游戏设置弹窗", "按钮.png")
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
            if (importer == null) throw new InvalidOperationException($"Texture importer was not found: {path}");
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
            if (root == null) throw new InvalidOperationException($"Prefab could not be opened: {path}");
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

        private static void ApplyMainMenu(GameObject root)
        {
            SetSprite(Find(root, "Background").GetComponent<Image>(), P("封面", "背景.png"), false, false);
            ConfigureMenuButton(Find(root, "NewGameButton"), P("封面", "新游戏.png"), P("封面", "新游戏（选中）.png"), new Vector2(548f, 219f));
            ConfigureMenuButton(Find(root, "SettingsButton"), P("封面", "设置.png"), P("封面", "设置 （选中）.png"), new Vector2(618f, -252f));
            ConfigureMenuButton(Find(root, "LoadButton"), P("封面", "读取存档.png"), P("封面", "读取存档 （选中）.png"), new Vector2(509f, -95f));
            ConfigureMenuButton(Find(root, "ExitButton"), P("封面", "退出游戏.png"), P("封面", "退出游戏 （选中）.png"), new Vector2(506f, -409f));

            var rope = Find(root, "Subtitle");
            if (rope != null)
            {
                var image = rope.GetComponent<Image>();
                SetSprite(image, P("封面", "红绳（此图层置于所有选项图层上）.png"), false, false);
                SetNativeRect(rope.GetComponent<RectTransform>(), image.sprite, new Vector2(550f, -118f));
            rope.transform.SetAsLastSibling();
            }
        }

        private static void ApplyNewGameConfirm(GameObject root)
        {
            var mask = Find(root, "Mask");
            SetSprite(mask.GetComponent<Image>(), P("开始游戏弹窗", "透明度蒙版 拷贝.png"), false, true);
            Stretch(mask.GetComponent<RectTransform>());

            var art = Find(root, "Tips") ?? Find(root, "DialogArt");
            if (art == null) throw new InvalidOperationException("NewGameConfirmPanel is missing its dialog art Image.");
            art.name = "DialogArt";
            var artImage = art.GetComponent<Image>();
            SetSprite(artImage, P("开始游戏弹窗", "弹窗.png"), false, false);
            artImage.raycastTarget = false;
            SetNativeRect(art.GetComponent<RectTransform>(), artImage.sprite, Vector2.zero);

            ConfigureConfirmButton(Find(root, "ConfirmButton"), P("开始游戏弹窗", "是的.png"), P("开始游戏弹窗", "是的（选中）.png"), new Vector2(-84f, -98f));
            ConfigureConfirmButton(Find(root, "CancelButton"), P("开始游戏弹窗", "取消_.png"), P("开始游戏弹窗", "取消（选中）.png"), new Vector2(192f, -64f));
        }

        private static void ApplySystemPanel(GameObject root)
        {
            var art = GetOrCreateImage(root.transform, "PauseArt");
            SetSprite(art.GetComponent<Image>(), P("设置一级弹窗", "背景.png"), false, false);
            SetNativeRect(art.GetComponent<RectTransform>(), art.GetComponent<Image>().sprite, Vector2.zero);
            art.GetComponent<Image>().raycastTarget = false;
            art.transform.SetSiblingIndex(1);

            var rope = GetOrCreateImage(root.transform, "PauseRope");
            SetSprite(rope.GetComponent<Image>(), P("设置一级弹窗", "红绳（置于所有图层上）.png"), false, false);
            SetNativeRect(rope.GetComponent<RectTransform>(), rope.GetComponent<Image>().sprite, new Vector2(0f, -92f));
            rope.GetComponent<Image>().raycastTarget = false;
            rope.transform.SetAsLastSibling();

            var oldTitle = Find(root, "PauseTitle");
            if (oldTitle != null) oldTitle.SetActive(false);

            ConfigureSystemButton(Find(root, "ContinueButton"), P("设置一级弹窗", "继续游戏.png"), P("设置一级弹窗", "继续游戏（选中）.png"), new Vector2(0f, 185f));
            ConfigureSystemButton(Find(root, "LoadButton"), P("设置一级弹窗", "读取存档.png"), P("设置一级弹窗", "读取存档（选中）.png"), new Vector2(0f, 10f));
            ConfigureSystemButton(Find(root, "SettingsButton"), P("设置一级弹窗", "设置.png"), P("设置一级弹窗", "设置（选中）.png"), new Vector2(0f, -165f));
            ConfigureSystemButton(Find(root, "MainMenuButton"), P("设置一级弹窗", "返回主菜单.png"), P("设置一级弹窗", "返回主菜单（选中）.png"), new Vector2(0f, -340f));
        }

        private static void ApplySettingsPanel(GameObject root)
        {
            var art = GetOrCreateImage(root.transform, "SettingsArt");
            SetSprite(art.GetComponent<Image>(), P("游戏设置弹窗", "设置弹窗背景.png"), false, false);
            Stretch(art.GetComponent<RectTransform>());
            art.GetComponent<Image>().raycastTarget = false;
            art.transform.SetSiblingIndex(0);

            var oldTitle = Find(root, "Title");
            if (oldTitle != null) oldTitle.SetActive(false);
            var close = Find(root, "CloseButton");
            if (close != null)
            {
                var closeImage = close.GetComponent<Image>();
                ConfigureButton(close, closeImage, P("游戏设置弹窗", "退出键.png"), P("游戏设置弹窗", "退出键 （选中）.png"), new Vector2(588f, 305f));
                var label = close.transform.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
            }

            var panel = root.GetComponent<MemorialArchive.Framework.UI.SettingsPanel>();
            if (panel == null) throw new InvalidOperationException("SettingsPanel component was not found.");
            var fullscreen = CreateButton(root.transform, "FullscreenButton", P("游戏设置弹窗", "全屏模式 未选中.png"), P("游戏设置弹窗", "全屏模式.png"), new Vector2(-174f, 80f));
            var windowed = CreateButton(root.transform, "WindowedButton", P("游戏设置弹窗", "窗口模式 (未选中）.png"), P("游戏设置弹窗", "窗口模式.png"), new Vector2(245f, 80f));
            BindClick(fullscreen, panel.SetFullscreen);
            BindClick(windowed, panel.SetWindowed);

            var resolutionNext = CreateButton(root.transform, "ResolutionNextButton", P("游戏设置弹窗", "下一项.png"), null, new Vector2(345f, -90f));
            resolutionNext.GetComponent<Image>().raycastTarget = true;
            BindClick(resolutionNext, panel.CycleResolution);

            var resolutionLabel = GetOrCreateText(root.transform, "ResolutionValue", new Vector2(35f, -90f), new Vector2(420f, 64f));
            resolutionLabel.alignment = TextAnchor.MiddleCenter;
            resolutionLabel.fontSize = 31;
            resolutionLabel.color = new Color(0.23f, 0.18f, 0.13f, 1f);
            resolutionLabel.raycastTarget = false;

            var backgroundVolume = CreateVolumeSlider(root.transform, "BackgroundVolume", new Vector2(100f, -200f));
            var gameVolume = CreateVolumeSlider(root.transform, "GameVolume", new Vector2(100f, -272f));
            var backgroundValue = GetOrCreateText(root.transform, "BackgroundVolumeValue", new Vector2(414f, -200f), new Vector2(70f, 50f));
            var gameValue = GetOrCreateText(root.transform, "GameVolumeValue", new Vector2(414f, -272f), new Vector2(70f, 50f));
            ConfigureVolumeValue(backgroundVolume, backgroundValue);
            ConfigureVolumeValue(gameVolume, gameValue);

            AssignSerialized(panel, "backgroundVolumeSlider", backgroundVolume);
            AssignSerialized(panel, "gameVolumeSlider", gameVolume);
            AssignSerialized(panel, "backgroundVolumeValueLabel", backgroundValue);
            AssignSerialized(panel, "gameVolumeValueLabel", gameValue);
            AssignSerialized(panel, "resolutionLabel", resolutionLabel);
            AssignSerialized(panel, "fullscreenButton", fullscreen.GetComponent<Button>());
            AssignSerialized(panel, "windowedButton", windowed.GetComponent<Button>());
            AssignSerialized(panel, "fullscreenNormalSprite", LoadSprite(P("游戏设置弹窗", "全屏模式 未选中.png")));
            AssignSerialized(panel, "fullscreenSelectedSprite", LoadSprite(P("游戏设置弹窗", "全屏模式.png")));
            AssignSerialized(panel, "windowedNormalSprite", LoadSprite(P("游戏设置弹窗", "窗口模式 (未选中）.png")));
            AssignSerialized(panel, "windowedSelectedSprite", LoadSprite(P("游戏设置弹窗", "窗口模式.png")));
        }

        private static void ConfigureMenuButton(GameObject go, string normal, string selected, Vector2 position) =>
            ConfigureButton(go, go.GetComponent<Image>(), normal, selected, position);

        private static void ConfigureSystemButton(GameObject go, string normal, string selected, Vector2 position) =>
            ConfigureButton(go, go.GetComponent<Image>(), normal, selected, position);

        private static void ConfigureConfirmButton(GameObject go, string normal, string selected, Vector2 position) =>
            ConfigureButton(go, go.GetComponent<Image>(), normal, selected, position);

        private static void ConfigureButton(GameObject go, Image image, string normal, string selected, Vector2 position)
        {
            if (go == null || image == null) throw new InvalidOperationException("Expected a named UI button with an Image component.");
            var normalSprite = LoadSprite(normal);
            var selectedSprite = string.IsNullOrEmpty(selected) ? null : LoadSprite(selected);
            SetSprite(image, normal, false, true);
            var rect = go.GetComponent<RectTransform>();
            SetNativeRect(rect, normalSprite, position);
            var button = go.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException($"Button component was not found on {go.name}.");
            button.transition = Selectable.Transition.SpriteSwap;
            button.targetGraphic = image;
            var state = button.spriteState;
            state.highlightedSprite = selectedSprite;
            state.pressedSprite = selectedSprite;
            state.selectedSprite = selectedSprite;
            state.disabledSprite = null;
            button.spriteState = state;
            var swap = go.GetComponent<MemorialArchive.Framework.UI.SpriteSwapNativeSize>();
            if (swap == null) swap = go.AddComponent<MemorialArchive.Framework.UI.SpriteSwapNativeSize>();
            AssignSerialized(swap, "targetImage", image);
        }

        private static Button CreateButton(Transform parent, string name, string normal, string selected, Vector2 position)
        {
            var go = Find(parent.gameObject, name);
            if (go == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
            }
            ConfigureButton(go, go.GetComponent<Image>(), normal, selected, position);
            return go.GetComponent<Button>();
        }

        private static Slider CreateVolumeSlider(Transform parent, string name, Vector2 position)
        {
            var go = Find(parent.gameObject, name);
            if (go == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
                go.transform.SetParent(parent, false);
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(544f, 50f);
            var rootImage = go.GetComponent<Image>();
            rootImage.sprite = null;
            rootImage.overrideSprite = null;
            rootImage.raycastTarget = false;

            var background = GetOrCreateImage(go.transform, "Background");
            SetSprite(background.GetComponent<Image>(), P("游戏设置弹窗", "音量底框.png"), false, true);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(544f, 23f);

            var fill = GetOrCreateImage(go.transform, "Fill");
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(273f, 23f);
            fillRect.anchoredPosition = new Vector2(-272f, 0f);
            SetSprite(fill.GetComponent<Image>(), P("游戏设置弹窗", "音量（可变滑条）.png"), false, false);
            fill.GetComponent<Image>().raycastTarget = false;

            var handle = GetOrCreateImage(go.transform, "Handle");
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(50f, 50f);
            SetSprite(handle.GetComponent<Image>(), P("游戏设置弹窗", "按钮.png"), false, true);
            handle.GetComponent<Image>().raycastTarget = true;

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.SetValueWithoutNotify(0.5f);
            return slider;
        }

        private static void ConfigureVolumeValue(Slider slider, Text value)
        {
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(v => value.text = Mathf.RoundToInt(v * 100f).ToString());
            value.text = "50";
        }

        private static void BindClick(Button button, UnityEngine.Events.UnityAction action)
        {
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, button.onClick.GetPersistentEventCount() - 1);
            }
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static Image GetOrCreateImage(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var image = existing.GetComponent<Image>();
                if (image == null) image = existing.gameObject.AddComponent<Image>();
                return image;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static Text GetOrCreateText(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            if (existing == null) go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetSprite(Image image, string path, bool preserveAspect, bool raycastTarget)
        {
            image.sprite = LoadSprite(path);
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.color = Color.white;
            image.raycastTarget = raycastTarget;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Sprite could not be loaded: {path}");
            return sprite;
        }

        private static void SetNativeRect(RectTransform rect, Sprite sprite, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            // Canvas UI uses pixel-sized RectTransforms at the project's
            // referencePixelsPerUnit (100), matching Image.SetNativeSize().
            rect.sizeDelta = sprite.rect.size;
        }

        private static void Stretch(RectTransform rect)
        {
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

        private static void AssignSerialized(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"Serialized field was not found: {target.name}.{propertyName}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
