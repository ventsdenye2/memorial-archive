using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Inventory.View;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Renders the stage-one inventory. Its layout is authored in the prefab;
    /// this class only refreshes static slot views and routes UI intent.
    /// </summary>
    public sealed class InventoryPanel : BasePanel
    {
        private const float SlotSize = 72f;

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

        // UI2.0 has separate slot artwork for the scene container, backpack,
        // and the four-cell equipment row. Keep the original fields above as
        // fallbacks so older prefabs can still be opened and rebuilt.
        [Header("UI2.0 slot artwork")]
        [SerializeField] private Sprite sceneSlotSprite;
        [SerializeField] private Sprite sceneSelectedSlotSprite;
        [SerializeField] private Sprite backpackSlotSprite;
        [SerializeField] private Sprite backpackSelectedSlotSprite;
        [SerializeField] private Sprite equipmentSlotSprite;
        [SerializeField] private Sprite equipmentSelectedSlotSprite;

        [Header("Prefab layout")]
        [SerializeField] private List<InventorySlotView> slots = new List<InventorySlotView>();
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text descriptionLabel;

        private InventorySlotView selectedSlot;
        private InventorySlotView dragSource;
        private RectTransform dragVisual;
        private Canvas dragCanvas;
        private GameObject discardConfirmationRoot;
        private Text discardPrompt;
        private string pendingDiscardInstanceId;
        private bool dragDroppedOnSlot;

        protected override void Awake()
        {
            base.Awake();
            if (slots.Count == 0)
            {
                Debug.LogError("InventoryPanel has no authored slot views. Rebuild its prefab layout from Tools/Memorial Archive.", this);
            }

            ConfigureActionButtons();
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
            HideDiscardConfirmation();
        }

        public override void Open()
        {
            base.Open();
            Subscribe();
            Refresh();
        }

        public override void Close()
        {
            selectedSlot = null;
            dragSource = null;
            DestroyDragVisual();
            HideDiscardConfirmation();
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

        public void BeginDrag(InventorySlotView slot, Vector2 screenPosition)
        {
            if (slot == null || string.IsNullOrEmpty(slot.ItemInstanceId))
            {
                return;
            }

            dragSource = slot;
            dragDroppedOnSlot = false;
            selectedSlot = slot;
            CreateDragVisual(slot);
            PositionDragVisual(screenPosition);
            Refresh();
        }

        public void Drag(Vector2 screenPosition)
        {
            if (dragVisual != null)
            {
                PositionDragVisual(screenPosition);
            }
        }

        public void DropOn(InventorySlotView destination)
        {
            if (dragSource == null || destination == null)
            {
                return;
            }

            // Releasing on any inventory slot is a deliberate in-panel drop,
            // even when the requested move is rejected by its own rules.
            dragDroppedOnSlot = true;
            if (dragSource != destination)
            {
                MoveTo(dragSource, destination);
            }
        }

        public void EndDrag()
        {
            if (dragSource != null && !dragDroppedOnSlot && IsPlayerSlot(dragSource))
            {
                DropOutsidePlayerInventory(dragSource);
            }

            dragSource = null;
            dragDroppedOnSlot = false;
            DestroyDragVisual();
        }

        public void CloseFromButton()
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

        public void EquipFromButton()
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            var placement = inventory != null && selectedSlot != null &&
                            selectedSlot.ContainerKind == InventoryContainerKind.Backpack
                ? FindPlacement(inventory, selectedSlot)
                : null;
            if (placement?.item == null)
            {
                SetStatus("请先选择背包中的物品。", true);
                return;
            }

            // Full or incompatible equipment targets deliberately leave the
            // selection and inventory unchanged.
            if (!inventory.TryEquipToFirstAvailableSlot(placement.item.instanceId))
            {
                return;
            }

            selectedSlot = null;
            SetStatus("物品已装备到空闲栏位。", false);
            Refresh();
        }

        public void DiscardFromButton()
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            var placement = inventory != null && selectedSlot != null &&
                            selectedSlot.ContainerKind == InventoryContainerKind.Backpack
                ? FindPlacement(inventory, selectedSlot)
                : null;
            if (placement?.item == null)
            {
                SetStatus("请先选择背包中的物品。", true);
                return;
            }

            ShowDiscardConfirmation(placement.item.instanceId, GetItemName(placement.item.itemId));
        }

