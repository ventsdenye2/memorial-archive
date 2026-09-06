#if UNITY_EDITOR
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Dialogue.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Applies the UI2.0 locator art to the two runtime prefabs owned by the HUD pass.
    /// The operation is intentionally idempotent so it can be rerun after a prefab or
    /// Unity import change without rebuilding the panels by hand.
    /// </summary>
    public static class UI2HudMigration
    {
        private const string HudPrefabPath = "Assets/Prefabs/UI/GameplayHUD.prefab";
        private const string OpeningDialoguePrefabPath = "Assets/Prefabs/UI/OpeningDialoguePanel.prefab";
        private const string FallbackFontPath = "Assets/Font/simhei.ttf";

        private const string GameUiPath = "Assets/Art/UI/Imported_UI2.0/UI2.0/游戏页面/";
        private const string SelfWhiteUiPath = "Assets/Art/UI/Imported_UI2.0/UI2.0/自白页/";
        private const string StoryUiPath = "Assets/Art/UI/Imported_UI2.0/UI2.0/剧情页/";

        [MenuItem("Memorial Archive/UI/Apply UI2.0 HUD and Opening Dialogue")]
        public static void Apply()
        {
            ApplyPrefab(HudPrefabPath, ConfigureGameplayHud);
            ApplyPrefab(OpeningDialoguePrefabPath, ConfigureOpeningDialogue);
            AssetDatabase.SaveAssets();
            Debug.Log("UI2.0 HUD migration applied to GameplayHUD and OpeningDialoguePanel.");
        }

        private static void ApplyPrefab(string path, System.Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
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

        private static void ConfigureGameplayHud(GameObject root)
        {
            var rootRect = root.GetComponent<RectTransform>();
            ConfigureRect(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var rootImage = root.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.sprite = null;
                rootImage.color = new Color(1f, 1f, 1f, 0f);
                rootImage.raycastTarget = false;
                EnsureCanvasRenderer(root);
            }

            var hudPanel = root.GetComponent<HUDPanel>();
            if (hudPanel == null)
            {
                hudPanel = root.AddComponent<HUDPanel>();
            }

            ConfigureHealth(root);
            var staminaBar = ConfigureStamina(root);
            ConfigureNavigation(root);
            ConfigureTaskArea(root);
            var shortcutSlots = ConfigureShortcutBar(root);
            ConfigureNarration(root, hudPanel);
            ConfigureHudPanelBindings(hudPanel, shortcutSlots, staminaBar);
            ConfigureGuideGraphics(root);
            EnsureCanvasRenderers(root);
        }

        private static void ConfigureHealth(GameObject root)
        {
            var health = EnsureRect(root.transform, "Health");
            ConfigureTopLeft(health, new Vector2(85f, -73f), new Vector2(270f, 80f));
            var emptySprite = LoadSprite(GameUiPath + "无生命值.png");
            var fullSprite = LoadSprite(GameUiPath + "1生命值.png");

            for (var i = 0; i < 3; i++)
            {
                var empty = EnsureRect(health, "EmptyHealth_" + (i + 1));
                ConfigureRect(empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(i * 100f, 0f), new Vector2(71f, 62f), new Vector2(0.5f, 0.5f));
                EnsureImage(empty.gameObject, emptySprite, false);

                var fill = EnsureRect(empty, "Health");
                ConfigureRect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                var fillImage = EnsureImage(fill.gameObject, fullSprite, false);
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = 0;
                fillImage.fillAmount = 1f;
            }
        }

        private static Image ConfigureStamina(GameObject root)
        {
            var stamina = EnsureRect(root.transform, "Stamina");
            ConfigureTopLeft(stamina, new Vector2(194f, -175f), new Vector2(324f, 82f));
            var groupImage = stamina.GetComponent<Image>();
            if (groupImage != null)
            {
                groupImage.sprite = null;
                groupImage.color = new Color(1f, 1f, 1f, 0f);
                groupImage.raycastTarget = false;
                EnsureCanvasRenderer(stamina.gameObject);
            }

            var icon = EnsureRect(stamina, "EnergyIcon");
            ConfigureRect(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-117f, 0f), new Vector2(70f, 82f), new Vector2(0.5f, 0.5f));
            EnsureImage(icon.gameObject, LoadSprite(GameUiPath + "能量icon.png"), false);

            var frame = EnsureRect(stamina, "EnergyFrame");
            ConfigureRect(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 3f), new Vector2(249f, 18f), new Vector2(0.5f, 0.5f));
            EnsureImage(frame.gameObject, LoadSprite(GameUiPath + "能量条底框.png"), false);

            var fill = EnsureRect(stamina, "StaminaBar");
            ConfigureRect(fill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 3f), new Vector2(247f, 14f), new Vector2(0.5f, 0.5f));
            var fillImage = EnsureImage(fill.gameObject, LoadSprite(GameUiPath + "可变能量条.png"), false);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            return fillImage;
        }

        private static void ConfigureNavigation(GameObject root)
        {
            ConfigureSpriteButton(root, "SystemButton", new Vector2(-89f, -73f), new Vector2(56f, 85f),
                GameUiPath + "设置.png", GameUiPath + "设置（选中）.png");
            ConfigureSpriteButton(root, "InventoryButton", new Vector2(-90f, -180f), new Vector2(52f, 87f),
                GameUiPath + "背包.png", GameUiPath + "背包（选中）.png");
            ConfigureSpriteButton(root, "DiaryButton", new Vector2(-89f, -290f), new Vector2(58f, 89f),
                GameUiPath + "笔记.png", GameUiPath + "笔记（选中）.png");
            ConfigureSpriteButton(root, "MapButton", new Vector2(-89f, -406f), new Vector2(62f, 88f),
                GameUiPath + "地图.png", GameUiPath + "地图（选中）.png");

            var taskButton = EnsureRect(root.transform, "TaskPlaceholderButton");
            ConfigureTopLeft(taskButton, new Vector2(70f, -280f), new Vector2(45f, 51f));
            var taskImage = EnsureImage(taskButton.gameObject, LoadSprite(GameUiPath + "任务.png"), false);
            var taskButtonComponent = taskButton.GetComponent<Button>();
            if (taskButtonComponent != null)
            {
                taskImage.raycastTarget = true;
                taskButtonComponent.targetGraphic = taskImage;
                taskButtonComponent.transition = Selectable.Transition.SpriteSwap;
                var state = taskButtonComponent.spriteState;
                state.highlightedSprite = LoadSprite(GameUiPath + "任务(选中）.png");
                state.pressedSprite = state.highlightedSprite;
                state.selectedSprite = state.highlightedSprite;
                taskButtonComponent.spriteState = state;
            }
        }

        private static void ConfigureSpriteButton(
            GameObject root,
            string name,
            Vector2 position,
            Vector2 size,
            string normalPath,
            string selectedPath)
        {
            var buttonRect = EnsureRect(root.transform, name);
            ConfigureTopRight(buttonRect, position, size);
            var normal = LoadSprite(normalPath);
            var selected = LoadSprite(selectedPath);
            var image = EnsureImage(buttonRect.gameObject, normal, false);
            image.preserveAspect = true;
            image.raycastTarget = true;

            var button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.highlightedSprite = selected;
            state.pressedSprite = selected;
            state.selectedSprite = selected;
            button.spriteState = state;
        }

        private static Image[] ConfigureShortcutBar(GameObject root)
        {
            var mount = EnsureRect(root.transform, "ShortcutMount");
            ConfigureBottomCenter(mount, new Vector2(520f, 64f), new Vector2(360f, 75f));
            var normal = LoadSprite(GameUiPath + "装备格子.png");
            var selected = LoadSprite(GameUiPath + "装备格子（选中）.png");
            var offhand = LoadSprite(GameUiPath + "装备格子（副手）.png");
            var slots = new Image[3];

            for (var i = 0; i < 3; i++)
            {
                var slot = EnsureRect(mount, "ShortcutSlot_" + (i + 1));
                ConfigureRect(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(i * 91f, 0f), new Vector2(91f, 75f), new Vector2(0.5f, 0.5f));
                var image = EnsureImage(slot.gameObject, normal, false);
                image.preserveAspect = true;
                image.raycastTarget = true;
                var button = slot.GetComponent<Button>();
                if (button == null)
                {
                    button = slot.gameObject.AddComponent<Button>();
                }

                button.targetGraphic = image;
                button.transition = Selectable.Transition.SpriteSwap;
                var state = button.spriteState;
                state.highlightedSprite = selected;
                state.pressedSprite = selected;
                state.selectedSprite = selected;
                button.spriteState = state;
                slots[i] = image;
            }

            var offhandRect = EnsureRect(mount, "OffhandSlot");
            ConfigureRect(offhandRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(273f, 0f), new Vector2(90f, 75f), new Vector2(0.5f, 0.5f));
            var offhandImage = EnsureImage(offhandRect.gameObject, offhand, false);
            offhandImage.preserveAspect = true;
            offhandImage.raycastTarget = true;
            var offhandButton = offhandRect.GetComponent<Button>();
            if (offhandButton == null)
            {
                offhandButton = offhandRect.gameObject.AddComponent<Button>();
            }

            offhandButton.targetGraphic = offhandImage;
            offhandButton.transition = Selectable.Transition.SpriteSwap;
            var offhandState = offhandButton.spriteState;
            offhandState.highlightedSprite = selected;
            offhandState.pressedSprite = selected;
            offhandState.selectedSprite = selected;
            offhandButton.spriteState = offhandState;

            return new[] { slots[0], slots[1], slots[2], offhandImage };
        }

        private static void ConfigureTaskArea(GameObject root)
        {
            var panel = EnsureRect(root.transform, "TaskPanel");
            ConfigureTopLeft(panel, new Vector2(191f, -395f), new Vector2(288f, 150f));

            var header = EnsureRect(panel, "TaskHeader");
            ConfigureRect(header, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 56f), new Vector2(288f, 38f), new Vector2(0.5f, 0.5f));
            EnsureImage(header.gameObject, LoadSprite(GameUiPath + "任务标题.png"), false);
            var headerText = EnsureText(header.gameObject, "TaskTitle", "任务一", 22, new Color(0.25f, 0.18f, 0.13f, 1f));
            ConfigureRect(headerText.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.92f, 1f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            headerText.alignment = TextAnchor.MiddleLeft;

            var content = EnsureRect(panel, "TaskContent");
            ConfigureRect(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -19f), new Vector2(288f, 112f), new Vector2(0.5f, 0.5f));
            EnsureImage(content.gameObject, LoadSprite(GameUiPath + "任务内容框.png"), false);
            var contentText = EnsureText(content.gameObject, "TaskDescription", "1234578912345678", 18,
                new Color(0.28f, 0.21f, 0.16f, 1f));
            ConfigureRect(contentText.rectTransform, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.82f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            contentText.alignment = TextAnchor.UpperLeft;

            var toggle = EnsureRect(panel, "TaskToggle");
            ConfigureRect(toggle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(102f, 56f), new Vector2(29f, 16f), new Vector2(0.5f, 0.5f));
            EnsureImage(toggle.gameObject, LoadSprite(GameUiPath + "任务栏展开按钮.png"), false);
        }

        private static void ConfigureNarration(GameObject root, HUDPanel hudPanel)
        {
            var narration = EnsureRect(root.transform, "NarrationPanel");
            ConfigureRect(narration, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            narration.gameObject.SetActive(false);

            var frame = EnsureRect(narration, "NarrationFrame");
            ConfigureBottomAnchored(frame, Vector2.zero, new Vector2(1545f, 242f));
            EnsureImage(frame.gameObject, LoadSprite(SelfWhiteUiPath + "话框.png"), false);

            var text = EnsureText(narration.gameObject, "NarrationText", string.Empty, 30,
                new Color(0.86f, 0.82f, 0.74f, 1f));
            ConfigureBottomCenter(text.rectTransform, new Vector2(0f, 95f), new Vector2(1250f, 120f));
            text.alignment = TextAnchor.UpperLeft;

            var hint = EnsureRect(narration, "ContinueHint");
            ConfigureBottomCenter(hint, new Vector2(610f, 68f), new Vector2(221f, 76f));
            EnsureImage(hint.gameObject, LoadSprite(SelfWhiteUiPath + "按任意键继续.png"), false);

            var view = root.GetComponent<DialoguePresentationView>();
            if (view == null)
            {
                view = root.AddComponent<DialoguePresentationView>();
            }

            var serializedView = new SerializedObject(view);
            SetObjectReference(serializedView, "dialogueText", text);
            SetObjectReference(serializedView, "cgImage", null);
            SetObjectReference(serializedView, "flashOverlay", null);
            SetObjectReference(serializedView, "effectAudioSource", null);
            SetObjectReferenceArray(serializedView, "portraitSlots", 0);
            SetFloat(serializedView, "inactivePortraitOverlayAlpha", 0.55f);
            SetBool(serializedView, "dimAllPortraitsForNarration", false);
            SetString(serializedView, "dialogueIdFilter", string.Empty);
            SetBool(serializedView, "narrationOnly", true);
            SetObjectReference(serializedView, "requiredOpenPanel", hudPanel);
            SetObjectReference(serializedView, "presentationRoot", narration.gameObject);
            SetInt(serializedView, "dialogueFontSize", 30);
            SetBool(serializedView, "useTypewriter", true);
            SetFloat(serializedView, "charactersPerSecond", 36f);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        private static void ConfigureHudPanelBindings(HUDPanel panel, Image[] slots, Image staminaBar)
        {
            var serializedPanel = new SerializedObject(panel);
            SetObjectReferenceArray(serializedPanel, "shortcutSlots", 3, slots[0], slots[1], slots[2]);
            SetObjectReference(serializedPanel, "offhandSlot", slots[3]);

            var health = panel.transform.Find("Health");
            if (health != null)
            {
                var healthFills = new UnityEngine.Object[3];
                for (var i = 0; i < healthFills.Length; i++)
                {
                    healthFills[i] = health.Find("EmptyHealth_" + (i + 1) + "/Health")?.GetComponent<Image>();
                }

                SetObjectReferenceArray(serializedPanel, "healthFills", healthFills);
            }

            SetObjectReference(serializedPanel, "staminaFill", staminaBar);
            SetObjectReference(serializedPanel, "normalSlotSprite", LoadSprite(GameUiPath + "装备格子.png"));
            SetObjectReference(serializedPanel, "selectedSlotSprite", LoadSprite(GameUiPath + "装备格子（选中）.png"));
            SetObjectReference(serializedPanel, "offhandSlotSprite", LoadSprite(GameUiPath + "装备格子（副手）.png"));
            SetEnum(serializedPanel, "panelId", (int)PanelId.Hud);
            SetBool(serializedPanel, "pausesGame", false);
            SetBool(serializedPanel, "startClosed", true);
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
        }

        private static void ConfigureGuideGraphics(GameObject root)
        {
            var guideVisibility = root.GetComponent<MemorialArchive.Gameplay.Guide.View.GuideHudVisibility>();
            if (guideVisibility != null)
            {
                // Keep the existing Diary/Map references intact. The task area remains
                // part of the HUD's persistent locator composition and has no new guide rule.
                EditorUtility.SetDirty(guideVisibility);
            }
        }

        private static void ConfigureOpeningDialogue(GameObject root)
        {
            var dialoguePanel = root.transform.Find("DialoguePanel") as RectTransform;
            if (dialoguePanel == null)
            {
                return;
            }

            var panelImage = dialoguePanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = null;
                panelImage.color = new Color(1f, 1f, 1f, 0f);
                panelImage.raycastTarget = false;
            }

            var frame = EnsureRect(dialoguePanel, "DialogueFrame");
            ConfigureBottomAnchored(frame, Vector2.zero, new Vector2(1920f, 415f));
            EnsureImage(frame.gameObject, LoadSprite(StoryUiPath + "剧情框.png"), false);
            frame.SetSiblingIndex(1);

            var dialogueText = dialoguePanel.Find("DialogueText")?.GetComponent<Text>();
            if (dialogueText != null)
            {
                ConfigureRect(dialogueText.rectTransform, new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.30f),
                    Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                dialogueText.font = LoadFont();
                dialogueText.fontSize = 30;
                dialogueText.color = new Color(0.82f, 0.76f, 0.66f, 1f);
                dialogueText.alignment = TextAnchor.UpperLeft;
                dialogueText.resizeTextForBestFit = false;
                dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
                dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
                dialogueText.raycastTarget = false;
                EnsureCanvasRenderer(dialogueText.gameObject);
                dialogueText.transform.SetSiblingIndex(frame.GetSiblingIndex() + 1);
            }

            var hint = dialoguePanel.Find("Hint") as RectTransform;
            if (hint == null)
            {
                hint = EnsureRect(dialoguePanel, "Hint");
            }

            ConfigureBottomCenter(hint, new Vector2(745f, 110f), new Vector2(274f, 87f));
            var hintImage = EnsureImage(hint.gameObject, LoadSprite(StoryUiPath + "按任意键继续.png"), false);
            hintImage.preserveAspect = true;
            var hintText = hint.GetComponent<Text>();
            if (hintText != null)
            {
                hintText.text = string.Empty;
                hintText.enabled = false;
                hintText.raycastTarget = false;
                EnsureCanvasRenderer(hint.gameObject);
            }

            hint.SetSiblingIndex(frame.GetSiblingIndex() + 2);

            var view = dialoguePanel.GetComponent<DialoguePresentationView>();
            if (view != null)
            {
                var serializedView = new SerializedObject(view);
                SetObjectReference(serializedView, "dialogueText", dialogueText);
                SetObjectReference(serializedView, "cgImage", dialoguePanel.Find("CG")?.GetComponent<Image>());
                SetObjectReference(serializedView, "flashOverlay", dialoguePanel.Find("FlashOverlay")?.GetComponent<Image>());
                SetObjectReference(serializedView, "effectAudioSource", dialoguePanel.GetComponent<AudioSource>());
                SetString(serializedView, "dialogueIdFilter", string.Empty);
                SetBool(serializedView, "narrationOnly", false);
                SetObjectReference(serializedView, "requiredOpenPanel", null);
                SetObjectReference(serializedView, "presentationRoot", null);
                SetInt(serializedView, "dialogueFontSize", 30);
                SetBool(serializedView, "useTypewriter", true);
                SetFloat(serializedView, "charactersPerSecond", 36f);
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }

            // Unity's UI import can leave these components absent in hand-authored
            // prefabs. Add them explicitly so the saved asset imports cleanly.
            EnsureCanvasRenderers(dialoguePanel.gameObject);
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null && child is RectTransform)
            {
                return (RectTransform)child;
            }

            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void ConfigureTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            ConfigureRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0.5f, 0.5f));
        }

        private static void ConfigureTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            ConfigureRect(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), position, size, new Vector2(0.5f, 0.5f));
        }

        private static void ConfigureBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            ConfigureRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, size, new Vector2(0.5f, 0.5f));
        }

        private static void ConfigureBottomAnchored(RectTransform rect, Vector2 position, Vector2 size)
        {
            ConfigureRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, size, new Vector2(0.5f, 0f));
        }

        private static void ConfigureRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Vector2 pivot)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image EnsureImage(GameObject gameObject, Sprite sprite, bool preserveAspect)
        {
            var image = gameObject.GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            EnsureCanvasRenderer(gameObject);
            return image;
        }

        private static Text EnsureText(GameObject parent, string name, string content, int fontSize, Color color)
        {
            var rect = EnsureRect(parent.transform, name);
            var text = rect.GetComponent<Text>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<Text>();
            }

            text.font = LoadFont();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            EnsureCanvasRenderer(rect.gameObject);
            return text;
        }

        private static Font LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FallbackFontPath);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("UI2.0 sprite was not found: " + path);
            }

            return sprite;
        }

        private static void EnsureCanvasRenderer(GameObject gameObject)
        {
            if (gameObject != null && gameObject.GetComponent<CanvasRenderer>() == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }
        }

        private static void EnsureCanvasRenderers(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                EnsureCanvasRenderer(graphic.gameObject);
            }
        }

        private static void SetObjectReference(SerializedObject serializedObject, string name, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetObjectReferenceArray(SerializedObject serializedObject, string name, params UnityEngine.Object[] values)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
            {
                return;
            }

            property.arraySize = values != null ? values.Length : 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void SetObjectReferenceArray(SerializedObject serializedObject, string name, int size, params UnityEngine.Object[] values)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
            {
                return;
            }

            property.arraySize = size;
            for (var i = 0; i < size; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values != null && i < values.Length ? values[i] : null;
            }
        }

        private static void SetFloat(SerializedObject serializedObject, string name, float value)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetInt(SerializedObject serializedObject, string name, int value)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string name, bool value)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetString(SerializedObject serializedObject, string name, string value)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetEnum(SerializedObject serializedObject, string name, int value)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }
    }
}
#endif
