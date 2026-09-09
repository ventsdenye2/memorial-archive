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
        public void DiscardRule_RemovesTheSelectedPlayerItemInstance()
        {
            var item = AddBackpackItem("discard", 1024);

            Assert.That(inventory.TryDiscardPlayerItem(item.instanceId), Is.True);
            Assert.That(Find(item.instanceId), Is.Null);
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
