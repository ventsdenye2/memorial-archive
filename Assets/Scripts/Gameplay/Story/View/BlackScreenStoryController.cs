using System;
using System.Collections;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Story.View
{
    /// <summary>
    /// Black-screen story presenter. The configured story uses ▼ as an authored
    /// paragraph break; each paragraph is revealed and advanced independently.
    /// </summary>
    public sealed class BlackScreenStoryController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Text storyText;
        [SerializeField] private Text continueHint;
        [SerializeField] private float charactersPerSecond = 36f;

        private readonly List<string> segments = new List<string>();
        private string activeStoryId;
        private string fullText;
        private int segmentIndex;
        private bool isRevealing;
        private Coroutine revealRoutine;
        private int lastAdvanceFrame = -1;

        private void Awake()
        {
            if (storyText == null)
            {
                storyText = GetComponentInChildren<Text>(true);
            }

            if (continueHint == null)
            {
                var hint = transform.Find("StoryFrame/ContinueHint");
                continueHint = hint != null ? hint.GetComponent<Text>() : null;
            }
        }

        private void OnEnable()
        {
            GameRoot.Instance?.Context?.Events.Subscribe<BlackScreenStoryStartedEvent>(HandleBlackScreenStoryStarted);
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<BlackScreenStoryStartedEvent>(HandleBlackScreenStoryStarted);
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(activeStoryId) && Input.anyKeyDown)
            {
                Advance();
            }
        }

        public void OnPointerClick(PointerEventData eventData) => Advance();

        private void HandleBlackScreenStoryStarted(BlackScreenStoryStartedEvent evt)
        {
            var config = GameRoot.Instance?.Context?.Configs.GetStory(evt.StoryId);
            activeStoryId = evt.StoryId;
            segments.Clear();
            segmentIndex = 0;

            if (config != null && !string.IsNullOrWhiteSpace(config.Content))
            {
                foreach (var segment in config.Content.Split(new[] { '▼' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = segment.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        segments.Add(trimmed);
                    }
                }
            }

            if (segments.Count == 0)
            {
                segments.Add("……");
            }

            BeginCurrentSegment();
        }

        private void BeginCurrentSegment()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
            }

            fullText = segments[segmentIndex];
            isRevealing = false;
            revealRoutine = null;
            lastAdvanceFrame = -1;
            if (storyText != null)
            {
                storyText.text = string.Empty;
            }

            SetContinueHint(false);
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

                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1f, charactersPerSecond));
            }

            isRevealing = false;
            revealRoutine = null;
            SetContinueHint(true);
        }

        private void FinishRevealImmediately()
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

            SetContinueHint(true);
        }

        private void Advance()
        {
            if (string.IsNullOrEmpty(activeStoryId) || lastAdvanceFrame == Time.frameCount)
            {
                return;
            }

            lastAdvanceFrame = Time.frameCount;
            if (isRevealing)
            {
                FinishRevealImmediately();
                return;
            }

            if (segmentIndex < segments.Count - 1)
            {
                segmentIndex++;
                BeginCurrentSegment();
                return;
            }

            FinishStory();
        }

        private void FinishStory()
        {
            var finishedStoryId = activeStoryId;
            activeStoryId = null;
            fullText = null;
            segments.Clear();
            if (storyText != null)
            {
                storyText.text = string.Empty;
            }

            SetContinueHint(false);
            GameRoot.Instance?.Context?.Events.Publish(new BlackScreenStoryFinishedEvent(finishedStoryId));
        }

        private void SetContinueHint(bool visible)
        {
            if (continueHint != null)
            {
                continueHint.gameObject.SetActive(visible);
            }
        }
    }
}
