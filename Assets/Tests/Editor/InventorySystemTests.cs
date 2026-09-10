using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using NUnit.Framework;
using UnityEditor;

namespace MemorialArchive.Tests.Editor
{
    public sealed class InventorySystemTests
    {
        private InventorySystem inventory;

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(
                "Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);

            var events = new EventBus();
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);
            inventory = new InventorySystem();
            inventory.Initialize(context);
        }

        [TearDown]
        public void TearDown()
        {
            inventory?.Dispose();
        }

        [Test]
        public void EquipButtonRule_MovesShortcutItemToFirstEmptyShortcut()
        {
            var item = AddBackpackItem("weapon", 1001);

            Assert.That(inventory.TryEquipToFirstAvailableSlot(item.instanceId), Is.True);

            var placement = Find(item.instanceId);
            Assert.That(placement.containerKind, Is.EqualTo(InventoryContainerKind.ShortcutBar));
            Assert.That(placement.slotIndex, Is.EqualTo(0));
        }

        [Test]
        public void EquipButtonRule_MovesOffhandItemToOffhandSlot()
        {
            var item = AddBackpackItem("splint", 1005);

            Assert.That(inventory.TryEquipToFirstAvailableSlot(item.instanceId), Is.True);

            Assert.That(Find(item.instanceId).containerKind, Is.EqualTo(InventoryContainerKind.Offhand));
        }

        [Test]
        public void EquipButtonRule_MovesLanternToShortcutBar()
        {
            var item = AddBackpackItem("lantern", 1006);

            Assert.That(inventory.TryEquipToFirstAvailableSlot(item.instanceId), Is.True);

            var placement = Find(item.instanceId);
            Assert.That(placement.containerKind, Is.EqualTo(InventoryContainerKind.ShortcutBar));
            Assert.That(placement.slotIndex, Is.EqualTo(0));
        }

        [Test]
        public void LanternRule_CannotBePlacedInOffhandSlot()
        {
            var item = AddBackpackItem("lantern-offhand-rejected", 1006);

            Assert.That(inventory.TryMoveToOffhand(item.instanceId), Is.False);
            Assert.That(Find(item.instanceId).containerKind, Is.EqualTo(InventoryContainerKind.Backpack));
        }

        [Test]
        public void EquipButtonRule_WhenShortcutBarIsFull_IsNoOp()
        {
            for (var slotIndex = 0; slotIndex < InventorySystem.ShortcutSlotCount; slotIndex++)
            {
                var equipped = AddBackpackItem("equipped-" + slotIndex, 1001 + slotIndex);
                Assert.That(inventory.TryMoveToShortcut(equipped.instanceId, slotIndex), Is.True);
            }

            var candidate = AddBackpackItem("candidate", 1004);

            Assert.That(inventory.TryEquipToFirstAvailableSlot(candidate.instanceId), Is.False);
            Assert.That(Find(candidate.instanceId).containerKind, Is.EqualTo(InventoryContainerKind.Backpack));
        }

        [Test]
        public void EquipButtonRule_NonEquippableItem_IsNoOp()
        {
            var item = AddBackpackItem("key", 1024);

            Assert.That(inventory.TryEquipToFirstAvailableSlot(item.instanceId), Is.False);
            Assert.That(Find(item.instanceId).containerKind, Is.EqualTo(InventoryContainerKind.Backpack));
        }

        [Test]
        public void ShortcutSelection_TogglesTheSelectedItemOff()
        {
            var item = AddBackpackItem("toggle-shortcut", 1001);
            Assert.That(inventory.TryMoveToShortcut(item.instanceId, 0), Is.True);

            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            Assert.That(inventory.PlayerInventory.selectedShortcutIndex, Is.EqualTo(0));
            Assert.That(inventory.GetSelectedShortcutPlacement()?.item, Is.SameAs(item));

            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            Assert.That(inventory.PlayerInventory.selectedShortcutIndex, Is.EqualTo(-1));
            Assert.That(inventory.GetSelectedShortcutPlacement(), Is.Null);
        }

        [Test]
        public void DiscardRule_RemovesTheSelectedPlayerItemInstance()
        {
            var item = AddBackpackItem("discard", 1024);

            Assert.That(inventory.TryDiscardPlayerItem(item.instanceId), Is.True);
            Assert.That(Find(item.instanceId), Is.Null);
        }

        [Test]
        public void SceneSeed_LootedContainerDoesNotRefillOnReentry()
        {
            var seeds = new[] { new InventoryItemPlacement { item = new InventoryItemInstance { instanceId = "key", itemId = 1027, quantity = 1 } } };
            Assert.That(inventory.TryInitializeSceneContainer("seed-test", seeds), Is.True);
            inventory.GetOrCreateSceneContainer("seed-test").items.Clear();
            Assert.That(inventory.TryInitializeSceneContainer("seed-test", seeds), Is.False);
            Assert.That(inventory.GetOrCreateSceneContainer("seed-test").items, Is.Empty);
        }

        [Test]
        public void SceneSeed_OldSaveWithEmptyContainerDoesNotRefill()
        {
            inventory.RestoreSaveData("{\"sceneContainers\":[{\"containerId\":\"old-looted\",\"items\":[]}]}");
            Assert.That(inventory.IsSceneContainerInitialized("old-looted"), Is.True);
            Assert.That(inventory.TryInitializeSceneContainer("old-looted", new InventoryItemPlacement[0]), Is.False);
        }

        [Test]
        public void SceneSeed_InvalidSeedRollsBackEntireInitialization()
        {
            var seeds = new[]
            {
                new InventoryItemPlacement { item = new InventoryItemInstance { instanceId = "valid", itemId = 1024, quantity = 1 } },
                new InventoryItemPlacement { item = new InventoryItemInstance { instanceId = "invalid", itemId = 999999, quantity = 1 }, x = 1 }
            };
            Assert.That(inventory.TryInitializeSceneContainer("invalid-seed", seeds), Is.False);
            Assert.That(inventory.IsSceneContainerInitialized("invalid-seed"), Is.False);
            Assert.That(inventory.GetOrCreateSceneContainer("invalid-seed").items, Is.Empty);
        }

        private InventoryItemInstance AddBackpackItem(string instanceId, int itemId)
        {
            var item = new InventoryItemInstance { instanceId = instanceId, itemId = itemId, quantity = 1 };
            Assert.That(inventory.TryAddToBackpack(item), Is.True);
            return item;
        }

        private InventoryItemPlacement Find(string instanceId)
        {
            return inventory.PlayerInventory.playerItems.FirstOrDefault(
                placement => placement?.item?.instanceId == instanceId);
        }
    }
}
