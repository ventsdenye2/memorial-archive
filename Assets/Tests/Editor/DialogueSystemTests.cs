using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Dialogue.Logic;
using MemorialArchive.Gameplay.Dialogue.View;
using MemorialArchive.Gameplay.Dialogue.Config;
using MemorialArchive.Gameplay.Dialogue.Data;
using System.Collections.Generic;
using System.Reflection;
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
        public void OpeningPortraits_AllNodesKeepSpeakerHighlightAndNameplateInSync()
        {
            var config = AssetDatabase.LoadAssetAtPath<DialogueSequenceConfig>(
                "Assets/GameConfigs/Dialogue/OpeningDialogue.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/OpeningDialoguePanel.prefab");
            var host = new GameObject("OpeningPortraitTestHost");
            host.SetActive(false); // Do not start the actual dialogue/event subscriptions.
            try
            {
                var instance = Object.Instantiate(prefab, host.transform);
                var panel = instance.transform.Find("DialoguePanel");
                var view = panel.GetComponent<DialoguePresentationView>();
                var apply = typeof(DialoguePresentationView).GetMethod("ApplyPortraits",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(apply, Is.Not.Null);
                var slots = new Dictionary<string, DialoguePortraitConfig>();
                var characterArt = new Dictionary<string, string>
                {
                    { "andre", "安德比尔" }, { "george", "乔治" }, { "emily", "艾米丽" }
                };
                // Repeat the sequence to also exercise clearing portraits on replay.
                for (var replay = 0; replay < 2; replay++)
                for (var index = 0; index < config.Nodes.Length; index++)
                {
                    var node = config.Nodes[index];
                    foreach (var portrait in node.Portraits) slots[portrait.SlotId] = portrait;
                    apply.Invoke(view, new object[] { new DialogueNodeData(config.DialogueId, index, node, null) });
                    Sprite expectedName = null;
                    foreach (var entry in slots)
                    {
                        var portrait = entry.Value;
                        var suffix = entry.Key == "left" ? "Left" : "Right";
                        var image = panel.Find("Portrait" + suffix).GetComponent<Image>();
                        Assert.That(image.gameObject.activeSelf, Is.EqualTo(portrait.Visible), node.NodeId);
                        Assert.That(panel.Find("Dim" + suffix).gameObject.activeSelf, Is.False, node.NodeId);
                        if (!portrait.Visible) continue;
                        Assert.That(portrait.Portrait, Is.Not.Null, node.NodeId);
                        Assert.That(portrait.InactivePortrait, Is.Not.Null, node.NodeId);
                        bool speaking = portrait.CharacterId == node.SpeakerId;
                        var expected = string.IsNullOrEmpty(node.SpeakerId) || speaking
                            ? portrait.Portrait : portrait.InactivePortrait;
                        Assert.That(image.sprite, Is.SameAs(expected), node.NodeId + " " + portrait.CharacterId);
                        Assert.That(image.color, Is.EqualTo(Color.white), node.NodeId);
                        Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(expected.rect.size), node.NodeId);
                        if (speaking)
                        {
                            expectedName = AssetDatabase.LoadAssetAtPath<Sprite>(
                                "Assets/Art/UI/Imported_UI2.0/UI2.0/对话页/" + characterArt[portrait.CharacterId] + ".png");
                            Assert.That(portrait.Nameplate, Is.SameAs(expectedName), node.NodeId);
                        }
                    }
                    var nameImage = panel.Find("SpeakerName").GetComponent<Image>();
                    Assert.That(nameImage.gameObject.activeSelf, Is.EqualTo(expectedName != null), node.NodeId);
                    Assert.That(nameImage.sprite, Is.SameAs(expectedName), node.NodeId);
                    Assert.That(panel.Find("DialogueFrame").GetSiblingIndex(),
                        Is.GreaterThan(panel.Find("PortraitRight").GetSiblingIndex()));
                    Assert.That(nameImage.transform.GetSiblingIndex(),
                        Is.GreaterThan(panel.Find("DialogueFrame").GetSiblingIndex()));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OpeningDialoguePrefab_UsesFixedWrappingTypewriterText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/OpeningDialoguePanel.prefab");
            Assert.That(prefab, Is.Not.Null);

            var text = prefab.transform.Find("DialoguePanel/DialogueText")?.GetComponent<Text>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text.fontSize, Is.EqualTo(32));
            Assert.That(text.color, Is.EqualTo(Color.white));
            Assert.That(text.font, Is.SameAs(AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/FZCHSJW.TTF")));
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
            Assert.That(serializedView.FindProperty("useAuthoredTextStyles").boolValue, Is.True);
            Assert.That(serializedView.FindProperty("speechFont").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/FZZJ-LZXTFSJW.TTF")));

            var cg = prefab.transform.Find("DialoguePanel/CG");
            Assert.That(cg.GetComponent<Image>().preserveAspect, Is.False);
            Assert.That(cg.GetComponent<AspectRatioFitter>().aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            var nameplate = (RectTransform)prefab.transform.Find("DialoguePanel/SpeakerName");
            Assert.That(text.rectTransform.anchoredPosition.y + text.rectTransform.rect.yMax,
                Is.LessThan(nameplate.anchoredPosition.y + nameplate.rect.yMin));
        }

        [Test]
        public void OpeningDialogue_GeorgeEntersWithFatherDescription_AndLightningMatchesItsSentence()
        {
            var config = AssetDatabase.LoadAssetAtPath<DialogueSequenceConfig>(
                "Assets/GameConfigs/Dialogue/OpeningDialogue.asset");
            var fatherDescription = System.Array.Find(config.Nodes, n => n.NodeId == "opening_024");
            Assert.That(fatherDescription.Portraits.Length, Is.EqualTo(1));
            Assert.That(fatherDescription.Portraits[0].CharacterId, Is.EqualTo("george"));
            Assert.That(fatherDescription.Portraits[0].Visible, Is.True);
            Assert.That(System.Array.Find(config.Nodes, n => n.NodeId == "opening_025").SpeakerId,
                Is.EqualTo("george"));
            foreach (var node in config.Nodes)
            {
                bool hasLightning = System.Array.IndexOf(node.EffectIds, "opening_thunder") >= 0;
                Assert.That(hasLightning, Is.EqualTo(node.Text.StartsWith("一道闪电劈开夜空")), node.NodeId);
                if (hasLightning) Assert.That(node.BlocksAdvance, Is.True);
            }
        }
    }
}
