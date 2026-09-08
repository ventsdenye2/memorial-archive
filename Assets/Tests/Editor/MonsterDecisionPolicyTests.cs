using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Monster.Logic;
using NUnit.Framework;

namespace MemorialArchive.Tests.Editor
{
    public sealed class MonsterDecisionPolicyTests
    {
        [Test]
        public void InAttackRange_AndAttackReady_TransitionsToAttacking()
        {
            var state = MonsterDecisionPolicy.Decide(true, true, 1f, 8f, 1.2f, true);

            Assert.That(state, Is.EqualTo(MonsterActionState.Attacking));
        }

        [Test]
        public void InAttackRange_WhileCoolingDown_TransitionsToAttackCooldown()
        {
            var state = MonsterDecisionPolicy.Decide(true, true, 1f, 8f, 1.2f, false);

            Assert.That(state, Is.EqualTo(MonsterActionState.AttackCooldown));
        }

        [Test]
        public void OutsideAttackRange_WithinDetectionRange_TransitionsToChasing()
        {
            var state = MonsterDecisionPolicy.Decide(true, true, 4f, 8f, 1.2f, true);

            Assert.That(state, Is.EqualTo(MonsterActionState.Chasing));
        }

        [Test]
        public void LockedState_DoesNotAcceptTargetReevaluation()
        {
            var stateMachine = new MonsterStateMachine();
            stateMachine.TryTransition(MonsterActionState.Attacking);

            var state = stateMachine.DecideTarget(true, true, 0.5f, 8f, 1.2f, true);

            Assert.That(state, Is.EqualTo(MonsterActionState.Attacking));
            Assert.That(stateMachine.IsActionLocked, Is.True);
        }

        [Test]
        public void DeadState_IsTerminalUntilExplicitReset()
        {
            var stateMachine = new MonsterStateMachine();
            stateMachine.TryTransition(MonsterActionState.Dead);

            Assert.That(stateMachine.TryTransition(MonsterActionState.Chasing), Is.False);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(MonsterActionState.Dead));

            stateMachine.Reset();
            Assert.That(stateMachine.CurrentState, Is.EqualTo(MonsterActionState.Idle));
        }
    }
}
