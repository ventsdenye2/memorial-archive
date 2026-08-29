using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.Logic;
using NUnit.Framework;

namespace MemorialArchive.Tests.Editor
{
    public sealed class InteractionLightingPolicyTests
    {
        [TestCase(InteractionType.SceneExit)]
        [TestCase(InteractionType.LightSource)]
        public void RecoveryInteractions_CanBeAttemptedInDarkness(InteractionType interactionType)
        {
            Assert.That(InteractionLightingPolicy.CanAttemptInDarkness(interactionType), Is.True);
        }

        [TestCase(InteractionType.Container)]
        [TestCase(InteractionType.Inspect)]
        [TestCase(InteractionType.Door)]
        [TestCase(InteractionType.Puzzle)]
        [TestCase(InteractionType.SavePoint)]
        public void RegularInteractions_RemainBlockedInDarkness(InteractionType interactionType)
        {
            Assert.That(InteractionLightingPolicy.CanAttemptInDarkness(interactionType), Is.False);
        }
    }
}
