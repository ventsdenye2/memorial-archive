using System;
using Spine;
using MemorialArchive.Gameplay.Monster.Data;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.View
{
    public sealed class MonsterAnimationView : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private string idleAnimation;
        [SerializeField] private string walkAnimation;
        [SerializeField] private string attackAnimation;
        [SerializeField] private string secondAttackAnimation;
        [SerializeField] private string normalSkin;
        [SerializeField] private string damagedSkin;
        [SerializeField] private string hurtAnimation;
        [SerializeField] private string deathAnimation;
        [SerializeField] private bool artFacesRight;
        [SerializeField, Range(0.05f, 0.95f)] private float fallbackAttackHitNormalized = 0.5f;

        private MonsterActionState currentState;
        private bool hasState;
        private bool facingRight;
        private TrackEntry attackTrackEntry;
        private bool attackHitReported;
        private bool hasAuthoredAttackHit;

        public event Action AttackHit;

        public float FacingDirection => facingRight ? 1f : -1f;
        public float CurrentAnimationDuration => skeletonAnimation?.state?.GetCurrent(0)?.Animation.Duration ?? 0f;

        public void UpdateHealth(float health, float maximumHealth)
        {
            if (skeletonAnimation == null || string.IsNullOrEmpty(normalSkin) || string.IsNullOrEmpty(damagedSkin)) return;
            skeletonAnimation.Initialize(false);
            var skeleton = skeletonAnimation.Skeleton;
            if (skeleton == null) return;
            var skin = skeleton.Data.FindSkin(maximumHealth > 0f && health <= maximumHealth * 0.5f ? damagedSkin : normalSkin);
            if (skin == null || skeleton.Skin == skin) return;
            skeleton.SetSkin(skin);
            skeleton.SetSlotsToSetupPose();
            skeletonAnimation.AnimationState.Apply(skeleton);
        }

        private void OnDisable()
        {
            UnbindAttackTrackEntry();
        }

        private void Update()
        {
            if (attackTrackEntry == null || attackHitReported || hasAuthoredAttackHit || currentState != MonsterActionState.Attacking)
            {
                return;
            }

            var duration = Mathf.Max(0.01f, attackTrackEntry.AnimationEnd - attackTrackEntry.AnimationStart);
            var normalized = (attackTrackEntry.AnimationTime - attackTrackEntry.AnimationStart) / duration;
            if (normalized >= fallbackAttackHitNormalized)
            {
                ReportAttackHit();
            }
        }

        public void PlayState(MonsterActionState state)
        {
            if (skeletonAnimation == null || (hasState && currentState == state))
            {
                return;
            }

            currentState = state;
            hasState = true;
            if (state != MonsterActionState.Attacking)
            {
                UnbindAttackTrackEntry();
            }
            switch (state)
            {
                case MonsterActionState.Chasing:
                    Play(walkAnimation, true);
                    break;
                case MonsterActionState.Attacking:
                    var attack = !string.IsNullOrEmpty(secondAttackAnimation) && UnityEngine.Random.Range(0, 2) == 1
                        ? secondAttackAnimation : attackAnimation;
                    BindAttackTrackEntry(Play(attack, false));
                    break;
                case MonsterActionState.Hurt:
                    Play(hurtAnimation, false);
                    break;
                case MonsterActionState.Dead:
                    Play(deathAnimation, false);
                    break;
                case MonsterActionState.AttackCooldown:
                case MonsterActionState.Idle:
                default:
                    Play(idleAnimation, true);
                    break;
            }
        }

        public void SetFacing(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) <= 0.0001f)
            {
                return;
            }

            facingRight = horizontalDirection > 0f;
            ApplyFacing();
        }

        private TrackEntry Play(string animationName, bool loop)
        {
            if (string.IsNullOrEmpty(animationName))
            {
                return null;
            }

            if (skeletonAnimation.state == null)
            {
                skeletonAnimation.Initialize(true);
            }

            var entry = skeletonAnimation.state?.SetAnimation(0, animationName, loop);
            ApplyFacing();
            return entry;
        }

        private void BindAttackTrackEntry(TrackEntry entry)
        {
            UnbindAttackTrackEntry();
            attackTrackEntry = entry;
            attackHitReported = false;
            if (attackTrackEntry != null)
            {
                foreach (var timeline in attackTrackEntry.Animation.Timelines)
                {
                    if (!(timeline is EventTimeline events)) continue;
                    foreach (var evt in events.Events)
                    {
                        if (IsAttackHitEvent(evt.Data.Name)) hasAuthoredAttackHit = true;
                    }
                }
                attackTrackEntry.Event += HandleAttackAnimationEvent;
                return;
            }

            // 配置缺失或动画初始化失败时仍只回报一次命中，让 AI 层执行距离校验。
            ReportAttackHit();
        }

        private void UnbindAttackTrackEntry()
        {
            if (attackTrackEntry != null)
            {
                attackTrackEntry.Event -= HandleAttackAnimationEvent;
            }
            attackTrackEntry = null;
            attackHitReported = false;
            hasAuthoredAttackHit = false;
        }

        private void HandleAttackAnimationEvent(TrackEntry entry, Spine.Event evt)
        {
            if (entry != attackTrackEntry || evt?.Data == null) return;
            if (IsAttackHitEvent(evt.Data.Name)) ReportAttackHit();
        }

        private static bool IsAttackHitEvent(string eventName)
        {
            eventName = eventName ?? string.Empty;
            return eventName.Equals("attack1", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("attack2", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("attack", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("attack_hit", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("melee_hit", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("hit", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("fire", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("shoushudao shot", StringComparison.OrdinalIgnoreCase) ||
                eventName.Equals("shoot", StringComparison.OrdinalIgnoreCase);
        }

        private void ReportAttackHit()
        {
            if (attackHitReported || currentState != MonsterActionState.Attacking) return;
            attackHitReported = true;
            AttackHit?.Invoke();
        }

        private void ApplyFacing()
        {
            if (skeletonAnimation != null && skeletonAnimation.skeleton != null)
            {
                skeletonAnimation.skeleton.ScaleX = facingRight == artFacesRight ? 1f : -1f;
            }
        }
    }
}
