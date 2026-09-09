using System;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    /// <summary>
    /// Phone save point panel. Its UI2.0 surface is authored in the prefab;
    /// runtime code only binds the three functional slots and refreshes data.
    /// </summary>
    public sealed class SavePanel : BasePanel
    {
        private static readonly Vector2[] SlotPositions =
        {
            new Vector2(-5f, 292.5f),
            new Vector2(-5f, 127.5f),
            new Vector2(-5f, -7.5f),
            new Vector2(-5f, -142.5f)
        };

        [Header("UI2.0 save artwork")]
        [SerializeField] private Sprite maskSprite;
        [SerializeField] private Sprite chestSprite;
        [SerializeField] private Sprite[] slotNormalSprites = new Sprite[4];
        [SerializeField] private Sprite[] slotSelectedSprites = new Sprite[4];
        [SerializeField] private Sprite cancelSprite;

        private readonly Button[] slotButtons = new Button[SaveManager.Stage1SlotCount];
        private readonly Text[] slotLabels = new Text[SaveManager.Stage1SlotCount];
        private Text statusLabel;
        private Button closeButton;
        private bool subscribed;
        private bool layoutBound;

        protected override void Awake()
        {
            base.Awake();
            BindAuthoredLayout();
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
            if (!layoutBound) return;
            if (statusLabel != null) statusLabel.text = status;
            var saves = GameRoot.Instance?.Context?.Saves;
            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                var button = slotButtons[slotIndex];
                var label = slotLabels[slotIndex];
                if (button == null || label == null) continue;
                label.text = FormatSlot(slotIndex, saves?.GetSlotInfo(slotIndex));
                SaveSlotPanelViewFactory.ConfigureSlotButton(button, GetNormalSprite(slotIndex),
                    GetSelectedSprite(slotIndex), saves != null);
            }
        }

        private static string FormatSlot(int slotIndex, SaveSlotInfo info)
        {
            if (info == null || !info.hasSave) return "空档";
            var time = DateTime.TryParse(info.saveTime, out var parsedTime)
                ? parsedTime.ToString("yyyy-MM-dd HH:mm")
                : "未知时间";
            return time + "\n" + (string.IsNullOrEmpty(info.currentSceneId) ? "未知场景" : info.currentSceneId);
        }

        private void BindAuthoredLayout()
        {
            var surface = SaveSlotPanelViewFactory.FindSurface(transform);
            if (surface == null)
            {
                Debug.LogError("SavePanel has no authored SaveSlotSurface. Run Tools/Memorial Archive/Apply UI2.0 Save Layout.", this);
                return;
            }

            statusLabel = surface.Find("Status")?.GetComponent<Text>();
            closeButton = SaveSlotPanelViewFactory.FindCloseButton(surface);
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
            }

            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                Text label;
                var button = SaveSlotPanelViewFactory.FindSlotButton(surface, slotIndex, out label);
                slotButtons[slotIndex] = button;
                slotLabels[slotIndex] = label;
                if (button == null || label == null)
                {
                    Debug.LogError("SavePanel is missing authored slot " + (slotIndex + 1) + ".", this);
                    continue;
                }

                var capturedIndex = slotIndex;
                SaveSlotPanelViewFactory.ConfigureSlotButton(button, GetNormalSprite(slotIndex),
                    GetSelectedSprite(slotIndex), true);
                button.onClick.AddListener(() => RequestSave(capturedIndex));
            }

            Text fourthLabel;
            var fourthImage = SaveSlotPanelViewFactory.FindDisabledSlot(surface, SaveManager.Stage1SlotCount, out fourthLabel);
            if (fourthImage != null)
            {
                SaveSlotPanelViewFactory.ConfigureDisabledSlot(fourthImage,
                    GetNormalSprite(SaveManager.Stage1SlotCount), GetSelectedSprite(SaveManager.Stage1SlotCount));
                if (fourthLabel != null) fourthLabel.text = "第一阶段预留";
            }

            layoutBound = true;
        }

        private void ClosePanel()
        {
            GameRoot.Instance?.Context?.UI?.Close(PanelId.Save);
        }

        private Sprite GetNormalSprite(int slotIndex)
        {
            return slotNormalSprites != null && slotIndex >= 0 && slotIndex < slotNormalSprites.Length
                ? slotNormalSprites[slotIndex]
                : null;
        }

        private Sprite GetSelectedSprite(int slotIndex)
        {
            return slotSelectedSprites != null && slotIndex >= 0 && slotIndex < slotSelectedSprites.Length
                ? slotSelectedSprites[slotIndex]
                : null;
        }

#if UNITY_EDITOR
        public void RebuildPrefabLayoutForEditor()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            var surface = SaveSlotPanelViewFactory.CreateAuthoredSurface(transform, "SaveSlotSurface");
            var mask = SaveSlotPanelViewFactory.CreateAuthoredImage(surface, "Mask", Vector2.zero,
                new Vector2(1920f, 1080f), maskSprite, true);
            var maskRect = mask.rectTransform;
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
            mask.transform.SetAsFirstSibling();

            SaveSlotPanelViewFactory.CreateAuthoredImage(surface, "Chest", new Vector2(0f, -5f),
                chestSprite != null ? chestSprite.rect.size : new Vector2(917f, 640f), chestSprite, false);
            for (var slotIndex = 0; slotIndex < SaveManager.Stage1SlotCount; slotIndex++)
            {
                SaveSlotPanelViewFactory.CreateAuthoredSlotButton(surface, slotIndex,
                    GetNormalSprite(slotIndex), GetSelectedSprite(slotIndex), SlotPositions[slotIndex], out _);
            }

            SaveSlotPanelViewFactory.CreateAuthoredDisabledSlot(surface, SaveManager.Stage1SlotCount,
                GetNormalSprite(SaveManager.Stage1SlotCount), GetSelectedSprite(SaveManager.Stage1SlotCount),
                SlotPositions[SaveManager.Stage1SlotCount], out _);
            SaveSlotPanelViewFactory.CreateAuthoredText(surface, "Status", new Vector2(0f, -292f),
                new Vector2(720f, 40f), 16, TextAnchor.MiddleCenter, new Color(0.28f, 0.18f, 0.12f, 1f));
            SaveSlotPanelViewFactory.CreateAuthoredCloseButton(surface, cancelSprite, new Vector2(0f, -390f));
        }
#endif
    }
}
