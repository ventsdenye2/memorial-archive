using MemorialArchive.Gameplay.Combat.View;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class ThrowableTrajectoryTests
    {
        [Test]
        public void Solver_ReachesClampedTargetWithSharedPhysicsEquation()
        {
            var origin = new Vector2(1f, 2f);
            var requested = new Vector2(20f, -1f);
            Assert.That(ThrowableTrajectoryUtility.TrySolve(origin, requested, 6f, 1.35f, 1.7f,
                out var velocity, out var seconds, out var target), Is.True);

            Assert.That(Vector2.Distance(origin, target), Is.EqualTo(6f).Within(0.001f));
            Assert.That(ThrowableTrajectoryUtility.Evaluate(origin, velocity, 1.35f, seconds).x,
                Is.EqualTo(target.x).Within(0.001f));
            Assert.That(ThrowableTrajectoryUtility.Evaluate(origin, velocity, 1.35f, seconds).y,
                Is.EqualTo(target.y).Within(0.001f));
        }
    }
}
