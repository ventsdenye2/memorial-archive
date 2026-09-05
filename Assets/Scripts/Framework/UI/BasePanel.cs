using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public abstract class BasePanel : MonoBehaviour
    {
        [SerializeField] private PanelId panelId;
        [SerializeField] private bool pausesGame;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool startClosed = true;

        public PanelId PanelId => panelId;
        public bool PausesGame => pausesGame;
        public bool IsOpen { get; private set; }

        public virtual void Open()
        {
            BindButtonAudio();
            IsOpen = true;
            SetVisible(true);
        }

        private void BindButtonAudio()
        {
            if (panelId == PanelId.MainMenu)
                foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (button.GetComponent<MemorialArchive.Framework.Audio.UIButtonAudio>() == null)
                        button.gameObject.AddComponent<MemorialArchive.Framework.Audio.UIButtonAudio>();
        }

        public virtual void Close()
        {
            IsOpen = false;
            SetVisible(false);
        }

        protected virtual void Awake()
        {
            BindButtonAudio();
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (startClosed)
            {
                IsOpen = false;
                SetVisible(false);
            }
            else
            {
                IsOpen = true;
                SetVisible(true);
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
