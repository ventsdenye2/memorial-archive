using System.Collections.Generic;
using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Lighting.Data;
using MemorialArchive.Gameplay.Lighting.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class LightingSystemTests
    {
        private ConfigManager configs;
        private InventorySystem inventory;
        private LightingSystem lighting;
        private EventBus events;

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(
                "Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);

            events = new EventBus();
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
        public void SpecialFixtureGlowsByDefaultAndGrowsBrighterAndLargerAfterInteraction()
        {
            var fixture = RegisterSpecialFixture();
            var lights = new List<ActiveLight>();
            lighting.CollectActiveLights(lights);
            var idle = lights.Single(light => light.Position == fixture.position);

            Assert.That(lighting.IsLightOn(fixture.lightId), Is.False);
            Assert.That(idle.Intensity, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(idle.Radius, Is.EqualTo(fixture.radius * configs.GetLightingGlobal().SpecialLightRadiusMultiplier));

            var lantern = inventory.PlayerInventory.playerItems.First(p => p?.item?.itemId == 1006);
            Assert.That(inventory.TryEquipToFirstAvailableSlot(lantern.item.instanceId), Is.True);
            var shortcut = inventory.PlayerInventory.playerItems.First(p => p?.item?.instanceId == lantern.item.instanceId);
            Assert.That(inventory.TrySelectShortcut(shortcut.slotIndex), Is.True);
            events.Publish(new LightSourceInteractRequestedEvent(fixture.lightId));

            lighting.CollectActiveLights(lights);
            var lit = lights.Single(light => light.Position == fixture.position);
            Assert.That(lighting.IsRegionLit(fixture.regionId), Is.True);
            Assert.That(lit.Intensity, Is.GreaterThan(idle.Intensity));
            Assert.That(lit.Radius, Is.GreaterThan(idle.Radius));
            Assert.That(lit.Intensity, Is.EqualTo(configs.GetLightingGlobal().SpecialLightLitIntensity));
            Assert.That(lit.Radius, Is.EqualTo(idle.Radius * configs.GetLightingGlobal().SpecialLightLitRadiusMultiplier));

            var save = JsonUtility.ToJson(lighting.CaptureSaveData());
            lighting.RestoreSaveData(save);
            lighting.CollectActiveLights(lights);
            var restored = lights.Single(light => light.Position == fixture.position);
            Assert.That(restored.Intensity, Is.EqualTo(lit.Intensity));
            Assert.That(restored.Radius, Is.EqualTo(lit.Radius));

            lighting.ResetForNewGame();
            lighting.CollectActiveLights(lights);
            var reset = lights.Single(light => light.Position == fixture.position);
            Assert.That(reset.Intensity, Is.EqualTo(idle.Intensity));
            Assert.That(reset.Radius, Is.EqualTo(idle.Radius));
        }

        [Test]
        public void SpecialFixtureGlowDoesNotGrantDarknessProtectionOrLeakIntoOtherScenes()
        {
            var fixture = RegisterSpecialFixture();
            events.Publish(new PlayerPositionChangedEvent(fixture.position));
            Assert.That(lighting.IsPlayerInLight(), Is.False);

            events.Publish(new SceneLoadedEvent("OtherScene"));
            var lights = new List<ActiveLight>();
            lighting.CollectActiveLights(lights);
            Assert.That(lights.Any(light => light.Position == fixture.position), Is.False);
        }

        private LightViewRegistration RegisterSpecialFixture()
        {
            var config = configs.GetLightSource("light_corridor_1f_special");
            Assert.That(config, Is.Not.Null);
            var fixture = new LightViewRegistration
            {
                lightId = config.LightId,
                regionId = config.RegionId,
                isSpecial = config.IsSpecial,
                position = new Vector2(20f, 5f),
                radius = config.Radius,
                sceneName = "Floor_1F"
            };
            events.Publish(new SceneLoadedEvent(fixture.sceneName));
            lighting.RegisterLightView(fixture);
            return fixture;
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
