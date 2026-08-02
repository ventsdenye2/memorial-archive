using MemorialArchive.Gameplay.Interaction.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Interaction.Config
{
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Interaction")]
    public sealed class InteractionConfig : ScriptableObject
    {
        [SerializeField] private string interactionId;
        [SerializeField] private InteractionType interactionType = InteractionType.Inspect;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneId;
        [SerializeField] private string containerId;
        [SerializeField] private string puzzleId;
        [SerializeField] private string storyId;
        [SerializeField] private string noteId;
        [SerializeField] private string transitionSceneId;
        [SerializeField] private string transitionSpawnPointId;
        [SerializeField] private string stairPrompt = "请选择前往楼层";
        [SerializeField] private string stairUpSceneId;
        [SerializeField] private string stairUpSpawnPointId;
        [SerializeField] private string stairDownSceneId;
        [SerializeField] private string stairDownSpawnPointId;

        public string InteractionId => interactionId;
        public InteractionType InteractionType => interactionType;
        public string DisplayName => displayName;
        public string SceneId => sceneId;
        public string ContainerId => containerId;
        public string PuzzleId => puzzleId;
        public string StoryId => storyId;
        public string NoteId => noteId;
        public string TransitionSceneId => transitionSceneId;
        public string TransitionSpawnPointId => transitionSpawnPointId;
        public string StairPrompt => stairPrompt;
        public string StairUpSceneId => stairUpSceneId;
        public string StairUpSpawnPointId => stairUpSpawnPointId;
        public string StairDownSceneId => stairDownSceneId;
        public string StairDownSpawnPointId => stairDownSpawnPointId;
        public bool HasStairDestinations =>
            !string.IsNullOrEmpty(stairUpSceneId) || !string.IsNullOrEmpty(stairDownSceneId);
    }
}
