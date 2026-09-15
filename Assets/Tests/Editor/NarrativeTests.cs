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
            Assert.That(content.notes.Length, Is.EqualTo(17));
            Assert.That(content.notes.Select(n => n.id).Distinct().Count(), Is.EqualTo(17));
            Assert.That(content.FindNote("guide_first_diary").isDiary, Is.True);
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
        public void Upstairs_QueuesOnArrivalAndDoesNotReplayAfterSaveRestore()
        {
            var events = new EventBus();
            narrative.Dispose();
            narrative.Initialize(new GameContext(events, null, null, null, null));
            events.Publish(new SceneLoadedEvent("Floor_2F"));
            var progress = (NarrativeProgress)narrative.CaptureSaveData();
            Assert.That(progress.pendingSequences, Is.EqualTo(new[] { "enter_second_floor" }));
            Assert.That(content.FindSequence("enter_second_floor").lines[0].text, Does.StartWith("拾级而上来到二楼"));
            Assert.That(content.FindSequence("enter_reception").lines[0].text, Does.StartWith("我推开二楼休息室的门"));
            progress.playedSequences.Add("enter_second_floor");
            progress.pendingSequences.Clear();
            narrative.RestoreSaveData(JsonUtility.ToJson(progress));
            events.Publish(new SceneLoadedEvent("Floor_2F"));
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).pendingSequences, Is.Empty);
        }
        [Test]
        public void ExploringSecondFloor_RequiresUnlockedRoomsConversationAndLockedDoorCheck()
        {
            var events = new EventBus();
            narrative.Dispose();
            narrative.Initialize(new GameContext(events, null, null, null, null));
            var flow = new MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem();
            typeof(MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem).GetField("narrative", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(flow, narrative);
            foreach (var room in new[] { "Room_ArchiveA", "Room_ArchiveB", "Room_Reception" })
            {
                events.Publish(new SceneLoadedEvent(room));
                Assert.That(flow.SecondFloorExplored, Is.False);
            }
            var flowProgress = (MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem.Progress)flow.CaptureSaveData();
            flowProgress.lockedOfficeChecked = true;
            Assert.That(flow.SecondFloorExplored, Is.False);
            ((NarrativeProgress)narrative.CaptureSaveData()).playedSequences.Add("cecil_conversation");
            narrative.RestoreSaveData(JsonUtility.ToJson(narrative.CaptureSaveData()));
            Assert.That(flow.SecondFloorExplored, Is.True);
            Assert.That(narrative.HasVisited("Room_Office"), Is.False);
            Assert.That(flow.TryTravel("Floor_2F", "Floor_3F"), Is.True);
        }

        [Test]
        public void UpstairsDenialRepeatsAndClosedDestinationsStayBlocked()
        {
            var flow = new MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem();
            typeof(MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem).GetField("narrative", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(flow, narrative);
            var progress = (NarrativeProgress)narrative.CaptureSaveData();
            for (var i = 0; i < 3; i++)
            {
                Assert.That(flow.TryTravel("Floor_2F", "Floor_3F"), Is.False);
                Assert.That(progress.pendingSequences.Count(s => s == "floor3_locked"), Is.EqualTo(1));
                progress.pendingSequences.Clear();
                if (!progress.playedSequences.Contains("floor3_locked")) progress.playedSequences.Add("floor3_locked");
            }
            foreach (var destination in new[] { "Room_TreatmentB", "Room_Director", "Floor_4F" })
                Assert.That(flow.TryTravel("Floor_3F", destination), Is.False);
        }

        [Test]
        public void DemoCompletionRequiresEveryOfficeObjectiveAndSurvivesSave()
        {
            var flow = new MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem).GetField("narrative", flags).SetValue(flow, narrative);
            typeof(MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem).GetField("context", flags).SetValue(flow, new GameContext(new EventBus(), null, null, null, null));
            narrative.RestoreSaveData("{\"visitedScenes\":[\"Room_ArchiveA\",\"Room_ArchiveB\",\"Room_Reception\"],\"playedSequences\":[\"cecil_conversation\"]}");
            var p = (MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem.Progress)flow.CaptureSaveData();
            p.lockedOfficeChecked = true;
            p.combatPhase = 5;
            p.officeCombatStarted = true;
            p.officeMeleeHealth = 0;
            p.officeRangedHealth = 1;
            foreach (var id in new[] { "Room_Office_main_note_1", "Room_Office_diary_2" })
            {
                narrative.Collect(id);
                narrative.RecordRead(id);
            }
            Assert.That(flow.DemoComplete, Is.False);
            p.officeRangedHealth = 0;
            Assert.That(flow.DemoComplete, Is.False);
            p.officeSafeChecked = true;
            Assert.That(flow.DemoComplete, Is.True);
            flow.RestoreSaveData(JsonUtility.ToJson(p));
            Assert.That(flow.DemoComplete, Is.True);
            flow.RestoreSaveData("{}");
            Assert.That(flow.DemoComplete, Is.False);
        }
        [Test]
        public void ExplorationProgress_OldSavesAndNewGamesDoNotInheritVisitedRooms()
        {
            narrative.RestoreSaveData("{\"visitedScenes\":[\"Room_Office\",\"Room_ArchiveA\"]}");
            narrative.RestoreSaveData("{\"playedSequences\":[]}");
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).visitedScenes, Is.Empty);
            narrative.RestoreSaveData("{\"visitedScenes\":[\"Room_Reception\"]}");
            narrative.ResetForNewGame();
            Assert.That(((NarrativeProgress)narrative.CaptureSaveData()).visitedScenes, Is.Empty);
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
        public void TerraceEntranceTriggerReachesThirdFloorWalkingLine()
        {
            const string scenePath = "Assets/Scenes/Floor_3F.unity";
            var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var point = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>(true))
                    .Single(p => p.InteractionId == "Room_Terrace_enter");
                var trigger = point.GetComponent<BoxCollider2D>();
                Assert.That(point.gameObject.activeInHierarchy && point.enabled && trigger.enabled && trigger.isTrigger, Is.True);
                // Test in local space so mirrored door artwork cannot flip the trigger off the walking line.
                var walkingPoint = (Vector2)point.transform.InverseTransformPoint(new Vector3(point.transform.position.x, -5.2f, 0));
                var localBounds = new Rect(trigger.offset - trigger.size * .5f, trigger.size);
                Assert.That(localBounds.Contains(walkingPoint), Is.True, "Terrace entrance must be reachable at the player's fixed walking height.");
                var db = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
                var entrance = db.Interactions.Single(c => c != null && c.InteractionId == point.InteractionId);
                Assert.That(entrance.TransitionSceneId, Is.EqualTo("Room_Terrace"));
                Assert.That(entrance.TransitionSpawnPointId, Is.EqualTo("Room_Terrace_spawn_entry"));
                Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Room_Terrace.unity"), Is.True);
            }
            finally
            {
                if (!wasLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                if (originalScene.IsValid() && originalScene.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
            }
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
