using MemorialArchive.Gameplay.Character.Config;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Monster.Config;
using MemorialArchive.Gameplay.Puzzle.Config;
using MemorialArchive.Gameplay.Story.Config;
using MemorialArchive.Gameplay.Dialogue.Config;
using UnityEngine;
using MemorialArchive.Framework.Scene;

namespace MemorialArchive.Framework.Config
{
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Game Config Database")]
    public sealed class GameConfigDatabase : ScriptableObject
    {
        [SerializeField] private ItemConfig[] items;
        [SerializeField] private CharacterAttributeConfig[] characterAttributes;
        [SerializeField] private CharacterStateConfig[] characterStates;
        [SerializeField] private MonsterConfig[] monsters;
        [SerializeField] private InteractionConfig[] interactions;
        [SerializeField] private PuzzleConfig[] puzzles;
        [SerializeField] private StoryEntryConfig[] stories;
        [SerializeField] private GuideSequenceConfig[] guideSequences;
        [SerializeField] private DialogueSequenceConfig[] dialogueSequences;
        [SerializeField] private DialogueEffectConfig[] dialogueEffects;
        [SerializeField] private LightingGlobalConfig lightingGlobal;
        [SerializeField] private LightSourceConfig[] lightSources;
        [SerializeField] private SceneAccessRule[] sceneAccessRules;

        public ItemConfig[] Items => items;
        public CharacterAttributeConfig[] CharacterAttributes => characterAttributes;
        public CharacterStateConfig[] CharacterStates => characterStates;
        public MonsterConfig[] Monsters => monsters;
        public InteractionConfig[] Interactions => interactions;
        public PuzzleConfig[] Puzzles => puzzles;
        public StoryEntryConfig[] Stories => stories;
        public GuideSequenceConfig[] GuideSequences => guideSequences;
        public DialogueSequenceConfig[] DialogueSequences => dialogueSequences;
        public DialogueEffectConfig[] DialogueEffects => dialogueEffects;
        public LightingGlobalConfig LightingGlobal => lightingGlobal;
        public LightSourceConfig[] LightSources => lightSources;
        public SceneAccessRule[] SceneAccessRules => sceneAccessRules;
    }
}
