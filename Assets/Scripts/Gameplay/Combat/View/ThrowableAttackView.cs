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
        [SerializeField] private string throwOriginBoneName = "Player_01_Weapon_Right";
        [SerializeField, Min(0.1f)] private float trajectoryArcHeight = 1.7f;
        [SerializeField, Min(0.1f)] private float gravityScale = 1.35f;
        [SerializeField, Range(8, 64)] private int trajectorySegments = 28;
        [SerializeField, Min(0.01f)] private float projectileRadius = 0.18f;
        [SerializeField] private LayerMask trajectoryCollisionLayers = ~0;
        [SerializeField, Min(0.005f)] private float trajectoryWidth = 0.035f;
        [SerializeField, Min(0.05f)] private float fallbackReleaseSeconds = 0.45f;
        [SerializeField, Min(0.02f)] private float releaseSpawnDelaySeconds = 0.12f;
        [SerializeField, Min(9f)] private float requestedAttackLifetimeSeconds = 9f;
        [SerializeField, Min(0.05f)] private float aimSweepSeconds = 2f;
        [SerializeField, Min(0.1f)] private float aimSweepStartDistance = 0.6f;

        private Spine.AnimationState boundAnimationState;
        private AttackContext pendingAttack;
        private float pendingSince;
        private bool releaseEventObserved;
        private float spawnAt;
        private bool isPreviewing;
        private int previewItemId;
        private Vector2 previewTarget;
        private float aimSweepElapsed = -1f;
        private LineRenderer trajectoryRenderer;
        private Material trajectoryMaterial;
        private readonly RaycastHit2D[] trajectoryHits = new RaycastHit2D[16];
        private Collider2D[] playerColliders;

        private void Awake()
        {
            if (skeletonAnimation == null)
            {
                skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }
            playerColliders = GetComponentsInChildren<Collider2D>(true);
            CreateTrajectoryRenderer();
        }

        private void OnEnable()
        {
            GameRoot.Instance?.Context?.Events.Subscribe<AttackStartedEvent>(HandleAttackStarted);
            GameRoot.Instance?.Context?.Events.Subscribe<ThrowableAimChangedEvent>(HandleThrowableAimChanged);
            GameRoot.Instance?.Context?.Events.Subscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            BindAnimationState();
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<AttackStartedEvent>(HandleAttackStarted);
            GameRoot.Instance?.Context?.Events.Unsubscribe<ThrowableAimChangedEvent>(HandleThrowableAimChanged);
            GameRoot.Instance?.Context?.Events.Unsubscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            UnbindAnimationState();
            pendingAttack = null;
            HideTrajectory();
        }

        private void Update()
        {
            BindAnimationState();
            var character = GameRoot.Instance?.GetSystem<MemorialArchive.Gameplay.Character.Logic.CharacterSystem>();
            if (pendingAttack == null)
            {
                return;
            }

            // The "throw" spine event fires while the arm is still winding down
            // toward the hip. Spawn slightly later so the projectile leaves the
            // raised hand instead of falling out of the crotch.
            if (releaseEventObserved)
            {
                if (Time.time >= spawnAt)
                {
                    SpawnPendingThrowable();
                }
                return;
            }

            // Covers skeletons without the "throw" event; the delay lands in the
            // same arm-raised window of the throw animation.
            if (character?.ActionState == MemorialArchive.Gameplay.Character.Data.CharacterActionState.Throwing && Time.time - pendingSince >= fallbackReleaseSeconds)
            {
                SpawnPendingThrowable();
            }
        }

        private void LateUpdate()
        {
            if (isPreviewing) DrawTrajectory(previewItemId, previewTarget);
        }

        private void HandleThrowableAimChanged(ThrowableAimChangedEvent evt)
        {
            var wasAiming = isPreviewing;
            isPreviewing = evt.IsAiming;
            previewItemId = evt.ItemId;
            previewTarget = evt.TargetWorldPosition;
            if (isPreviewing && !wasAiming)
            {
                // 每次重新开始瞄准时，轨迹从角色近处向鼠标位置扫过去。
                aimSweepElapsed = 0f;
            }

            if (!isPreviewing) HideTrajectory();
        }

        private void HandleCharacterStateChanged(CharacterActionStateChangedEvent evt)
        {
            if (pendingAttack != null && evt.State != MemorialArchive.Gameplay.Character.Data.CharacterActionState.Throwing)
                pendingAttack = null;
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
            spawnAt = Time.time + releaseSpawnDelaySeconds;
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
            var origin = GetThrowOrigin(direction);
            var requestedTarget = attack.HasTargetWorldPosition ? attack.TargetWorldPosition : origin + direction * attack.Range;
            if (!ThrowableTrajectoryUtility.TrySolve(origin, requestedTarget, attack.Range, gravityScale, trajectoryArcHeight,
                    out var launchVelocity, out _, out _))
                launchVelocity = direction * 6f + Vector2.up * 2.4f;
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
            body.gravityScale = gravityScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.velocity = launchVelocity;
            // ThrowableTrajectoryUtility solves a drag-free arc, so the body must
            // match it or the real flight falls short of the previewed landing.
            body.drag = 0f;
            body.angularVelocity = direction.x >= 0f ? -420f : 420f;

            var projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.radius = projectileRadius;
            var bounceMaterial = new PhysicsMaterial2D($"ThrowableMaterial_{attack.AttackInstanceId}")
            {
                // 手雷保留轻微弹跳，但显著降低连续弹跳和落地滑行距离。
                bounciness = attack.WeaponItemId == GrenadeItemId ? 0.15f : 0.03f,
                friction = attack.WeaponItemId == GrenadeItemId ? 0.68f : 0.55f
            };
            projectileCollider.sharedMaterial = bounceMaterial;

            var projectile = projectileObject.AddComponent<ThrowableProjectileView>();
            projectile.Initialize(
                attack,
                body,
                projectileCollider,
                playerColliders,
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

        private Vector2 GetThrowOrigin(Vector2 fallbackDirection)
        {
            var skeleton = skeletonAnimation != null ? skeletonAnimation.skeleton : null;
            var bone = skeleton?.FindBone(throwOriginBoneName);
            if (bone != null)
                return skeletonAnimation.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            if (throwOrigin != null && throwOrigin != transform) return throwOrigin.position;
            // Chest height so a missing bone never drops the throwable at the waist.
            return (Vector2)transform.position + fallbackDirection * 0.4f + Vector2.up * 1.6f;
        }

        private void CreateTrajectoryRenderer()
        {
            trajectoryRenderer = GetComponent<LineRenderer>();
            if (trajectoryRenderer == null) trajectoryRenderer = gameObject.AddComponent<LineRenderer>();
            trajectoryMaterial = new Material(Shader.Find("Sprites/Default"));
            trajectoryRenderer.material = trajectoryMaterial;
            trajectoryRenderer.useWorldSpace = true;
            trajectoryRenderer.startWidth = trajectoryWidth;
            trajectoryRenderer.endWidth = trajectoryWidth;
            trajectoryRenderer.startColor = new Color(1f, 0.85f, 0.25f, 0.92f);
            trajectoryRenderer.endColor = new Color(1f, 0.35f, 0.12f, 0.92f);
            trajectoryRenderer.sortingOrder = 60;
            trajectoryRenderer.enabled = false;
        }

        private void DrawTrajectory(int itemId, Vector2 requestedTarget)
        {
            var config = GameRoot.Instance?.Context?.Configs.GetItem(itemId);
            var range = config != null ? config.AttackRange : 6f;
            var direction = requestedTarget.x >= transform.position.x ? Vector2.right : Vector2.left;
            var origin = GetThrowOrigin(direction);
            requestedTarget = ApplyAimSweep(origin, requestedTarget);
            if (!ThrowableTrajectoryUtility.TrySolve(origin, requestedTarget, range, gravityScale, trajectoryArcHeight,
                    out var velocity, out var flightSeconds, out _))
            {
                HideTrajectory();
                return;
            }

            trajectoryRenderer.enabled = true;
            trajectoryRenderer.positionCount = trajectorySegments + 1;
            trajectoryRenderer.SetPosition(0, origin);
            var previous = origin;
            var written = 1;
            for (var index = 1; index <= trajectorySegments; index++)
            {
                var seconds = flightSeconds * index / trajectorySegments;
                var next = ThrowableTrajectoryUtility.Evaluate(origin, velocity, gravityScale, seconds);
                if (TryFindFirstCollision(previous, next, out var impact))
                {
                    trajectoryRenderer.SetPosition(written++, impact);
                    break;
                }
                trajectoryRenderer.SetPosition(written++, next);
                previous = next;
            }
            trajectoryRenderer.positionCount = written;
        }

        /// <summary>
        /// 瞄准开始后，轨迹落点先从角色近处向鼠标位置扫动，扫完才自由跟随鼠标。
        /// 仅影响预览表现；实际投掷目标仍是松手时的真实鼠标位置。
        /// </summary>
        private Vector2 ApplyAimSweep(Vector2 origin, Vector2 requestedTarget)
        {
            if (aimSweepElapsed < 0f)
            {
                return requestedTarget;
            }

            aimSweepElapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(aimSweepElapsed / aimSweepSeconds);
            if (progress >= 1f)
            {
                aimSweepElapsed = -1f;
                return requestedTarget;
            }

            var toTarget = requestedTarget - origin;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return requestedTarget;
            }

            var nearPoint = origin + toTarget.normalized * aimSweepStartDistance;
            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            return Vector2.Lerp(nearPoint, requestedTarget, eased);
        }

        private bool TryFindFirstCollision(Vector2 from, Vector2 to, out Vector2 impact)
        {
            var delta = to - from;
            var count = Physics2D.CircleCastNonAlloc(from, projectileRadius, delta.normalized, trajectoryHits, delta.magnitude, trajectoryCollisionLayers);
            var bestDistance = float.MaxValue;
            impact = to;
            for (var index = 0; index < count; index++)
            {
                var hit = trajectoryHits[index];
                if (hit.collider == null || hit.collider.isTrigger || IsPlayerCollider(hit.collider, playerColliders)) continue;
                if (hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                impact = hit.centroid;
            }
            return bestDistance < float.MaxValue;
        }

        private static bool IsPlayerCollider(Collider2D candidate, Collider2D[] playerColliders)
        {
            for (var index = 0; index < playerColliders.Length; index++)
                if (playerColliders[index] == candidate) return true;
            return false;
        }

        private void HideTrajectory()
        {
            isPreviewing = false;
            aimSweepElapsed = -1f;
            if (trajectoryRenderer != null) trajectoryRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (trajectoryMaterial != null) Destroy(trajectoryMaterial);
        }
    }
}
