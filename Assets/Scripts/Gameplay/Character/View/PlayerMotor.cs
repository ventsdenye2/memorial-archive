using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Camera.View;
using MemorialArchive.Gameplay.Character.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.View
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour, ISceneSpawnTarget
    {
        public const float DesignUnitsToWorldUnits = 0.01f;

        [SerializeField] private Rigidbody2D body;
        private CharacterSystem character;
        private bool dodgeAnimationPlaying;
        private bool dodgeMotionActive;
        private Vector2 dodgeMotionStartPosition;
        private Vector2 dodgeMotionDirection;
        private float dodgeMotionDistanceWorld;
        private float dodgeMotionDuration;
        private float dodgeMotionElapsed;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            // 本项目当前是横向 2D 平面玩法：出生时所在的场景下边缘就是唯一可行走线。
            // 输入和 CharacterSystem 已经不提供 Y 方向；这里再锁住物理 Y，避免碰撞或外力把玩家推离该线。
            if (body != null)
            {
                body.constraints |= RigidbodyConstraints2D.FreezePositionY;
            }
        }

        private void OnEnable()
        {
            character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            GameRoot.Instance?.Context?.Events.Subscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            GameRoot.Instance?.Context?.Events.Subscribe<DodgeAnimationStateChangedEvent>(HandleDodgeAnimationStateChanged);
            BindCamera();
        }

        // CameraFollowView 位于表现层，只绑定角色 Transform；跟随与边界限制由相机组件自己处理。
        private void BindCamera()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                return;
            }

            var follow = cam.GetComponent<CameraFollowView>();
            if (follow != null)
            {
                follow.SetTarget(transform);
            }
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<DodgeAnimationStateChangedEvent>(HandleDodgeAnimationStateChanged);
            GameRoot.Instance?.Context?.Events.Unsubscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            dodgeAnimationPlaying = false;
            dodgeMotionActive = false;
        }

        private void FixedUpdate()
        {
            if (character == null)
            {
                character = GameRoot.Instance?.GetSystem<CharacterSystem>();
                if (character == null)
                {
                    return;
                }
            }

            Vector2 nextPosition;
            if (dodgeMotionActive)
            {
                nextPosition = EvaluateDodgeMotion();
            }
            else
            {
                // dodge 播放期间只显示 Spine 原始动画，不叠加普通 A/D 位移。
                var velocity = dodgeAnimationPlaying
                    ? Vector2.zero
                    : character.MoveDirection * character.CurrentMoveSpeed * DesignUnitsToWorldUnits;
                nextPosition = body.position + velocity * Time.fixedDeltaTime;
            }

            body.MovePosition(nextPosition);
            character.Data.position = body.position;
            GameRoot.Instance?.Context?.Events.Publish(new PlayerPositionChangedEvent(body.position));
        }

        public void MoveToSceneSpawn(Vector3 position)
        {
            transform.position = position;
            if (body != null)
            {
                body.position = position;
                body.velocity = Vector2.zero;
            }

            dodgeMotionActive = false;
            dodgeMotionElapsed = 0f;

            if (character == null)
            {
                character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            }

            if (character != null)
            {
                character.Data.position = position;
            }

            GameRoot.Instance?.Context?.Events.Publish(new PlayerPositionChangedEvent(position));
        }

        private void HandleDodgeAnimationStateChanged(DodgeAnimationStateChangedEvent evt)
        {
            dodgeAnimationPlaying = evt.IsPlaying;

            if (!evt.IsPlaying && dodgeMotionActive)
            {
                // 动画比配置的位移时长更短时，仍然保证后撤落点结算完整。
                dodgeMotionElapsed = dodgeMotionDuration;
                var finalPosition = EvaluateDodgeMotion();
                body.position = finalPosition;
            }
        }

        private void HandleDodgeRequested(DodgeRequestedEvent evt)
        {
            if (body == null)
            {
                return;
            }

            var direction = evt.Direction;
            if (Mathf.Abs(direction.x) <= 0.0001f)
            {
                direction = Vector2.left;
            }

            dodgeMotionStartPosition = body.position;
            dodgeMotionDirection = new Vector2(Mathf.Sign(direction.x), 0f);
            dodgeMotionDistanceWorld = Mathf.Max(0f, evt.DistanceDesignUnits) * DesignUnitsToWorldUnits;
            dodgeMotionDuration = Mathf.Max(0.01f, evt.DurationSeconds);
            dodgeMotionElapsed = 0f;
            dodgeMotionActive = true;
            PublishDodgeMotionProgress(0f);
        }

        private Vector2 EvaluateDodgeMotion()
        {
            dodgeMotionElapsed = Mathf.Min(dodgeMotionDuration, dodgeMotionElapsed + Time.fixedDeltaTime);
            var normalizedProgress = Mathf.Clamp01(dodgeMotionElapsed / dodgeMotionDuration);
            var horizontalProgress = Mathf.SmoothStep(0f, 1f, normalizedProgress);
            var position = dodgeMotionStartPosition + dodgeMotionDirection * (dodgeMotionDistanceWorld * horizontalProgress);
            position.y = body.position.y;

            PublishDodgeMotionProgress(normalizedProgress);
            if (normalizedProgress >= 1f)
            {
                dodgeMotionActive = false;
            }

            return position;
        }

        private static void PublishDodgeMotionProgress(float normalizedProgress)
        {
            GameRoot.Instance?.Context?.Events.Publish(new DodgeMotionProgressEvent(normalizedProgress));
        }
    }
}
