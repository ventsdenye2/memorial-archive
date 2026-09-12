using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Story.Data;
using MemorialArchive.Gameplay.Story.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class DiaryPanel : BasePanel
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text pageText;
        private bool diary = true;
        private int entryIndex;
        private int pageIndex;
        private int openedFrame;
        private List<ReadingEntry> entries = new List<ReadingEntry>();
        private List<string> pages = new List<string>();
        private Button previous, next;
        private Toggle diaryTab, noteTab;
        private NarrativeSystem Narrative => GameRoot.Instance?.GetSystem<NarrativeSystem>();
        protected override void Awake()
        {
            base.Awake();
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.name == "LeftArrow") { previous = button; button.onClick.AddListener(Previous); }
                if (button.name == "RightArrow") { next = button; button.onClick.AddListener(Next); }
            }
            foreach (var toggle in GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.name == "DiaryTab") { diaryTab = toggle; toggle.onValueChanged.AddListener(value => { if (value) SetCategory(true); }); }
                if (toggle.name == "NoteTab") { noteTab = toggle; toggle.onValueChanged.AddListener(value => { if (value) SetCategory(false); }); }
            }
        }
        public override void Open() { base.Open(); openedFrame = Time.frameCount; RefreshEntries(); }
        public override void Close() { if (IsOpen) FinishCurrentReading(); base.Close(); }
        public void ShowNote(string id)
        {
            var note = Narrative?.Content.FindNote(id);
            if (note == null) return;
            FinishCurrentReading();
            diary = note.isDiary;
            entries = Narrative.GetCollectedNotes(diary);
            entryIndex = Mathf.Max(0, entries.FindIndex(n => n.id == note.id));
            pageIndex = 0;
            Render();
        }
        private void SetCategory(bool value)
        {
            FinishCurrentReading();
            diary = value;
            entryIndex = pageIndex = 0;
            RefreshEntries();
        }
        private void RefreshEntries()
        {
            entries = Narrative?.GetCollectedNotes(diary) ?? new List<ReadingEntry>();
            entryIndex = Mathf.Clamp(entryIndex, 0, Mathf.Max(0, entries.Count - 1));
            Render();
        }
        private void Render()
        {
            diaryTab?.SetIsOnWithoutNotify(diary);
            noteTab?.SetIsOnWithoutNotify(!diary);
            pages.Clear();
            if (entries.Count > 0) { pages = Paginate(entries[entryIndex].content, bodyText); pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1); }
            if (titleText != null) titleText.text = entries.Count > 0 ? entries[entryIndex].title : diary ? "日记" : "纸条与档案";
            if (bodyText != null) bodyText.text = pages.Count > 0 ? pages[pageIndex] : "尚未收集到相关记录。";
            if (pageText != null) pageText.text = entries.Count > 0 ? $"{entryIndex + 1} / {entries.Count} 篇　·　{pageIndex + 1} / {pages.Count} 页" : "";
            if (previous != null) previous.interactable = entries.Count > 0 && (entryIndex > 0 || pageIndex > 0);
            if (next != null) next.interactable = entries.Count > 0 && (entryIndex < entries.Count - 1 || pageIndex < pages.Count - 1);
        }
        private void FinishCurrentReading()
        {
            if (Time.frameCount > openedFrame && entries.Count > 0 && pages.Count > 0 && pageIndex == pages.Count - 1) Narrative?.RecordRead(entries[entryIndex].id);
        }
        private void Previous()
        {
            FinishCurrentReading();
            if (pageIndex > 0) pageIndex--;
            else if (entryIndex > 0) { entryIndex--; pageIndex = Paginate(entries[entryIndex].content, bodyText).Count - 1; }
            Render();
        }
        private void Next()
        {
            FinishCurrentReading();
            if (pageIndex < pages.Count - 1) pageIndex++;
            else if (entryIndex < entries.Count - 1) { entryIndex++; pageIndex = 0; }
            Render();
        }
        public static List<string> Paginate(string text, Text view = null)
        {
            var result = new List<string>();
            text = text ?? "";
            while (text.Length > 0)
            {
                var length = Mathf.Min(340, text.Length);
                if (view != null)
                {
                    var settings = view.GetGenerationSettings(view.rectTransform.rect.size);
                    var height = Mathf.Max(1f, view.rectTransform.rect.height - view.fontSize);
                    var generator = new TextGenerator();
                    while (length > 1 && generator.GetPreferredHeight(text.Substring(0, length), settings) / view.pixelsPerUnit > height) length--;
                }
                if (length < text.Length)
                {
                    var end = text.LastIndexOf('\n', length - 1, length);
                    if (end > length / 2) length = end + 1;
                }
                result.Add(text.Substring(0, length));
                text = text.Substring(length);
            }
            if (result.Count == 0) result.Add("");
            return result;
        }
    }
}
