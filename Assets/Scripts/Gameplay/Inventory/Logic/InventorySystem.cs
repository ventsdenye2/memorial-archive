using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Item.Config;
using UnityEngine;

namespace MemorialArchive.Gameplay.Inventory.Logic
{
    public sealed class InventorySystem : IGameSystem, ISaveModule
    {
        public const int BackpackWidth = 3;
        public const int BackpackHeight = 3;
        public const int ShortcutSlotCount = 3;
        public const int SceneContainerWidth = 2;
        public const int SceneContainerHeight = 2;

        private readonly InventoryGrid backpackGrid = new InventoryGrid(BackpackWidth, BackpackHeight);
        private readonly InventoryGrid sceneContainerGrid = new InventoryGrid(SceneContainerWidth, SceneContainerHeight);
        private readonly Dictionary<string, SceneContainerData> sceneContainers = new Dictionary<string, SceneContainerData>();
        private InventoryData playerInventory = new InventoryData();
        private GameContext context;
        private string activeSceneContainerId;

        public string ModuleKey => "inventory";
        public InventoryData PlayerInventory => playerInventory;
        public string ActiveSceneContainerId => activeSceneContainerId;
        public bool HasOpenSceneContainer => !string.IsNullOrEmpty(activeSceneContainerId);

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
            context.Events.Subscribe<ContainerClosedEvent>(HandleContainerClosed);
            context.Events.Subscribe<ShortcutEquipPressedEvent>(HandleShortcutEquipPressed);
            context.Events.Subscribe<ItemUseRequestedEvent>(HandleItemUseRequested);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
                context.Events.Unsubscribe<ContainerClosedEvent>(HandleContainerClosed);
                context.Events.Unsubscribe<ShortcutEquipPressedEvent>(HandleShortcutEquipPressed);
                context.Events.Unsubscribe<ItemUseRequestedEvent>(HandleItemUseRequested);
            }

