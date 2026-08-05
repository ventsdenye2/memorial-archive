using System;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>
    /// Spawns approved throwable attacks at the Spine release frame. Projectile
    /// movement and collision stay in View components; damage remains in CombatSystem.
    /// </summary>
    public sealed class ThrowableAttackView : MonoBehaviour
    {
        private const int GrenadeItemId = 1010;
        private const int MolotovItemId = 1011;

        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private Transform throwOrigin;
        [SerializeField, Min(0.1f)] private float throwSpeed = 6f;
        [SerializeField, Min(0f)] private float upwardBoost = 2.4f;
        [SerializeField, Min(0.05f)] private float fallbackReleaseSeconds = 0.3f;
        [SerializeField, Min(9f)] private float requestedAttackLifetimeSeconds = 9f;

        private Spine.AnimationState boundAnimationState;
        private AttackContext pendingAttack;
        private float pendingSince;
        private bool releaseEventObserved;

        private void Awake()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }
        }

        private void OnEnable()
        {
            GameRoot.Instance?.Context?.Events.Subscribe<AttackStartedEvent>(HandleAttackStarted);
            BindAnimationState();
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<AttackStartedEvent>(HandleAttackStarted);
            UnbindAnimationState();
            pendingAttack = null;
        }

        private void Update()
        {
            BindAnimationState();
            if (pendingAttack != null && !releaseEventObserved && Time.time - pendingSince >= fallbackReleaseSeconds)
            {
                SpawnPendingThrowable();
            }
        }

        private void HandleAttackStarted(AttackStartedEvent evt)
        {
            var attack = evt.Attack;
            if (attack == null || attack.AttackKind != CombatAttackKind.Throwable || !IsSupportedThrowable(attack.WeaponItemId))
            {
                return;
            }

            // Only one release animation can be presented at a time. Keeping
            // the first request prevents rapid clicks from duplicating one item.
            if (pendingAttack != null)
            {
                return;
            }

            pendingAttack = attack;
            pendingSince = Time.time;
            releaseEventObserved = false;
            GameRoot.Instance?.Context?.Events.Publish(new CombatAttackLifetimeRequestedEvent(
                attack.AttackInstanceId, requestedAttackLifetimeSeconds));
        }

        private void HandleSpineEvent(TrackEntry trackEntry, Spine.Event evt)
        {
            if (pendingAttack == null || evt == null || evt.Data == null ||
                !evt.Data.Name.Equals("throw", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            releaseEventObserved = true;
            SpawnPendingThrowable();
        }

        private void SpawnPendingThrowable()
        {
            var attack = pendingAttack;
            pendingAttack = null;
            if (attack == null)
            {
                return;
            }

            var direction = attack.Direction.sqrMagnitude > 0.0001f ? attack.Direction.normalized : Vector2.right;
            var origin = throwOrigin != null
                ? (Vector2)throwOrigin.position
                : (Vector2)transform.position + direction * 0.35f + Vector2.up * 0.35f;
            var projectileObject = new GameObject($"Throwable_{attack.WeaponItemId}_{attack.AttackInstanceId}");
            projectileObject.transform.position = origin;

            var config = GameRoot.Instance?.Context?.Configs.GetItem(attack.WeaponItemId);
            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = config != null ? config.Icon : null;
            renderer.color = attack.WeaponItemId == MolotovItemId
                ? new Color(1f, 0.42f, 0.12f, 1f)
                : Color.white;
            renderer.sortingOrder = 20;
            projectileObject.transform.localScale = Vector3.one * 0.65f;

            var body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 1.35f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.velocity = direction * throwSpeed + Vector2.up * upwardBoost;
            body.angularVelocity = direction.x >= 0f ? -420f : 420f;

            var projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.18f;
            var bounceMaterial = new PhysicsMaterial2D($"ThrowableMaterial_{attack.AttackInstanceId}")
            {
                bounciness = attack.WeaponItemId == GrenadeItemId ? 0.35f : 0.05f,
                friction = 0.45f
            };
            projectileCollider.sharedMaterial = bounceMaterial;

            var projectile = projectileObject.AddComponent<ThrowableProjectileView>();
            projectile.Initialize(
                attack,
                body,
                projectileCollider,
                GetComponentsInChildren<Collider2D>(true),
                bounceMaterial);

            GameRoot.Instance?.Context?.Events.Publish(new InventoryItemConsumeRequestedEvent(attack.WeaponInstanceId));
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

        private static bool IsSupportedThrowable(int itemId)
        {
            return itemId == GrenadeItemId || itemId == MolotovItemId;
        }
    }
}
