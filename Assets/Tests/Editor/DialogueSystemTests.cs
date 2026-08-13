using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Dialogue.Logic;
using MemorialArchive.Gameplay.Dialogue.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Tests.Editor
{
    public sealed class DialogueSystemTests
    {
        private EventBus events;
        private DialogueSystem dialogue;

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(
                "Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);

            events = new EventBus();
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);
            dialogue = new DialogueSystem();
            dialogue.Initialize(context);
            events.Publish(new DialoguePlayRequestedEvent("opening_dialogue"));
        }

        [TearDown]
        public void TearDown()
        {
            dialogue?.Dispose();
        }

        [Test]
        public void AdvanceWhileTypewriterIsPlaying_DoesNotAdvanceNode()
        {
            var completionRequestCount = 0;
            events.Subscribe<DialogueTypewriterCompletionRequestedEvent>(_ => completionRequestCount++);
            events.Publish(new DialogueTypewriterStateChangedEvent("opening_dialogue", 0, true));

            events.Publish(new DialogueAdvancePressedEvent());

            Assert.That(dialogue.CurrentNodeIndex, Is.EqualTo(0));
            Assert.That(completionRequestCount, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceAfterTypewriterCompletes_AdvancesOneNode()
        {
            events.Publish(new DialogueTypewriterStateChangedEvent("opening_dialogue", 0, true));
            events.Publish(new DialogueTypewriterStateChangedEvent("opening_dialogue", 0, false));

            events.Publish(new DialogueAdvancePressedEvent());

            Assert.That(dialogue.CurrentNodeIndex, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceThatPresentsNextNode_DoesNotCompleteTheNewNode()
        {
            var completionRequestCount = 0;
            events.Subscribe<DialogueTypewriterCompletionRequestedEvent>(_ => completionRequestCount++);
            events.Subscribe<DialogueNodePresentedEvent>(evt =>
            {
                if (evt.Node.NodeIndex == 1)
                {
                    events.Publish(new DialogueTypewriterStateChangedEvent("opening_dialogue", 1, true));
                }
            });
            events.Publish(new DialogueTypewriterStateChangedEvent("opening_dialogue", 0, false));

            events.Publish(new DialogueAdvancePressedEvent());

            Assert.That(dialogue.CurrentNodeIndex, Is.EqualTo(1));
            Assert.That(completionRequestCount, Is.Zero);
        }

        [Test]
        public void OpeningDialoguePrefab_UsesFixedWrappingTypewriterText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/OpeningDialoguePanel.prefab");
            Assert.That(prefab, Is.Not.Null);

            var text = prefab.transform.Find("DialoguePanel/DialogueText")?.GetComponent<Text>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text.fontSize, Is.EqualTo(30));
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.supportRichText, Is.False);
            Assert.That(text.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(text.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));

            var view = prefab.GetComponentInChildren<DialoguePresentationView>(true);
            Assert.That(view, Is.Not.Null);
            var serializedView = new SerializedObject(view);
            Assert.That(serializedView.FindProperty("dialogueFontSize").intValue, Is.EqualTo(30));
            Assert.That(serializedView.FindProperty("useTypewriter").boolValue, Is.True);
            Assert.That(serializedView.FindProperty("charactersPerSecond").floatValue, Is.EqualTo(36f));
        }
    }
}
