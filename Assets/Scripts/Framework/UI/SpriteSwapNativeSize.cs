using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class SpriteSwapNativeSize : MonoBehaviour
    {
        [SerializeField] private Image targetImage;

        private Sprite activeSprite;

        private void Awake()
        {
            ResolveTargetImage();
            RefreshSize(force: true);
        }

        private void OnEnable()
        {
            ResolveTargetImage();
            RefreshSize(force: true);
        }

        private void LateUpdate()
        {
            RefreshSize(force: false);
        }

        private void ResolveTargetImage()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }
        }

        private void RefreshSize(bool force)
        {
            if (targetImage == null)
            {
                return;
            }

            Sprite currentSprite = targetImage.overrideSprite != null
                ? targetImage.overrideSprite
                : targetImage.sprite;

            if (!force && currentSprite == activeSprite)
            {
                return;
            }

            activeSprite = currentSprite;
            if (activeSprite != null)
            {
                targetImage.SetNativeSize();
            }
        }
    }
}
