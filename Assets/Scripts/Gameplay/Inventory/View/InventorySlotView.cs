using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Inventory.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Inventory.View
{
    [RequireComponent(typeof(Image))]
    public sealed class InventorySlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private InventoryContainerKind containerKind = InventoryContainerKind.Backpack;
        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private int slotIndex = -1;

        [SerializeField] private InventoryPanel owner;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        private string itemInstanceId;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;

        public InventoryContainerKind ContainerKind => containerKind;
        public int X => x;
        public int Y => y;
        public int SlotIndex => slotIndex;
        public string ItemInstanceId => itemInstanceId;

        public void Configure(
            InventoryPanel panel,
            InventoryContainerKind kind,
            int slotX,
            int slotY,
            int index,
            Sprite idleSprite = null,
            Sprite highlightSprite = null)
        {
            owner = panel;
            containerKind = kind;
            x = slotX;
            y = slotY;
            slotIndex = index;
            normalSprite = idleSprite;
            selectedSprite = highlightSprite;
            background = GetComponent<Image>();
            if (background != null && normalSprite != null)
            {
                background.sprite = normalSprite;
                background.color = Color.white;
            }
            EnsureIcon();
            EnsureLabel();
        }

        public void Refresh(string instanceId, string displayName, Sprite itemIcon, int quantity, bool selected)
        {
            itemInstanceId = instanceId;
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (background != null)
            {
                if (normalSprite != null)
                {
                    background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
                    background.color = Color.white;
                }
                else
                {
                    background.color = selected
                        ? new Color(0.86f, 0.63f, 0.18f, 0.96f)
                        : string.IsNullOrEmpty(instanceId)
                            ? new Color(0.12f, 0.14f, 0.18f, 0.92f)
                            : new Color(0.23f, 0.37f, 0.46f, 0.96f);
                }
            }

            if (icon != null)
            {
                icon.sprite = itemIcon;
                icon.enabled = itemIcon != null;
            }

            if (label != null)
            {
                // UI2.0 keeps item names in the parchment description area;
                // only a compact stack count belongs on a slot itself.
                label.text = string.IsNullOrEmpty(instanceId)
                    ? string.Empty
                    : quantity > 1 ? "×" + quantity : string.Empty;
            }
        }

        public void OnPointerClick(PointerEventData eventData) => owner?.HandleSlotClicked(this);
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(this, eventData.position);
        public void OnDrag(PointerEventData eventData) => owner?.Drag(eventData.position);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
        public void OnDrop(PointerEventData eventData) => owner?.DropOn(this);

        private void EnsureIcon()
        {
            if (icon != null)
            {
                return;
            }

            var iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(transform, false);
            var rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.18f, 0.18f);
            rect.anchorMax = new Vector2(0.82f, 0.82f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
        }

        private void EnsureLabel()
        {
            if (label != null)
            {
                return;
            }

            var labelObject = new GameObject("ItemLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(transform, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 12;
            label.alignment = TextAnchor.LowerCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = new Color(0.2f, 0.13f, 0.09f, 1f);
            label.raycastTarget = false;
        }
    }
}
