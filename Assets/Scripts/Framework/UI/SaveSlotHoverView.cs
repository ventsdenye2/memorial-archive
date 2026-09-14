using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Applies save-card hover artwork and optional selection motion explicitly.
    /// Unity's SpriteSwap state is not sufficient here because the authored card
    /// images overlap; a narrow hit area identifies the card that owns the pointer.
    /// </summary>
    internal sealed class SaveSlotHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private const float SelectionLiftDuration = 0.18f;

        private Image artwork;
        private Sprite normalSprite;
        private Sprite highlightedSprite;
        private RectTransform slotRect;
        private Vector2 restingPosition;
        private Coroutine moveCoroutine;
        private float selectedYOffset;
        private bool pointerInside;
        private bool selected;
        private bool positionInitialized;

        public void Configure(Image targetArtwork, Sprite normal, Sprite highlighted, float yOffset = 0f)
        {
            artwork = targetArtwork;
            normalSprite = normal;
            highlightedSprite = highlighted;
            selectedYOffset = Mathf.Max(0f, yOffset);

            var targetRect = transform as RectTransform;
            if (!positionInitialized || slotRect != targetRect)
            {
                slotRect = targetRect;
                if (slotRect != null)
                {
                    restingPosition = slotRect.anchoredPosition;
                    positionInitialized = true;
                }
            }

            ApplyState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            ApplyState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            ApplyState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            // A mouse click must not latch the lift after the pointer leaves.
            // Retain selection motion for keyboard/controller navigation.
            selected = !(eventData is PointerEventData);
            ApplyState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            ApplyState();
        }

        public void ResetInteraction()
        {
            pointerInside = false;
            selected = false;
            ApplyState();
        }

        private void OnDisable()
        {
            pointerInside = false;
            selected = false;
            StopMovement();
            if (positionInitialized && slotRect != null)
            {
                slotRect.anchoredPosition = restingPosition;
            }
        }

        private void ApplyState()
        {
            if (artwork == null)
            {
                return;
            }

            // Hover describes pointer location, independently of save/load availability.
            // Empty load slots and reserved slots still provide visual feedback.
            artwork.overrideSprite = null;
            artwork.sprite = pointerInside && highlightedSprite != null
                ? highlightedSprite
                : normalSprite;
            // Availability belongs to Button.interactable and the slot label.
            // Tinting empty slots makes occupied slots appear permanently highlighted.
            artwork.color = Color.white;

            var targetPosition = restingPosition + Vector2.up *
                (pointerInside || selected ? selectedYOffset : 0f);
            MoveTo(targetPosition);
        }

        private void MoveTo(Vector2 targetPosition)
        {
            if (!positionInitialized || slotRect == null)
            {
                return;
            }

            StopMovement();
            if (!Application.isPlaying || !isActiveAndEnabled || SelectionLiftDuration <= 0f || selectedYOffset <= 0f)
            {
                slotRect.anchoredPosition = targetPosition;
                return;
            }

            moveCoroutine = StartCoroutine(AnimatePosition(targetPosition));
        }

        private IEnumerator AnimatePosition(Vector2 targetPosition)
        {
            var startPosition = slotRect.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < SelectionLiftDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / SelectionLiftDuration);
                progress = progress * progress * (3f - 2f * progress);
                slotRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, progress);
                yield return null;
            }

            slotRect.anchoredPosition = targetPosition;
            moveCoroutine = null;
        }

        private void StopMovement()
        {
            if (moveCoroutine == null)
            {
                return;
            }

            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }
}
