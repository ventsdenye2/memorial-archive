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
        private bool pointerInside;

        public void Configure(Image targetArtwork, Sprite normal, Sprite highlighted)
        {
            artwork = targetArtwork;
            normalSprite = normal;
            highlightedSprite = highlighted;
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

            // Hover describes pointer location, independently of save/load availability.
            // Empty load slots and reserved slots still provide visual feedback.
            artwork.overrideSprite = null;
            artwork.sprite = pointerInside && highlightedSprite != null
                ? highlightedSprite
                : normalSprite;
            // Availability belongs to Button.interactable and the slot label.
            // Tinting empty slots makes occupied slots appear permanently highlighted.
            artwork.color = Color.white;
        }
    }
}
