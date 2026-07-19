using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Framework.UI;

namespace MemorialArchive.Framework.Core
{
    public sealed class GameContext
    {
        public GameContext(
            EventBus events,
            ConfigManager configs,
            SaveManager saves,
            UIManager ui,
            SceneFlowManager scenes)
        {
            Events = events;
            Configs = configs;
            Saves = saves;
            UI = ui;
            Scenes = scenes;
        }

        public EventBus Events { get; }
        public ConfigManager Configs { get; }
        public SaveManager Saves { get; }
        public UIManager UI { get; }
        public SceneFlowManager Scenes { get; }
    }
}
