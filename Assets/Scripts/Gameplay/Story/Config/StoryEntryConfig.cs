using UnityEngine;

namespace MemorialArchive.Gameplay.Story.Config
{
    public enum StoryPresentationType
    {
        BlackScreen,
        GalgameTextBox,
        Note,
        DialogueScene
    }

    [CreateAssetMenu(menuName = "Memorial Archive/Config/Story Entry")]
    public sealed class StoryEntryConfig : ScriptableObject
    {
        [SerializeField] private string storyId;
        [SerializeField] private StoryPresentationType presentationType = StoryPresentationType.BlackScreen;
        [TextArea(3, 8)]
        [SerializeField] private string content;
        [SerializeField] private bool returnToMainMenuWhenFinished;
        [SerializeField] private string dialogueId;
        [SerializeField] private string dialogueSceneId;
        [SerializeField] private string dialogueSceneSpawnPointId;

        public string StoryId => storyId;
        public StoryPresentationType PresentationType => presentationType;
        public string Content => content;
        public bool ReturnToMainMenuWhenFinished => returnToMainMenuWhenFinished;
        public string DialogueId => dialogueId;
        public string DialogueSceneId => dialogueSceneId;
        public string DialogueSceneSpawnPointId => dialogueSceneSpawnPointId;
    }
}