#if UNITY_EDITOR
        public void RebuildPrefabLayoutForEditor()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            slots.Clear();
            statusLabel = null;
            descriptionLabel = null;

            var mask = CreateImage("InventoryMask", transform, Vector2.zero, new Vector2(1920f, 1080f), maskSprite);
            var maskRect = mask.rectTransform;
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
            mask.raycastTarget = true;
            mask.transform.SetAsFirstSibling();

            // Coordinates are measured from the 1920x1080 UI2.0 reference:
            // the item panel is centered at (523,510), the backpack at
            // (1345,497), and each grid starts at the marked top-left cell.
            CreateImage("SceneContainer", transform, new Vector2(-437f, 30f), new Vector2(678f, 551f), scenePanelSprite);
            CreateImage("BackpackPanel", transform, new Vector2(385f, 43f), new Vector2(1068f, 960f), backpackPanelSprite);
            var sceneNormal = sceneSlotSprite != null ? sceneSlotSprite : slotSprite;
            var sceneSelected = sceneSelectedSlotSprite != null ? sceneSelectedSlotSprite : selectedSlotSprite;
            var backpackNormal = backpackSlotSprite != null ? backpackSlotSprite : slotSprite;
            var backpackSelected = backpackSelectedSlotSprite != null ? backpackSelectedSlotSprite : selectedSlotSprite;
            var equipmentNormal = equipmentSlotSprite != null ? equipmentSlotSprite : hudSlotSprite;
            var equipmentSelected = equipmentSelectedSlotSprite != null ? equipmentSelectedSlotSprite : hudSelectedSlotSprite;
            CreateGrid(InventoryContainerKind.SceneContainer, -1, new Vector2(-340f, -26f), 2, 2,
                new Vector2(129f, 109f), Vector2.zero, sceneNormal, sceneSelected);
            CreateGrid(InventoryContainerKind.Backpack, -1, new Vector2(398.5f, 214f), 3, 3,
                new Vector2(129f, 108f), Vector2.zero, backpackNormal, backpackSelected);

            // The parchment is part of the new backpack artwork. Only create
            // the legacy frame when a fallback sprite is assigned.
            if (descriptionSprite != null)
            {
                CreateImage("ItemDescriptionFrame", transform, new Vector2(385f, -165f), new Vector2(680f, 126f), descriptionSprite);
            }
            descriptionLabel = CreateText("ItemDescription", transform, new Vector2(385f, -165f), new Vector2(680f, 126f), 15, TextAnchor.MiddleCenter);
            descriptionLabel.color = new Color(0.349f, 0.286f, 0.224f, 1f); // #594939
            descriptionLabel.text = "选择物品后，这里会显示名称和数量。";

            CreateButton("CloseButton", transform, new Vector2(763f, 423f), new Vector2(183f, 182f), closeSprite, null, InventoryPanelButtonActionType.Close);
            CreateButton("EquipButton", transform, new Vector2(236f, -16f), new Vector2(231f, 71f), selectSprite, selectHighlightedSprite, InventoryPanelButtonActionType.Equip);
            CreateButton("DiscardButton", transform, new Vector2(545f, -16f), new Vector2(229f, 71f), cancelSprite, cancelHighlightedSprite, InventoryPanelButtonActionType.Discard);

            // Four contiguous cells match the shortcut row in the reference;
            // they remain valid drop targets for the inventory interactions.
            CreateGrid(InventoryContainerKind.ShortcutBar, 0, new Vector2(611.5f, -430f), 3, 1,
                new Vector2(91f, 75f), Vector2.zero, equipmentNormal, equipmentSelected);
            CreateGrid(InventoryContainerKind.Offhand, 0, new Vector2(793.5f, -430f), 1, 1,
                new Vector2(91f, 75f), Vector2.zero, equipmentNormal, equipmentSelected);
            statusLabel = CreateText("InventoryStatus", transform, new Vector2(-437f, -235f), new Vector2(420f, 54f), 14, TextAnchor.MiddleCenter);
            statusLabel.color = new Color(0.2f, 0.13f, 0.09f, 1f);
            statusLabel.text = "拖拽物品到背包、快捷栏或副手栏。";
        }
