using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class StairTravelPanel : BasePanel
    {
        [SerializeField] private Text messageText;
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button cancelButton;

        private StairTravelRequestedEvent request;

        protected override void Awake()
        {
            base.Awake();
            upButton?.onClick.AddListener(GoUp);
            downButton?.onClick.AddListener(GoDown);
            cancelButton?.onClick.AddListener(Cancel);
        }

        private void OnDestroy()
        {
            upButton?.onClick.RemoveListener(GoUp);
            downButton?.onClick.RemoveListener(GoDown);
            cancelButton?.onClick.RemoveListener(Cancel);
        }

        public void Show(StairTravelRequestedEvent value)
        {
            request = value;
            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(value.Message) ? "请选择前往楼层" : value.Message;
            }

            if (upButton != null)
            {
                upButton.gameObject.SetActive(value.CanGoUp);
            }

            if (downButton != null)
            {
                downButton.gameObject.SetActive(value.CanGoDown);
            }

            ArrangeButtons(value.CanGoUp, value.CanGoDown);
        }

        private void GoUp() => Travel(request.UpSceneId, request.UpSpawnPointId);
        private void GoDown() => Travel(request.DownSceneId, request.DownSpawnPointId);

        private void Cancel()
        {
            GameRoot.Instance?.Context?.UI?.Close(PanelId.StairTravel);
        }

        private void Travel(string sceneId, string spawnPointId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return;
            }

            var context = GameRoot.Instance?.Context;
            if (context == null)
            {
                return;
            }

            context.UI.Close(PanelId.StairTravel);
            context.Events.Publish(new SceneTransitionRequestedEvent(sceneId, spawnPointId));
        }

        private void ArrangeButtons(bool canGoUp, bool canGoDown)
        {
            if (cancelButton == null)
            {
                return;
            }

            if (canGoUp && canGoDown)
            {
                SetButtonX(upButton, -170f);
                SetButtonX(downButton, 0f);
                SetButtonX(cancelButton, 170f);
                return;
            }

            SetButtonX(canGoUp ? upButton : downButton, -90f);
            SetButtonX(cancelButton, 90f);
        }

        private static void SetButtonX(Button button, float x)
        {
            if (button == null || !(button.transform is RectTransform rect))
            {
                return;
            }

            var position = rect.anchoredPosition;
            position.x = x;
            rect.anchoredPosition = position;
        }
    }
}
