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
        public IEnumerator Narrative_RuntimeReadingPauseBranchesAndSceneRestore()
        {
            yield return new EnterPlayMode();
            System.IO.File.WriteAllText("Logs/narrative-runtime.txt", "RUNNING");
            SceneManager.LoadScene("Room_Office");
            for (var i = 0; i < 8; i++) yield return null;
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
            SceneManager.LoadScene("Room_Office");
            for (var i = 0; i < 8; i++) yield return null;
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
                root.Context.UI.CloseAll(); narrative.ResetForNewGame();
                SceneManager.LoadScene("Room_Reception");
                for (var i = 0; i < 8; i++) yield return null;
                Assert.That(narrative.Current?.id, Is.EqualTo("enter_reception"));
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
            SceneManager.LoadScene("Room_Terrace");
            for (var i = 0; i < 8; i++) yield return null;
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
    }
}
