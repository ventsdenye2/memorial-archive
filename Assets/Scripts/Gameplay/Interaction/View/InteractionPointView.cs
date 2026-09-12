using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Interaction.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Interaction.View
{
    public sealed class InteractionPointView : MonoBehaviour
    {
        [SerializeField] private string interactionId;
        [SerializeField] private InteractionType interactionType = InteractionType.Inspect;
        [SerializeField] private string playerTag = "Player";

        private Collider2D triggerCollider;
        private Collider2D focusedPlayerCollider;
        private bool focusPublishPending;
        private int focusPublishDelayFrames;

        public string InteractionId => interactionId;
        public InteractionType InteractionType => interactionType;

        public void Configure(string id, InteractionType type)
        {
            interactionId = id;
            interactionType = type;
        }

        private void OnDisable()
        {
            if (focusedPlayerCollider != null) PublishFocus(false);
            focusedPlayerCollider = null;
            focusPublishPending = false;
        }

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            // 场景初始时玩家可能已经位于触发范围内；此时不一定会再收到
            // OnTriggerEnter2D，因此主动同步一次焦点。
            QueueFocusForOverlappingPlayer();
        }

        private void Update()
        {
            if (!focusPublishPending)
            {
                return;
            }

            // 让 GameRoot、InteractionSystem 与玩家 HUD 都先完成 OnEnable/Start
            // 订阅，避免开场首帧的一次性焦点事件被任何一层遗漏。
            if (focusPublishDelayFrames > 0)
            {
                focusPublishDelayFrames--;
                return;
            }

            if (focusedPlayerCollider == null)
            {
                focusPublishPending = false;
                return;
            }

            if (PublishFocus(true))
            {
                focusPublishPending = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                QueueFocus(other);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == focusedPlayerCollider || other.CompareTag(playerTag))
            {
                focusedPlayerCollider = null;
                focusPublishPending = false;
                PublishFocus(false);
            }
        }

        private void QueueFocus(Collider2D playerCollider)
        {
            focusedPlayerCollider = playerCollider;
            focusPublishPending = true;
            focusPublishDelayFrames = 1;
        }

        private void QueueFocusForOverlappingPlayer()
        {
            if (triggerCollider == null || !triggerCollider.isActiveAndEnabled)
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                return;
            }

            foreach (var playerCollider in player.GetComponentsInChildren<Collider2D>())
            {
                if (playerCollider != null && playerCollider.isActiveAndEnabled && triggerCollider.Distance(playerCollider).isOverlapped)
                {
                    QueueFocus(playerCollider);
                    return;
                }
            }
        }

        private bool PublishFocus(bool hasFocus)
        {
            var root = GameRoot.Instance;
            if (root == null || root.Context == null)
            {
                return false;
            }

            root.Context.Events.Publish(new InteractionFocusChangedEvent(interactionId, interactionType, hasFocus));
            return true;
        }
    }
}
