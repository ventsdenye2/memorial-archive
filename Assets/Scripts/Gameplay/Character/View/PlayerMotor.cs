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
        private Vector2 dodgeVelocity;
        private float dodgeRemaining;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
        }

        private void OnEnable()
        {
            character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            GameRoot.Instance?.Context?.Events.Subscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            BindCamera();
        }

        // 3.5.5：Camera 通过 SetTarget 或运行时查找 Player 绑定。
        // CameraFollowView.Start 也会按 Tag 查找，这里主动 SetTarget 确保不依赖激活顺序。
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
            GameRoot.Instance?.Context?.Events.Unsubscribe<DodgeRequestedEvent>(HandleDodgeRequested);
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

            Vector2 velocity;
            if (dodgeRemaining > 0f)
            {
                velocity = dodgeVelocity;
                dodgeRemaining = Mathf.Max(0f, dodgeRemaining - Time.fixedDeltaTime);
            }
            else
            {
                velocity = character.MoveDirection * character.CurrentMoveSpeed * DesignUnitsToWorldUnits;
            }

            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
            character.Data.position = body.position;
        }

        private void HandleDodgeRequested(DodgeRequestedEvent evt)
        {
            var duration = Mathf.Max(0.01f, evt.DurationSeconds);
            dodgeVelocity = evt.Direction.normalized * evt.DistanceDesignUnits * DesignUnitsToWorldUnits / duration;
            dodgeRemaining = duration;
        }
    }
}
