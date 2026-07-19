using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Story.Config;
using MemorialArchive.Gameplay.Story.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Story.Logic
{
    public sealed class StorySystem : IGameSystem, ISaveModule
    {
        private StorySaveData saveData = new StorySaveData();
        private GameContext context;

        public string ModuleKey => "story";

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<StoryUnlockedEvent>(HandleStoryUnlocked);
            context.Events.Subscribe<NoteUnlockedEvent>(HandleNoteUnlocked);
            context.Events.Subscribe<BlackScreenStoryFinishedEvent>(HandleBlackScreenStoryFinished);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<StoryUnlockedEvent>(HandleStoryUnlocked);
                context.Events.Unsubscribe<NoteUnlockedEvent>(HandleNoteUnlocked);
                context.Events.Unsubscribe<BlackScreenStoryFinishedEvent>(HandleBlackScreenStoryFinished);
            }

            context = null;
            saveData = new StorySaveData();
        }

        public void PlayStory(string storyId)
        {
            if (string.IsNullOrEmpty(storyId))
            {
                return;
            }

            var config = context.Configs.GetStory(storyId);
            if (config == null)
            {
                return;
            }

            if (!saveData.unlockedStoryIds.Contains(storyId))
            {
                saveData.unlockedStoryIds.Add(storyId);
            }

            if (config.PresentationType == StoryPresentationType.BlackScreen)
            {
                context.UI.Open(PanelId.BlackScreenStory);
                context.Events.Publish(new BlackScreenStoryStartedEvent(storyId));
            }
        }

        public object CaptureSaveData()
        {
            return saveData;
        }

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var restored = JsonUtility.FromJson<StorySaveData>(json);
            if (restored != null)
            {
                saveData = restored;
            }
        }

        private void HandleStoryUnlocked(StoryUnlockedEvent evt)
        {
            PlayStory(evt.StoryId);
        }

        private void HandleNoteUnlocked(NoteUnlockedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.NoteId) && !saveData.unlockedNoteIds.Contains(evt.NoteId))
            {
                saveData.unlockedNoteIds.Add(evt.NoteId);
            }
        }

        private void HandleBlackScreenStoryFinished(BlackScreenStoryFinishedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.StoryId) && !saveData.playedStoryIds.Contains(evt.StoryId))
            {
                saveData.playedStoryIds.Add(evt.StoryId);
            }

            var config = context.Configs.GetStory(evt.StoryId);
            if (config != null && config.ReturnToMainMenuWhenFinished)
            {
                context.UI.Open(PanelId.MainMenu);
            }
        }
    }
}
