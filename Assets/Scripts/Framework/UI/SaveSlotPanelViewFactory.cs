using System;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>保存/读取面板的运行时 UGUI 结构，避免两套预制体的槽位布局分叉。</summary>
    internal static class SaveSlotPanelViewFactory
    {
        private static readonly Color SurfaceColor = new Color(0.075f, 0.09f, 0.11f, 0.98f);
        private static readonly Color SlotColor = new Color(0.16f, 0.2f, 0.23f, 1f);
        private static readonly Color AccentColor = new Color(0.6f, 0.78f, 0.76f, 1f);

        public static Transform CreateSurface(Transform parent, string name, string title, out Text statusLabel)
        {
            var surface = new GameObject(name, typeof(RectTransform), typeof(Image));
            surface.transform.SetParent(parent, false);
            var surfaceRect = surface.GetComponent<RectTransform>();
            surfaceRect.anchorMin = surfaceRect.anchorMax = new Vector2(0.5f, 0.5f);
            surfaceRect.anchoredPosition = Vector2.zero;
            surfaceRect.sizeDelta = new Vector2(700f, 500f);
            surface.GetComponent<Image>().color = SurfaceColor;

            var titleLabel = CreateText("Title", surface.transform, new Vector2(0f, 192f), new Vector2(600f, 52f), 30, TextAnchor.MiddleCenter);
            titleLabel.text = title;
            titleLabel.fontStyle = FontStyle.Bold;
            titleLabel.color = AccentColor;

            statusLabel = CreateText("Status", surface.transform, new Vector2(0f, 142f), new Vector2(600f, 32f), 16, TextAnchor.MiddleCenter);
            statusLabel.color = new Color(0.82f, 0.86f, 0.86f, 1f);
            return surface.transform;
        }

        public static Button CreateSlotButton(Transform parent, int slotIndex, out Text label)
        {
            var buttonObject = new GameObject($"Slot_{slotIndex + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f - slotIndex * 92f);
            rect.sizeDelta = new Vector2(590f, 76f);

            var image = buttonObject.GetComponent<Image>();
            image.color = SlotColor;
            var button = buttonObject.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
            button.colors = colors;

            label = CreateText("Label", buttonObject.transform, Vector2.zero, new Vector2(540f, 68f), 18, TextAnchor.MiddleLeft);
            label.color = new Color(0.94f, 0.96f, 0.94f, 1f);
            return button;
        }

        public static void CreateCloseButton(Transform parent, string text, Action onClick)
        {
            var buttonObject = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -190f);
            rect.sizeDelta = new Vector2(160f, 46f);
            buttonObject.GetComponent<Image>().color = new Color(0.28f, 0.35f, 0.36f, 1f);
            var button = buttonObject.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick?.Invoke());

            var label = CreateText("Label", buttonObject.transform, Vector2.zero, new Vector2(150f, 40f), 18, TextAnchor.MiddleCenter);
            label.text = text;
            label.fontStyle = FontStyle.Bold;
        }

        private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
