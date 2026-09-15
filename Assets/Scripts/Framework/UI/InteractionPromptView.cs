using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.View;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptText;
        [Header("交互按钮")]
        [SerializeField] private Sprite viewSprite;
        [SerializeField] private Sprite viewHighlightedSprite;
        [SerializeField] private Sprite pickupSprite;
        [SerializeField] private Sprite pickupHighlightedSprite;
        [SerializeField] private Sprite saveSprite;
        [SerializeField] private Sprite saveHighlightedSprite;
        [SerializeField] private Sprite stairSprite;
        [SerializeField] private Sprite stairHighlightedSprite;
        [SerializeField] private Sprite enterLeftSprite;
        [SerializeField] private Sprite enterLeftHighlightedSprite;
        [SerializeField] private Sprite enterRightSprite;
        [SerializeField] private Sprite enterRightHighlightedSprite;

        private bool isInitialized;
        private EventBus boundEvents;
        private bool hasInteractionFocus;
        private string activeInteractionId;
        private InteractionType activeInteractionType;
        private bool guideObstacleFocused;
        private bool accessDenied;
        private Image promptImage;
        private Button promptButton;
        private Sprite promptBackgroundSprite;

        private void Awake()
        {
            if (promptRoot == null)
            {
                Debug.LogWarning("[InteractionPromptView] promptRoot is not assigned in Inspector.", this);
                return;
            }

            ResolveButton();
            promptRoot.SetActive(false);
            isInitialized = true;
        }

        private void OnEnable()
        {
            TryBindEvents();
        }

        private void Update()
        {
            if (isInitialized && promptRoot != null)
            {
                var obstacleFocused = GameRoot.Instance?.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem>()?.CanInteractObstacle == true;
                if (obstacleFocused != guideObstacleFocused)
                {
                    guideObstacleFocused = obstacleFocused;
                    accessDenied = false;
                    ApplyArtwork(guideObstacleFocused ? "guide_obstacle" : activeInteractionId, activeInteractionType);
                }
                SetPromptVisible((hasInteractionFocus || guideObstacleFocused) && CanUsePrompt());
            }
            // Player objects can enable before GameRoot has built its context.
            // Retry only until the event bus becomes available.
            if (boundEvents == null)
            {
                TryBindEvents();
            }
        }

        private void OnDisable()
        {
            hasInteractionFocus = false;
            guideObstacleFocused = false;
            activeInteractionId = null;
            activeInteractionType = InteractionType.None;
            accessDenied = false;
            SetPromptVisible(false);
            if (boundEvents != null)
            {
                boundEvents.Unsubscribe<ActiveInteractionChangedEvent>(HandleActiveInteractionChanged);
                boundEvents.Unsubscribe<SceneAccessDeniedEvent>(HandleAccessDenied);
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
                boundEvents.Unsubscribe<ActiveInteractionChangedEvent>(HandleActiveInteractionChanged);
                boundEvents.Unsubscribe<SceneAccessDeniedEvent>(HandleAccessDenied);
            }

            events.Subscribe<ActiveInteractionChangedEvent>(HandleActiveInteractionChanged);
            events.Subscribe<SceneAccessDeniedEvent>(HandleAccessDenied);
            boundEvents = events;
        }

        private void HandleAccessDenied(SceneAccessDeniedEvent evt)
        {
            if (!isInitialized || promptRoot == null || promptText == null) return;
            if (!CanUsePrompt())
            {
                SetPromptVisible(false);
                return;
            }
            accessDenied = true;
            if (promptImage != null)
            {
                promptImage.sprite = promptBackgroundSprite;
                promptImage.color = new Color(0f, 0f, 0f, 0.6f);
                promptImage.enabled = true;
            }
            if (promptButton != null)
            {
                promptButton.interactable = false;
            }
            promptText.text = evt.Message;
            promptText.enabled = true;
            promptRoot.SetActive(true);
        }

        private void HandleActiveInteractionChanged(ActiveInteractionChangedEvent evt)
        {
            // This is the system's resolved focus snapshot, not an individual
            // trigger exit. No focus is published with a null interaction ID.
            hasInteractionFocus = evt.HasFocus;
            activeInteractionId = evt.InteractionId;
            activeInteractionType = evt.InteractionType;
            accessDenied = false;
            if (!isInitialized || promptRoot == null)
            {
                return;
            }

            if (evt.HasFocus || guideObstacleFocused)
            {
                ApplyArtwork(guideObstacleFocused ? "guide_obstacle" : evt.InteractionId, evt.InteractionType);
                SetPromptVisible(CanUsePrompt());
            }
            else
            {
                SetPromptVisible(false);
            }
        }

        private void ResolveButton()
        {
            promptImage = promptRoot.transform.Find("BG")?.GetComponent<Image>() ?? promptRoot.GetComponent<Image>();
            if (promptImage != null)
            {
                promptBackgroundSprite = promptImage.sprite;
                promptImage.preserveAspect = true;
                promptImage.raycastTarget = true;
                promptButton = promptImage.GetComponent<Button>();
            }

            if (promptButton == null)
            {
                promptButton = promptRoot.GetComponent<Button>();
            }

            if (promptButton != null)
            {
                promptButton.targetGraphic = promptImage;
                promptButton.onClick.AddListener(HandlePromptClicked);
            }

            if (promptText != null)
            {
                promptText.enabled = false;
            }
        }

        private void HandlePromptClicked()
        {
            if ((!hasInteractionFocus && !guideObstacleFocused) || accessDenied || !CanUsePrompt())
            {
                return;
            }

            var root = GameRoot.Instance;
            if (root?.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem>()?.TryInteract() != true)
                root?.Context?.Events.Publish(new InteractPressedEvent());
        }

        private void ApplyArtwork(string interactionId, InteractionType type)
        {
            var artwork = GetArtwork(interactionId, type);
            if (promptImage != null)
            {
                // Keep the world-space prompt scale uniform. The new interaction art is
                // taller than the previous square assets; a non-uniform parent scale
                // would visibly squash it.
                var rootRect = promptRoot != null ? promptRoot.GetComponent<RectTransform>() : null;
                if (rootRect != null)
                {
                    var scale = rootRect.localScale;
                    var uniform = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    if (uniform > 0f) rootRect.localScale = new Vector3(uniform, uniform, scale.z);
                    promptImage.preserveAspect = true;
                }
                promptImage.sprite = artwork.normal;
                promptImage.color = Color.white;
                promptImage.enabled = artwork.normal != null;
            }

            if (promptButton != null)
            {
                var state = promptButton.spriteState;
                state.highlightedSprite = artwork.highlighted ?? artwork.normal;
                state.pressedSprite = artwork.highlighted ?? artwork.normal;
                state.selectedSprite = artwork.highlighted ?? artwork.normal;
                state.disabledSprite = null;
                promptButton.spriteState = state;
                promptButton.interactable = artwork.normal != null;
            }

            if (promptText != null)
            {
                promptText.enabled = false;
            }
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptRoot == null)
            {
                return;
            }

            promptRoot.SetActive(visible);
            if (promptButton != null)
            {
                promptButton.interactable = visible && (hasInteractionFocus || guideObstacleFocused) && !accessDenied && promptImage != null && promptImage.sprite != null;
            }
        }

        private bool IsLanternEquipped()
        {
            return GameRoot.Instance?.GetSystem<MemorialArchive.Gameplay.Lighting.Logic.LightingSystem>()?.IsLanternEquipped == true;
        }

        private bool CanUsePrompt()
        {
            if (guideObstacleFocused) return true;
            if (activeInteractionType == InteractionType.SceneExit &&
                GameRoot.Instance?.Context?.Configs?.GetInteraction(activeInteractionId)?.HasStairDestinations == true) return true;
            return IsLanternEquipped();
        }

        private (Sprite normal, Sprite highlighted) GetArtwork(string interactionId, InteractionType type)
        {
            if (interactionId == "guide_obstacle") return (pickupSprite, pickupHighlightedSprite);
            switch (type)
            {
                case InteractionType.SavePoint:
                    return (saveSprite, saveHighlightedSprite);
                case InteractionType.ItemPickup:
                case InteractionType.NotePickup:
                case InteractionType.LightSource:
                    return (pickupSprite, pickupHighlightedSprite);
                case InteractionType.SceneExit:
                    return GetSceneExitArtwork(interactionId);
                default:
                    return (viewSprite, viewHighlightedSprite);
            }
        }

        private (Sprite normal, Sprite highlighted) GetSceneExitArtwork(string interactionId)
        {
            var config = GameRoot.Instance?.Context?.Configs?.GetInteraction(interactionId);
            if (config != null && config.HasStairDestinations)
            {
                // 当前补充资源没有单独的“下楼”图标，用楼梯图统一表达上下楼梯入口。
                return (stairSprite, stairHighlightedSprite);
            }

            var point = FindInteractionPoint(interactionId);
            if (point != null && point.transform.position.x < transform.position.x)
            {
                return (enterLeftSprite, enterLeftHighlightedSprite);
            }

            return (enterRightSprite, enterRightHighlightedSprite);
        }

        private static InteractionPointView FindInteractionPoint(string interactionId)
        {
            if (string.IsNullOrEmpty(interactionId))
            {
                return null;
            }

            var points = Object.FindObjectsOfType<InteractionPointView>(true);
            foreach (var point in points)
            {
                if (point != null && point.InteractionId == interactionId)
                {
                    return point;
                }
            }

            return null;
        }
    }
}
