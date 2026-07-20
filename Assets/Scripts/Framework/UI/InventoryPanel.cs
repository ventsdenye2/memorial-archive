using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Inventory.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Runtime UI for the first-stage inventory contract.  The rules remain in
    /// InventorySystem; this class only renders slots and turns UI intent into
    /// system calls.
    /// </summary>
    public sealed class InventoryPanel : BasePanel
    {
        private const float SlotSize = 72f;
        private const float SlotGap = 8f;

        [Header("Stage-one artwork")]
        [SerializeField] private Sprite maskSprite;
        [SerializeField] private Sprite scenePanelSprite;
        [SerializeField] private Sprite backpackPanelSprite;
        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Sprite selectedSlotSprite;
        [SerializeField] private Sprite hudSlotSprite;
        [SerializeField] private Sprite hudSelectedSlotSprite;
        [SerializeField] private Sprite descriptionSprite;
        [SerializeField] private Sprite closeSprite;
        [SerializeField] private Sprite selectSprite;
        [SerializeField] private Sprite selectHighlightedSprite;
        [SerializeField] private Sprite cancelSprite;
        [SerializeField] private Sprite cancelHighlightedSprite;

        private readonly List<InventorySlotView> slots = new List<InventorySlotView>();
        private InventorySlotView selectedSlot;
        private InventorySlotView dragSource;
        private RectTransform dragVisual;
        private Text statusLabel;
        private Text descriptionLabel;
        private bool built;

        protected override void Awake()
        {
            base.Awake();
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DestroyDragVisual();
        }

        public override void Open()
        {
            base.Open();
            BuildIfNeeded();
            Subscribe();
            Refresh();
        }

        public override void Close()
        {
            selectedSlot = null;
            dragSource = null;
            DestroyDragVisual();
            base.Close();
        }

        public void HandleSlotClicked(InventorySlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            if (selectedSlot == null)
            {
                if (!string.IsNullOrEmpty(slot.ItemInstanceId))
                {
                    selectedSlot = slot;
                    SetStatus("已选中物品：点击目标格，或直接拖拽。", false);
                    Refresh();
                }
                return;
            }

            if (selectedSlot == slot)
            {
                selectedSlot = null;
                SetStatus("已取消选择。", false);
                Refresh();
                return;
            }

            MoveTo(selectedSlot, slot);
        }

        public void BeginDrag(InventorySlotView slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.ItemInstanceId))
            {
                return;
            }

            dragSource = slot;
            selectedSlot = slot;
            CreateDragVisual(slot);
            Refresh();
        }

        public void Drag(Vector2 screenPosition)
        {
            if (dragVisual != null)
            {
                dragVisual.position = screenPosition;
            }
        }

        public void DropOn(InventorySlotView destination)
        {
            if (dragSource != null && destination != null && dragSource != destination)
            {
                MoveTo(dragSource, destination);
            }
        }

        public void EndDrag()
        {
            dragSource = null;
            DestroyDragVisual();
        }

        private void MoveTo(InventorySlotView source, InventorySlotView destination)
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventory == null || string.IsNullOrEmpty(source.ItemInstanceId))
            {
                SetStatus("背包系统尚未准备好。", true);
                return;
            }

            var success = false;
            if (!string.IsNullOrEmpty(destination.ItemInstanceId))
            {
                success = inventory.TryMoveOrSwap(
                    source.ItemInstanceId,
                    destination.ItemInstanceId,
                    destination.ContainerKind,
                    destination.X,
                    destination.Y,
                    destination.SlotIndex);
            }
            else
            {
                switch (destination.ContainerKind)
                {
                    case InventoryContainerKind.Backpack:
                        success = inventory.TryMoveToBackpack(source.ItemInstanceId, destination.X, destination.Y);
                        break;
                    case InventoryContainerKind.ShortcutBar:
                        success = inventory.TryMoveToShortcut(source.ItemInstanceId, destination.SlotIndex);
                        break;
                    case InventoryContainerKind.Offhand:
                        success = inventory.TryMoveToOffhand(source.ItemInstanceId);
                        break;
                    case InventoryContainerKind.SceneContainer:
                        success = source.ContainerKind == InventoryContainerKind.SceneContainer
                            ? inventory.TryMoveWithinActiveSceneContainer(source.ItemInstanceId, destination.X, destination.Y)
                            : inventory.TryDropToSceneContainer(source.ItemInstanceId, null, destination.X, destination.Y);
                        break;
                }
            }

            selectedSlot = null;
            SetStatus(success ? "物品已移动、交换或合并。" : "无法放入该位置：格子已占用或物品类型不允许。", !success);
            Refresh();
        }

        private void BuildIfNeeded()
        {
            if (built)
            {
                return;
            }

            built = true;
            var rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            var mask = CreateImage("InventoryMask", transform, Vector2.zero, new Vector2(1920f, 1080f), maskSprite);
            var maskRect = mask.rectTransform;
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
            mask.raycastTarget = true;
            mask.transform.SetAsFirstSibling();

            CreateImage("SceneContainerPanel", transform, new Vector2(-390f, 70f), new Vector2(400f, 399f), scenePanelSprite);
            CreateImage("BackpackPanel", transform, new Vector2(310f, 15f), new Vector2(484f, 652f), backpackPanelSprite);
            CreateGrid(InventoryContainerKind.SceneContainer, -1, new Vector2(-390f, 75f), 2, 2,
                new Vector2(128f, 86f), new Vector2(8f, 8f), slotSprite, selectedSlotSprite);
            CreateGrid(InventoryContainerKind.Backpack, -1, new Vector2(310f, 105f), 3, 3,
                new Vector2(116f, 78f), new Vector2(4f, 3f), slotSprite, selectedSlotSprite);

            CreateImage("ItemDescriptionFrame", transform, new Vector2(310f, -175f), new Vector2(412f, 83f), descriptionSprite);
            descriptionLabel = CreateText("ItemDescription", transform, new Vector2(310f, -175f), new Vector2(360f, 64f), 15, TextAnchor.MiddleLeft);
            descriptionLabel.color = new Color(0.18f, 0.11f, 0.08f, 1f);
            descriptionLabel.text = "选择物品后，这里会显示名称和数量。";

            CreateButton("CloseButton", transform, new Vector2(650f, 385f), new Vector2(54f, 54f), closeSprite, null, CloseInventory);
            CreateButton("SelectButton", transform, new Vector2(230f, -275f), new Vector2(126f, 66f), selectSprite, selectHighlightedSprite, SelectCurrent);
            CreateButton("CancelButton", transform, new Vector2(390f, -275f), new Vector2(126f, 66f), cancelSprite, cancelHighlightedSprite, CancelSelection);

            // The reference keeps the HUD row visible while the bag is open. These
            // slots remain the actual drag/drop targets: 1, 2, 3, then offhand.
            CreateGrid(InventoryContainerKind.ShortcutBar, 0, new Vector2(-45f, -455f), 3, 1,
                new Vector2(75f, 73f), new Vector2(16f, 0f), hudSlotSprite, hudSelectedSlotSprite);
            CreateGrid(InventoryContainerKind.Offhand, 0, new Vector2(137f, -455f), 1, 1,
                new Vector2(75f, 73f), Vector2.zero, hudSlotSprite, hudSelectedSlotSprite);
            for (var i = 0; i < 3; i++)
            {
                CreateHint((i + 1).ToString(), new Vector2(-136f + i * 91f, -430f));
            }
            CreateHint("副", new Vector2(137f, -430f));

            statusLabel = CreateText("InventoryStatus", transform, new Vector2(-390f, -165f), new Vector2(390f, 58f), 14, TextAnchor.MiddleCenter);
            statusLabel.color = new Color(0.2f, 0.13f, 0.09f, 1f);
            statusLabel.text = "拖拽物品到背包、快捷栏或副手栏。";
        }

        private void CreateGrid(
            InventoryContainerKind kind,
            int baseSlotIndex,
            Vector2 center,
            int width,
            int height,
            Vector2 cellSize,
            Vector2 gap,
            Sprite normalSprite,
            Sprite highlightSprite)
        {
            var startX = center.x - (width - 1) * (cellSize.x + gap.x) * 0.5f;
            var startY = center.y + (height - 1) * (cellSize.y + gap.y) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var slotObject = new GameObject(kind + "_" + x + "_" + y, typeof(RectTransform), typeof(Image), typeof(InventorySlotView));
                    slotObject.transform.SetParent(transform, false);
                    var slotRect = slotObject.GetComponent<RectTransform>();
                    slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                    slotRect.anchoredPosition = new Vector2(startX + x * (cellSize.x + gap.x), startY - y * (cellSize.y + gap.y));
                    slotRect.sizeDelta = cellSize;
                    var image = slotObject.GetComponent<Image>();
                    image.sprite = normalSprite;
                    image.color = normalSprite != null ? Color.white : new Color(0.12f, 0.14f, 0.18f, 0.92f);
                    var slot = slotObject.GetComponent<InventorySlotView>();
                    var slotIndex = kind == InventoryContainerKind.ShortcutBar ? baseSlotIndex + x : -1;
                    if (kind == InventoryContainerKind.Offhand)
                    {
                        slotIndex = 0;
                    }
                    slot.Configure(this, kind, x, y, slotIndex, normalSprite, highlightSprite);
                    slots.Add(slot);
                }
            }
        }

        private void CreateHint(string text, Vector2 position)
        {
            var label = CreateText("SlotHint", transform, position, new Vector2(28f, 24f), 16, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.22f, 0.14f, 0.09f, 1f);
            label.text = text;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0.72f);
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Sprite normal,
            Sprite highlighted,
            UnityAction callback)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = buttonObject.GetComponent<Image>();
            image.sprite = normal;
            image.color = Color.white;
            var button = buttonObject.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (highlighted != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                var state = button.spriteState;
                state.highlightedSprite = highlighted;
                state.selectedSprite = highlighted;
                state.pressedSprite = highlighted;
                button.spriteState = state;
            }
            button.onClick.AddListener(callback);
            return button;
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

        private void Refresh()
        {
            if (!built)
            {
                return;
            }

            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventory == null)
            {
                return;
            }

            foreach (var slot in slots)
            {
                var placement = FindPlacement(inventory, slot);
                var config = placement?.item == null ? null : GameRoot.Instance.Context.Configs.GetItem(placement.item.itemId);
                var itemName = config != null ? config.ItemName : placement != null ? GetItemName(placement.item.itemId) : string.Empty;
                slot.Refresh(placement?.item?.instanceId, itemName, config != null ? config.Icon : null,
                    placement?.item?.quantity ?? 0, slot == selectedSlot);
            }

            if (descriptionLabel != null)
            {
                var selectedPlacement = selectedSlot != null ? FindPlacement(inventory, selectedSlot) : null;
                descriptionLabel.text = selectedPlacement?.item == null
                    ? "选择物品后，这里会显示名称和数量。"
                    : GetItemName(selectedPlacement.item.itemId) + "  ×" + selectedPlacement.item.quantity;
            }
        }

        private void CloseInventory()
        {
            var root = GameRoot.Instance;
            var inventory = root?.GetSystem<InventorySystem>();
            if (inventory != null && inventory.HasOpenSceneContainer)
            {
                root.Context.Events.Publish(new ContainerClosedEvent());
                return;
            }

            root?.Context?.UI?.Close(PanelId.Inventory);
        }

        private void SelectCurrent()
        {
            SetStatus(selectedSlot == null ? "请先选择一个物品。" : "已选择，可拖到目标格。", selectedSlot == null);
        }

        private void CancelSelection()
        {
            selectedSlot = null;
            SetStatus("已取消选择。", false);
            Refresh();
        }

        private static InventoryItemPlacement FindPlacement(InventorySystem inventory, InventorySlotView slot)
        {
            IEnumerable<InventoryItemPlacement> candidates = null;
            if (slot.ContainerKind == InventoryContainerKind.SceneContainer)
            {
                candidates = inventory.GetActiveSceneContainer()?.items;
            }
            else
            {
                candidates = inventory.GetPlayerPlacements(slot.ContainerKind);
            }

            if (candidates == null)
            {
                return null;
            }

            foreach (var placement in candidates)
            {
                if (placement == null || placement.item == null)
                {
                    continue;
                }

                if (slot.ContainerKind == InventoryContainerKind.ShortcutBar && placement.slotIndex == slot.SlotIndex)
                {
                    return placement;
                }

                if (slot.ContainerKind == InventoryContainerKind.Offhand && placement.slotIndex == 0)
                {
                    return placement;
                }

                if (slot.ContainerKind == InventoryContainerKind.Backpack &&
                    slot.X >= placement.x && slot.X < placement.x + placement.width &&
                    slot.Y >= placement.y && slot.Y < placement.y + placement.height)
                {
                    return placement;
                }

                if (slot.ContainerKind == InventoryContainerKind.SceneContainer && placement.x == slot.X && placement.y == slot.Y)
                {
                    return placement;
                }
            }

            return null;
        }

        private static string GetItemName(int itemId)
        {
            var config = GameRoot.Instance?.Context?.Configs?.GetItem(itemId);
            return config != null ? config.ItemName : "未知道具 " + itemId;
        }

        private void Subscribe()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events == null)
            {
                return;
            }

            events.Unsubscribe<InventoryChangedEvent>(HandleInventoryChanged);
            events.Unsubscribe<InventoryMoveFailedEvent>(HandleMoveFailed);
            events.Subscribe<InventoryChangedEvent>(HandleInventoryChanged);
            events.Subscribe<InventoryMoveFailedEvent>(HandleMoveFailed);
        }

        private void Unsubscribe()
        {
            var events = GameRoot.Instance?.Context?.Events;
            events?.Unsubscribe<InventoryChangedEvent>(HandleInventoryChanged);
            events?.Unsubscribe<InventoryMoveFailedEvent>(HandleMoveFailed);
        }

        private void HandleInventoryChanged(InventoryChangedEvent evt) => Refresh();
        private void HandleMoveFailed(InventoryMoveFailedEvent evt) => SetStatus(evt.Reason, true);

        private void SetStatus(string message, bool isError)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message;
            statusLabel.color = isError ? new Color(1f, 0.48f, 0.42f, 1f) : new Color(0.7f, 0.94f, 0.75f, 1f);
        }

        private void CreateDragVisual(InventorySlotView source)
        {
            DestroyDragVisual();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var visual = new GameObject("InventoryDragVisual", typeof(RectTransform), typeof(Image));
            visual.transform.SetParent(canvas.transform, false);
            dragVisual = visual.GetComponent<RectTransform>();
            dragVisual.sizeDelta = new Vector2(SlotSize, SlotSize);
            var image = visual.GetComponent<Image>();
            image.color = new Color(0.86f, 0.63f, 0.18f, 0.75f);
            image.raycastTarget = false;
            var label = CreateText("Label", visual.transform, Vector2.zero, new Vector2(SlotSize, SlotSize), 13, TextAnchor.MiddleCenter);
            label.text = GetItemName(GetItemId(source));
        }

        private int GetItemId(InventorySlotView slot)
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            return inventory == null ? 0 : FindPlacement(inventory, slot)?.item?.itemId ?? 0;
        }

        private void DestroyDragVisual()
        {
            if (dragVisual != null)
            {
                Destroy(dragVisual.gameObject);
                dragVisual = null;
            }
        }
    }
}
