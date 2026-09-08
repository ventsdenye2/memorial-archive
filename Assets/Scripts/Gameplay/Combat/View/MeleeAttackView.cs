using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Monster.View;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>
    /// Presentation-side melee hit detection. It consumes approved attacks,
    /// listens for Spine hit-frame events, and only reports confirmed targets.
    /// Damage, stamina, combo state, and target health stay in their systems.
    /// </summary>
    public sealed class MeleeAttackView : MonoBehaviour
    {
        private const int CombatDaggerItemId = 1001;
        private const int BayonetItemId = 1002;
        private const int FireAxeItemId = 1003;
        private const int OfficerSwordItemId = 1004;

        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private Transform hitboxOrigin;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField, Min(0.1f)] private float hitboxHeight = 0.9f;
        [SerializeField, Range(0f, 1f)] private float fallbackStartNormalized = 0.5f;
        [SerializeField, Min(0.02f)] private float fallbackWindowSeconds = 0.12f;

        private readonly HashSet<string> reportedTargetIds = new HashSet<string>();
        private Spine.AnimationState boundAnimationState;
        private AttackContext currentAttack;
        private float attackStartedAt;
        private float activeWindowEndsAt;
        private bool activeWindow;
        private bool frameEventObserved;
        private bool fallbackTriggered;

        private void Awake()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            if (hitboxOrigin == null)
            {
                hitboxOrigin = transform;
            }
        }

        private void OnEnable()
        {
            GameRoot.Instance?.Context?.Events.Subscribe<AttackStartedEvent>(HandleAttackStarted);
            GameRoot.Instance?.Context?.Events.Subscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            BindAnimationState();
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<AttackStartedEvent>(HandleAttackStarted);
            GameRoot.Instance?.Context?.Events.Unsubscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            UnbindAnimationState();
            ClearAttack();
        }

        private void Update()
        {
            BindAnimationState();
            if (currentAttack == null)
            {
                return;
            }

            var now = Time.time;
            var elapsed = now - attackStartedAt;
            if (activeWindow)
            {
                ScanCurrentHitbox();
                if (now >= activeWindowEndsAt)
                {
                    activeWindow = false;
                }
            }

            if (!frameEventObserved && !fallbackTriggered &&
                elapsed >= currentAttack.ActiveSeconds * fallbackStartNormalized)
            {
                fallbackTriggered = true;
                OpenHitWindow(fallbackWindowSeconds);
            }

            if (elapsed > currentAttack.ActiveSeconds + 0.45f)
            {
                ClearAttack();
            }
        }

        private void HandleAttackStarted(AttackStartedEvent evt)
        {
            var attack = evt.Attack;
            if (attack == null || attack.AttackKind != CombatAttackKind.Melee || !IsSupportedWeapon(attack.WeaponItemId))
            {
                return;
            }

            currentAttack = attack;
            attackStartedAt = Time.time;
            activeWindow = false;
            frameEventObserved = false;
            fallbackTriggered = false;
            reportedTargetIds.Clear();
        }

        private void HandleCharacterStateChanged(CharacterActionStateChangedEvent evt)
        {
            if (!IsAttackState(evt.State))
            {
                ClearAttack();
            }
        }

        private static bool IsAttackState(CharacterActionState state)
        {
            return state == CharacterActionState.Attack1 ||
                   state == CharacterActionState.Attack2 ||
                   state == CharacterActionState.Attack3;
        }

        private void HandleSpineEvent(TrackEntry trackEntry, Spine.Event evt)
        {
            if (currentAttack == null || evt == null || evt.Data == null)
            {
                return;
            }

            var eventName = evt.Data.Name ?? string.Empty;
            if (eventName.Equals("melee_hit_start", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("hitbox_on", StringComparison.OrdinalIgnoreCase))
            {
                frameEventObserved = true;
                OpenHitWindow(Mathf.Max(fallbackWindowSeconds, currentAttack.ActiveSeconds));
                return;
            }

            if (eventName.Equals("melee_hit_end", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("hitbox_off", StringComparison.OrdinalIgnoreCase))
            {
                frameEventObserved = true;
                activeWindow = false;
                return;
            }

            if (eventName.Equals("melee_hit", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("attack", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("attack2", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("attack3", StringComparison.OrdinalIgnoreCase))
            {
                frameEventObserved = true;
                ScanCurrentHitbox();
            }
        }

        private void OpenHitWindow(float duration)
        {
            activeWindow = true;
            activeWindowEndsAt = Time.time + Mathf.Max(0.02f, duration);
            ScanCurrentHitbox();
        }

        private void ScanCurrentHitbox()
        {
            if (currentAttack == null)
            {
                return;
            }

            var origin = hitboxOrigin != null ? (Vector2)hitboxOrigin.position : currentAttack.Origin;
            var direction = currentAttack.Direction.sqrMagnitude > 0.0001f
                ? currentAttack.Direction.normalized
                : Vector2.right;
            var range = Mathf.Max(0.1f, currentAttack.Range);
            var center = origin + direction * (range * 0.5f);
            var size = new Vector2(range, hitboxHeight);
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var colliders = Physics2D.OverlapBoxAll(center, size, angle, targetLayers);
            foreach (var targetCollider in colliders)
            {
                if (targetCollider == null)
                {
                    continue;
                }

                var target = targetCollider.GetComponentInParent<MonsterTargetView>();
                if (target == null || string.IsNullOrEmpty(target.TargetId) || !reportedTargetIds.Add(target.TargetId))
                {
                    continue;
                }

                var hitPoint = targetCollider.ClosestPoint(center);
                GameRoot.Instance?.Context?.Events.Publish(new CombatHitReportedEvent(
                    new CombatHitReport(currentAttack.AttackInstanceId, target.TargetId, hitPoint)));
            }
        }

        private void BindAnimationState()
        {
            var currentState = skeletonAnimation != null ? skeletonAnimation.state : null;
            if (ReferenceEquals(boundAnimationState, currentState))
            {
                return;
            }

            UnbindAnimationState();
            boundAnimationState = currentState;
            if (boundAnimationState != null)
            {
                boundAnimationState.Event += HandleSpineEvent;
            }
        }

        private void UnbindAnimationState()
        {
            if (boundAnimationState != null)
            {
                boundAnimationState.Event -= HandleSpineEvent;
                boundAnimationState = null;
            }
        }

        private void ClearAttack()
        {
            currentAttack = null;
            activeWindow = false;
            frameEventObserved = false;
            fallbackTriggered = false;
            reportedTargetIds.Clear();
        }

        private static bool IsSupportedWeapon(int itemId)
        {
            return itemId == CombatDaggerItemId || itemId == BayonetItemId ||
                   itemId == FireAxeItemId || itemId == OfficerSwordItemId;
        }

        private void OnDrawGizmosSelected()
        {
            var originTransform = hitboxOrigin != null ? hitboxOrigin : transform;
            var direction = currentAttack != null ? currentAttack.Direction : Vector2.right;
            var range = currentAttack != null ? Mathf.Max(0.1f, currentAttack.Range) : 1f;
            var center = (Vector2)originTransform.position + direction.normalized * (range * 0.5f);
            Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.35f);
            Gizmos.DrawCube(center, new Vector3(range, hitboxHeight, 0.05f));
        }
    }
}
