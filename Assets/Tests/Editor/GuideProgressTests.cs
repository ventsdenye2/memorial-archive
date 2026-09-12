using System.Reflection;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Guide.Logic;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class GuideProgressTests
    {
        private GuideSystem guide;
        private GuidePresentationConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<GuidePresentationConfig>();
            config.pages = new[]
            {
                new GuidePage { id = "ordinary", message = "ordinary" },
                new GuidePage { id = "inventory", message = "inventory" }
            };

            guide = new GuideSystem();
            guide.Initialize(new GameContext(new EventBus(), null, null, null, null));
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
}
