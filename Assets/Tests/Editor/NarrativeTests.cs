using System.Collections.Generic;
using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Story.Data;
using MemorialArchive.Gameplay.Story.Logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class NarrativeTests
    {
        private NarrativeContent content;
        private NarrativeSystem narrative;
        [SetUp]
        public void SetUp()
        {
            content = NarrativeContent.Load();
            narrative = new NarrativeSystem(content);
            narrative.Initialize(new GameContext(new EventBus(), null, null, null, null));
        }
        [TearDown] public void TearDown() => narrative.Dispose();

        [Test]
        public void Catalog_HasAllReviewedNotesAndValidDialogueLinks()
        {
            Assert.That(content.notes.Length, Is.EqualTo(16));
            Assert.That(content.notes.Select(n => n.id).Distinct().Count(), Is.EqualTo(16));
            foreach (var note in content.notes) Assert.That(note.content, Is.Not.Null.And.Not.Empty, note.id);
            foreach (var sequence in content.sequences)
            {
                foreach (var id in sequence.requiredNotes) Assert.That(content.FindNote(id), Is.Not.Null);
                foreach (var line in sequence.lines)
                {
                    Assert.That(line.next, Is.InRange(-1, sequence.lines.Length - 1));
                    foreach (var choice in line.choices) Assert.That(choice.next, Is.InRange(0, sequence.lines.Length - 1));
                }
            }
        }
        [Test]
        public void Reading_LongUnpunctuatedTextAndParagraphsAreNeverLost()
        {
            foreach (var note in content.notes)
            {
                var pages = DiaryPanel.Paginate(note.content);
                Assert.That(string.Concat(pages), Is.EqualTo(note.content), note.id);
                Assert.That(pages.All(p => p.Length <= 340), Is.True);
            }
        }
        [Test]
        public void LegacySaveWithoutNarrativeModuleDoesNotInheritCurrentProgress()
        {
            narrative.Collect("Room_Director_main_note_1"); narrative.RecordRead("Room_Director_main_note_1");
            var saves = new MemorialArchive.Framework.Save.SaveManager();
            saves.RegisterModule(narrative);
            saves.RestoreSaveData(new MemorialArchive.Framework.Save.SaveData());
            Assert.That(narrative.HasRead("Room_Director_main_note_1"), Is.False);
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).collectedNotes, Is.Empty);
        }
        [Test]
        public void Director_RequiresBothReadDocumentsAndSurvivesRestore()
        {
            narrative.Collect("Room_Director_main_note_1"); narrative.RecordRead("Room_Director_main_note_1");
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).pendingSequences, Does.Not.Contain("director_conclusion"));
            narrative.RestoreSaveData(JsonUtility.ToJson(narrative.CaptureSaveData()));
            narrative.Collect("Room_Director_note_2"); narrative.RecordRead("Room_Director_note_2");
            narrative.RecordRead("Room_Director_note_2");
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).pendingSequences.Count(s => s == "director_conclusion"), Is.EqualTo(1));
        }
        [Test]
        public void CollectedButUnreadDocumentsDoNotTriggerConclusion()
        {
            narrative.Collect("Room_Director_main_note_1"); narrative.Collect("Room_Director_note_2");
            narrative.RecordRead("Room_Director_note_2");
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).pendingSequences, Does.Not.Contain("director_conclusion"));
        }
        [Test]
        public void CompletedReflectionsDoNotReplayAfterLoadOrRepeatedInteraction()
        {
            var note = content.notes.First(n => !string.IsNullOrEmpty(n.reflectionId));
            narrative.Collect(note.id); narrative.RecordRead(note.id);
            var data = (NarrativeProgress)narrative.CaptureSaveData();
            data.playedSequences.Add(note.reflectionId); data.pendingSequences.Remove(note.reflectionId);
            narrative.RestoreSaveData(JsonUtility.ToJson(data));
            narrative.Collect(note.id); narrative.RecordRead(note.id);
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).pendingSequences, Does.Not.Contain(note.reflectionId));
            Assert.That(narrative.GetCollectedNotes(note.isDiary).Count(n => n.id == note.id), Is.EqualTo(1));
        }
        [Test]
        public void Cecil_ChoicesHaveDistinctAnswersAndConvergeBeforeDisappearance()
        {
            var cecil = content.FindSequence("cecil_conversation");
            var first = Path(cecil, cecil.lines[0].choices[0].next);
            var second = Path(cecil, cecil.lines[0].choices[1].next);
            Assert.That(first, Has.Member(1).And.Member(2).And.Not.Member(3));
            Assert.That(second, Has.Member(3).And.Member(4).And.Not.Member(1));
            Assert.That(first.Intersect(second), Is.EqualTo(new[] {5,6,7,8,9}));
            Assert.That(cecil.lines[8].fadeCecil, Is.True);
        }
        private static List<int> Path(NarrativeSequence sequence, int index)
        {
            var result = new List<int>();
            while (index >= 0)
            {
                Assert.That(result, Has.No.Member(index), "Dialogue cycle"); result.Add(index); index = sequence.lines[index].next;
            }
            return result;
        }
        [Test]
        public void AuthoredInteractionConfigsPointToEveryReadingEntry()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            foreach (var note in content.notes) Assert.That(db.Interactions.Any(c => c != null && c.NoteId == note.id), Is.True, note.id);
            var back = db.Interactions.First(c => c.InteractionId == "Room_Terrace_return");
            Assert.That(back.TransitionSceneId, Is.EqualTo("Floor_3F"));
        }
    }
}