            context = null;
            sceneContainers.Clear();
            playerInventory = new InventoryData();
            activeSceneContainerId = null;
        }

        public SceneContainerData GetActiveSceneContainer()
        {
            return HasOpenSceneContainer ? GetOrCreateSceneContainer(activeSceneContainerId) : null;
        }

        public IEnumerable<InventoryItemPlacement> GetPlayerPlacements(InventoryContainerKind kind)
        {
            return GetPlayerItems(kind);
        }

        public SceneContainerData GetOrCreateSceneContainer(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            if (!sceneContainers.TryGetValue(containerId, out var container))
            {
                container = new SceneContainerData { containerId = containerId };
                sceneContainers[containerId] = container;
            }

            return container;
        }

        public bool TryAddToBackpack(InventoryItemInstance item)
        {
            if (item == null)
            {
                return Fail(null, "Item is null.");
            }

            EnsureInstanceId(item);
            var config = context.Configs.GetItem(item.itemId);
            var width = config != null ? config.BackpackWidth : 1;
            var height = config != null ? config.BackpackHeight : 1;

            for (var y = 0; y < BackpackHeight; y++)
            {
                for (var x = 0; x < BackpackWidth; x++)
                {
                    var placement = BuildPlayerPlacement(item, InventoryContainerKind.Backpack, x, y, width, height, -1);
                    if (backpackGrid.CanPlace(placement, GetPlayerItems(InventoryContainerKind.Backpack)))
                    {
                        playerInventory.playerItems.Add(placement);
                        context.Events.Publish(new InventoryChangedEvent());
                        return true;
                    }
                }
            }

            return Fail(item.instanceId, "Backpack has no legal free space.");
        }

        public bool TryMoveToBackpack(string instanceId, int x, int y)
        {
            var source = FindPlacement(instanceId);
            if (source == null)
            {
                return Fail(instanceId, "Item instance was not found in the player inventory or active scene container.");
            }

            var config = context.Configs.GetItem(source.item.itemId);
            var width = config != null ? config.BackpackWidth : 1;
            var height = config != null ? config.BackpackHeight : 1;
            var candidate = BuildPlayerPlacement(source.item, InventoryContainerKind.Backpack, x, y, width, height, -1);

            if (!backpackGrid.CanPlace(candidate, GetPlayerItems(InventoryContainerKind.Backpack), source.item.instanceId))
            {
                return Fail(instanceId, "Target backpack cells are occupied or outside the 3x3 grid.");
            }

            RemovePlacement(source);
            playerInventory.playerItems.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            PublishCharacterEquipment();
            return true;
        }

        public bool TryMoveToShortcut(string instanceId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ShortcutSlotCount)
            {
                return Fail(instanceId, "Shortcut slot index must be 0, 1, or 2.");
            }

            if (FindPlayerSlot(InventoryContainerKind.ShortcutBar, slotIndex) != null)
            {
                return Fail(instanceId, "Shortcut slot is already occupied.");
            }

            var source = FindPlacement(instanceId);
            if (source == null)
            {
                return Fail(instanceId, "Item instance was not found.");
            }

            var config = context.Configs.GetItem(source.item.itemId);
            if (config != null && !config.CanEquipToShortcut)
            {
                return Fail(instanceId, "Item configuration forbids shortcut equipment.");
            }

            var candidate = BuildPlayerPlacement(source.item, InventoryContainerKind.ShortcutBar, 0, 0, 1, 1, slotIndex);
            RemovePlacement(source);
            playerInventory.playerItems.Add(candidate);
            context.Events.Publish(new ShortcutChangedEvent());
            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        public bool TryMoveToOffhand(string instanceId)
        {
            if (FindPlayerSlot(InventoryContainerKind.Offhand, 0) != null)
            {
                return Fail(instanceId, "The single offhand slot is already occupied.");
            }

            var source = FindPlacement(instanceId);
            if (source == null)
            {
                return Fail(instanceId, "Item instance was not found.");
            }

            var config = context.Configs.GetItem(source.item.itemId);
            if (config == null || !config.CanEquipToOffhand || config.OffhandType == OffhandType.None)
            {
                return Fail(instanceId, "Item configuration forbids offhand equipment.");
            }

            var candidate = BuildPlayerPlacement(source.item, InventoryContainerKind.Offhand, 0, 0, 1, 1, 0);
            RemovePlacement(source);
            playerInventory.playerItems.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            PublishCharacterEquipment();
            return true;
        }

        public bool TryMergeStack(string sourceInstanceId, string targetInstanceId)
        {
            var source = FindPlacement(sourceInstanceId);
            var target = FindPlacement(targetInstanceId);
            if (source == null || target == null || source == target || source.item == null || target.item == null)
            {
                return Fail(sourceInstanceId, "Both source and target stack instances must exist.");
            }

            if (source.item.itemId != target.item.itemId)
            {
                return Fail(sourceInstanceId, "Only identical item IDs can merge.");
            }

            var config = context.Configs.GetItem(source.item.itemId);
            var maxStack = config != null ? config.MaxStack : 1;
            if (maxStack <= 1 || target.item.quantity >= maxStack)
            {
                return Fail(sourceInstanceId, "Target item is not stackable or is already full.");
            }

            var moved = Mathf.Min(source.item.quantity, maxStack - target.item.quantity);
            target.item.quantity += moved;
            source.item.quantity -= moved;
            if (source.item.quantity <= 0)
            {
                RemovePlacement(source);
            }

            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        public bool TryDropToSceneContainer(string instanceId, string containerId, int x, int y)
        {
            var source = FindPlayerPlacement(instanceId);
            var effectiveContainerId = string.IsNullOrEmpty(containerId) ? activeSceneContainerId : containerId;
            var target = GetOrCreateSceneContainer(effectiveContainerId);
            if (source == null || target == null)
            {
                return Fail(instanceId, "Dropping requires a valid player item and an open scene container.");
            }

            var candidate = new InventoryItemPlacement
            {
                item = source.item,
                containerKind = InventoryContainerKind.SceneContainer,
                containerId = effectiveContainerId,
                x = x,
                y = y,
                width = 1,
                height = 1,
                slotIndex = -1
            };

            if (!sceneContainerGrid.CanPlace(candidate, target.items))
            {
                return Fail(instanceId, "Target scene-container cell is occupied or outside the 2x2 grid.");
            }

            RemovePlacement(source);
            target.items.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        /// <summary>
        /// Moves an item that is already in the opened scene container.  Scene
        /// containers deliberately use a 1x1 representation for every item;
        /// the 1x2 rule belongs to the player's backpack only.
        /// </summary>
        public bool TryMoveWithinActiveSceneContainer(string instanceId, int x, int y)
        {
            if (!HasOpenSceneContainer || !sceneContainers.TryGetValue(activeSceneContainerId, out var container))
            {
                return Fail(instanceId, "No scene container is currently open.");
            }

            var source = container.items.Find(item => item != null && item.item != null && item.item.instanceId == instanceId);
            if (source == null)
            {
                return Fail(instanceId, "Item instance was not found in the active scene container.");
            }

            var candidate = new InventoryItemPlacement
            {
                item = source.item,
                containerKind = InventoryContainerKind.SceneContainer,
                containerId = activeSceneContainerId,
                x = x,
                y = y,
                width = 1,
                height = 1,
                slotIndex = -1
            };

            if (!sceneContainerGrid.CanPlace(candidate, container.items, instanceId))
            {
                return Fail(instanceId, "Target scene-container cell is occupied or outside the 2x2 grid.");
            }

            container.items.Remove(source);
            container.items.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        /// <summary>
        /// Moves an item onto an occupied slot. Identical items are merged when
        /// possible; otherwise the two placements are exchanged only when both
        /// items are legal at their new destinations.
        /// </summary>
        public bool TryMoveOrSwap(
            string sourceInstanceId,
            string targetInstanceId,
            InventoryContainerKind targetKind,
            int targetX,
            int targetY,
            int targetSlotIndex)
        {
            var source = FindPlacement(sourceInstanceId);
            var target = FindPlacement(targetInstanceId);
            if (source == null || target == null || source == target)
            {
                return Fail(sourceInstanceId, "Both source and target items are required for an occupied-slot move.");
            }

            if (source.item.itemId == target.item.itemId)
            {
                return TryMergeStack(sourceInstanceId, targetInstanceId);
            }

            var sourceContainerId = source.containerKind == InventoryContainerKind.SceneContainer
                ? activeSceneContainerId
                : null;
            var targetContainerId = targetKind == InventoryContainerKind.SceneContainer
                ? activeSceneContainerId
                : null;
            if (string.IsNullOrEmpty(targetContainerId) && targetKind == InventoryContainerKind.SceneContainer)
            {
                return Fail(sourceInstanceId, "A scene container must be open before swapping into it.");
            }

            var sourceCandidate = BuildPlacementForDestination(
                source.item, targetKind, targetContainerId, targetX, targetY, targetSlotIndex);
            var targetCandidate = BuildPlacementForDestination(
                target.item, source.containerKind, sourceContainerId, source.x, source.y, source.slotIndex);
            if (!CanEnterContainer(source.item, sourceCandidate.containerKind) ||
                !CanEnterContainer(target.item, targetCandidate.containerKind) ||
                !CanPlaceCandidate(sourceCandidate, sourceInstanceId, targetInstanceId) ||
                !CanPlaceCandidate(targetCandidate, sourceInstanceId, targetInstanceId))
            {
                return Fail(sourceInstanceId, "The two items cannot be exchanged at these positions.");
            }

            RemovePlacement(source);
            RemovePlacement(target);
            AddPlacement(sourceCandidate);
            AddPlacement(targetCandidate);
            if (source.containerKind == InventoryContainerKind.ShortcutBar || targetKind == InventoryContainerKind.ShortcutBar)
            {
                context.Events.Publish(new ShortcutChangedEvent());
            }

            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        public bool TrySelectShortcut(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ShortcutSlotCount)
            {
                return Fail(null, "Shortcut slot index must be 0, 1, or 2.");
            }

            // 数字键选择的是快捷栏格子本身。即使该格暂时为空，也需要保留
            // 选中反馈；空格只会让 SelectedItemChangedEvent 携带 null，不会使用物品。
            var placement = FindPlayerSlot(InventoryContainerKind.ShortcutBar, slotIndex);
            playerInventory.selectedShortcutIndex = slotIndex;
            context.Events.Publish(new SelectedItemChangedEvent(placement?.item));
            PublishCharacterEquipment();
            return true;
        }

        public object CaptureSaveData()
        {
            return new InventorySaveData
            {
                playerInventory = playerInventory,
                sceneContainers = new List<SceneContainerData>(sceneContainers.Values)
            };
        }

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var saveData = JsonUtility.FromJson<InventorySaveData>(json);
            if (saveData == null)
            {
                return;
            }

            playerInventory = saveData.playerInventory ?? new InventoryData();
            sceneContainers.Clear();
            if (saveData.sceneContainers == null)
            {
                return;
            }

            foreach (var container in saveData.sceneContainers)
            {
                if (container != null && !string.IsNullOrEmpty(container.containerId))
                {
                    sceneContainers[container.containerId] = container;
                }
            }
        }

        private void HandleOpenContainerRequested(OpenContainerRequestedEvent evt)
        {
            activeSceneContainerId = evt.ContainerId;
            GetOrCreateSceneContainer(evt.ContainerId);

            // UIManager subscribes before InventorySystem, so the inventory
            // panel may be opened and refreshed before the active container is
            // assigned. Notify it again after the container data is ready.
            context.Events.Publish(new InventoryChangedEvent());
        }

        private void HandleContainerClosed(ContainerClosedEvent evt)
        {
            activeSceneContainerId = null;
        }

        private void HandleShortcutEquipPressed(ShortcutEquipPressedEvent evt)
        {
            TrySelectShortcut(evt.SlotIndex);
        }

        private void PublishCharacterEquipment()
        {
            var selected = FindPlayerSlot(InventoryContainerKind.ShortcutBar, playerInventory.selectedShortcutIndex)?.item;
            var offhand = FindPlayerSlot(InventoryContainerKind.Offhand, 0)?.item;
            var offhandConfig = offhand == null ? null : context.Configs.GetItem(offhand.itemId);
            context.Events.Publish(new CharacterEquipmentChangedEvent(selected?.itemId ?? 0, offhandConfig?.OffhandType ?? OffhandType.None));
        }

        private void HandleItemUseRequested(ItemUseRequestedEvent evt)
        {
            if (evt.Item == null)
            {
                return;
            }

            var config = context.Configs.GetItem(evt.Item.itemId);
            if (config == null)
            {
                return;
            }

            if (config.CanPlaceAmmo)
            {
                if (HasCompatibleWeaponInShortcut(config))
                {
                    context.Events.Publish(new AmmoReloadRequestedEvent(config.ItemId));
                    return;
                }

                context.Events.Publish(new ItemUseFailedEvent(config.ItemId, "No compatible weapon in shortcut bar."));
                return;
            }

            if (config.CanUse)
            {
                context.Events.Publish(new ItemEffectAppliedEvent(config.ItemId, config.EffectId));
            }
        }

        private bool HasCompatibleWeaponInShortcut(ItemConfig ammoConfig)
        {
            foreach (var placement in playerInventory.playerItems)
            {
                if (placement == null || placement.containerKind != InventoryContainerKind.ShortcutBar)
                {
                    continue;
                }

                var weaponConfig = context.Configs.GetItem(placement.item.itemId);
                if (weaponConfig == null || weaponConfig.Category != ItemCategory.Weapon)
                {
                    continue;
                }

                if (ammoConfig.CompatibleWeaponItemId <= 0 || ammoConfig.CompatibleWeaponItemId == weaponConfig.ItemId)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<InventoryItemPlacement> GetPlayerItems(InventoryContainerKind kind)
        {
            foreach (var placement in playerInventory.playerItems)
            {
                if (placement != null && placement.containerKind == kind)
                {
                    yield return placement;
                }
            }
        }

        private InventoryItemPlacement FindPlacement(string instanceId)
        {
            var playerPlacement = FindPlayerPlacement(instanceId);
            if (playerPlacement != null)
            {
                return playerPlacement;
            }

            if (!string.IsNullOrEmpty(activeSceneContainerId) && sceneContainers.TryGetValue(activeSceneContainerId, out var container))
            {
                return container.items.Find(item => item != null && item.item != null && item.item.instanceId == instanceId);
            }

            return null;
        }

        private InventoryItemPlacement FindPlayerPlacement(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            return playerInventory.playerItems.Find(item => item != null && item.item != null && item.item.instanceId == instanceId);
        }

        private InventoryItemPlacement FindPlayerSlot(InventoryContainerKind kind, int slotIndex)
        {
            return playerInventory.playerItems.Find(item => item != null && item.containerKind == kind && item.slotIndex == slotIndex);
        }

        private InventoryItemPlacement BuildPlacementForDestination(
            InventoryItemInstance item,
            InventoryContainerKind kind,
            string containerId,
            int x,
            int y,
            int slotIndex)
        {
            var config = context.Configs.GetItem(item.itemId);
            var isBackpack = kind == InventoryContainerKind.Backpack;
            return new InventoryItemPlacement
            {
                item = item,
                containerKind = kind,
                containerId = kind == InventoryContainerKind.SceneContainer ? containerId : null,
                x = x,
                y = y,
                width = isBackpack && config != null ? config.BackpackWidth : 1,
                height = isBackpack && config != null ? config.BackpackHeight : 1,
                slotIndex = kind == InventoryContainerKind.Backpack || kind == InventoryContainerKind.SceneContainer ? -1 : slotIndex
            };
        }

        private bool CanEnterContainer(InventoryItemInstance item, InventoryContainerKind kind)
        {
            var config = context.Configs.GetItem(item.itemId);
            if (kind == InventoryContainerKind.ShortcutBar)
            {
                return config == null || config.CanEquipToShortcut;
            }

            if (kind == InventoryContainerKind.Offhand)
            {
                return config != null && config.CanEquipToOffhand && config.OffhandType != OffhandType.None;
            }

            return true;
        }

        private bool CanPlaceCandidate(InventoryItemPlacement candidate, string firstIgnoredId, string secondIgnoredId)
        {
            if (candidate.containerKind == InventoryContainerKind.Backpack)
            {
                return backpackGrid.CanPlace(candidate, GetPlayerItems(InventoryContainerKind.Backpack), firstIgnoredId, secondIgnoredId);
            }

            if (candidate.containerKind == InventoryContainerKind.SceneContainer)
            {
                var container = GetOrCreateSceneContainer(candidate.containerId);
                return container != null && sceneContainerGrid.CanPlace(candidate, container.items, firstIgnoredId, secondIgnoredId);
            }

            return FindPlayerSlot(candidate.containerKind, candidate.slotIndex) == null ||
                   FindPlayerSlot(candidate.containerKind, candidate.slotIndex).item.instanceId == firstIgnoredId ||
                   FindPlayerSlot(candidate.containerKind, candidate.slotIndex).item.instanceId == secondIgnoredId;
        }

        private void AddPlacement(InventoryItemPlacement placement)
        {
            if (placement.containerKind == InventoryContainerKind.SceneContainer)
            {
                GetOrCreateSceneContainer(placement.containerId).items.Add(placement);
                return;
            }

            playerInventory.playerItems.Add(placement);
        }

        private void RemovePlacement(InventoryItemPlacement placement)
        {
            if (placement == null)
            {
                return;
            }

            if (playerInventory.playerItems.Remove(placement))
            {
                return;
            }

            foreach (var container in sceneContainers.Values)
            {
                if (container.items.Remove(placement))
                {
                    return;
                }
            }
        }

        private static InventoryItemPlacement BuildPlayerPlacement(
            InventoryItemInstance item,
            InventoryContainerKind kind,
            int x,
            int y,
            int width,
            int height,
            int slotIndex)
        {
            return new InventoryItemPlacement
            {
                item = item,
                containerKind = kind,
                x = x,
                y = y,
                width = width,
                height = height,
                slotIndex = slotIndex
            };
        }

        private static void EnsureInstanceId(InventoryItemInstance item)
        {
            if (string.IsNullOrEmpty(item.instanceId))
            {
                item.instanceId = Guid.NewGuid().ToString("N");
            }
        }

        private bool Fail(string instanceId, string reason)
        {
            context?.Events.Publish(new InventoryMoveFailedEvent(instanceId, reason));
            return false;
        }
    }
}
