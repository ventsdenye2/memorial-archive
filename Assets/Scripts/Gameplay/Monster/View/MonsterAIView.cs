using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Combat.View;
using MemorialArchive.Gameplay.Monster.Config;
using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Monster.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.View
{
    [RequireComponent(typeof(Rigidbody2D), typeof(MonsterTargetView))]
    public sealed class MonsterAIView : MonoBehaviour
    {
        private const float DesignUnitsToWorldUnits = 0.01f;
        private const float EnemyKnifeVisualHeight = 1.35f;
        private const float EnemyKnifeVisualForwardOffset = 6f;

        [SerializeField] private Rigidbody2D body;
        [SerializeField] private MonsterTargetView targetView;
        [SerializeField] private MonsterAnimationView animationView;

        private MonsterConfig config;
        private readonly MonsterStateMachine stateMachine = new MonsterStateMachine();
        private Vector2 playerPosition;
        private bool hasPlayerPosition;
        private bool playerAlive = true;
        private float decisionRemaining;
        private float attackCooldownRemaining;
        private float actionLockRemaining;
        private bool attackAwaitingHit;
        private AudioSource movementAudio;
        private string AudioPrefix => config != null && config.AttackMode == MonsterAttackMode.Ranged
            ? "sfx_monster_nurse_" : "sfx_monster_guard_";

        private void PlayAudio(string suffix)
        {
            MemorialArchive.Framework.Audio.AudioSystem.Current?.Playback?.Play(AudioPrefix + suffix, transform);
        }

        public MonsterActionState State => stateMachine.CurrentState;
        public MonsterConfig Config => config;

        private void Awake()
        {
            body = body != null ? body : GetComponent<Rigidbody2D>();
            targetView = targetView != null ? targetView : GetComponent<MonsterTargetView>();
            animationView = animationView != null ? animationView : GetComponent<MonsterAnimationView>();
        }

        private void OnEnable()
        {
            var events = GameRoot.Instance?.Context?.Events;
            events?.Subscribe<PlayerPositionChangedEvent>(HandlePlayerPositionChanged);
            events?.Subscribe<CharacterDiedEvent>(HandlePlayerDied);
            events?.Subscribe<MonsterDamagedEvent>(HandleMonsterDamaged);
            events?.Subscribe<MonsterDiedEvent>(HandleMonsterDied);
            if (animationView != null) animationView.AttackHit += HandleAttackHit;
        }

        private void Start()
        {
            if (targetView != null && !string.IsNullOrEmpty(targetView.TargetId))
            {
                InitializeRuntime(targetView);
            }
        }

        private void OnDisable()
        {
            MemorialArchive.Framework.Audio.AudioSystem.Current?.Playback?.Stop(movementAudio);
            movementAudio = null;
            var events = GameRoot.Instance?.Context?.Events;
            events?.Unsubscribe<PlayerPositionChangedEvent>(HandlePlayerPositionChanged);
            events?.Unsubscribe<CharacterDiedEvent>(HandlePlayerDied);
            events?.Unsubscribe<MonsterDamagedEvent>(HandleMonsterDamaged);
            events?.Unsubscribe<MonsterDiedEvent>(HandleMonsterDied);
            if (animationView != null) animationView.AttackHit -= HandleAttackHit;
            attackAwaitingHit = false;
            CancelInvoke();
        }

        public void InitializeRuntime(MonsterTargetView runtimeTarget)
        {
            targetView = runtimeTarget != null ? runtimeTarget : targetView;
            config = targetView != null
                ? GameRoot.Instance?.Context?.Configs.GetMonster(targetView.MonsterId)
                : null;
            if (config == null)
            {
                Debug.LogError($"MonsterConfig is missing for monster id {targetView?.MonsterId}.", this);
                enabled = false;
                return;
            }

            if (body != null)
            {
                body.simulated = true;
            }

            playerAlive = true;
            playerPosition = Vector2.zero;
            hasPlayerPosition = false;
            decisionRemaining = 0f;
            attackCooldownRemaining = 0f;
            actionLockRemaining = 0f;
            attackAwaitingHit = false;
            stateMachine.Reset(MonsterActionState.Idle);
            SetState(MonsterActionState.Idle, true);
        }

        private void FixedUpdate()
        {
            if (config == null || State == MonsterActionState.Dead)
            {
                return;
            }

            var deltaTime = Time.fixedDeltaTime;
            attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - deltaTime);
            decisionRemaining -= deltaTime;

            if (stateMachine.IsActionLocked)
            {
                actionLockRemaining -= deltaTime;
                if (actionLockRemaining > 0f)
                {
                    UpdateRuntimeSnapshot();
                    return;
                }

                // 攻击命中通常由动画事件报告，但动画是表现层，不能决定
                // 攻击是否生效。若事件缺失或未被当前 Spine 轨道回报，
                // 在攻击状态结束前补交一次命中；CommitAttackHit 自身仍会
                // 校验目标存活和当前距离，因此不会把已经离开的玩家判定为命中。
                if (State == MonsterActionState.Attacking && attackAwaitingHit)
                {
                    CommitAttackHit();
                }

                SetState(MonsterActionState.Idle);
                decisionRemaining = 0f;
            }

            // 冷却刚结束时立即重新评估，不额外等待下一次普通 AI 间隔。
            if (State == MonsterActionState.AttackCooldown && attackCooldownRemaining <= 0f)
            {
                decisionRemaining = 0f;
            }

            if (decisionRemaining <= 0f)
            {
                decisionRemaining = config.DecisionInterval;
                MakeDecision();
            }

            if (stateMachine.CanMove)
            {
                MoveTowardsPlayer(deltaTime);
            }

            UpdateRuntimeSnapshot();
        }

        private void MakeDecision()
        {
            // 横板玩法只比较水平距离。玩家与不同 Spine 素材的根节点高度并不相同，
            // 把 Y 纳入距离会让视觉上同一地面的角色误判距离。
            var distance = hasPlayerPosition
                ? Mathf.Abs(playerPosition.x - body.position.x)
                : float.PositiveInfinity;
            ApplyDecision(distance);
        }

        private void ApplyDecision(float distance)
        {
            var nextState = stateMachine.DecideTarget(
                hasPlayerPosition,
                playerAlive,
                distance,
                config.DetectionRange,
                config.AttackRange,
                attackCooldownRemaining <= 0f);

            if (nextState == MonsterActionState.Attacking)
            {
                PerformAttack();
                return;
            }

            SetState(nextState);
        }

        private void MoveTowardsPlayer(float deltaTime)
        {
            var horizontalOffset = playerPosition.x - body.position.x;
            var distance = Mathf.Abs(horizontalOffset);
            if (distance <= config.AttackRange)
            {
                // 目标已在攻击距离内时不能继续保留 Chasing；马上走一次状态决策。
                MakeDecision();
                return;
            }

            var maximumStep = config.MoveSpeed * DesignUnitsToWorldUnits * deltaTime;
            // 追击只负责按正常速度靠近目标，不把位置强行截到
            // AttackRange 边界。跨入攻击范围后由状态决策立即切换为攻击，
            // 这样“是否攻击”只由实际距离决定，而不是由某个边界落点决定。
            var step = Mathf.Min(maximumStep, distance);
            var nextPosition = body.position + Vector2.right * Mathf.Sign(horizontalOffset) * step;
            body.MovePosition(nextPosition);
            animationView?.SetFacing(horizontalOffset);

            // MovePosition 在本次物理步末才提交，但决策应基于本步结束后的预计位置。
            // 否则怪物会在已经进入攻击范围时仍保留 Chasing，直到下一次 AI tick 才切换。
            var projectedDistance = Mathf.Abs(playerPosition.x - nextPosition.x);
            if (projectedDistance <= config.AttackRange)
            {
                decisionRemaining = 0f;
                ApplyDecision(projectedDistance);
            }
        }

        private void PerformAttack()
        {
            attackFacingDirection = playerPosition.x >= body.position.x ? 1f : -1f;
            animationView?.SetFacing(attackFacingDirection);
            if (config.AttackMode == MonsterAttackMode.Melee) PlayAudio("attack");
            attackAwaitingHit = true;
            SetState(MonsterActionState.Attacking);
            actionLockRemaining = config.AttackInterval;
            attackCooldownRemaining = config.AttackInterval;

            // 没有可用动画组件时仍保持一次安全回退，避免怪物永久无法造成伤害。
            if (animationView == null)
            {
                CommitAttackHit();
            }
        }

        private void HandleAttackHit()
        {
            if (State == MonsterActionState.Attacking)
            {
                CommitAttackHit();
            }
        }

        private float attackFacingDirection = 1f;

        private void CommitAttackHit()
        {
            if (!attackAwaitingHit || config == null || targetView == null) return;
            attackAwaitingHit = false;
            if (!playerAlive || !hasPlayerPosition) return;
            // Lock facing at attack start; crossing behind during wind-up evades both monsters.
            if (!MonsterDecisionPolicy.IsTargetInFront(playerPosition.x - body.position.x, attackFacingDirection)) return;
            if (config.AttackMode == MonsterAttackMode.Melee &&
                Mathf.Abs(playerPosition.x - body.position.x) > config.AttackRange)
            {
                return;
            }
            if (config.AttackMode == MonsterAttackMode.Ranged)
            {
                PlayAudio("attack");
                // The game is a horizontal side-view. Keep the knife effect
                // level and place it just beyond the monster's facing edge;
                // using the full target vector made the effect drift upward or
                // downward with the player's world-space height.
                var horizontalDirection = attackFacingDirection;
                if (Mathf.Abs(horizontalDirection) <= 0.0001f)
                {
                    horizontalDirection = -1f;
                }

                var projectileDirection = horizontalDirection > 0f ? Vector2.right : Vector2.left;
                var projectilePosition = body.position + new Vector2(
                    projectileDirection.x * EnemyKnifeVisualForwardOffset,
                    EnemyKnifeVisualHeight);
                SpineEffectPlayer.TryPlayAt(
                    SpineEffectPlayer.EnemyKnifeProjectileResource,
                    "animation",
                    projectilePosition,
                    // The authored knife faces the opposite local direction,
                    // so apply the requested 180-degree visual correction.
                    projectileDirection.x > 0f ? 180f : 0f,
                    1f,
                    58,
                    maximumLifetimeSeconds: 0.32f,
                    reversePlayback: true,
                    reverseStartTimeSeconds: 0.32f);
            }
            var request = new DamageRequest(
                0,
                targetView.TargetId,
                CombatTargetIds.Player,
                0,
                config.AttackMode == MonsterAttackMode.Ranged ? DamageType.Bullet : DamageType.Physical,
                config.AttackDamage,
                playerPosition);
            GameRoot.Instance?.Context?.Events.Publish(new DamageRequestedEvent(request));
        }

        private void HandlePlayerPositionChanged(PlayerPositionChangedEvent evt)
        {
            playerPosition = evt.Position;
            hasPlayerPosition = true;

            // 玩家在追踪状态下进入攻击距离，或在等待状态下离开攻击距离时，
            // 下一物理步必须立即重算，不能等完整的 decisionInterval。
            if (config != null && !stateMachine.IsActionLocked)
            {
                var distance = Mathf.Abs(playerPosition.x - body.position.x);
                if ((State == MonsterActionState.Chasing && distance <= config.AttackRange) ||
                    (State == MonsterActionState.AttackCooldown && distance > config.AttackRange))
                {
                    decisionRemaining = 0f;
                }
            }
        }

        private void HandlePlayerDied(CharacterDiedEvent evt)
        {
            playerAlive = false;
            attackAwaitingHit = false;
            actionLockRemaining = 0f;
            SetState(MonsterActionState.Idle);
        }

        private void HandleMonsterDamaged(MonsterDamagedEvent evt)
        {
            if (targetView == null || evt.MonsterInstanceId != targetView.TargetId || evt.RemainingHealth <= 0f)
            {
                return;
            }

            if (evt.TriggersHurt)
            {
                PlayAudio("hurt");
                attackAwaitingHit = false;
                actionLockRemaining = config.HurtDuration;
                SetState(MonsterActionState.Hurt);
            }
        }

        private void HandleMonsterDied(MonsterDiedEvent evt)
        {
            if (targetView == null || evt.MonsterInstanceId != targetView.TargetId)
            {
                return;
            }

            SetState(MonsterActionState.Dead);
            PlayAudio("death");
            attackAwaitingHit = false;
            if (body != null)
            {
                body.simulated = false;
            }

            Invoke(nameof(HideAfterDeath), config.DeathPresentationSeconds);
        }

        private void HideAfterDeath()
        {
            gameObject.SetActive(false);
        }

        private void SetState(MonsterActionState nextState, bool force = false)
        {
            if (!stateMachine.TryTransition(nextState, force))
            {
                return;
            }

            var audio = MemorialArchive.Framework.Audio.AudioSystem.Current?.Playback;
            audio?.Stop(movementAudio);
            movementAudio = State == MonsterActionState.Chasing
                ? audio?.Play("sfx_monster_ghost_move", transform) : null;
            if (State != MonsterActionState.Attacking)
            {
                attackAwaitingHit = false;
            }
            animationView?.PlayState(State);
            if (targetView != null && !string.IsNullOrEmpty(targetView.TargetId))
            {
                GameRoot.Instance?.Context?.Events.Publish(new MonsterStateChangedEvent(targetView.TargetId, State));
            }
        }

        private void UpdateRuntimeSnapshot()
        {
            if (targetView != null && body != null)
            {
                GameRoot.Instance?.GetSystem<MonsterSystem>()?.UpdateRuntimeState(targetView.TargetId, body.position, State);
            }
        }
    }
}
