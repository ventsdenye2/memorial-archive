using MemorialArchive.Gameplay.Monster.Data;

namespace MemorialArchive.Gameplay.Monster.Logic
{
    /// <summary>
    /// 怪物状态的纯逻辑边界。MonoBehaviour 只负责把状态变化同步到物理、动画和事件。
    /// </summary>
    public sealed class MonsterStateMachine
    {
        public MonsterActionState CurrentState { get; private set; } = MonsterActionState.Idle;

        public bool IsActionLocked =>
            CurrentState == MonsterActionState.Hurt ||
            CurrentState == MonsterActionState.Attacking;

        public bool CanEvaluateTarget =>
            CurrentState != MonsterActionState.Hurt &&
            CurrentState != MonsterActionState.Attacking &&
            CurrentState != MonsterActionState.Dead;

        public bool CanMove => CurrentState == MonsterActionState.Chasing;

        public void Reset(MonsterActionState initialState = MonsterActionState.Idle)
        {
            CurrentState = initialState;
        }

        public MonsterActionState DecideTarget(
            bool canSeePlayer,
            bool playerAlive,
            float distance,
            float detectionRange,
            float attackRange,
            bool attackReady)
        {
            return CanEvaluateTarget
                ? MonsterDecisionPolicy.Decide(
                    canSeePlayer,
                    playerAlive,
                    distance,
                    detectionRange,
                    attackRange,
                    attackReady)
                : CurrentState;
        }

        public bool TryTransition(MonsterActionState nextState, bool force = false)
        {
            if (!force && CurrentState == MonsterActionState.Dead && nextState != MonsterActionState.Dead)
            {
                return false;
            }

            if (!force && CurrentState == nextState)
            {
                return false;
            }

            CurrentState = nextState;
            return true;
        }
    }
}
