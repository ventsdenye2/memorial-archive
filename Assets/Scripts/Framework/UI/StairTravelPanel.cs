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
            BindButtons();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        public override void Open()
        {
            base.Open();
            transform.SetAsLastSibling();
            BindButtons();
        }

        public void Show(StairTravelRequestedEvent value)
        {
            request = value;
            transform.SetAsLastSibling();
            BindButtons();
            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(value.Message) ? "请选择目的楼层" : value.Message;
            }

            if (upButton != null)
            {
                upButton.gameObject.SetActive(value.CanGoUp);
                SetButtonLabel(upButton, BuildDestinationLabel(value.UpSceneId, "上楼"));
            }

            if (downButton != null)
            {
                downButton.gameObject.SetActive(value.CanGoDown);
                SetButtonLabel(downButton, BuildDestinationLabel(value.DownSceneId, "下楼"));
            }

            ArrangeButtons(value.CanGoUp, value.CanGoDown);
        }

        private void GoUp() => Travel(request.UpSceneId, request.UpSpawnPointId);
        private void GoDown() => Travel(request.DownSceneId, request.DownSpawnPointId);

        private void Cancel()
        {
            ClosePanel();
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

            ClosePanel();
            context.Events.Publish(new SceneTransitionRequestedEvent(sceneId, spawnPointId));
        }

        private void ClosePanel()
        {
            var ui = GameRoot.Instance?.Context?.UI;
            if (ui != null)
            {
                ui.Close(PanelId.StairTravel);
            }
            else
            {
                Close();
            }
        }

        private void BindButtons()
        {
            UnbindButtons();
            upButton?.onClick.AddListener(GoUp);
            downButton?.onClick.AddListener(GoDown);
            cancelButton?.onClick.AddListener(Cancel);
        }

        private void UnbindButtons()
        {
            upButton?.onClick.RemoveListener(GoUp);
            downButton?.onClick.RemoveListener(GoDown);
            cancelButton?.onClick.RemoveListener(Cancel);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (text != null)
            {
                text.text = label;
            }
        }

        private static string BuildDestinationLabel(string sceneId, string fallback)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return fallback;
            }

            if (sceneId.Contains("1F")) return "前往一楼";
            if (sceneId.Contains("2F")) return "前往二楼";
            if (sceneId.Contains("3F")) return "前往三楼";
            if (sceneId.Contains("4F")) return "前往四楼";
            return fallback;
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
