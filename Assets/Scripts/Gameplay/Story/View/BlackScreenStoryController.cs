using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Story.View
{
    public sealed class BlackScreenStoryController : MonoBehaviour
    {
        [SerializeField] private Text storyText;
        [SerializeField] private float charactersPerSecond = 18f;

        private string activeStoryId;
        private string fullText;
        private bool isRevealing;
        private Coroutine revealRoutine;

        private void OnEnable()
        {
            if (GameRoot.Instance != null && GameRoot.Instance.Context != null)
            {
                GameRoot.Instance.Context.Events.Subscribe<BlackScreenStoryStartedEvent>(HandleBlackScreenStoryStarted);
            }
        }

        private void OnDisable()
        {
            if (GameRoot.Instance != null && GameRoot.Instance.Context != null)
            {
                GameRoot.Instance.Context.Events.Unsubscribe<BlackScreenStoryStartedEvent>(HandleBlackScreenStoryStarted);
            }
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(activeStoryId) || !Input.anyKeyDown)
            {
                return;
            }

            if (isRevealing)
            {
                FinishReveal();
                return;
            }

            FinishStory();
        }

        private void HandleBlackScreenStoryStarted(BlackScreenStoryStartedEvent evt)
        {
            var config = GameRoot.Instance.Context.Configs.GetStory(evt.StoryId);
            activeStoryId = evt.StoryId;
            fullText = config != null ? config.Content : string.Empty;

            if (storyText != null)
            {
                storyText.text = string.Empty;
            }

            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
            }

            revealRoutine = StartCoroutine(RevealText());
        }

        private IEnumerator RevealText()
        {
            isRevealing = true;
            var visibleCount = 0;
            while (visibleCount < fullText.Length)
            {
                visibleCount++;
                if (storyText != null)
                {
                    storyText.text = fullText.Substring(0, visibleCount);
                }

                yield return new WaitForSeconds(1f / Mathf.Max(1f, charactersPerSecond));
            }

            FinishReveal();
        }

        private void FinishReveal()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            isRevealing = false;
            if (storyText != null)
            {
                storyText.text = fullText;
            }
        }

        private void FinishStory()
        {
            var finishedStoryId = activeStoryId;
            activeStoryId = null;
            fullText = null;
            if (storyText != null)
            {
                storyText.text = string.Empty;
            }

            GameRoot.Instance.Context.Events.Publish(new BlackScreenStoryFinishedEvent(finishedStoryId));
        }
    }
}
