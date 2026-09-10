using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Shared binding and authoring helpers for the save/load prefabs.
    /// Runtime code only resolves the authored hierarchy; the editor helpers
    /// are used by UI2SaveMigration to write the hierarchy into the prefab.
    /// </summary>
    internal static class SaveSlotPanelViewFactory
    {
        // The closest authored slot centres are 135 units apart.
        private const float SlotHitAreaHeight = 135f;

        public static Transform FindSurface(Transform panel)
        {
            if (panel == null)
            {
                return null;
            }

            return panel.Find("SaveSlotSurface")
                ?? panel.Find("SaveSlotRuntime")
                ?? panel;
        }

        public static Button FindSlotButton(Transform surface, int slotIndex, out Text label)
        {
            label = null;
            if (surface == null)
            {
                return null;
            }

            var oneBased = slotIndex + 1;
            var slot = surface.Find($"Slot_{oneBased}")
                ?? surface.Find($"Slot{oneBased}")
                ?? surface.Find($"Slot_{slotIndex}")
                ?? surface.Find($"Slot{slotIndex}");
            if (slot == null)
            {
                return null;
            }

            label = (slot.Find("SlotLabel") ?? slot.Find("Label"))?.GetComponent<Text>();
            return slot.GetComponent<Button>();
        }

        public static Image FindDisabledSlot(Transform surface, int slotIndex, out Text label)
        {
            label = null;
            if (surface == null)
            {
                return null;
            }

            var oneBased = slotIndex + 1;
            var slot = surface.Find($"Slot_{oneBased}")
                ?? surface.Find($"Slot{oneBased}")
                ?? surface.Find($"Slot_{slotIndex}")
                ?? surface.Find($"Slot{slotIndex}");
            if (slot == null)
            {
                return null;
            }

            label = (slot.Find("SlotLabel") ?? slot.Find("Label"))?.GetComponent<Text>();
            return slot.GetComponent<Image>();
        }

        public static Button FindCloseButton(Transform surface)
        {
            return surface?.Find("CancelButton")?.GetComponent<Button>() ??
                   surface?.Find("Close")?.GetComponent<Button>() ??
                   surface?.Find("CloseButton")?.GetComponent<Button>();
        }

        public static void ConfigureSlotButton(Button button, Sprite normal, Sprite highlighted, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = normal;
                image.overrideSprite = null;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
                // The authored save cards overlap vertically. The card artwork
                // must not own the raycast, otherwise the top card captures the
                // pointer over all cards below it.
                image.raycastTarget = false;
            }

            var hitArea = EnsureHitArea(button.transform);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;
            button.interactable = interactable;

            var hoverView = button.GetComponent<SaveSlotHoverView>();
            if (hoverView == null)
            {
                hoverView = button.gameObject.AddComponent<SaveSlotHoverView>();
            }

            hoverView.Configure(image, normal, highlighted);
        }

        private static Image EnsureHitArea(Transform slot)
        {
            var hitArea = slot.Find("HitArea")?.GetComponent<Image>();
            if (hitArea == null)
            {
                var hitObject = new GameObject("HitArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hitObject.transform.SetParent(slot, false);
                hitArea = hitObject.GetComponent<Image>();
            }

            var rootRect = slot as RectTransform;
            var hitRect = hitArea.rectTransform;
            hitRect.anchorMin = hitRect.anchorMax = new Vector2(0.5f, 0.5f);
            hitRect.anchoredPosition = Vector2.zero;
            hitRect.sizeDelta = new Vector2(rootRect != null ? rootRect.rect.width : 837f, SlotHitAreaHeight);
            hitRect.localScale = Vector3.one;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            return hitArea;
        }

        public static void ConfigureDisabledSlot(Image image, Sprite normal, Sprite highlighted)
        {
            if (image == null)
            {
                return;
            }

            image.color = Color.white;
            image.raycastTarget = false;
            EnsureHitArea(image.transform);
            var hoverView = image.GetComponent<SaveSlotHoverView>();
            if (hoverView == null)
            {
                hoverView = image.gameObject.AddComponent<SaveSlotHoverView>();
            }

            hoverView.Configure(image, normal, highlighted);
        }

#if UNITY_EDITOR
        public static Transform CreateAuthoredSurface(Transform parent, string name)
        {
            var surfaceObject = new GameObject(name, typeof(RectTransform));
            surfaceObject.transform.SetParent(parent, false);
            var surfaceRect = surfaceObject.GetComponent<RectTransform>();
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = surfaceRect.offsetMax = Vector2.zero;
            surfaceRect.localScale = Vector3.one;
            return surfaceObject.transform;
        }

        public static Image CreateAuthoredImage(Transform parent, string name, Vector2 position,
            Vector2 size, Sprite sprite, bool raycastTarget)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0.72f);
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateAuthoredText(Transform parent, string name, Vector2 position,
            Vector2 size, int fontSize, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateAuthoredSlotButton(Transform parent, int slotIndex,
            Sprite normal, Sprite highlighted, Vector2 position, out Text label)
        {
            var buttonObject = new GameObject("Slot_" + (slotIndex + 1),
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = normal != null ? normal.rect.size : new Vector2(837f, 285f);
            rect.localScale = Vector3.one;
            var button = buttonObject.GetComponent<Button>();
            ConfigureSlotButton(button, normal, highlighted, true);
            label = CreateAuthoredText(buttonObject.transform, "SlotLabel", new Vector2(100f, -22f),
                new Vector2(330f, 58f), 16, TextAnchor.MiddleCenter, new Color(0.25f, 0.16f, 0.1f, 1f));
            label.text = "空档";
            return button;
        }

        public static Image CreateAuthoredDisabledSlot(Transform parent, int slotIndex, Sprite normal, Sprite highlighted,
            Vector2 position, out Text label)
        {
            var image = CreateAuthoredImage(parent, "Slot_" + (slotIndex + 1), position,
                normal != null ? normal.rect.size : new Vector2(837f, 285f), normal, false);
            ConfigureDisabledSlot(image, normal, highlighted);
            label = CreateAuthoredText(image.transform, "SlotLabel", new Vector2(100f, -22f),
                new Vector2(330f, 58f), 16, TextAnchor.MiddleCenter, new Color(0.39f, 0.2f, 0.12f, 0.94f));
            label.text = "第一阶段预留";
            return image;
        }

        public static Button CreateAuthoredCloseButton(Transform parent, Sprite sprite, Vector2 position)
        {
            var buttonObject = new GameObject("CancelButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sprite != null ? sprite.rect.size : new Vector2(481f, 183f);
            rect.localScale = Vector3.one;
            var image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = true;
            var button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1f);
            colors.pressedColor = new Color(0.82f, 0.7f, 0.56f, 1f);
            button.colors = colors;
            return button;
        }
#endif
    }
}