#endif

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
                success = inventory.TryMoveOrSwap(source.ItemInstanceId, destination.ItemInstanceId, destination.ContainerKind,
                    destination.X, destination.Y, destination.SlotIndex);
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

        private void DropOutsidePlayerInventory(InventorySlotView source)
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventory == null)
            {
                return;
            }

            if (!inventory.HasOpenSceneContainer)
            {
                ShowDiscardConfirmation(source.ItemInstanceId, GetItemName(GetItemId(source)));
                return;
            }

            foreach (var slot in slots)
            {
                if (slot == null || slot.ContainerKind != InventoryContainerKind.SceneContainer ||
                    FindPlacement(inventory, slot) != null)
                {
                    continue;
                }

                var success = inventory.TryDropToSceneContainer(source.ItemInstanceId, null, slot.X, slot.Y);
                SetStatus(success ? "物品已放入场景物品栏。" : "无法放入场景物品栏。", !success);
                Refresh();
                return;
            }

            SetStatus("场景物品栏已满，物品保留在原位置。", true);
        }

        private static bool IsPlayerSlot(InventorySlotView slot)
        {
            return slot.ContainerKind == InventoryContainerKind.Backpack ||
                   slot.ContainerKind == InventoryContainerKind.ShortcutBar ||
                   slot.ContainerKind == InventoryContainerKind.Offhand;
        }

