using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Logic;
using UnityEngine;
using UnityEngine.UI;
namespace MemorialArchive.Gameplay.Guide.View
{
    public sealed class GuideOverlayPanel : BasePanel
    {
        [SerializeField] private Image artwork;
        [SerializeField] private Image closeHint;
        [SerializeField] private Text message;
        [SerializeField] private CanvasGroup inputShield;
        private GuideSystem guide;
        public override void Open()
        {
            base.Open(); transform.SetAsLastSibling();
            guide = GameRoot.Instance?.GetSystem<GuideSystem>();
            if (guide != null) { guide.Changed -= Refresh; guide.Changed += Refresh; }
            Refresh();
        }
        public override void Close() { if (guide != null) guide.Changed -= Refresh; base.Close(); }
        private void OnDestroy() { if (guide != null) guide.Changed -= Refresh; }
        private void Refresh()
        {
            var page = guide?.Current;
            artwork.sprite = page?.artwork; artwork.enabled = page?.artwork != null;
            closeHint.sprite = guide?.Config?.closeHint;
            closeHint.enabled = page != null && page.dismissKey == KeyCode.None;
            message.text = page?.message ?? string.Empty;
            message.gameObject.SetActive(!string.IsNullOrEmpty(message.text));
            inputShield.blocksRaycasts = page?.pause == true;
            inputShield.interactable = page?.pause == true;
        }
    }
}
