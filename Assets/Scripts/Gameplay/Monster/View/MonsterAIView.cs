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
        private MonsterActionState state;
        private Vector2 playerPosition;
        private bool hasPlayerPosition;
        private bool playerAlive = true;
        private float decisionRemaining;
        private float attackCooldownRemaining;
        private float actionLockRemaining;
        private bool attackAwaitingHit;

        public MonsterActionState State => state;
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
            decisionRemaining = 0f;
            attackCooldownRemaining = 0f;
            actionLockRemaining = 0f;
            attackAwaitingHit = false;
            SetState(MonsterActionState.Idle, true);
        }

        private void FixedUpdate()
        {
            if (config == null || state == MonsterActionState.Dead)
            {
                return;
            }

            var deltaTime = Time.fixedDeltaTime;
            attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining - deltaTime);
            decisionRemaining -= deltaTime;

            if (state == MonsterActionState.Hurt || state == MonsterActionState.Attacking)
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

            if (decisionRemaining <= 0f)
            {
                decisionRemaining = config.DecisionInterval;
                MakeDecision();
            }

            if (state == MonsterActionState.Chasing)
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
            var nextState = MonsterDecisionPolicy.Decide(
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
                return;
            }

            var maximumStep = config.MoveSpeed * DesignUnitsToWorldUnits * deltaTime;
            var step = Mathf.Min(maximumStep, distance - config.AttackRange);
            body.MovePosition(body.position + Vector2.right * Mathf.Sign(horizontalOffset) * step);
            animationView?.SetFacing(horizontalOffset);
        }

        private void PerformAttack()
        {
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
            if (state == MonsterActionState.Attacking)
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
            if (!force && state == nextState)
            {
                return;
            }

            state = nextState;
            if (state != MonsterActionState.Attacking)
            {
                attackAwaitingHit = false;
            }
            animationView?.PlayState(state);
            if (targetView != null && !string.IsNullOrEmpty(targetView.TargetId))
            {
                GameRoot.Instance?.Context?.Events.Publish(new MonsterStateChangedEvent(targetView.TargetId, state));
            }
        }

        private void UpdateRuntimeSnapshot()
        {
            if (targetView != null && body != null)
            {
                GameRoot.Instance?.GetSystem<MonsterSystem>()?.UpdateRuntimeState(targetView.TargetId, body.position, state);
            }
        }
    }
}
