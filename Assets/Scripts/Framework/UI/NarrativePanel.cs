using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Story.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class NarrativePanel : BasePanel
    {
        [SerializeField] private Text dialogueText;
        [SerializeField] private GameObject continueHint;
        [SerializeField] private Button[] choiceButtons;
        private NarrativeSystem narrative;
        private int openedFrame;
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
            if (narrative.CurrentLine.choices.Length == 0 && Input.anyKeyDown) narrative.Advance();
        }
        private void Refresh()
        {
            var line = narrative?.CurrentLine;
            if (line == null) return;
            dialogueText.text = string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + "：" + line.text;
            continueHint.SetActive(line.choices.Length == 0);
            for (var i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(i < line.choices.Length);
                if (i < line.choices.Length) choiceButtons[i].GetComponentInChildren<Text>().text = line.choices[i].text;
            }
        }
    }
}
