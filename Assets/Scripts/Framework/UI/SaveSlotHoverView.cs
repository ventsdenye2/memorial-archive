using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Applies save-card hover artwork explicitly. Unity's SpriteSwap state is
    /// not sufficient here because the authored card images overlap; a narrow
    /// hit area identifies the card that actually owns the pointer.
    /// </summary>
    internal sealed class SaveSlotHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image artwork;
        private Sprite normalSprite;
        private Sprite highlightedSprite;
        private bool interactable;
        private bool pointerInside;

        public void Configure(Image targetArtwork, Sprite normal, Sprite highlighted, bool canInteract)
        {
            artwork = targetArtwork;
            normalSprite = normal;
            highlightedSprite = highlighted;
            interactable = canInteract;
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

        private void OnDisable()
        {
            pointerInside = false;
            ApplyState();
        }

        private void ApplyState()
        {
            if (artwork == null)
            {
                return;
            }

            artwork.sprite = interactable && pointerInside && highlightedSprite != null
                ? highlightedSprite
                : normalSprite;
            artwork.color = interactable ? Color.white : new Color(0.58f, 0.52f, 0.46f, 0.78f);
        }
    }
}
