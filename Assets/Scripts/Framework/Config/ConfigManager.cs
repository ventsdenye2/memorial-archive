using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Character.Config;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Monster.Config;
using MemorialArchive.Gameplay.Puzzle.Config;
using MemorialArchive.Gameplay.Story.Config;

namespace MemorialArchive.Framework.Config
{
    public sealed class ConfigManager : IGameSystem
    {
        private readonly GameConfigDatabase database;
        private readonly Dictionary<int, ItemConfig> items = new Dictionary<int, ItemConfig>();
        private readonly Dictionary<int, CharacterAttributeConfig> characterAttributes = new Dictionary<int, CharacterAttributeConfig>();
        private readonly Dictionary<int, CharacterStateConfig> characterStates = new Dictionary<int, CharacterStateConfig>();
        private readonly Dictionary<int, MonsterConfig> monsters = new Dictionary<int, MonsterConfig>();
        private readonly Dictionary<string, InteractionConfig> interactions = new Dictionary<string, InteractionConfig>();
        private readonly Dictionary<string, PuzzleConfig> puzzles = new Dictionary<string, PuzzleConfig>();
        private readonly Dictionary<string, StoryEntryConfig> stories = new Dictionary<string, StoryEntryConfig>();
        private readonly Dictionary<string, GuideSequenceConfig> guideSequences = new Dictionary<string, GuideSequenceConfig>();

        public ConfigManager(GameConfigDatabase database)
        {
            this.database = database;
        }

        public void Initialize(GameContext context)
        {
            RebuildIndexes();
        }

        public void Dispose()
        {
            items.Clear();
            characterAttributes.Clear();
            characterStates.Clear();
            monsters.Clear();
            interactions.Clear();
            puzzles.Clear();
            stories.Clear();
            guideSequences.Clear();
        }

        public ItemConfig GetItem(int itemId) => items.TryGetValue(itemId, out var config) ? config : null;
        public GuideSequenceConfig GetGuideSequence(string id) => !string.IsNullOrEmpty(id) && guideSequences.TryGetValue(id, out var config) ? config : null;
        public CharacterAttributeConfig GetCharacterAttribute(int id) => characterAttributes.TryGetValue(id, out var config) ? config : null;
        public CharacterStateConfig GetCharacterState(int id) => characterStates.TryGetValue(id, out var config) ? config : null;
        public MonsterConfig GetMonster(int monsterId) => monsters.TryGetValue(monsterId, out var config) ? config : null;
        public InteractionConfig GetInteraction(string id) => !string.IsNullOrEmpty(id) && interactions.TryGetValue(id, out var config) ? config : null;
        public PuzzleConfig GetPuzzle(string id) => !string.IsNullOrEmpty(id) && puzzles.TryGetValue(id, out var config) ? config : null;
        public StoryEntryConfig GetStory(string id) => !string.IsNullOrEmpty(id) && stories.TryGetValue(id, out var config) ? config : null;

        private void RebuildIndexes()
        {
            items.Clear();
            characterAttributes.Clear();
            characterStates.Clear();
            monsters.Clear();
            interactions.Clear();
            puzzles.Clear();
            stories.Clear();
            guideSequences.Clear();

            if (database == null)
            {
                return;
            }

            AddAll(database.Items, items, item => item.ItemId, "ItemConfig");
            AddAll(database.CharacterAttributes, characterAttributes, config => config.AttributeId, "CharacterAttributeConfig");
            AddAll(database.CharacterStates, characterStates, config => config.StateId, "CharacterStateConfig");
            AddAll(database.Monsters, monsters, monster => monster.MonsterId, "MonsterConfig");
            AddAll(database.Interactions, interactions, interaction => interaction.InteractionId, "InteractionConfig");
            AddAll(database.Puzzles, puzzles, puzzle => puzzle.PuzzleId, "PuzzleConfig");
            AddAll(database.Stories, stories, story => story.StoryId, "StoryEntryConfig");
            AddAll(database.GuideSequences, guideSequences, sequence => sequence.SequenceId, "GuideSequenceConfig");
        }

        private static void AddAll<TKey, TValue>(IEnumerable<TValue> values, IDictionary<TKey, TValue> target, System.Func<TValue, TKey> keySelector, string label)
            where TValue : UnityEngine.Object
        {
            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }

                var key = keySelector(value);
                if (EqualityComparer<TKey>.Default.Equals(key, default))
                {
                    UnityEngine.Debug.LogError($"{label} '{value.name}' has an empty/default ID.");
                    continue;
                }

                if (target.ContainsKey(key))
                {
                    UnityEngine.Debug.LogError($"Duplicate {label} ID '{key}'. Keep IDs unique before integration.");
                    continue;
                }

                target.Add(key, value);
            }
        }
    }
}
