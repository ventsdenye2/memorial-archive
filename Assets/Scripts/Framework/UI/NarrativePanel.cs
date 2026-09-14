using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Story.Logic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MemorialArchive.Gameplay.Story.Data;

namespace MemorialArchive.Framework.UI
{
    public sealed class NarrativePanel : BasePanel
    {
        [SerializeField] private Text dialogueText;
        [SerializeField] private GameObject continueHint;
        [SerializeField] private Button[] choiceButtons;
        private NarrativeSystem narrative;
        private int openedFrame;
        private NarrativeLine displayedLine;
        private List<string> pages = new List<string>();
        private int pageIndex;
        protected override void Awake()
        {
            base.Awake();
            for (var i = 0; i < choiceButtons.Length; i++)
            {
                var index = i;
                choiceButtons[i].onClick.AddListener(() => narrative?.Advance(index));
            }
        }
        public override void Open()
        {
            base.Open();
            openedFrame = Time.frameCount;
            displayedLine = null;
            if (narrative != null) narrative.Changed -= Refresh;
            narrative = GameRoot.Instance?.GetSystem<NarrativeSystem>();
            if (narrative != null) narrative.Changed += Refresh;
            Refresh();
        }
        public override void Close()
        {
            if (narrative != null) narrative.Changed -= Refresh;
            base.Close();
        }
        private void OnDestroy() { if (narrative != null) narrative.Changed -= Refresh; }
        private void Update()
        {
            if (!IsOpen || narrative?.CurrentLine == null || Time.frameCount <= openedFrame) return;
            if (!Input.anyKeyDown) return;
            if (pageIndex + 1 < pages.Count)
            {
                pageIndex++;
                RenderPage();
            }
            else if (narrative.CurrentLine.choices.Length == 0) narrative.Advance();
        }
        private void Refresh()
        {
            var line = narrative?.CurrentLine;
            if (line == null) return;
            if (displayedLine != line)
            {
                displayedLine = line;
                pageIndex = 0;
                Canvas.ForceUpdateCanvases();
                pages = DiaryPanel.Paginate(string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + "：" + line.text, dialogueText);
            }
            RenderPage();
        }
        private void RenderPage()
        {
            var line = displayedLine;
            if (line == null) return;
            dialogueText.text = pages[pageIndex];
            var lastPage = pageIndex == pages.Count - 1;
            continueHint.SetActive(!lastPage || line.choices.Length == 0);
            for (var i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(lastPage && i < line.choices.Length);
                if (i < line.choices.Length) choiceButtons[i].GetComponentInChildren<Text>().text = line.choices[i].text;
            }
        }
    }
}
