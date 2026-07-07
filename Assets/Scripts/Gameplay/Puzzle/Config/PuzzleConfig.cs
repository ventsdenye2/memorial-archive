using UnityEngine;

namespace MemorialArchive.Gameplay.Puzzle.Config
{
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Puzzle")]
    public sealed class PuzzleConfig : ScriptableObject
    {
        [SerializeField] private string puzzleId;
        [SerializeField] private string displayName;
        [SerializeField] private string requiredItemTag;
        [SerializeField] private string solvedStoryId;
        [SerializeField] private string unlockedInteractionId;

        public string PuzzleId => puzzleId;
        public string DisplayName => displayName;
        public string RequiredItemTag => requiredItemTag;
        public string SolvedStoryId => solvedStoryId;
        public string UnlockedInteractionId => unlockedInteractionId;
    }
}
