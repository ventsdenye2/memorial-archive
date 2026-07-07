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

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
            context.Events.Subscribe<ShortcutEquipPressedEvent>(HandleShortcutEquipPressed);
            context.Events.Subscribe<ItemUseRequestedEvent>(HandleItemUseRequested);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
                context.Events.Unsubscribe<ShortcutEquipPressedEvent>(HandleShortcutEquipPressed);
                context.Events.Unsubscribe<ItemUseRequestedEvent>(HandleItemUseRequested);
            }

            context = null;
            sceneContainers.Clear();
            playerInventory = new InventoryData();
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
                return false;
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

            return false;
        }

        public bool TryMoveToBackpack(string instanceId, int x, int y)
        {
            var source = FindPlacement(instanceId);
            if (source == null)
            {
                return false;
            }

            var config = context.Configs.GetItem(source.item.itemId);
            var width = config != null ? config.BackpackWidth : 1;
            var height = config != null ? config.BackpackHeight : 1;
            var candidate = BuildPlayerPlacement(source.item, InventoryContainerKind.Backpack, x, y, width, height, -1);

            if (!backpackGrid.CanPlace(candidate, GetPlayerItems(InventoryContainerKind.Backpack), source.item.instanceId))
            {
                return false;
            }

            RemovePlacement(source);
            playerInventory.playerItems.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        public bool TryMoveToShortcut(string instanceId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ShortcutSlotCount)
            {
                return false;
            }

            if (FindPlayerSlot(InventoryContainerKind.ShortcutBar, slotIndex) != null)
            {
                return false;
            }

            var source = FindPlacement(instanceId);
            if (source == null)
            {
                return false;
            }

            var config = context.Configs.GetItem(source.item.itemId);
            if (config != null && !config.CanEquipToShortcut)
            {
                return false;
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
                return false;
            }

            var source = FindPlacement(instanceId);
            if (source == null)
            {
                return false;
            }

            var config = context.Configs.GetItem(source.item.itemId);
            if (config == null || !config.CanEquipToOffhand || config.OffhandType == OffhandType.None)
            {
                return false;
            }

            var candidate = BuildPlayerPlacement(source.item, InventoryContainerKind.Offhand, 0, 0, 1, 1, 0);
            RemovePlacement(source);
            playerInventory.playerItems.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        public bool TryDropToSceneContainer(string instanceId, string containerId, int x, int y)
        {
            var source = FindPlayerPlacement(instanceId);
            var target = GetOrCreateSceneContainer(containerId);
            if (source == null || target == null)
            {
                return false;
            }

            var candidate = new InventoryItemPlacement
            {
                item = source.item,
                containerKind = InventoryContainerKind.SceneContainer,
                containerId = containerId,
                x = x,
                y = y,
                width = 1,
                height = 1,
                slotIndex = -1
            };

            if (!sceneContainerGrid.CanPlace(candidate, target.items))
            {
                return false;
            }

            RemovePlacement(source);
            target.items.Add(candidate);
            context.Events.Publish(new InventoryChangedEvent());
            return true;
        }

        public bool TrySelectShortcut(int slotIndex)
        {
            var placement = FindPlayerSlot(InventoryContainerKind.ShortcutBar, slotIndex);
            if (placement == null)
            {
                return false;
            }

            playerInventory.selectedShortcutIndex = slotIndex;
            context.Events.Publish(new SelectedItemChangedEvent(placement.item));
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
        }

        private void HandleShortcutEquipPressed(ShortcutEquipPressedEvent evt)
        {
            TrySelectShortcut(evt.SlotIndex);
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
    }
}
