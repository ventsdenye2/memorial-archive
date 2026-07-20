using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Keeps the stage-one HUD in sync with character and inventory state.
    /// The bottom row is deliberately three numbered shortcuts plus one offhand slot.
    /// </summary>
    public sealed class HUDPanel : BasePanel
    {
        [SerializeField] private Image[] shortcutSlots = new Image[3];
        [SerializeField] private Image offhandSlot;
        [SerializeField] private Image[] healthFills = new Image[3];
        [SerializeField] private Image staminaFill;
        [SerializeField] private Sprite normalSlotSprite;
        [SerializeField] private Sprite selectedSlotSprite;

        private readonly List<Image> itemIcons = new List<Image>();
        private readonly List<Text> itemLabels = new List<Text>();
        private bool subscribed;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            EnsureSlotContent();
        }

        private void Start()
        {
            SubscribeAndRefresh();
        }

        private void OnEnable()
        {
            SubscribeAndRefresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public override void Open()
        {
            base.Open();
            SubscribeAndRefresh();
        }

        private void ResolveReferences()
        {
            for (var i = 0; i < shortcutSlots.Length; i++)
            {
                if (shortcutSlots[i] == null)
                {
                    shortcutSlots[i] = transform.Find("ShortcutMount/ShortcutSlot_" + (i + 1))?.GetComponent<Image>();
                }
            }

            if (offhandSlot == null)
            {
                offhandSlot = transform.Find("ShortcutMount/OffhandSlot")?.GetComponent<Image>() ??
                              transform.Find("ShortcutMount/ExtraShortcutSlot")?.GetComponent<Image>();
            }

            if (staminaFill == null)
            {
                staminaFill = transform.Find("Stamina/StaminaBar")?.GetComponent<Image>();
            }

            for (var i = 0; i < healthFills.Length; i++)
            {
                if (healthFills[i] == null)
                {
                    healthFills[i] = transform.Find("Health/EmptyHealth_" + (i + 1) + "/Health")?.GetComponent<Image>();
                }
            }
        }

        private void EnsureSlotContent()
        {
            itemIcons.Clear();
            itemLabels.Clear();
            for (var i = 0; i < shortcutSlots.Length; i++)
            {
                EnsureSlotContent(shortcutSlots[i], (i + 1).ToString());
            }

            EnsureSlotContent(offhandSlot, "副");
        }

        private void EnsureSlotContent(Image slot, string keyHint)
        {
            if (slot == null)
            {
                itemIcons.Add(null);
                itemLabels.Add(null);
                return;
            }

            var iconTransform = slot.transform.Find("ItemIcon");
            if (iconTransform == null)
            {
                var iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(slot.transform, false);
                var rect = iconObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.16f, 0.16f);
                rect.anchorMax = new Vector2(0.84f, 0.84f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                iconTransform = iconObject.transform;
            }

            var icon = iconTransform.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            itemIcons.Add(icon);

            var hintTransform = slot.transform.Find("KeyHint");
            if (hintTransform == null)
            {
                var hintObject = new GameObject("KeyHint", typeof(RectTransform), typeof(Text));
                hintObject.transform.SetParent(slot.transform, false);
                var rect = hintObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(8f, -5f);
                rect.sizeDelta = new Vector2(30f, 24f);
                hintTransform = hintObject.transform;
            }

            var hint = hintTransform.GetComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 16;
            hint.fontStyle = FontStyle.Bold;
            hint.alignment = TextAnchor.UpperLeft;
            hint.color = new Color(0.25f, 0.18f, 0.13f, 1f);
            hint.raycastTarget = false;
            hint.text = keyHint;
            itemLabels.Add(hint);
        }

        private void SubscribeAndRefresh()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events == null)
            {
                return;
            }

            if (!subscribed)
            {
                events.Subscribe<CharacterStatsChangedEvent>(HandleStatsChanged);
                events.Subscribe<InventoryChangedEvent>(HandleInventoryChanged);
                events.Subscribe<ShortcutChangedEvent>(HandleShortcutChanged);
                events.Subscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
                subscribed = true;
            }

            RefreshAll();
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            var events = GameRoot.Instance?.Context?.Events;
            events?.Unsubscribe<CharacterStatsChangedEvent>(HandleStatsChanged);
            events?.Unsubscribe<InventoryChangedEvent>(HandleInventoryChanged);
            events?.Unsubscribe<ShortcutChangedEvent>(HandleShortcutChanged);
            events?.Unsubscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            subscribed = false;
        }

        private void RefreshAll()
        {
            RefreshStats();
            RefreshInventory();
        }

        private void RefreshStats()
        {
            var character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            if (character == null)
            {
                return;
            }

            var attributes = GameRoot.Instance.Context.Configs.GetCharacterAttribute(character.Data.attributeId);
            var maxHealth = attributes != null ? attributes.MaxHealth : healthFills.Length;
            var maxStamina = attributes != null ? attributes.MaxStamina : Mathf.Max(1, character.Data.stamina);
            for (var i = 0; i < healthFills.Length; i++)
            {
                if (healthFills[i] != null)
                {
                    healthFills[i].enabled = i < character.Data.health && i < maxHealth;
                }
            }

            if (staminaFill != null)
            {
                staminaFill.type = Image.Type.Filled;
                staminaFill.fillMethod = Image.FillMethod.Horizontal;
                staminaFill.fillOrigin = 0;
                staminaFill.fillAmount = Mathf.Clamp01(character.Data.stamina / (float)Mathf.Max(1, maxStamina));
            }
        }

        private void RefreshInventory()
        {
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventory == null)
            {
                return;
            }

            for (var i = 0; i < shortcutSlots.Length; i++)
            {
                var placement = FindPlacement(inventory.GetPlayerPlacements(InventoryContainerKind.ShortcutBar), i);
                RefreshSlot(i, shortcutSlots[i], placement,
                    inventory.PlayerInventory.selectedShortcutIndex == i);
            }

            var offhand = FindPlacement(inventory.GetPlayerPlacements(InventoryContainerKind.Offhand), 0);
            RefreshSlot(shortcutSlots.Length, offhandSlot, offhand, false);
        }

        private void RefreshSlot(int contentIndex, Image slot, InventoryItemPlacement placement, bool selected)
        {
            if (slot != null)
            {
                slot.sprite = selected && selectedSlotSprite != null ? selectedSlotSprite : normalSlotSprite;
            }

            if (contentIndex < 0 || contentIndex >= itemIcons.Count || itemIcons[contentIndex] == null)
            {
                return;
            }

            var config = placement?.item == null ? null : GameRoot.Instance.Context.Configs.GetItem(placement.item.itemId);
            itemIcons[contentIndex].sprite = config != null ? config.Icon : null;
            itemIcons[contentIndex].enabled = itemIcons[contentIndex].sprite != null;
        }

        private static InventoryItemPlacement FindPlacement(IEnumerable<InventoryItemPlacement> placements, int slotIndex)
        {
            if (placements == null)
            {
                return null;
            }

            foreach (var placement in placements)
            {
                if (placement != null && placement.slotIndex == slotIndex)
                {
                    return placement;
                }
            }

            return null;
        }

        private void HandleStatsChanged(CharacterStatsChangedEvent evt) => RefreshStats();
        private void HandleInventoryChanged(InventoryChangedEvent evt) => RefreshInventory();
        private void HandleShortcutChanged(ShortcutChangedEvent evt) => RefreshInventory();
        private void HandleSelectedItemChanged(SelectedItemChangedEvent evt) => RefreshInventory();
    }
}
