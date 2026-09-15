using System.Reflection;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Guide.Logic;
using MemorialArchive.Gameplay.Guide.View;
using MemorialArchive.Framework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class GuideProgressTests
    {
        private GuideSystem guide;
        private GuidePresentationConfig config;
        private GameObject uiObject;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<GuidePresentationConfig>();
            config.pages = new[]
            {
                new GuidePage { id = "ordinary", message = "ordinary" },
                new GuidePage { id = "inventory", message = "inventory" },
                new GuidePage { id = "light", message = "light" },
                new GuidePage { id = "systems", message = "systems" }
            };

            guide = new GuideSystem();
            uiObject = new GameObject("GuideTestUI");
            var ui = uiObject.AddComponent<UIManager>();
            var panel = uiObject.AddComponent<GuideTestPanel>();
            typeof(BasePanel).GetField("panelId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, PanelId.GuideOverlay);
            ui.RegisterPersistentPanel(panel);
            var context = new GameContext(new EventBus(), null, null, ui, null);
            ui.Initialize(context);
            guide.Initialize(context);
            // Initialize intentionally loads the production asset. Replace it with an
            // in-memory catalog so these EditMode tests remain self-contained.
            typeof(GuideSystem)
                .GetField("<Config>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(guide, config);
        }

        [TearDown]
        public void TearDown()
        {
            guide.Dispose();
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(uiObject);
        }

        [Test]
        public void DiaryMapButtons_WaitForBothPagesAndUnlockWhenSystemsOpens()
        {
            guide.Record("map_unlocked");
            guide.Request("systems");
            guide.Tick(0f);
            Assert.That(guide.Current, Is.Null);
            Assert.That(guide.DiaryMapButtonsVisible, Is.False);
            guide.TryShowLightTutorial(true);
            Assert.That(guide.Current.id, Is.EqualTo("light"));
            Assert.That(guide.DiaryMapButtonsVisible, Is.False);
            guide.Acknowledge();
            guide.Tick(0f);
            Assert.That(guide.Current.id, Is.EqualTo("systems"));
            Assert.That(guide.DiaryMapButtonsVisible, Is.True);
            var saved = JsonUtility.ToJson(guide.CaptureSaveData());
            guide.ResetForNewGame();
            Assert.That(guide.DiaryMapButtonsVisible, Is.False);
            guide.RestoreSaveData(saved);
            Assert.That(guide.DiaryMapButtonsVisible, Is.True);
        }

        [Test]
        public void DiaryMapButtons_LegacySaveRequiresBothTutorials()
        {
            guide.RestoreSaveData("{\"completedStepIds\":[\"map_unlocked\"]}");
            Assert.That(guide.DiaryMapButtonsVisible, Is.False);
            guide.RestoreSaveData("{\"completedStepIds\":[\"light\",\"systems\"],\"startedStepIds\":null}");
            Assert.That(guide.DiaryMapButtonsVisible, Is.True);
        }

        [Test]
        public void LightTutorial_WaitsForAlignment_EvenAfterRestoringPendingRequest()
        {
            guide.Request("light");
            var json = JsonUtility.ToJson(guide.CaptureSaveData());
            guide.RestoreSaveData(json);
            guide.Tick(0f);
            guide.TryShowLightTutorial(false);
            Assert.That(guide.Current, Is.Null);
            guide.TryShowLightTutorial(true);
            Assert.That(guide.Current.id, Is.EqualTo("light"));
            guide.Acknowledge();
            guide.TryShowLightTutorial(true);
            Assert.That(guide.Current, Is.Null);
            Assert.That(guide.HasCompleted("light"), Is.True);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1600f, 1200f)]
        public void LightHighlight_UsesTheArtworkAreaAcrossAspectRatios(float width, float height)
        {
            var size = new Vector2(width, height);
            var scale = Mathf.Min(width / 1920f, height / 1080f);
            var offset = (size - new Vector2(1920f, 1080f) * scale) * 0.5f;
            var center = offset + new Vector2(917f, 584.5f) * scale;
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(center.x, center.y, 10f), size), Is.True);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(center.x, center.y, -1f), size), Is.False);
            foreach (var outside in new[] { new Vector2(831f, 584f), new Vector2(1003f, 584f), new Vector2(917f, 490f), new Vector2(917f, 679f) })
            {
                var point = offset + outside * scale;
                Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(point.x, point.y, 10f), size), Is.False);
            }
        }

        [Test]
        public void FrontHallLamp_KeepsOriginalSizeAndWaitsForCenterInsideHighlight()
        {
            var screenSize = new Vector2(1920f, 1080f);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(917f, 584.5f, 10f), screenSize, 143.5f, true), Is.True);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(980f, 584.5f, 10f), screenSize, 143.5f, true), Is.True);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(1003f, 584.5f, 10f), screenSize, 143.5f, true), Is.False);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(917f, 400.5f, 10f), screenSize, 143.5f, true), Is.False);
        }

        [Test]
        public void LightHighlight_TallSceneLampMustAlignHorizontallyAndOverlapVertically()
        {
            var screenSize = new Vector2(1920f, 1080f);
            // FrontHall's replacement lamp is 2.87 units tall and centered at y=-1.395.
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(917f, 400.5f, 10f), screenSize, 143.5f), Is.True);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(1200f, 400.5f, 10f), screenSize, 143.5f), Is.False);
            Assert.That(GuideWorldView.IsInsideLightHighlight(new Vector3(917f, 200f, 10f), screenSize, 143.5f), Is.False);
        }

        [Test]
        public void Request_IsIdempotent_AndCompletedStepDoesNotRequeue()
        {
            guide.Request("ordinary");
            guide.Request("ordinary");
            var progress = (GuideSystem.Progress)guide.CaptureSaveData();
            Assert.That(progress.pending, Is.EqualTo(new[] { "ordinary" }));

            progress.pending.Clear();
            progress.completedStepIds.Add("ordinary");
            guide.Request("ordinary");
            Assert.That(progress.pending, Is.Empty);
        }

        [Test]
        public void CaptureAndRestore_PreserveCompletedAndPendingProgress()
        {
            guide.Request("inventory");
            var saved = (GuideSystem.Progress)guide.CaptureSaveData();
            saved.completedStepIds.Add("ordinary");
            var json = JsonUtility.ToJson(saved);
            guide.ResetForNewGame();
            guide.RestoreSaveData(json);

            Assert.That(guide.HasCompleted("ordinary"), Is.True);
            var restored = (GuideSystem.Progress)guide.CaptureSaveData();
            Assert.That(restored.pending, Is.EqualTo(new[] { "inventory" }));
        }

        [Test]
        public void ResetForNewGame_ClearsCompletedAndPendingProgress()
        {
            guide.Request("ordinary");
            ((GuideSystem.Progress)guide.CaptureSaveData()).completedStepIds.Add("ordinary");

            guide.ResetForNewGame();

            var progress = (GuideSystem.Progress)guide.CaptureSaveData();
            Assert.That(progress.completedStepIds, Is.Empty);
            Assert.That(progress.pending, Is.Empty);
        }

        [Test]
        public void InventoryRequest_IsPlacedBeforeExistingOrdinaryRequests()
        {
            guide.Request("ordinary");
            guide.Request("inventory");

            var progress = (GuideSystem.Progress)guide.CaptureSaveData();
            Assert.That(progress.pending, Is.EqualTo(new[] { "inventory", "ordinary" }));
        }

        [Test]
        public void RestoreSaveData_AcceptsLegacyNullLists()
        {
            guide.RestoreSaveData("{\"completedStepIds\":null,\"pending\":null}");

            var progress = (GuideSystem.Progress)guide.CaptureSaveData();
            Assert.That(progress.completedStepIds, Is.Not.Null.And.Empty);
            Assert.That(progress.pending, Is.Not.Null.And.Empty);
        }
    }

    public sealed class GuideTestPanel : BasePanel { }
}
