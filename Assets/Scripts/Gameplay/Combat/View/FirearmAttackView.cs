using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Monster.View;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>
    /// Resolves an approved firearm attack at the animation-driven shot frame
    /// as one hitscan query. This View reports only the nearest valid monster;
    /// CombatSystem remains the sole owner of configured damage and target health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirearmAttackView : MonoBehaviour
    {
        private const int PistolItemId = 1007;

        [SerializeField] private Transform muzzleOrigin;
        [SerializeField] private Vector2 muzzleLocalOffset = new Vector2(0f, 1.25f);
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private string muzzleBoneName = "gun_example2";
        [SerializeField, Range(-180f, 180f)] private float visualAngleOffset;
        [SerializeField, Min(0f)] private float bulletVisualForwardOffset = 8f;
        [SerializeField, Min(0f)] private float bulletEffectPlaybackSeconds = 0.22f;
        [SerializeField] private LayerMask targetLayers = ~0;
        // ItemConfig.AttackRange is authoritative. This only keeps a legacy
        // firearm with a missing/zero range usable until its asset is fixed.
        [SerializeField, Min(0.1f)] private float fallbackRange = 30f;
        [SerializeField] private bool nonTargetCollidersBlockShot = true;

        private readonly HashSet<int> reportedAttackIds = new HashSet<int>();
        private Collider2D[] ownerColliders;

        private void Awake()
        {
            ownerColliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void OnEnable()
        {
            GameRoot.Instance?.Context?.Events.Subscribe<FirearmShotFrameEvent>(HandleFirearmShotFrame);
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<FirearmShotFrameEvent>(HandleFirearmShotFrame);
            reportedAttackIds.Clear();
        }

        private void HandleFirearmShotFrame(FirearmShotFrameEvent evt)
        {
            var attack = evt.Attack;
            if (attack == null || attack.AttackKind != CombatAttackKind.Firearm ||
                attack.AttackerId != CombatTargetIds.Player || !reportedAttackIds.Add(attack.AttackInstanceId))
            {
                return;
            }

            var events = GameRoot.Instance?.Context?.Events;
            if (events == null)
            {
                return;
            }

            var origin = GetMuzzlePosition();
            var direction = attack.Direction.sqrMagnitude > 0.0001f
                ? attack.Direction.normalized
                : Vector2.right;
            var shotDirection = direction;
            if (attack.WeaponItemId == PistolItemId && attack.HasTargetWorldPosition)
            {
                var muzzleToTarget = attack.TargetWorldPosition - origin;
                if (IsFinite(muzzleToTarget) && muzzleToTarget.sqrMagnitude > 0.0001f)
                {
                    shotDirection = muzzleToTarget.normalized;
                }
            }

            // dandao's authored projectile axis is the local +X axis. The
            // hitscan still follows the real muzzle-to-target vector below,
            // while the red visual is deliberately kept horizontal so its
            // presentation does not inherit a steep aim angle.
            if (attack.WeaponItemId == PistolItemId)
            {
                var horizontalVisualDirection = ResolveHorizontalVisualDirection(attack, shotDirection);
                var visualAngle = horizontalVisualDirection.x < 0f ? 180f : 0f;
                visualAngle += visualAngleOffset;
                var visualOrigin = origin + horizontalVisualDirection * Mathf.Max(0f, bulletVisualForwardOffset);
                SpineEffectPlayer.TryPlayAt(
                    SpineEffectPlayer.PlayerBulletResource,
                    "animation",
                    visualOrigin,
                    visualAngle,
                    1f,
                    60,
                    // Only reverse the short authored projectile window. The
                    // remaining dandao timeline mostly cycles attachments.
                    maximumLifetimeSeconds: bulletEffectPlaybackSeconds,
                    reversePlayback: true,
                    reverseStartTimeSeconds: bulletEffectPlaybackSeconds);
            }

            var range = attack.Range > 0.0001f ? attack.Range : Mathf.Max(0.1f, fallbackRange);
            // Use the allocating overload deliberately: a pistol shot must not
            // silently miss a target when a scene contains more than a fixed
            // number of colliders along the ray.
            var hits = Physics2D.RaycastAll(origin, shotDirection, range, targetLayers);
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            MonsterTargetView nearestTarget = null;
            var nearestTargetDistance = float.PositiveInfinity;
            var nearestBlockingDistance = float.PositiveInfinity;
            for (var index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];
                var collider = hit.collider;
                if (collider == null || IsOwnerCollider(collider))
                {
                    continue;
                }

                var target = collider.GetComponentInParent<MonsterTargetView>();
                if (target != null && !string.IsNullOrEmpty(target.TargetId))
                {
                    if (hit.distance < nearestTargetDistance)
                    {
                        nearestTarget = target;
                        nearestTargetDistance = hit.distance;
                    }
                }
                // Camera confiners and other gameplay volumes are trigger
                // colliders. They are included by Physics2D queries (the
                // project enables QueriesHitTriggers) but must not stop a
                // projectile. Only solid, non-target geometry blocks shots.
                else if (nonTargetCollidersBlockShot && !collider.isTrigger &&
                         hit.distance < nearestBlockingDistance)
                {
                    nearestBlockingDistance = hit.distance;
                }
            }

            if (nearestTarget == null || nearestTargetDistance > nearestBlockingDistance)
            {
                return;
            }

            var hitPoint = origin + shotDirection * nearestTargetDistance;
            events.Publish(new CombatHitReportedEvent(
                new CombatHitReport(attack.AttackInstanceId, nearestTarget.TargetId, hitPoint)));
        }

        private Vector2 ResolveHorizontalVisualDirection(AttackContext attack, Vector2 shotDirection)
        {
            const float horizontalEpsilon = 0.0001f;
            if (Mathf.Abs(shotDirection.x) > horizontalEpsilon)
            {
                return shotDirection.x >= 0f ? Vector2.right : Vector2.left;
            }

            // A nearly vertical shot has no stable visual side. Prefer the
            // release direction's sign, then the rendered Spine facing, so the
            // effect cannot randomly flip as the pointer crosses x == 0.
            if (attack != null && Mathf.Abs(attack.Direction.x) > horizontalEpsilon)
            {
                return attack.Direction.x >= 0f ? Vector2.right : Vector2.left;
            }

            var skeleton = skeletonAnimation != null ? skeletonAnimation.skeleton : null;
            if (skeleton != null && Mathf.Abs(skeleton.ScaleX) > horizontalEpsilon)
            {
                return skeleton.ScaleX >= 0f ? Vector2.right : Vector2.left;
            }

            var scaleX = transform.lossyScale.x;
            return scaleX < 0f ? Vector2.left : Vector2.right;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private Vector2 GetMuzzlePosition()
        {
            if (muzzleOrigin != null)
            {
                return muzzleOrigin.position;
            }

            var skeleton = skeletonAnimation != null ? skeletonAnimation.skeleton : null;
            var muzzleBone = skeleton?.FindBone(muzzleBoneName);
            if (muzzleBone != null)
            {
                // Spine WorldX/WorldY are in the skeleton's world space.
                // TransformPoint applies the same facing, scale, and parent
                // transform as the rendered gun.
                return skeletonAnimation.transform.TransformPoint(
                    new Vector3(muzzleBone.WorldX, muzzleBone.WorldY, 0f));
            }

            return transform.TransformPoint(muzzleLocalOffset);
        }

        private bool IsOwnerCollider(Collider2D candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            if (ownerColliders != null)
            {
                foreach (var ownerCollider in ownerColliders)
                {
                    if (ownerCollider == candidate)
                    {
                        return true;
                    }
                }
            }

            var current = candidate.transform;
            while (current != null)
            {
                if (current == transform || current.CompareTag("Player"))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
