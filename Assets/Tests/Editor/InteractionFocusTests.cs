using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.Logic;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Lighting.Logic;
using MemorialArchive.Gameplay.Inventory.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
        private GameObject promptOwner;
        private GameObject prompt;
        private Button promptButton;
        private Sprite inspectSprite;
        private Sprite pickupSprite;
        private InventorySystem inventory;
        private LightingSystem lighting;
        private GameObject testRootOwner;
        private GameRoot previousRoot;
        private InteractionPromptView view;

        [SetUp]
        public void SetUp()
        {
            special = MakeLight("special", true);
            ordinary = MakeLight("ordinary", false);
            database = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset"));
            typeof(GameConfigDatabase).GetField("lightSources", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(database, new[] { special, ordinary });
            configs = new ConfigManager(database);
            configs.Initialize(null);
            events = new EventBus();
            var context = new GameContext(events, configs, null, null, null);
            inventory = new InventorySystem();
            inventory.Initialize(context);
            lighting = new LightingSystem(inventory);
            lighting.Initialize(context);
            testRootOwner = new GameObject("PromptTestGameRoot");
            testRootOwner.SetActive(false);
            var root = testRootOwner.AddComponent<GameRoot>();
            ((List<IGameSystem>)typeof(GameRoot).GetField("systems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(root)).Add(lighting);
            previousRoot = GameRoot.Instance;
            typeof(GameRoot).GetProperty("Instance").SetValue(null, root);
            var lantern = inventory.PlayerInventory.playerItems.First(p => p.item.itemId == 1006);
            Assert.That(inventory.TryMoveToShortcut(lantern.item.instanceId, 0), Is.True);
            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            interactions = new InteractionSystem();
            interactions.Initialize(new GameContext(events, configs, null, null, null));
            promptOwner = new GameObject("InteractionPromptTest");
            promptOwner.SetActive(false);
            view = promptOwner.AddComponent<InteractionPromptView>();
            prompt = new GameObject("Prompt", typeof(RectTransform), typeof(Image), typeof(Button));
            prompt.transform.SetParent(promptOwner.transform, false);
            promptButton = prompt.GetComponent<Button>();
            inspectSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            pickupSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(InteractionPromptView).GetField("promptRoot", flags).SetValue(view, prompt);
            typeof(InteractionPromptView).GetField("viewSprite", flags).SetValue(view, inspectSprite);
            typeof(InteractionPromptView).GetField("pickupSprite", flags).SetValue(view, pickupSprite);
            typeof(InteractionPromptView).GetMethod("Awake", flags).Invoke(view, null);
            var handler = (System.Action<ActiveInteractionChangedEvent>)System.Delegate.CreateDelegate(
                typeof(System.Action<ActiveInteractionChangedEvent>), view,
                typeof(InteractionPromptView).GetMethod("HandleActiveInteractionChanged", flags));
            events.Subscribe(handler);
        }

        [TearDown]
        public void TearDown()
        {
            interactions.Dispose();
            typeof(GameRoot).GetProperty("Instance").SetValue(null, previousRoot);
            Object.DestroyImmediate(testRootOwner);
            lighting.Dispose();
            inventory.Dispose();
            configs.Dispose();
            Object.DestroyImmediate(promptOwner);
            Object.DestroyImmediate(inspectSprite);
            Object.DestroyImmediate(pickupSprite);
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

        [TestCase(false)]
        [TestCase(true)]
        public void ClearingFocusHidesAndDisablesPrompt_AndReentryRestoresIt(bool sceneChange)
        {
            events.Publish(new InteractionFocusChangedEvent("inspect", InteractionType.Inspect, true));
            Assert.That(prompt.activeSelf, Is.True);
            Assert.That(promptButton.interactable, Is.True);

            if (sceneChange) events.Publish(new SceneLoadedEvent("next_scene"));
            else events.Publish(new InteractionFocusChangedEvent("inspect", InteractionType.Inspect, false));

            Assert.That(FocusedId(), Is.Null);
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(promptButton.interactable, Is.False);

            events.Publish(new InteractionFocusChangedEvent("inspect", InteractionType.Inspect, true));
            Assert.That(prompt.activeSelf, Is.True);
            Assert.That(promptButton.interactable, Is.True);
        }

        [Test]
        public void OverlappingInteractionsUpdatePromptAndRestoreFallbackArtwork()
        {
            events.Publish(new InteractionFocusChangedEvent("inspect", InteractionType.Inspect, true));
            events.Publish(new InteractionFocusChangedEvent("note", InteractionType.NotePickup, true));
            Assert.That(prompt.GetComponent<Image>().sprite, Is.SameAs(pickupSprite));

            events.Publish(new InteractionFocusChangedEvent("note", InteractionType.NotePickup, false));
            Assert.That(FocusedId(), Is.EqualTo("inspect"));
            Assert.That(prompt.activeSelf, Is.True);
            Assert.That(promptButton.interactable, Is.True);
            Assert.That(prompt.GetComponent<Image>().sprite, Is.SameAs(inspectSprite));

            events.Publish(new InteractionFocusChangedEvent("inspect", InteractionType.Inspect, false));
            Assert.That(prompt.activeSelf, Is.False);
        }

        [TestCase(InteractionType.LightSource)]
        [TestCase(InteractionType.Inspect)]
        [TestCase(InteractionType.NotePickup)]
        [TestCase(InteractionType.Container)]
        [TestCase(InteractionType.SceneExit)]
        public void PromptRequiresSelectedLantern_AndTracksEquipmentWithoutReenteringTrigger(InteractionType type)
        {
            events.Publish(new InteractionFocusChangedEvent("target", type, true));
            Assert.That(prompt.activeSelf, Is.True);

            // The lantern remains in the shortcut bar, but is no longer equipped.
            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            UpdatePrompt();
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(promptButton.interactable, Is.False);

            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            UpdatePrompt();
            Assert.That(prompt.activeSelf, Is.True);

            var lantern = inventory.PlayerInventory.playerItems.First(p => p.item.itemId == 1006);
            Assert.That(inventory.TryMoveToBackpack(lantern.item.instanceId, 2, 0), Is.True);
            UpdatePrompt();
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(promptButton.interactable, Is.False);

            // A new focus must not reveal the prompt while the lantern is in the backpack.
            events.Publish(new InteractionFocusChangedEvent("next", type, true));
            Assert.That(prompt.activeSelf, Is.False);
            Assert.That(inventory.TryMoveToShortcut(lantern.item.instanceId, 1), Is.True);
            UpdatePrompt();
            Assert.That(prompt.activeSelf, Is.False, "A lantern in an unselected shortcut slot is not equipped.");
            Assert.That(inventory.TrySelectShortcut(1), Is.True);
            UpdatePrompt();
            Assert.That(prompt.activeSelf, Is.True);
        }

        private void UpdatePrompt() => typeof(InteractionPromptView)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);

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
