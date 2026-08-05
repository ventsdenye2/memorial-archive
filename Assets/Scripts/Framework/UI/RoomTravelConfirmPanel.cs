using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class RoomTravelConfirmPanel : BasePanel
    {
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private RoomTravelConfirmationRequestedEvent request;

        protected override void Awake()
        {
            base.Awake();
            confirmButton?.onClick.AddListener(Confirm);
            cancelButton?.onClick.AddListener(Cancel);
        }

        private void OnDestroy()
        {
            confirmButton?.onClick.RemoveListener(Confirm);
            cancelButton?.onClick.RemoveListener(Cancel);
        }

        public void Show(RoomTravelConfirmationRequestedEvent value)
        {
            request = value;
            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(value.Message) ? "确认前往该房间？" : value.Message;
            }
        }

        private void Confirm()
        {
            var context = GameRoot.Instance?.Context;
            if (context == null || string.IsNullOrEmpty(request.SceneId))
            {
                return;
            }

            context.UI.Close(PanelId.RoomTravelConfirm);
            context.Events.Publish(new SceneTransitionRequestedEvent(request.SceneId, request.SpawnPointId));
        }

        private void Cancel()
        {
            GameRoot.Instance?.Context?.UI?.Close(PanelId.RoomTravelConfirm);
        }
    }
}
