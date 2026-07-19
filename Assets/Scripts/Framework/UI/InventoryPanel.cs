using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Inventory.View;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private readonly List<InventorySlotView> slots = new List<InventorySlotView>();
        private InventorySlotView selectedSlot;
        private InventorySlotView dragSource;
        private RectTransform dragVisual;
        private Text statusLabel;
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

            CreateSection("场景容器 2 x 2", new Vector2(-315f, 150f));
            CreateGrid(InventoryContainerKind.SceneContainer, -1, new Vector2(-315f, 55f), 2, 2);
            CreateSection("背包 3 x 3", new Vector2(-40f, 150f));
            CreateGrid(InventoryContainerKind.Backpack, -1, new Vector2(-40f, 20f), 3, 3);
            CreateSection("副手栏", new Vector2(285f, 150f));
            CreateGrid(InventoryContainerKind.Offhand, 0, new Vector2(285f, 55f), 1, 1);
            CreateSection("快捷栏（按 1 / 2 / 3 选择）", new Vector2(0f, -185f));
            CreateGrid(InventoryContainerKind.ShortcutBar, 0, new Vector2(0f, -245f), 3, 1);

            statusLabel = CreateText("InventoryStatus", transform, new Vector2(0f, -325f), new Vector2(760f, 38f), 15, TextAnchor.MiddleCenter);
            statusLabel.color = new Color(0.86f, 0.9f, 0.95f, 1f);
            statusLabel.text = "从场景容器拖拽物品；点击也可以完成同样操作。";
        }

        private void CreateGrid(InventoryContainerKind kind, int baseSlotIndex, Vector2 center, int width, int height)
        {
            var startX = center.x - (width - 1) * (SlotSize + SlotGap) * 0.5f;
            var startY = center.y + (height - 1) * (SlotSize + SlotGap) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var slotObject = new GameObject(kind + "_" + x + "_" + y, typeof(RectTransform), typeof(Image), typeof(InventorySlotView));
                    slotObject.transform.SetParent(transform, false);
                    var slotRect = slotObject.GetComponent<RectTransform>();
                    slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                    slotRect.anchoredPosition = new Vector2(startX + x * (SlotSize + SlotGap), startY - y * (SlotSize + SlotGap));
                    slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);
                    var image = slotObject.GetComponent<Image>();
                    image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
                    var slot = slotObject.GetComponent<InventorySlotView>();
                    var slotIndex = kind == InventoryContainerKind.ShortcutBar ? baseSlotIndex + x : -1;
                    slot.Configure(this, kind, x, y, slotIndex);
                    slots.Add(slot);
                }
            }
        }

        private void CreateSection(string text, Vector2 position)
        {
            var label = CreateText("Section", transform, position, new Vector2(220f, 28f), 17, TextAnchor.MiddleCenter);
            label.color = new Color(0.96f, 0.78f, 0.4f, 1f);
            label.text = text;
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
                var itemName = placement != null ? GetItemName(placement.item.itemId) : string.Empty;
                slot.Refresh(placement?.item?.instanceId, itemName, placement?.item?.quantity ?? 0, slot == selectedSlot);
            }
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
