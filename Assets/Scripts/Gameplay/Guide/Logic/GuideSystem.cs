using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Config;
using UnityEngine;
namespace MemorialArchive.Gameplay.Guide.Logic
{
    /// <summary>Owns page requests, acknowledgement and saved progress. The view only renders Current.</summary>
    public sealed class GuideSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        [Serializable] public sealed class Progress
        {
            public List<string> completedStepIds = new List<string>();
            public List<string> pending = new List<string>();
        }
        private Progress progress = new Progress();
        private GameContext context;
        private int openedFrame;
        private bool suspended;
        private int consumedFrame = -1;
        public GuidePresentationConfig Config { get; private set; }
        public GuidePage Current { get; private set; }
        public string ModuleKey => "guide";
        public bool SequenceActive => !HasCompleted("map_unlocked");
        public bool BlocksInput => Current?.pause == true || consumedFrame == Time.frameCount;
        public event Action Changed;
        public void Initialize(GameContext value)
        {
            context = value;
            Config = Resources.Load<GuidePresentationConfig>("Guide/Presentation");
            context.Events.Subscribe<SceneLoadedEvent>(OnScene);
            context.Events.Subscribe<CharacterDiedEvent>(OnDeath);
        }
        public void Dispose() { context.Events.Unsubscribe<SceneLoadedEvent>(OnScene); context.Events.Unsubscribe<CharacterDiedEvent>(OnDeath); Hide(); Changed = null; }
        public bool HasCompleted(string id) => progress.completedStepIds.Contains(id);
        public void Record(string id)
        {
            if (HasCompleted(id)) return;
            progress.completedStepIds.Add(id);
            context.Events.Publish(new GuideStepCompletedEvent(id));
            context.Events.Publish(new GuideSequenceActiveChangedEvent(SequenceActive));
        }
        public void Request(string id)
        {
            if (HasCompleted(id) || progress.pending.Contains(id)) return;
            if (Config?.Find(id) == null) { Debug.LogError("Missing guide page: " + id); return; }
            // The first backpack tutorial must not wait behind a page that cannot appear over inventory.
            if (id == "inventory") { if (Current != null && !Current.pause) Hide(); progress.pending.Insert(0, id); } else progress.pending.Add(id);
        }
        public void Tick(float deltaTime)
        {
            if (suspended || Current != null || progress.pending.Count == 0 || context.UI == null) return;
            var id = progress.pending[0];
            if (context.UI.IsGameplayInputBlocked && !(id == "inventory" && context.UI.IsOpen(PanelId.Inventory))) return;
            if (context.UI.Open(PanelId.GuideOverlay) == null) return;
            Current = Config.Find(id);
            openedFrame = Time.frameCount;
            context.UI.SetPauseOwner(this, Current.pause);
            Changed?.Invoke();
            context.Events.Publish(new GuideStepStartedEvent(id));
        }
        public bool ProcessInput()
        {
            if (consumedFrame == Time.frameCount) return true;
            if (Current == null) return false;
            if (Time.frameCount > openedFrame + 1 && (Current.dismissKey == KeyCode.None ? Input.anyKeyDown : Input.GetKeyDown(Current.dismissKey)))
            { Acknowledge(); return true; }
            return Current.pause;
        }
        public void Acknowledge()
        {
            if (Current == null) return;
            var id = Current.id;
            consumedFrame = Time.frameCount;
            progress.pending.Remove(id);
            Hide();
            Record(id);
        }
        public void CompleteStep(string id) => Record(id);
        private void Hide()
        {
            Current = null;
            context?.UI?.SetPauseOwner(this, false);
            context?.UI?.Close(PanelId.GuideOverlay);
            Changed?.Invoke();
        }
        private void OnScene(SceneLoadedEvent evt) { Hide(); suspended = false; }
        private void OnDeath(CharacterDiedEvent evt) { Hide(); suspended = true; }
        public void ResetForNewGame() { Hide(); progress = new Progress(); consumedFrame = -1; suspended = false; context.Events.Publish(new GuideSequenceActiveChangedEvent(true)); }
        public object CaptureSaveData() => progress;
        public void RestoreSaveData(string json)
        {
            Hide();
            progress = string.IsNullOrEmpty(json) ? new Progress() : JsonUtility.FromJson<Progress>(json) ?? new Progress();
            progress.completedStepIds = progress.completedStepIds ?? new List<string>();
            progress.pending = progress.pending ?? new List<string>();
            if (HasCompleted("op_move_hint") && !HasCompleted("movement")) progress.completedStepIds.Add("movement");
            if (HasCompleted("op_lantern_equip") && !HasCompleted("inventory")) progress.completedStepIds.Add("inventory");
            context.Events.Publish(new GuideSequenceActiveChangedEvent(SequenceActive));
        }
    }
}
