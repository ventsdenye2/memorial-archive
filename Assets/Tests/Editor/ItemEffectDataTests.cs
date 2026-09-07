using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Item.Data;
using MemorialArchive.Gameplay.Item.Config;
using UnityEngine;
using MemorialArchive.Gameplay.Item.Logic;
using NUnit.Framework;
using UnityEditor;

namespace MemorialArchive.Tests.Editor
{
    public sealed class ItemEffectDataTests
    {
        [TestCase("restore_full_stamina", 0f, true, 0f, 1f, 1f)]
        [TestCase("no_stamina_cost_180", 0f, false, 180f, 0f, 1f)]
        [TestCase("melee_damage_bonus_20_300", 0f, false, 300f, 1f, 1.2f)]
        [TestCase("restore_health_1_5", 1.5f, false, 0f, 1f, 1f)]
        [TestCase("restore_health_0_5_stamina_half_60", 0.5f, false, 60f, 0.5f, 1f)]
        [TestCase("restore_health_0_5_cure_bleeding", 0.5f, false, 0f, 1f, 1f)]
        [TestCase("restore_health_1_cure_poison", 1f, false, 0f, 1f, 1f)]
        [TestCase("opium_tincture", 0f, false, 120f, 0f, 1.2f)]
        public void TryCreate_MapsConfiguredCharacterEffects(string effectId, float healthRestore,
            bool restoresFullStamina, float duration, float staminaMultiplier, float meleeMultiplier)
        {
            Assert.That(ItemEffectData.TryCreate(LoadEffectConfig(effectId), out var effect), Is.True);
            Assert.That(effect.HealthRestore, Is.EqualTo(healthRestore));
            Assert.That(effect.RestoreFullStamina, Is.EqualTo(restoresFullStamina));
            Assert.That(effect.DurationSeconds, Is.EqualTo(duration));
            Assert.That(effect.StaminaCostMultiplier, Is.EqualTo(staminaMultiplier));
            Assert.That(effect.MeleeDamageMultiplier, Is.EqualTo(meleeMultiplier));
        }

        [Test]
        public void TryCreate_OpiumTinctureCarriesItsExpiryPenalty()
        {
            Assert.That(ItemEffectData.TryCreate(LoadEffectConfig("opium_tincture"), out var effect), Is.True);
            Assert.That(effect.RestoreFullHealth, Is.True);
            Assert.That(effect.RestoreHealthAtExpiry, Is.True);
            Assert.That(effect.ExhaustAtExpiry, Is.True);
        }

        [Test]
        public void TryCreate_RejectsEffectsWithoutCharacterRuntimeRules()
        {
            Assert.That(ItemEffectData.TryCreate(LoadEffectConfig("reload_pistol"), out _), Is.False);
        }

        [Test]
        public void CharacterSystem_HealthConsumableRestoresAndClampsHealth()
        {
            var character = CreateCharacterSystem(out var events);
            character.Data.health = 2.5f;
            ItemEffectData.TryCreate(LoadEffectConfig("restore_health_1_5"), out var effect);

            events.Publish(new CharacterItemEffectRequestedEvent(1015, effect));

            Assert.That(character.Data.health, Is.EqualTo(3f));
            character.Dispose();
        }

        [Test]
        public void CharacterSystem_OpiumEffectRestoresThenRevertsHealthAndStamina()
        {
            var character = CreateCharacterSystem(out var events);
            character.Data.health = 1f;
            ItemEffectData.TryCreate(LoadEffectConfig("opium_tincture"), out var effect);

            events.Publish(new CharacterItemEffectRequestedEvent(1019, effect));
            Assert.That(character.Data.health, Is.EqualTo(3f));
            Assert.That(character.StaminaCostMultiplier, Is.EqualTo(0f));
            Assert.That(character.MeleeDamageMultiplier, Is.EqualTo(1.2f));

            character.Tick(120f);

            Assert.That(character.Data.health, Is.EqualTo(1f));
            Assert.That(character.Data.stamina, Is.EqualTo(0));
            Assert.That(character.StaminaCostMultiplier, Is.EqualTo(1f));
            Assert.That(character.MeleeDamageMultiplier, Is.EqualTo(1f));
            character.Dispose();
        }

        [Test]
        public void PrimaryAction_UsesConfiguredConsumableAndRemovesItsInventoryInstance()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);

            var events = new EventBus();
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);
            var character = new CharacterSystem();
            var inventory = new InventorySystem();
            var itemEffects = new ItemEffectSystem();
            character.Initialize(context);
            inventory.Initialize(context);
            itemEffects.Initialize(context);

            var item = new InventoryItemInstance { instanceId = "beef", itemId = 1015, quantity = 1 };
            Assert.That(inventory.TryAddToBackpack(item), Is.True);
            Assert.That(inventory.TryMoveToShortcut(item.instanceId, 0), Is.True);
            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            character.Data.health = 1f;

            events.Publish(new PrimaryActionPressedEvent());

            Assert.That(character.Data.health, Is.EqualTo(2.5f));
            Assert.That(inventory.PlayerInventory.playerItems, Is.Empty);
            itemEffects.Dispose();
            inventory.Dispose();
            character.Dispose();
        }

        [Test]
        public void TryCreate_UsesEditedConfigValuesWithoutChangingEffectId()
        {
            var config = Object.Instantiate(LoadEffectConfig("melee_damage_bonus_20_300"));
            try
            {
                var serialized = new SerializedObject(config);
                serialized.FindProperty("healthRestore").floatValue = 2.25f;
                serialized.FindProperty("effectDurationSeconds").floatValue = 45f;
                serialized.FindProperty("staminaCostMultiplier").floatValue = 0.25f;
                serialized.FindProperty("meleeDamageMultiplier").floatValue = 1.75f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(ItemEffectData.TryCreate(config, out var effect), Is.True);
                Assert.That(effect.HealthRestore, Is.EqualTo(2.25f));
                Assert.That(effect.DurationSeconds, Is.EqualTo(45f));
                Assert.That(effect.StaminaCostMultiplier, Is.EqualTo(0.25f));
                Assert.That(effect.MeleeDamageMultiplier, Is.EqualTo(1.75f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static ItemConfig LoadEffectConfig(string effectId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ItemConfig", new[] { "Assets/GameConfigs/Items" }))
            {
                var config = AssetDatabase.LoadAssetAtPath<ItemConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config.EffectId == effectId)
                    return config;
            }
            Assert.Fail("Missing item config for " + effectId);
            return null;
        }

        private static CharacterSystem CreateCharacterSystem(out EventBus events)
        {
            events = new EventBus();
            var configs = new ConfigManager(null);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);
            var character = new CharacterSystem();
            character.Initialize(context);
            return character;
        }
    }
}
