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
            GameRoot.Instance?.Context?.Events.Subscribe<DodgeAnimationStateChangedEvent>(HandleDodgeAnimationStateChanged);
            GameRoot.Instance?.Context?.Events.Subscribe<DodgePositionDeltaEvent>(HandleDodgePositionDelta);
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
            GameRoot.Instance?.Context?.Events.Unsubscribe<DodgePositionDeltaEvent>(HandleDodgePositionDelta);
            dodgeAnimationPlaying = false;
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

            // dodge 播放期间只显示 Spine 原始动画，不叠加普通 A/D 位移。
            var velocity = dodgeAnimationPlaying
                ? Vector2.zero
                : character.MoveDirection * character.CurrentMoveSpeed * DesignUnitsToWorldUnits;

            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
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
        }

        private void HandleDodgePositionDelta(DodgePositionDeltaEvent evt)
        {
            // 动画结束时一次性改变真实坐标。事件是同步发布的，因此在动画
            // 切回 Idle 前 Player 根节点就已到达最终位置，不会横向弹回。
            if (body == null)
            {
                return;
            }

            body.position += evt.WorldDelta;
            if (character != null)
            {
                character.Data.position = body.position;
                GameRoot.Instance?.Context?.Events.Publish(new PlayerPositionChangedEvent(body.position));
            }
        }
    }
}
