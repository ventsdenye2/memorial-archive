using MemorialArchive.Gameplay.Monster.Data;

namespace MemorialArchive.Gameplay.Monster.Logic
{
    public static class MonsterDecisionPolicy
    {
        public static bool IsTargetInFront(float horizontalOffset, float attackFacingDirection) =>
            horizontalOffset * attackFacingDirection > 0f;

        public static MonsterActionState Decide(
            bool canSeePlayer,
            bool playerAlive,
            float distance,
            float detectionRange,
            float attackRange,
            bool attackReady)
        {
            // Callers may naturally have a signed horizontal offset rather than
            // a magnitude. Target decisions are side-agnostic, so normalize it
            // at the policy boundary instead of relying on every caller to do so.
            if (distance < 0f)
            {
                distance = -distance;
            }

            if (!canSeePlayer || !playerAlive || distance > detectionRange)
            {
                return MonsterActionState.Idle;
            }

            if (distance <= attackRange)
            {
                return attackReady ? MonsterActionState.Attacking : MonsterActionState.AttackCooldown;
            }

            return MonsterActionState.Chasing;
        }
    }
}
