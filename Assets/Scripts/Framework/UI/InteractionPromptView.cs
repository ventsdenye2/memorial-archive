using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Interaction.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptText;
        [SerializeField] private string interactKey = "F";

        private bool isInitialized;
        private EventBus boundEvents;

        private void Awake()
        {
            if (promptRoot == null)
            {
                Debug.LogWarning("[InteractionPromptView] promptRoot is not assigned in Inspector.", this);
                return;
            }
            promptRoot.SetActive(false);
            isInitialized = true;
        }

        private void OnEnable()
        {
            TryBindEvents();
        }

        private void Update()
        {
            // Player objects can enable before GameRoot has built its context.
            // Retry only until the event bus becomes available.
            if (boundEvents == null)
            {
                TryBindEvents();
            }
        }

        private void OnDisable()
        {
            if (boundEvents != null)
            {
                boundEvents.Unsubscribe<InteractionFocusChangedEvent>(HandleFocusChanged);
                boundEvents = null;
            }
        }

        private void TryBindEvents()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events == null || ReferenceEquals(events, boundEvents))
            {
                return;
            }

            if (boundEvents != null)
            {
                boundEvents.Unsubscribe<InteractionFocusChangedEvent>(HandleFocusChanged);
            }

            events.Subscribe<InteractionFocusChangedEvent>(HandleFocusChanged);
            boundEvents = events;
        }

        private void HandleFocusChanged(InteractionFocusChangedEvent evt)
        {
            if (!isInitialized || promptRoot == null || promptText == null)
            {
                return;
            }

            if (evt.HasFocus)
            {
                var actionName = evt.InteractionType == InteractionType.SceneExit
                    ? "前往"
                    : GetActionName(evt.InteractionType);
                promptText.text = $"[{interactKey}] {actionName}";
                promptRoot.SetActive(true);
            }
            else
            {
                promptRoot.SetActive(false);
            }
        }

        private static string GetActionName(InteractionType type)
        {
            return type switch
            {
                InteractionType.Container => "打开",
                InteractionType.Inspect => "检查",
                InteractionType.Door => "开门",
                InteractionType.Puzzle => "互动",
                InteractionType.SavePoint => "存档",
                InteractionType.NotePickup => "拾取",
                InteractionType.Npc => "对话",
                _ => "互动",
            };
        }
    }
}
