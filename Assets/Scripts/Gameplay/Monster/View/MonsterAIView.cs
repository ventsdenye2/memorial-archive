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
            var step = Mathf.Min(maximumStep, distance - config.AttackRange);
            var nextPosition = body.position + Vector2.right * Mathf.Sign(horizontalOffset) * step;
            body.MovePosition(nextPosition);
            animationView?.SetFacing(horizontalOffset);

            // MovePosition 在本次物理步末才提交。先离开 Chasing，下一物理步再用已提交的位置
            // 决定是攻击还是继续等待冷却，避免到达边界后出现一帧以上的追踪残留。
            if (Mathf.Abs(playerPosition.x - nextPosition.x) <= config.AttackRange)
            {
                decisionRemaining = 0f;
                SetState(MonsterActionState.AttackCooldown);
            }
        }

        private void PerformAttack()
        {
            if (config.AttackMode == MonsterAttackMode.Melee) PlayAudio("attack");
            attackAwaitingHit = true;
            SetState(MonsterActionState.Attacking);
            actionLockRemaining = config.AttackInterval;
            attackCooldownRemaining = config.AttackInterval;
            animationView?.SetFacing(playerPosition.x - body.position.x);

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

        private void CommitAttackHit()
        {
            if (!attackAwaitingHit || config == null || targetView == null) return;
            attackAwaitingHit = false;
            if (!playerAlive || !hasPlayerPosition) return;
            if (config.AttackMode == MonsterAttackMode.Melee &&
                Mathf.Abs(playerPosition.x - body.position.x) > config.AttackRange)
            {
                return;
            }
            if (config.AttackMode == MonsterAttackMode.Ranged)
            {
                PlayAudio("attack");
                var projectileDirection = playerPosition - body.position;
                if (projectileDirection.sqrMagnitude <= 0.0001f)
                {
                    projectileDirection = Vector2.left;
                }
                projectileDirection.Normalize();
                SpineEffectPlayer.TryPlayAt(
                    SpineEffectPlayer.EnemyKnifeProjectileResource,
                    "animation",
                    body.position + Vector2.up * 1.35f,
                    Mathf.Atan2(projectileDirection.y, projectileDirection.x) * Mathf.Rad2Deg,
                    1f,
                    58,
                    0.32f);
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
