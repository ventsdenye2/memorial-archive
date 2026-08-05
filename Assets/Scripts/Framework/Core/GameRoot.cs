using System.Collections.Generic;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Combat.Logic;
using MemorialArchive.Gameplay.Interaction.Logic;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Item.Logic;
using MemorialArchive.Gameplay.Monster.Logic;
using MemorialArchive.Gameplay.Puzzle.Logic;
using MemorialArchive.Gameplay.Story.Logic;
using MemorialArchive.Gameplay.Dialogue.Logic;
using UnityEngine;

namespace MemorialArchive.Framework.Core
{
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameConfigDatabase configDatabase;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly List<IGameSystem> systems = new List<IGameSystem>();
        private readonly List<ITickableSystem> tickableSystems = new List<ITickableSystem>();
        private bool initialized;

        public static GameRoot Instance { get; private set; }
        public GameContext Context { get; private set; }

        public T GetSystem<T>() where T : class
        {
            foreach (var system in systems)
            {
                if (system is T typedSystem)
                {
                    return typedSystem;
                }
            }

            return null;
        }

        public void ResetForNewGame()
        {
            if (!initialized)
            {
                return;
            }

            foreach (var system in systems)
            {
                if (system is INewGameResettable resettable)
                {
                    resettable.ResetForNewGame();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeSystems();
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            foreach (var tickable in tickableSystems)
            {
                tickable.Tick(deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            for (var i = systems.Count - 1; i >= 0; i--)
            {
                systems[i].Dispose();
            }

            systems.Clear();
            tickableSystems.Clear();
            Context?.Events.Clear();
            Context = null;
            Instance = null;
            initialized = false;
        }

        private void InitializeSystems()
        {
            if (initialized)
            {
                return;
            }

            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }

            var events = new EventBus();
            var configs = new ConfigManager(configDatabase);
            var saves = new SaveManager();
            var scenes = new SceneFlowManager();
            Context = new GameContext(events, configs, saves, uiManager, scenes);

            RegisterSystem(configs);
            RegisterSystem(saves);
            RegisterSystem(scenes);
            if (uiManager != null)
            {
                RegisterSystem(uiManager);
            }

            RegisterSystem(new CharacterSystem());
            RegisterSystem(new InventorySystem());
            RegisterSystem(new ItemEffectSystem());
            RegisterSystem(new CombatSystem());
            RegisterSystem(new InteractionSystem());
            RegisterSystem(new MonsterSystem());
            RegisterSystem(new PuzzleSystem());
            RegisterSystem(new StorySystem());
            RegisterSystem(new DialogueSystem());

            foreach (var system in systems)
            {
                system.Initialize(Context);
                if (system is ITickableSystem tickableSystem)
                {
                    tickableSystems.Add(tickableSystem);
                }

                if (system is ISaveModule saveModule)
                {
                    saves.RegisterModule(saveModule);
                }
            }

            initialized = true;
        }

        private void RegisterSystem(IGameSystem system)
        {
            if (system != null && !systems.Contains(system))
            {
                systems.Add(system);
            }
        }
    }
}
