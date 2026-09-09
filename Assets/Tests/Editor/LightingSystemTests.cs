using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Lighting.Logic;
using NUnit.Framework;
using UnityEditor;

namespace MemorialArchive.Tests.Editor
{
    public sealed class LightingSystemTests
    {
        private ConfigManager configs;
        private InventorySystem inventory;
        private LightingSystem lighting;

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(
                "Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);

            var events = new EventBus();
            configs = new ConfigManager(database);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);

            inventory = new InventorySystem();
            inventory.Initialize(context);
            lighting = new LightingSystem(inventory);
            lighting.Initialize(context);
        }

        [TearDown]
        public void TearDown()
        {
            lighting?.Dispose();
            inventory?.Dispose();
            configs?.Dispose();
        }

        [Test]
        public void LanternIsEquippedOnlyWhenItsShortcutSlotIsSelected()
        {
            var lantern = inventory.PlayerInventory.playerItems.First(
                placement => placement?.item?.itemId == 1006);

            Assert.That(lantern, Is.Not.Null);
            Assert.That(inventory.TryEquipToFirstAvailableSlot(lantern.item.instanceId), Is.True);
            var shortcut = inventory.PlayerInventory.playerItems.First(
                placement => placement?.item?.instanceId == lantern.item.instanceId);

            Assert.That(shortcut.containerKind, Is.EqualTo(InventoryContainerKind.ShortcutBar));
            Assert.That(lighting.IsLanternEquipped, Is.False,
                "A lantern placed in the bar is not active until its slot is selected.");

            Assert.That(inventory.TrySelectShortcut(shortcut.slotIndex), Is.True);
            Assert.That(lighting.IsLanternEquipped, Is.True);

            Assert.That(inventory.TrySelectShortcut((shortcut.slotIndex + 1) % InventorySystem.ShortcutSlotCount), Is.True);
            Assert.That(lighting.IsLanternEquipped, Is.False,
                "Selecting another shortcut must unequip the lantern from the lighting system.");
            Assert.That(lighting.IsLanternLit, Is.False);
        }
    }
}
