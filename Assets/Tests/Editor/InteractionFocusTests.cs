using System.Reflection;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.Logic;
using MemorialArchive.Gameplay.Lighting.Config;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class InteractionFocusTests
    {
        private EventBus events;
        private ConfigManager configs;
        private GameConfigDatabase database;
        private LightSourceConfig special;
        private LightSourceConfig ordinary;
        private InteractionSystem interactions;

        [SetUp]
        public void SetUp()
        {
            special = MakeLight("special", true);
            ordinary = MakeLight("ordinary", false);
            database = ScriptableObject.CreateInstance<GameConfigDatabase>();
            typeof(GameConfigDatabase).GetField("lightSources", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(database, new[] { special, ordinary });
            configs = new ConfigManager(database);
            configs.Initialize(null);
            events = new EventBus();
            interactions = new InteractionSystem();
            interactions.Initialize(new GameContext(events, configs, null, null, null));
        }

        [TearDown]
        public void TearDown()
        {
            interactions.Dispose();
            configs.Dispose();
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(special);
            Object.DestroyImmediate(ordinary);
        }

        [Test]
        public void SpecialLightWinsOverLaterOrdinaryLight_AndFocusFallsBackOnExit()
        {
            events.Publish(new InteractionFocusChangedEvent("special", InteractionType.LightSource, true));
            events.Publish(new InteractionFocusChangedEvent("ordinary", InteractionType.LightSource, true));
            Assert.That(FocusedId(), Is.EqualTo("special"));

            events.Publish(new InteractionFocusChangedEvent("ordinary", InteractionType.LightSource, false));
            Assert.That(FocusedId(), Is.EqualTo("special"));

            events.Publish(new InteractionFocusChangedEvent("special", InteractionType.LightSource, false));
            Assert.That(FocusedId(), Is.Null);
        }

        [Test]
        public void LeavingSpecialLightFallsBackToStillActiveOrdinaryLight()
        {
            events.Publish(new InteractionFocusChangedEvent("ordinary", InteractionType.LightSource, true));
            events.Publish(new InteractionFocusChangedEvent("special", InteractionType.LightSource, true));
            events.Publish(new InteractionFocusChangedEvent("special", InteractionType.LightSource, false));

            Assert.That(FocusedId(), Is.EqualTo("ordinary"));
        }

        [Test]
        public void OtherInteractionTypesKeepMostRecentlyAcquiredFocus()
        {
            events.Publish(new InteractionFocusChangedEvent("first", InteractionType.Inspect, true));
            events.Publish(new InteractionFocusChangedEvent("second", InteractionType.Door, true));

            Assert.That(FocusedId(), Is.EqualTo("second"));
            events.Publish(new InteractionFocusChangedEvent("second", InteractionType.Door, false));
            Assert.That(FocusedId(), Is.EqualTo("first"));
        }

        [Test]
        public void SceneLoadClearsAllFocusCandidates()
        {
            events.Publish(new InteractionFocusChangedEvent("special", InteractionType.LightSource, true));
            events.Publish(new SceneLoadedEvent("next_scene"));

            Assert.That(FocusedId(), Is.Null);
        }

        private string FocusedId() => (string)typeof(InteractionSystem)
            .GetField("focusedInteractionId", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(interactions);

        private static LightSourceConfig MakeLight(string id, bool isSpecial)
        {
            var light = ScriptableObject.CreateInstance<LightSourceConfig>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LightSourceConfig).GetField("lightId", flags).SetValue(light, id);
            typeof(LightSourceConfig).GetField("isSpecial", flags).SetValue(light, isSpecial);
            return light;
        }
    }
}
