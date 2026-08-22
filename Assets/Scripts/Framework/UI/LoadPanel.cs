using System;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>主菜单和死亡流程复用的三槽读档面板。</summary>
    public sealed class LoadPanel : BasePanel
    {
        private readonly Button[] slotButtons = new Button[SaveManager.Stage1SlotCount];
        private readonly Text[] slotLabels = new Text[SaveManager.Stage1SlotCount];
        private Text statusLabel;
        private bool subscribed;

        protected override void Awake()
        {
            base.Awake();
            BuildRuntimeLayout();
        }

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public override void Open()
        {
            base.Open();
            RefreshSlots("选择要继续的档案。");
        }

        private void Subscribe()
        {
            if (subscribed || GameRoot.Instance?.Context?.Events == null) return;
            GameRoot.Instance.Context.Events.Subscribe<LoadFailedEvent>(HandleLoadFailed);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || GameRoot.Instance?.Context?.Events == null)
            {
                subscribed = false;
                return;
            }

            GameRoot.Instance.Context.Events.Unsubscribe<LoadFailedEvent>(HandleLoadFailed);
            subscribed = false;
        }

        private void HandleLoadFailed(LoadFailedEvent evt) => RefreshSlots($"读档失败：{evt.Reason}");
        private void RequestLoad(int slotIndex) => GameRoot.Instance?.Context?.Events.Publish(new LoadRequestedEvent(slotIndex));

        private void RefreshSlots(string status)
        {
            if (statusLabel != null) statusLabel.text = status;
            var saves = GameRoot.Instance?.Context?.Saves;
            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                if (slotLabels[slotIndex] == null) continue;
                var info = saves?.GetSlotInfo(slotIndex);
                slotLabels[slotIndex].text = FormatSlot(slotIndex, info);
                slotButtons[slotIndex].interactable = info != null && info.hasSave;
            }
        }

        private static string FormatSlot(int slotIndex, SaveSlotInfo info)
        {
            var title = $"档案 {slotIndex + 1}";
            if (info == null || !info.hasSave) return title + "\n空档";
            var time = DateTime.TryParse(info.saveTime, out var parsedTime) ? parsedTime.ToString("yyyy-MM-dd HH:mm") : "未知时间";
            return title + "\n" + time + "  ·  " + (string.IsNullOrEmpty(info.currentSceneId) ? "未知场景" : info.currentSceneId);
        }

        private void BuildRuntimeLayout()
        {
            if (transform.Find("SaveSlotRuntime") != null) return;
            foreach (Transform child in transform) child.gameObject.SetActive(false);

            var root = SaveSlotPanelViewFactory.CreateSurface(transform, "SaveSlotRuntime", "读取档案", out statusLabel);
            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                var capturedIndex = slotIndex;
                var button = SaveSlotPanelViewFactory.CreateSlotButton(root, slotIndex, out var label);
                button.onClick.AddListener(() => RequestLoad(capturedIndex));
                slotButtons[slotIndex] = button;
                slotLabels[slotIndex] = label;
            }

            SaveSlotPanelViewFactory.CreateCloseButton(root, "返回", () => GameRoot.Instance?.Context?.UI?.Close(PanelId.Load));
        }
    }
}
