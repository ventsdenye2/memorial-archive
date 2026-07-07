using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public abstract class BasePanel : MonoBehaviour
    {
        [SerializeField] private PanelId panelId;
        [SerializeField] private bool pausesGame;
        [SerializeField] private CanvasGroup canvasGroup;

        public PanelId PanelId => panelId;
        public bool PausesGame => pausesGame;
        public bool IsOpen { get; private set; }

        public virtual void Open()
        {
            IsOpen = true;
            SetVisible(true);
        }

        public virtual void Close()
        {
            IsOpen = false;
            SetVisible(false);
        }

        protected virtual void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
