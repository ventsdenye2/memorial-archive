using System.Collections;
using System.Linq;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Story.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MemorialArchive.Tests.Editor
{
    public sealed class NarrativeRuntimeTests
    {
        [UnityTest]
        public IEnumerator ExploringAllSecondFloorRooms_PlaysHintOnceOnReturn()
        {
            yield return new EnterPlayMode();
            yield return LoadSceneAndSettle("Room_Office");
            var root = GameRoot.Instance;
            root.Context.UI.CloseAll();
            root.ResetForNewGame();
            root.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideSystem>().CompleteStep("light");
            var narrative = root.GetSystem<NarrativeSystem>();
            yield return LoadSceneAndSettle("Floor_2F");
            while (narrative.Current != null) { narrative.Advance(); yield return null; }
            var rooms = new[] { "Room_ArchiveB", "Room_Reception", "Room_Office", "Room_ArchiveA" };
            for (var i = 0; i < rooms.Length; i++)
            {
                yield return LoadSceneAndSettle(rooms[i]);
                while (narrative.Current != null) { narrative.Advance(); yield return null; }
                Assert.That(narrative.HasPlayed("explored_second_floor"), Is.False);
                narrative.RestoreSaveData(JsonUtility.ToJson(narrative.CaptureSaveData()));
                yield return LoadSceneAndSettle("Floor_2F");
                if (i < rooms.Length - 1) Assert.That(narrative.Current, Is.Null);
            }
            Assert.That(narrative.Current?.id, Is.EqualTo("explored_second_floor"));
            Assert.That(narrative.CurrentLine.text, Is.EqualTo("或许我应该上三楼看看"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(narrative.BlocksGameplayInput, Is.True);
            narrative.Advance(); yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1));
            narrative.RestoreSaveData(JsonUtility.ToJson(narrative.CaptureSaveData()));
            yield return LoadSceneAndSettle("Room_ArchiveA");
            yield return LoadSceneAndSettle("Floor_2F");
            Assert.That(narrative.Current, Is.Null);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator UpstairsAndReception_TransitionAutomaticallyPausesAndResumes()
        {
            yield return new EnterPlayMode();
            yield return LoadSceneAndSettle("Room_Office");
            var root = GameRoot.Instance;
            var narrative = root.GetSystem<NarrativeSystem>();
            root.Context.UI.CloseAll();
            root.ResetForNewGame();
            // This route starts upstairs, after the first-floor lamp lesson.
            root.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideSystem>().CompleteStep("light");
            var stairs = root.Context.Configs.GetInteraction("Corridor_1F_3_stairs");
            root.Context.Events.Publish(new SceneTransitionRequestedEvent(stairs.StairUpSceneId, stairs.StairUpSpawnPointId));
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Floor_2F"));
            Assert.That(narrative.Current?.id, Is.EqualTo("enter_second_floor"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(narrative.BlocksGameplayInput, Is.True);
            ScreenCapture.CaptureScreenshot("Logs/NarrativeQA/second-floor-entry.png");
            yield return null;
            while (narrative.Current != null) { narrative.Advance(); yield return null; }
            Assert.That(Time.timeScale, Is.EqualTo(1));
            narrative.RestoreSaveData(JsonUtility.ToJson(narrative.CaptureSaveData()));
            var door = root.Context.Configs.GetInteraction("Room_Reception_enter");
            root.Context.Events.Publish(new SceneTransitionRequestedEvent(door.TransitionSceneId, door.TransitionSpawnPointId));
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room_Reception"));
            Assert.That(narrative.Current?.id, Is.EqualTo("enter_reception"));
            Assert.That(narrative.CurrentLine.text, Does.StartWith("我推开二楼休息室的门"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(narrative.BlocksGameplayInput, Is.True);
            ScreenCapture.CaptureScreenshot("Logs/NarrativeQA/reception-entry.png");
            yield return null;
            while (narrative.Current != null) { narrative.Advance(); yield return null; }
            Assert.That(Time.timeScale, Is.EqualTo(1));
            var back = root.Context.Configs.GetInteraction("Room_Reception_return");
            root.Context.Events.Publish(new SceneTransitionRequestedEvent(back.TransitionSceneId, back.TransitionSpawnPointId));
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(narrative.Current, Is.Null, "Upstairs entry replayed on returning from reception");
            root.Context.Events.Publish(new SceneTransitionRequestedEvent(door.TransitionSceneId, door.TransitionSpawnPointId));
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(narrative.Current, Is.Null, "Reception entry replayed on second visit");
            var inventory = root.GetSystem<MemorialArchive.Gameplay.Inventory.Logic.InventorySystem>();
            var lamp = inventory.PlayerInventory.playerItems.First(p => p.item.itemId == 1006);
            Assert.That(inventory.TryMoveToShortcut(lamp.item.instanceId, 0), Is.True);
            Assert.That(inventory.TrySelectShortcut(0), Is.True);
            var cecil = GameObject.Find("Cecil");
            GameObject.FindGameObjectWithTag("Player").GetComponent<MemorialArchive.Framework.Scene.ISceneSpawnTarget>()
                .MoveToSceneSpawn(new Vector3(cecil.transform.position.x, -5.2f, 0));
            yield return new WaitForSecondsRealtime(.3f);
            var promptView = Object.FindObjectOfType<InteractionPromptView>();
            var prompt = (GameObject)new UnityEditor.SerializedObject(promptView).FindProperty("promptRoot").objectReferenceValue;
            Assert.That(prompt.activeInHierarchy, Is.True, "Cecil interaction prompt is hidden with lantern selected");
            root.Context.Events.Publish(new InteractPressedEvent());
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(narrative.Current?.id, Is.EqualTo("cecil_conversation"), "Actual interaction did not start Cecil dialogue");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Narrative_RuntimeReadingPauseBranchesAndSceneRestore()
        {
            yield return new EnterPlayMode();
            System.IO.File.WriteAllText("Logs/narrative-runtime.txt", "RUNNING");
            yield return LoadSceneAndSettle("Room_Office");
            var root = GameRoot.Instance;
            var narrative = root.GetSystem<NarrativeSystem>();
            Assert.That(narrative.Current?.id, Is.EqualTo("enter_office"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(narrative.BlocksGameplayInput, Is.True);
            ScreenCapture.CaptureScreenshot("Logs/NarrativeQA/office-dialogue.png");
            yield return null;
            while (narrative.Current != null) { narrative.Advance(); yield return null; }
            Assert.That(Time.timeScale, Is.EqualTo(1));
            var saved = JsonUtility.ToJson(narrative.CaptureSaveData());
            narrative.RestoreSaveData(saved);
            yield return LoadSceneAndSettle("Room_Office");
            Assert.That(narrative.Current, Is.Null, "Completed office entry replayed after restore");

            // Exercise the event used by scene note interactions, not just the content catalog.
            foreach (var note in narrative.Content.notes)
            {
                root.Context.Events.Publish(new NoteUnlockedEvent(note.id));
                yield return null;
                var diary = Object.FindObjectOfType<DiaryPanel>();
                Assert.That(diary.IsOpen, Is.True);
                var body = diary.GetComponentsInChildren<Text>().First(t => t.name == "Body");
                Assert.That(body.font.name, Is.EqualTo("FZCHSJW"));
                var pages = DiaryPanel.Paginate(note.content, body);
                Assert.That(string.Concat(pages), Is.EqualTo(note.content));
                foreach (var page in pages)
                {
                    var height = new TextGenerator().GetPreferredHeight(page, body.GetGenerationSettings(body.rectTransform.rect.size)) / body.pixelsPerUnit;
                    Assert.That(height, Is.LessThanOrEqualTo(body.rectTransform.rect.height), note.id);
                }
                var next = diary.GetComponentsInChildren<Button>().First(b => b.name == "RightArrow");
                for (var i = 1; i < pages.Count; i++) { next.onClick.Invoke(); yield return null; }
                root.Context.UI.Close(PanelId.Diary);
                Assert.That(narrative.HasRead(note.id), Is.True, note.id);
            }
            for (var branch = 0; branch < 2; branch++)
            {
                root.Context.UI.CloseAll(); root.ResetForNewGame();
                root.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideSystem>().CompleteStep("light");
                yield return LoadSceneAndSettle("Room_Reception");
                Assert.That(narrative.Current?.id, Is.EqualTo("enter_reception"), "branch=" + branch);
                while (narrative.Current != null) { narrative.Advance(); yield return null; }
                GameObject.FindGameObjectWithTag("Player").GetComponent<MemorialArchive.Framework.Scene.ISceneSpawnTarget>().MoveToSceneSpawn(new Vector3(-6.5f, -5.2f, 0));
                yield return new WaitForSecondsRealtime(.5f);
                root.Context.Events.Publish(new InspectRequestedEvent("Cecil"));
                for (var i = 0; i < 4; i++) yield return null;
                Assert.That(narrative.Current?.id, Is.EqualTo("cecil_conversation"));
                var panel = Object.FindObjectOfType<NarrativePanel>();
                var choices = panel.GetComponentsInChildren<Button>().OrderBy(b => b.name).ToArray();
                Assert.That(choices.Length, Is.EqualTo(2));
                ScreenCapture.CaptureScreenshot("Logs/NarrativeQA/cecil-choice-" + branch + ".png");
                yield return null;
                choices[branch].onClick.Invoke(); yield return null;
                var expected = narrative.Current.lines[branch == 0 ? 1 : 3];
                Assert.That(narrative.CurrentLine, Is.SameAs(expected));
                while (narrative.Current != null) { narrative.Advance(); yield return null; }
                Assert.That(narrative.HasPlayed("cecil_conversation"), Is.True);
                yield return new WaitForSecondsRealtime(1);
                var cecil = GameObject.Find("Cecil");
                Assert.That(cecil.GetComponent<Collider2D>().enabled, Is.False);
                Assert.That(cecil.GetComponentInChildren<Spine.Unity.SkeletonAnimation>().Skeleton.A, Is.Zero);
            }
            yield return LoadSceneAndSettle("Room_Terrace");
            Assert.That(root.Context.Configs.GetInteraction("Room_Terrace_return").TransitionSceneId, Is.EqualTo("Floor_3F"));
            ScreenCapture.CaptureScreenshot("Logs/NarrativeQA/terrace.png");
            yield return null;
            root.Context.Events.Publish(new SceneTransitionRequestedEvent("Floor_3F", "Floor3_spawn_from_Terrace"));
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Floor_3F"));
            Assert.That(GameObject.FindGameObjectWithTag("Player").transform.position.x, Is.EqualTo(-6.5f).Within(.2f));
            System.IO.File.WriteAllText("Logs/narrative-runtime.txt", "PASS: entry pause/resume; restored entry does not replay; all 16 records open/read without text overflow; both Cecil buttons select correct branches and fade; terrace returns to third-floor spawn.");
            yield return new ExitPlayMode();
        }

        private static IEnumerator LoadSceneAndSettle(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName);
            Assert.That(operation, Is.Not.Null, "Scene is not in the build settings: " + sceneName);
            while (!operation.isDone) yield return null;
            while (SceneManager.GetActiveScene().name != sceneName) yield return null;
            for (var i = 0; i < 4; i++) yield return null;
        }
    }
}
