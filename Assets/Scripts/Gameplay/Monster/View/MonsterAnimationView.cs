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
        [SerializeField] private string hurtAnimation;
        [SerializeField] private string deathAnimation;
        [SerializeField] private bool artFacesRight;

        private MonsterActionState currentState;
        private bool hasState;
        private bool facingRight;

        public void PlayState(MonsterActionState state)
        {
            if (skeletonAnimation == null || (hasState && currentState == state))
            {
                return;
            }

            currentState = state;
            hasState = true;
            switch (state)
            {
                case MonsterActionState.Chasing:
                    Play(walkAnimation, true);
                    break;
                case MonsterActionState.Attacking:
                    Play(attackAnimation, false);
                    break;
                case MonsterActionState.Hurt:
                    Play(hurtAnimation, false);
                    break;
                case MonsterActionState.Dead:
                    Play(deathAnimation, false);
                    break;
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

        private void Play(string animationName, bool loop)
        {
            if (string.IsNullOrEmpty(animationName))
            {
                return;
            }

            if (skeletonAnimation.state == null)
            {
                skeletonAnimation.Initialize(true);
            }

            skeletonAnimation.state?.SetAnimation(0, animationName, loop);
            ApplyFacing();
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
