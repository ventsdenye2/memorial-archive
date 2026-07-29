using MemorialArchive.Gameplay.Monster.Data;

namespace MemorialArchive.Gameplay.Monster.Logic
{
    public static class MonsterDecisionPolicy
    {
        public static MonsterActionState Decide(
            bool canSeePlayer,
            bool playerAlive,
            float distance,
            float detectionRange,
            float attackRange,
            bool attackReady)
        {
            if (!canSeePlayer || !playerAlive || distance > detectionRange)
            {
                return MonsterActionState.Idle;
            }

            if (distance <= attackRange)
            {
                return attackReady ? MonsterActionState.Attacking : MonsterActionState.Idle;
            }

            return MonsterActionState.Chasing;
        }
    }
}