#if UNITY_EDITOR
        private void CreateGrid(InventoryContainerKind kind, int baseSlotIndex, Vector2 center, int width, int height,
            Vector2 cellSize, Vector2 gap, Sprite normalSprite, Sprite highlightSprite)
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
                    var slotIndex = kind == InventoryContainerKind.ShortcutBar ? baseSlotIndex + x : kind == InventoryContainerKind.Offhand ? 0 : -1;
                    var slot = slotObject.GetComponent<InventorySlotView>();
                    slot.Configure(this, kind, x, y, slotIndex, normalSprite, highlightSprite);
                    slots.Add(slot);
                }
            }
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

        private void CreateButton(string name, Transform parent, Vector2 position, Vector2 size, Sprite normal,
            Sprite highlighted, InventoryPanelButtonActionType actionType)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(InventoryPanelButtonAction));
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

            buttonObject.GetComponent<InventoryPanelButtonAction>().Configure(this, actionType);
            ConfigureActionButton(buttonObject.transform, actionType);
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
#endif

        private void Refresh()
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventory == null)
            {
                return;
            }

            SetSceneContainerVisible(inventory.HasOpenSceneContainer);
            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

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

        private static InventoryItemPlacement FindPlacement(InventorySystem inventory, InventorySlotView slot)
        {
            IEnumerable<InventoryItemPlacement> candidates = slot.ContainerKind == InventoryContainerKind.SceneContainer
                ? inventory.GetActiveSceneContainer()?.items
                : inventory.GetPlayerPlacements(slot.ContainerKind);
            if (candidates == null)
            {
                return null;
            }

            foreach (var placement in candidates)
            {
                if (placement?.item == null)
                {
                    continue;
                }

                if (slot.ContainerKind == InventoryContainerKind.ShortcutBar && placement.slotIndex == slot.SlotIndex ||
                    slot.ContainerKind == InventoryContainerKind.Offhand && placement.slotIndex == 0 ||
                    slot.ContainerKind == InventoryContainerKind.Backpack && slot.X >= placement.x && slot.X < placement.x + placement.width && slot.Y >= placement.y && slot.Y < placement.y + placement.height ||
                    slot.ContainerKind == InventoryContainerKind.SceneContainer && placement.x == slot.X && placement.y == slot.Y)
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

        private void ConfigureActionButtons()
        {
            foreach (var action in GetComponentsInChildren<InventoryPanelButtonAction>(true))
            {
                if (action != null)
                {
                    ConfigureActionButton(action.transform, action.ActionType);
                }
            }
        }

        private void ConfigureActionButton(Transform buttonTransform, InventoryPanelButtonActionType actionType)
        {
            if (buttonTransform == null || actionType == InventoryPanelButtonActionType.Close)
            {
                return;
            }

            var captionSprite = actionType == InventoryPanelButtonActionType.Equip ? selectSprite : cancelSprite;
            var captionHighlightedSprite = actionType == InventoryPanelButtonActionType.Equip
                ? selectHighlightedSprite
                : cancelHighlightedSprite;
            buttonTransform.name = actionType == InventoryPanelButtonActionType.Equip ? "EquipButton" : "DiscardButton";

            var image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = captionSprite != null ? captionSprite : descriptionSprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }

            var button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                if (captionHighlightedSprite != null)
                {
                    button.transition = Selectable.Transition.SpriteSwap;
                    var state = button.spriteState;
                    state.highlightedSprite = captionHighlightedSprite;
                    state.selectedSprite = captionHighlightedSprite;
                    state.pressedSprite = captionHighlightedSprite;
                    button.spriteState = state;
                }
                else
                {
                    button.transition = Selectable.Transition.ColorTint;
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1f, 0.9f, 0.72f, 1f);
                    colors.pressedColor = new Color(0.84f, 0.68f, 0.5f, 1f);
                    colors.selectedColor = colors.highlightedColor;
                    button.colors = colors;
                }
            }

            // UI2.0 action captions are baked into their button sprites. Hide
            // an authored legacy label when the new artwork is available.
            var authoredLabel = buttonTransform.Find("ActionLabel");
            if (captionSprite != null && authoredLabel != null)
            {
                authoredLabel.gameObject.SetActive(false);
                return;
            }

            // UI2.0 captions are part of the button artwork. Do not create a
            // new legacy Text child when the authored label is already baked
            // into the sprite; this keeps the prefab free of duplicate text.
            if (captionSprite != null)
            {
                return;
            }

            var label = buttonTransform.Find("ActionLabel")?.GetComponent<Text>();
            if (label == null)
            {
                var labelObject = new GameObject("ActionLabel", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(buttonTransform, false);
                var rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                label = labelObject.GetComponent<Text>();
            }

            label.text = actionType == InventoryPanelButtonActionType.Equip ? "装备" : "丢弃";
            label.font = descriptionLabel != null && descriptionLabel.font != null
                ? descriptionLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.349f, 0.286f, 0.224f, 1f); // #594939
            label.raycastTarget = false;
        }

        private void CreateDragVisual(InventorySlotView source)
        {
            DestroyDragVisual();
            dragCanvas = GetComponentInParent<Canvas>();
            if (dragCanvas == null)
            {
                return;
            }

            var visual = new GameObject("InventoryDragVisual", typeof(RectTransform), typeof(Image));
            visual.transform.SetParent(dragCanvas.transform, false);
            dragVisual = visual.GetComponent<RectTransform>();
            dragVisual.anchorMin = dragVisual.anchorMax = new Vector2(0.5f, 0.5f);
            dragVisual.pivot = new Vector2(0.5f, 0.5f);
            dragVisual.sizeDelta = new Vector2(SlotSize, SlotSize);
            var image = visual.GetComponent<Image>();
            var itemId = GetItemId(source);
            var icon = GameRoot.Instance?.Context?.Configs?.GetItem(itemId)?.Icon;
            image.sprite = icon;
            image.preserveAspect = icon != null;
            image.color = icon != null ? new Color(1f, 1f, 1f, 0.82f) : new Color(0.86f, 0.63f, 0.18f, 0.82f);
            image.raycastTarget = false;
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(visual.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(SlotSize, SlotSize);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.text = GetItemName(itemId);
        }

        private void PositionDragVisual(Vector2 screenPosition)
        {
            if (dragVisual == null || dragCanvas == null)
            {
                return;
            }

            var canvasRect = dragCanvas.transform as RectTransform;
            var camera = dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dragCanvas.worldCamera;
            if (canvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPosition, camera, out var worldPosition))
            {
                dragVisual.position = worldPosition;
            }
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

            dragCanvas = null;
        }

        private void SetSceneContainerVisible(bool visible)
        {
            // The authored prefab uses SceneContainer. Keep the old generated-layout
            // name as a fallback so both layouts follow the same visibility rule.
            var panel = transform.Find("SceneContainer") ?? transform.Find("SceneContainerPanel");
            if (panel != null)
            {
                panel.gameObject.SetActive(visible);
            }

            foreach (var slot in slots)
            {
                if (slot != null && slot.ContainerKind == InventoryContainerKind.SceneContainer)
                {
                    slot.gameObject.SetActive(visible);
                }
            }
        }

        private void ShowDiscardConfirmation(string instanceId, string itemName)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            BuildDiscardConfirmationIfNeeded();
            pendingDiscardInstanceId = instanceId;
            discardPrompt.text = "确定丢弃“" + itemName + "”吗？\n丢弃后物品将直接消失，无法找回。";
            discardConfirmationRoot.SetActive(true);
            discardConfirmationRoot.transform.SetAsLastSibling();
        }

        private void ConfirmDiscard()
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            var instanceId = pendingDiscardInstanceId;
            HideDiscardConfirmation();
            var success = inventory != null && !string.IsNullOrEmpty(instanceId) &&
                          inventory.TryDiscardPlayerItem(instanceId);
            selectedSlot = null;
            SetStatus(success ? "物品已丢弃。" : "物品丢弃失败。", !success);
            Refresh();
        }

        private void CancelDiscard()
        {
            HideDiscardConfirmation();
            SetStatus("已取消丢弃。", false);
        }

        private void HideDiscardConfirmation()
        {
            pendingDiscardInstanceId = null;
            if (discardConfirmationRoot != null)
            {
                discardConfirmationRoot.SetActive(false);
            }
        }

        private void BuildDiscardConfirmationIfNeeded()
        {
            if (discardConfirmationRoot != null)
            {
                return;
            }

            discardConfirmationRoot = new GameObject("DiscardConfirmation", typeof(RectTransform), typeof(Image));
            discardConfirmationRoot.transform.SetParent(transform, false);
            var overlay = discardConfirmationRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            discardConfirmationRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            dialog.transform.SetParent(discardConfirmationRoot.transform, false);
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(520f, 250f);
            dialog.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);

            discardPrompt = CreateRuntimeText(dialog.transform, "Prompt", new Vector2(0f, 48f),
                new Vector2(470f, 110f), 18);
            CreateDiscardButton(dialog.transform, "Confirm", "确认丢弃", new Vector2(-115f, -75f), ConfirmDiscard);
            CreateDiscardButton(dialog.transform, "Cancel", "取消", new Vector2(115f, -75f), CancelDiscard);
            discardConfirmationRoot.SetActive(false);
        }

        private Text CreateRuntimeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = root.GetComponent<Text>();
            text.font = descriptionLabel != null && descriptionLabel.font != null
                ? descriptionLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void CreateDiscardButton(Transform parent, string name, string label, Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 44f);
            root.GetComponent<Image>().color = new Color(0.36f, 0.24f, 0.16f, 1f);
            root.GetComponent<Button>().onClick.AddListener(action);
            var text = CreateRuntimeText(root.transform, "Label", Vector2.zero, rect.sizeDelta, 16);
            text.text = label;
        }

    }
}
