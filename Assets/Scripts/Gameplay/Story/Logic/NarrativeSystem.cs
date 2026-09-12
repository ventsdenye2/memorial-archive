using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Story.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Story.Logic
{
    public sealed class NarrativeSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        private readonly NarrativeContent suppliedContent;
        private GameContext context;
        private NarrativeContent content;
        private NarrativeProgress progress = new NarrativeProgress();
        private string sceneId;
        private int sceneReadyFrame;
        private int lastAdvanceFrame = -1;
        private int releaseInputFrame = -1;
        private int lineIndex;
        public string ModuleKey => "narrative";
        public NarrativeSequence Current { get; private set; }
        public NarrativeLine CurrentLine => Current != null ? Current.lines[lineIndex] : null;
        public bool BlocksGameplayInput => Current != null || Time.frameCount <= releaseInputFrame;
        public NarrativeContent Content => content;
        public event Action Changed;

        public NarrativeSystem(NarrativeContent content = null) => suppliedContent = content;
        public void Initialize(GameContext value)
        {
            context = value;
            content = suppliedContent ?? NarrativeContent.Load();
            context.Events.Subscribe<NoteUnlockedEvent>(OnNote);
            context.Events.Subscribe<InspectRequestedEvent>(OnInspect);
            context.Events.Subscribe<SceneLoadedEvent>(OnScene);
            context.Events.Subscribe<LoadCompletedEvent>(OnLoaded);
        }
        public void Dispose()
        {
            context.Events.Unsubscribe<NoteUnlockedEvent>(OnNote);
            context.Events.Unsubscribe<InspectRequestedEvent>(OnInspect);
            context.Events.Unsubscribe<SceneLoadedEvent>(OnScene);
            context.Events.Unsubscribe<LoadCompletedEvent>(OnLoaded);
            Changed = null;
        }
        public void ResetForNewGame()
        {
            progress = new NarrativeProgress();
            Current = null;
            sceneId = null;
            Changed?.Invoke();
        }
        public bool HasPlayed(string id) => progress.playedSequences.Contains(id);
        public bool HasRead(string id) => progress.readNotes.Contains(id);
        public List<ReadingEntry> GetCollectedNotes(bool diary)
        {
            var result = new List<ReadingEntry>();
            foreach (var id in progress.collectedNotes)
            {
                var note = content.FindNote(id);
                if (note != null && note.isDiary == diary) result.Add(note);
            }
            return result;
        }
        public ReadingEntry Collect(string id)
        {
            var note = content.FindNote(id);
            if (note != null && !progress.collectedNotes.Contains(note.id)) progress.collectedNotes.Add(note.id);
            return note;
        }
        public void RecordRead(string id)
        {
            var note = content.FindNote(id);
            if (note == null || !progress.collectedNotes.Contains(note.id)) return;
            if (!progress.readNotes.Contains(note.id)) progress.readNotes.Add(note.id);
            Queue(note.reflectionId);
            foreach (var sequence in content.sequences)
                if (sequence.requiredNotes.Length > 0 && Array.TrueForAll(sequence.requiredNotes, HasRead)) Queue(sequence.id);
        }
        private void OnNote(NoteUnlockedEvent evt)
        {
            var note = Collect(evt.NoteId);
            if (note == null) return;
            var panel = context.UI?.Open(PanelId.Diary) as DiaryPanel;
            panel?.ShowNote(note.id);
        }
        private void OnInspect(InspectRequestedEvent evt)
        {
            foreach (var sequence in content.sequences)
                if (sequence.interactionId == evt.InspectId) Queue(sequence.id);
        }
        private void OnScene(SceneLoadedEvent evt)
        {
            sceneId = evt.SceneId;
            Current = null;
            sceneReadyFrame = Time.frameCount + 2;
            foreach (var sequence in content.sequences)
                if (sequence.onFirstEnter && sequence.sceneId == sceneId) Queue(sequence.id);
            Changed?.Invoke();
        }
        private void OnLoaded(LoadCompletedEvent evt)
        {
            // Old saves know collected IDs through StorySystem but have no narrative module.
            var old = GameRoot.Instance?.GetSystem<StorySystem>()?.CaptureSaveData() as StorySaveData;
            if (old?.unlockedNoteIds != null) foreach (var id in old.unlockedNoteIds) Collect(id);
        }
        public void Queue(string id)
        {
            if (string.IsNullOrEmpty(id) || content.FindSequence(id) == null || HasPlayed(id) || progress.pendingSequences.Contains(id)) return;
            progress.pendingSequences.Add(id);
        }
        public void Tick(float deltaTime)
        {
            if (Current != null || Time.frameCount <= sceneReadyFrame || context.UI == null || context.UI.IsGameplayInputBlocked) return;
            foreach (var id in progress.pendingSequences.ToArray())
            {
                var sequence = content.FindSequence(id);
                if (sequence == null || HasPlayed(id)) { progress.pendingSequences.Remove(id); continue; }
                if (!string.IsNullOrEmpty(sequence.sceneId) && sequence.sceneId != sceneId) continue;
                if (sequence.lines.Length == 0) { progress.pendingSequences.Remove(id); continue; }
                Current = sequence;
                lineIndex = 0;
                lastAdvanceFrame = Time.frameCount;
                if (context.UI.Open(PanelId.Narrative) == null) { Current = null; return; }
                Changed?.Invoke();
                return;
            }
        }
        public void Advance(int choiceIndex = -1)
        {
            if (Current == null || lastAdvanceFrame == Time.frameCount) return;
            var line = CurrentLine;
            var next = line.next;
            if (line.choices.Length > 0)
            {
                if (choiceIndex < 0 || choiceIndex >= line.choices.Length) return;
                next = line.choices[choiceIndex].next;
            }
            lastAdvanceFrame = Time.frameCount;
            if (next < 0 || next >= Current.lines.Length)
            {
                var finished = Current.id;
                progress.playedSequences.Add(finished);
                progress.pendingSequences.Remove(finished);
                Current = null;
                releaseInputFrame = Time.frameCount;
                context.UI?.Close(PanelId.Narrative);
            }
            else lineIndex = next;
            Changed?.Invoke();
        }
        public object CaptureSaveData() => progress;
        public void RestoreSaveData(string json)
        {
            progress = JsonUtility.FromJson<NarrativeProgress>(json) ?? new NarrativeProgress();
            progress.collectedNotes = progress.collectedNotes ?? new List<string>();
            progress.readNotes = progress.readNotes ?? new List<string>();
            progress.playedSequences = progress.playedSequences ?? new List<string>();
            progress.pendingSequences = progress.pendingSequences ?? new List<string>();
            Current = null;
            Changed?.Invoke();
        }
    }
}
