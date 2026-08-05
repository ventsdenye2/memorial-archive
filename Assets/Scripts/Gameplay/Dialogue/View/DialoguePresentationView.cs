using System;
using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Dialogue.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Dialogue.View
{
    public sealed class DialoguePresentationView : MonoBehaviour
    {
        [Serializable]
        private sealed class PortraitSlot
        {
            [SerializeField] private string slotId;
            [NonSerialized] private string currentCharacterId;
            [SerializeField] private Image portraitImage;
            [SerializeField] private Image dimOverlay;

            public string SlotId => slotId;
            public Image PortraitImage => portraitImage;
            public Image DimOverlay => dimOverlay;
            public string CurrentCharacterId { get => currentCharacterId; set => currentCharacterId = value; }
        }

        [SerializeField] private Text dialogueText;
        [SerializeField] private Image cgImage;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private AudioSource effectAudioSource;
        [SerializeField] private PortraitSlot[] portraitSlots = Array.Empty<PortraitSlot>();
        [SerializeField, Range(0f, 1f)] private float inactivePortraitOverlayAlpha = 0.55f;
        [SerializeField] private bool dimAllPortraitsForNarration;
        [SerializeField] private bool useTypewriter = true;
        [SerializeField, Min(1f)] private float charactersPerSecond = 36f;

        private bool subscribed;
        private Coroutine typewriterRoutine;
        private Coroutine releaseTypewriterRoutine;
        private bool isTyping;
        private string typewriterText;
        private string typewriterDialogueId;
        private int typewriterNodeIndex = -1;

        private void OnEnable()
        {
            subscribed = false;
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                GameRoot.Instance?.Context?.Events.Unsubscribe<DialogueNodePresentedEvent>(HandleNodePresented);
                GameRoot.Instance?.Context?.Events.Unsubscribe<DialogueAdvancePressedEvent>(HandleAdvancePressed);
                subscribed = false;
            }

            StopAllCoroutines();
            typewriterRoutine = null;
            releaseTypewriterRoutine = null;
            isTyping = false;
            SetFlashAlpha(0f);
        }

        private IEnumerator SubscribeWhenReady()
        {
            while (GameRoot.Instance == null || GameRoot.Instance.Context == null)
            {
                yield return null;
            }

            GameRoot.Instance.Context.Events.Subscribe<DialogueNodePresentedEvent>(HandleNodePresented);
            GameRoot.Instance.Context.Events.Subscribe<DialogueAdvancePressedEvent>(HandleAdvancePressed);
            subscribed = true;
        }

        private void HandleNodePresented(DialogueNodePresentedEvent evt)
        {
            var node = evt.Node;
            if (node == null)
            {
                return;
            }

            if (dialogueText != null)
            {
                StartTypewriter(node);
            }

            ApplyCg(node);
            ApplyPortraits(node);

            if (node.Effects != null && node.Effects.Length > 0)
            {
                StartCoroutine(PlayEffects(node));
            }
        }

        private void ApplyCg(DialogueNodeData node)
        {
            if (cgImage == null || node.CgCommand == Config.DialogueCgCommand.Keep)
            {
                return;
            }

            if (node.CgCommand == Config.DialogueCgCommand.Clear)
            {
                cgImage.sprite = null;
                cgImage.gameObject.SetActive(false);
                return;
            }

            cgImage.sprite = node.CgSprite;
            cgImage.gameObject.SetActive(node.CgSprite != null);
        }

        private void ApplyPortraits(DialogueNodeData node)
        {
            foreach (var portrait in node.Portraits)
            {
                var slot = FindSlot(portrait.SlotId);
                if (slot == null || slot.PortraitImage == null)
                {
                    continue;
                }

                slot.PortraitImage.sprite = portrait.Portrait;
                slot.PortraitImage.color = portrait.Portrait != null ? Color.white : portrait.PlaceholderColor;
                slot.PortraitImage.gameObject.SetActive(portrait.Visible);
                slot.CurrentCharacterId = portrait.CharacterId;
            }

            foreach (var slot in portraitSlots)
            {
                if (slot != null && slot.PortraitImage != null)
                {
                    SetDimmed(slot.DimOverlay, slot.PortraitImage.gameObject.activeSelf && ShouldDimCharacter(node, slot.CurrentCharacterId));
                }
            }
        }

        private bool ShouldDimPortrait(DialogueNodeData node, DialoguePortraitData portrait)
        {
            return string.IsNullOrEmpty(node.SpeakerId)
                ? dimAllPortraitsForNarration
                : portrait.CharacterId != node.SpeakerId;
        }

        private bool ShouldDimCharacter(DialogueNodeData node, string characterId)
        {
            return string.IsNullOrEmpty(node.SpeakerId)
                ? dimAllPortraitsForNarration
                : characterId != node.SpeakerId;
        }

        private PortraitSlot FindSlot(string slotId)
        {
            foreach (var slot in portraitSlots)
            {
                if (slot != null && slot.SlotId == slotId)
                {
                    return slot;
                }
            }

            return null;
        }

        private IEnumerator PlayEffects(DialogueNodeData node)
        {
            foreach (var effect in node.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (effectAudioSource != null && effect.AudioClip != null)
                {
                    effectAudioSource.PlayOneShot(effect.AudioClip);
                }

                yield return PlayFlash(effect);
            }

            if (node.BlocksAdvance)
            {
                GameRoot.Instance?.Context?.Events.Publish(
                    new DialogueBlockingEffectFinishedEvent(node.DialogueId, node.NodeIndex));
            }
        }

        private IEnumerator PlayFlash(Config.DialogueEffectConfig effect)
        {
            if (flashOverlay == null || effect.FlashCount <= 0)
            {
                yield break;
            }

            flashOverlay.color = effect.ScreenFlashColor;
            for (var i = 0; i < effect.FlashCount; i++)
            {
                yield return FadeFlash(0f, 1f, effect.FlashInDuration);
                yield return FadeFlash(1f, 0f, effect.FlashOutDuration);
            }
        }

        private IEnumerator FadeFlash(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetFlashAlpha(to);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFlashAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetFlashAlpha(to);
        }

        private void HandleAdvancePressed(DialogueAdvancePressedEvent evt)
        {
            if (!isTyping)
            {
                return;
            }

            CompleteTypewriter();
            if (releaseTypewriterRoutine == null)
            {
                releaseTypewriterRoutine = StartCoroutine(ReleaseTypewriterState());
            }
        }

        private void StartTypewriter(DialogueNodeData node)
        {
            if (typewriterRoutine != null)
            {
                StopCoroutine(typewriterRoutine);
                typewriterRoutine = null;
            }

            if (releaseTypewriterRoutine != null)
            {
                StopCoroutine(releaseTypewriterRoutine);
                releaseTypewriterRoutine = null;
            }

            typewriterText = node.Text ?? string.Empty;
            typewriterDialogueId = node.DialogueId;
            typewriterNodeIndex = node.NodeIndex;
            isTyping = useTypewriter && typewriterText.Length > 0;

            if (!isTyping)
            {
                if (dialogueText != null)
                {
                    dialogueText.text = typewriterText;
                }
                PublishTypewriterState(false);
                return;
            }

            if (dialogueText != null)
            {
                dialogueText.text = string.Empty;
            }
            PublishTypewriterState(true);
            typewriterRoutine = StartCoroutine(TypewriterRoutine());
        }

        private IEnumerator TypewriterRoutine()
        {
            var visibleCount = 0;
            while (visibleCount < typewriterText.Length && isTyping)
            {
                visibleCount++;
                if (dialogueText != null)
                {
                    dialogueText.text = typewriterText.Substring(0, visibleCount);
                }
                yield return new WaitForSecondsRealtime(1f / Mathf.Max(1f, charactersPerSecond));
            }

            CompleteTypewriter();
            typewriterRoutine = null;
            PublishTypewriterState(false);
        }

        private void CompleteTypewriter()
        {
            if (typewriterRoutine != null)
            {
                StopCoroutine(typewriterRoutine);
                typewriterRoutine = null;
            }

            isTyping = false;
            if (dialogueText != null)
            {
                dialogueText.text = typewriterText;
            }
        }

        private IEnumerator ReleaseTypewriterState()
        {
            yield return null;
            releaseTypewriterRoutine = null;
            PublishTypewriterState(false);
        }

        private void PublishTypewriterState(bool typing)
        {
            GameRoot.Instance?.Context?.Events.Publish(
                new DialogueTypewriterStateChangedEvent(typewriterDialogueId, typewriterNodeIndex, typing));
        }

        private void SetDimmed(Image overlay, bool dimmed)
        {
            if (overlay == null)
            {
                return;
            }

            var color = overlay.color;
            color.a = dimmed ? inactivePortraitOverlayAlpha : 0f;
            overlay.color = color;
            overlay.gameObject.SetActive(dimmed);
        }

        private void SetFlashAlpha(float alpha)
        {
            if (flashOverlay == null)
            {
                return;
            }

            var color = flashOverlay.color;
            color.a = alpha;
            flashOverlay.color = color;
            flashOverlay.gameObject.SetActive(alpha > 0f);
        }
    }
}
