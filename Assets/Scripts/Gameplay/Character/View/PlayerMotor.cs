using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Camera.View;
using MemorialArchive.Gameplay.Character.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.View
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
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
            BindCamera();
        }

        // 3.5.5：Camera 通过 SetTarget 或运行时查找 Player 绑定。
        // Cinemachine 接管后，这里同时把 vcam.Follow 指向 Player；CameraFollowView 仍保留为空壳（Validator 要求）。
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

            var brain = cam.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain != null)
            {
                var vcam = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
                if (vcam != null)
                {
                    vcam.Follow = transform;
                }
            }
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<DodgeAnimationStateChangedEvent>(HandleDodgeAnimationStateChanged);
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

            // dodge 已经在 Spine 动画中烘焙了整体后移。播放期间不再叠加
            // Rigidbody 闪避位移或普通 A/D 位移，避免双重位移和方向冲突。
            var velocity = dodgeAnimationPlaying
                ? Vector2.zero
                : character.MoveDirection * character.CurrentMoveSpeed * DesignUnitsToWorldUnits;

            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
            character.Data.position = body.position;
        }

        private void HandleDodgeAnimationStateChanged(DodgeAnimationStateChangedEvent evt)
        {
            dodgeAnimationPlaying = evt.IsPlaying;
        }
    }
}
