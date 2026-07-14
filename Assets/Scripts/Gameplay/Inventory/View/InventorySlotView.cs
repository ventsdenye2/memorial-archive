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

        private InventoryPanel owner;
        private Image background;
        private Text label;
        private string itemInstanceId;

        public InventoryContainerKind ContainerKind => containerKind;
        public int X => x;
        public int Y => y;
        public int SlotIndex => slotIndex;
        public string ItemInstanceId => itemInstanceId;

        public void Configure(InventoryPanel panel, InventoryContainerKind kind, int slotX, int slotY, int index)
        {
            owner = panel;
            containerKind = kind;
            x = slotX;
            y = slotY;
            slotIndex = index;
            background = GetComponent<Image>();
            EnsureLabel();
        }

        public void Refresh(string instanceId, string displayName, int quantity, bool selected)
        {
            itemInstanceId = instanceId;
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (background != null)
            {
                background.color = selected
                    ? new Color(0.86f, 0.63f, 0.18f, 0.96f)
                    : string.IsNullOrEmpty(instanceId)
                        ? new Color(0.12f, 0.14f, 0.18f, 0.92f)
                        : new Color(0.23f, 0.37f, 0.46f, 0.96f);
            }

            if (label != null)
            {
                label.text = string.IsNullOrEmpty(instanceId)
                    ? string.Empty
                    : quantity > 1 ? displayName + " x" + quantity : displayName;
            }
        }

        public void OnPointerClick(PointerEventData eventData) => owner?.HandleSlotClicked(this);
        public void OnBeginDrag(PointerEventData eventData) => owner?.BeginDrag(this);
        public void OnDrag(PointerEventData eventData) => owner?.Drag(eventData.position);
        public void OnEndDrag(PointerEventData eventData) => owner?.EndDrag();
        public void OnDrop(PointerEventData eventData) => owner?.DropOn(this);

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
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }
}
