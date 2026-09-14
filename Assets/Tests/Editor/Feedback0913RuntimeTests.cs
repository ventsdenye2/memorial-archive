using System.Collections;
using System.IO;
using System.Linq;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Dialogue.Logic;
using MemorialArchive.Gameplay.Guide.Logic;
using MemorialArchive.Gameplay.Story.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MemorialArchive.Tests.Editor
{
    public sealed class Feedback0913RuntimeTests
    {
        [UnityTest]
        public IEnumerator NewGame_OpeningAndGameplayHandoff()
        {
            Directory.CreateDirectory("Logs/Feedback0913QA");
            yield return new EnterPlayMode();
            SceneManager.LoadScene("MainMenu");
            for (var i = 0; i < 12; i++) yield return null;
            var root = GameRoot.Instance;
            var menu = Object.FindObjectOfType<MainMenuPanel>();
            Assert.That(menu, Is.Not.Null);
            menu.GetComponentsInChildren<Button>(true).Single(b => b.name == "NewGameButton").onClick.Invoke();
            yield return null;
            ScreenCapture.CaptureScreenshot("Logs/Feedback0913QA/01-confirm.png");
            yield return null;
            var confirm = Object.FindObjectOfType<ConfirmDialogPanel>();
            Assert.That(confirm.IsOpen, Is.True);
            confirm.GetComponentsInChildren<Button>(true).Single(b => b.name == "ConfirmButton").onClick.Invoke();
            for (var i = 0; i < 20; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("OpeningStory"));
            var dialogue = root.GetSystem<DialogueSystem>();
            var seen = -1;
            for (var frame = 0; frame < 1200 && SceneManager.GetActiveScene().name == "OpeningStory"; frame++)
            {
                if (!dialogue.IsInputModeActive) { yield return null; continue; }
                root.Context.Events.Publish(new DialogueTypewriterCompletionRequestedEvent(dialogue.CurrentDialogueId, dialogue.CurrentNodeIndex));
                yield return null;
                if (seen != dialogue.CurrentNodeIndex)
                {
                    seen = dialogue.CurrentNodeIndex;
                    ScreenCapture.CaptureScreenshot("Logs/Feedback0913QA/opening-" + seen.ToString("D2") + ".png");
                    yield return null;
                }
                root.Context.Events.Publish(new DialogueAdvancePressedEvent());
                yield return null;
            }
            for (var i = 0; i < 20; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("FrontHall"));
            var narrative = root.GetSystem<NarrativeSystem>();
            Assert.That(narrative.Current?.id, Is.EqualTo("enter_front_hall"));
            ScreenCapture.CaptureScreenshot("Logs/Feedback0913QA/02-front-hall.png");
            yield return null;
            while (narrative.Current != null) { narrative.Advance(); yield return null; }
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot("Logs/Feedback0913QA/03-guide.png");
            yield return null;

            var guide = root.GetSystem<GuideSystem>();
            Assert.That(guide.Current, Is.Not.Null);
            guide.Acknowledge();
            yield return null;
            Assert.That(root.Context.UI.IsOpen(PanelId.GuideOverlay), Is.False);

            foreach (var id in new[] { PanelId.System, PanelId.Load, PanelId.Settings })
            {
                var panel = root.Context.UI.Open(id);
                Assert.That(panel, Is.Not.Null, id.ToString());
                yield return null;
                ScreenCapture.CaptureScreenshot("Logs/Feedback0913QA/04-" + id + ".png");
                yield return null;
                root.Context.UI.Close(id);
            }
            yield return new ExitPlayMode();
        }
    }
}
