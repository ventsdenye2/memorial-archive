using System;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>电话存档点使用的三槽存档面板。</summary>
    public sealed class SavePanel : BasePanel
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
            RefreshSlots("选择一个档案位保存当前进度。");
        }

        private void Subscribe()
        {
            if (subscribed || GameRoot.Instance?.Context?.Events == null) return;
            var events = GameRoot.Instance.Context.Events;
            events.Subscribe<SaveCompletedEvent>(HandleSaveCompleted);
            events.Subscribe<SaveFailedEvent>(HandleSaveFailed);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || GameRoot.Instance?.Context?.Events == null)
            {
                subscribed = false;
                return;
            }

            var events = GameRoot.Instance.Context.Events;
            events.Unsubscribe<SaveCompletedEvent>(HandleSaveCompleted);
            events.Unsubscribe<SaveFailedEvent>(HandleSaveFailed);
            subscribed = false;
        }

        private void HandleSaveCompleted(SaveCompletedEvent evt) => RefreshSlots(evt.OverwroteExisting ? "存档已覆盖。" : "存档完成。");
        private void HandleSaveFailed(SaveFailedEvent evt) => RefreshSlots($"存档失败：{evt.Reason}");
        private void RequestSave(int slotIndex) => GameRoot.Instance?.Context?.Events.Publish(new SaveRequestedEvent(slotIndex));

        private void RefreshSlots(string status)
        {
            if (statusLabel != null) statusLabel.text = status;
            var saves = GameRoot.Instance?.Context?.Saves;
            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                if (slotLabels[slotIndex] == null) continue;
                slotLabels[slotIndex].text = FormatSlot(slotIndex, saves?.GetSlotInfo(slotIndex));
                slotButtons[slotIndex].interactable = saves != null;
            }
        }

        private static string FormatSlot(int slotIndex, SaveSlotInfo info)
        {
            var title = $"档案 {slotIndex + 1}";
            if (info == null || !info.hasSave) return title + "\n空档";
            return title + "\n" + FormatSaveTime(info.saveTime) + "  ·  " + (string.IsNullOrEmpty(info.currentSceneId) ? "未知场景" : info.currentSceneId);
        }

        private static string FormatSaveTime(string rawTime) =>
            DateTime.TryParse(rawTime, out var time) ? time.ToString("yyyy-MM-dd HH:mm") : "未知时间";

        private void BuildRuntimeLayout()
        {
            if (transform.Find("SaveSlotRuntime") != null) return;
            foreach (Transform child in transform) child.gameObject.SetActive(false);

            var root = SaveSlotPanelViewFactory.CreateSurface(transform, "SaveSlotRuntime", "电话亭存档", out statusLabel);
            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                var capturedIndex = slotIndex;
                var button = SaveSlotPanelViewFactory.CreateSlotButton(root, slotIndex, out var label);
                button.onClick.AddListener(() => RequestSave(capturedIndex));
                slotButtons[slotIndex] = button;
                slotLabels[slotIndex] = label;
            }

            SaveSlotPanelViewFactory.CreateCloseButton(root, "离开", () => GameRoot.Instance?.Context?.UI?.Close(PanelId.Save));
        }
    }
}
