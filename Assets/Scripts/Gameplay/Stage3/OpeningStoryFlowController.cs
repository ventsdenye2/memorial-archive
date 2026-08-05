using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Stage1;
using MemorialArchive.Gameplay.Story.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Stage3
{
    public sealed class OpeningStoryFlowController : MonoBehaviour
    {
        [SerializeField] private string storyId = Stage1Ids.OpeningStory;
        private bool started;

        private void OnEnable()
        {
            StartCoroutine(StartWhenReady());
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<BlackScreenStoryFinishedEvent>(HandleStoryFinished);
        }

        private IEnumerator StartWhenReady()
        {
            // ScenePanelRegistry registers during Awake; one frame guarantees the
            // persistent GameRoot and the story panel are both ready.
            yield return null;
            while (GameRoot.Instance == null || GameRoot.Instance.Context == null)
            {
                yield return null;
            }

            if (started)
            {
                yield break;
            }

            started = true;
            GameRoot.Instance.Context.Events.Subscribe<BlackScreenStoryFinishedEvent>(HandleStoryFinished);
            var storySystem = GameRoot.Instance.GetSystem<StorySystem>();
            if (storySystem == null)
            {
                Debug.LogError("OpeningStoryFlowController cannot find StorySystem.");
                yield break;
            }

            storySystem.PlayStory(storyId);
        }

        private void HandleStoryFinished(BlackScreenStoryFinishedEvent evt)
        {
            if (evt.StoryId != storyId)
            {
                return;
            }

            var context = GameRoot.Instance?.Context;
            if (context == null)
            {
                return;
            }

            context.UI.Close(PanelId.BlackScreenStory);
            context.Events.Publish(new SceneTransitionRequestedEvent(
                Stage3Ids.FirstCorridorSceneName,
                Stage3Ids.FirstCorridorSpawnPointId));
        }
    }
}
