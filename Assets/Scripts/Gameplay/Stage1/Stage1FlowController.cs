using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Story.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Stage1
{
    public sealed class Stage1FlowController : MonoBehaviour
    {
        [SerializeField] private GameObject blackScreenStoryRoot;
        [SerializeField] private GameObject stage1DemoRoot;
        [SerializeField] private string openingStoryId = Stage1Ids.OpeningStory;

        private bool started;

        private void OnEnable()
        {
            StartCoroutine(StartWhenGameRootReady());
        }

        private void OnDisable()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events != null)
            {
                events.Unsubscribe<BlackScreenStoryFinishedEvent>(HandleStoryFinished);
            }
        }

        private IEnumerator StartWhenGameRootReady()
        {
            // Let all scene registries finish Start() before opening a panel.
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

            if (stage1DemoRoot != null)
            {
                stage1DemoRoot.SetActive(false);
            }

            if (blackScreenStoryRoot != null)
            {
                blackScreenStoryRoot.SetActive(true);
            }

            GameRoot.Instance.Context.UI.Close(PanelId.Hud);
            var storySystem = GameRoot.Instance.GetSystem<StorySystem>();
            if (storySystem == null)
            {
                Debug.LogError("Stage1FlowController cannot find StorySystem.");
                yield break;
            }

            storySystem.PlayStory(openingStoryId);
        }

        private void HandleStoryFinished(BlackScreenStoryFinishedEvent evt)
        {
            if (evt.StoryId != openingStoryId)
            {
                return;
            }

            GameRoot.Instance.Context.UI.Close(PanelId.BlackScreenStory);
            if (blackScreenStoryRoot != null)
            {
                blackScreenStoryRoot.SetActive(false);
            }

            if (stage1DemoRoot != null)
            {
                stage1DemoRoot.SetActive(true);
            }

            GameRoot.Instance.Context.UI.Open(PanelId.Hud);
            GameRoot.Instance.Context.Events.Publish(new Stage1GameplayStartedEvent());
        }
    }
}
