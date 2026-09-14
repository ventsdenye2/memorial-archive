using System;
using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
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
            public Sprite SpeakingSprite { get; set; }
            public Sprite InactiveSprite { get; set; }
            public Sprite Nameplate { get; set; }

            public string SlotId => slotId;
            public Image PortraitImage => portraitImage;
            public Image DimOverlay => dimOverlay;
            public string CurrentCharacterId { get => currentCharacterId; set => currentCharacterId = value; }
        }

        [SerializeField] private Text dialogueText;
        [SerializeField] private Text specialText;
        [SerializeField] private Image cgImage;
        [SerializeField] private bool fillCgViewport;
        [SerializeField] private Image speakerNameImage;
        [SerializeField] private Image dialogueFrame;
        [SerializeField] private Sprite narrationFrameSprite;
        [SerializeField] private Sprite spokenFrameSprite;
        [SerializeField] private bool useNativePortraitSize;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private AudioSource effectAudioSource;
        [SerializeField] private PortraitSlot[] portraitSlots = Array.Empty<PortraitSlot>();
        [SerializeField, Range(0f, 1f)] private float inactivePortraitOverlayAlpha = 0.55f;
        [SerializeField] private bool dimAllPortraitsForNarration;
        [Header("Optional HUD narration filter")]
        [SerializeField] private string dialogueIdFilter;
        [SerializeField] private bool narrationOnly;
        [SerializeField] private BasePanel requiredOpenPanel;
        [SerializeField] private GameObject presentationRoot;
        [Header("Dialogue text")]
        [SerializeField, Min(1)] private int dialogueFontSize = 30;
        [SerializeField] private bool useAuthoredTextStyles;
        [SerializeField] private Font narrationFont;
        [SerializeField] private Font speechFont;
        [SerializeField, Min(1)] private int narrationFontSize = 32;
        [SerializeField, Min(1)] private int speechFontSize = 40;
        [SerializeField] private bool useTypewriter = true;
        [SerializeField, Min(1f)] private float charactersPerSecond = 36f;

        private bool subscribed;
        private Coroutine typewriterRoutine;
        private bool isTyping;
        private string typewriterText;
        private string typewriterDialogueId;
        private int typewriterNodeIndex = -1;
        private Config.DialogueTextPresentation currentTextPresentation = Config.DialogueTextPresentation.Narration;
        private static readonly Color QuotationTextColor = Color.white;
        private const float LeftPortraitX = -544.5f;
        private const float RightPortraitX = 588.5f;

        private void Awake()
        {
            ApplyDialogueTextStyle();
            ApplyCgLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyDialogueTextStyle();
        }
#endif

        private void OnEnable()
        {
            ApplyDialogueTextStyle();
            subscribed = false;
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                GameRoot.Instance?.Context?.Events.Unsubscribe<DialogueNodePresentedEvent>(HandleNodePresented);
                GameRoot.Instance?.Context?.Events.Unsubscribe<DialogueTypewriterCompletionRequestedEvent>(HandleTypewriterCompletionRequested);
                GameRoot.Instance?.Context?.Events.Unsubscribe<DialogueFinishedEvent>(HandleDialogueFinished);
                subscribed = false;
            }

            StopAllCoroutines();
            typewriterRoutine = null;
            isTyping = false;
            SetFlashAlpha(0f);
            HidePresentationRoot();
        }

        private void LateUpdate()
        {
            if (presentationRoot != null && requiredOpenPanel != null && !requiredOpenPanel.IsOpen)
            {
                HidePresentationRoot();
            }
        }

        private IEnumerator SubscribeWhenReady()
        {
            while (GameRoot.Instance == null || GameRoot.Instance.Context == null)
            {
                yield return null;
            }

            GameRoot.Instance.Context.Events.Subscribe<DialogueNodePresentedEvent>(HandleNodePresented);
            GameRoot.Instance.Context.Events.Subscribe<DialogueTypewriterCompletionRequestedEvent>(HandleTypewriterCompletionRequested);
            GameRoot.Instance.Context.Events.Subscribe<DialogueFinishedEvent>(HandleDialogueFinished);
            subscribed = true;
        }

        private void HandleNodePresented(DialogueNodePresentedEvent evt)
        {
            var node = evt.Node;
            if (!ShouldPresentNode(node))
            {
                if (ShouldHideForIgnoredNode(node))
                {
                    StopTypewriterForIgnoredNode(node);
                    HidePresentationRoot();
                }

                return;
            }

            if (presentationRoot != null)
            {
                presentationRoot.SetActive(true);
            }

            if (dialogueText != null)
            {
                currentTextPresentation = ResolveTextPresentation(node);
                currentCenterText = node.CenterText;
                ApplyDialogueTextStyle();
                StartTypewriter(node);
            }

            ApplyCg(node);
            ApplyCgTint(ResolveTextPresentation(node) == Config.DialogueTextPresentation.Speech);
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
            ApplyCgLayout();
            cgImage.gameObject.SetActive(node.CgSprite != null);
        }

        private void ApplyCgLayout()
        {
            if (!fillCgViewport || cgImage == null) return;
            // Cover the viewport at any Game View aspect ratio without stretching
            // the 1920x1080 art or introducing pillarbox/letterbox margins.
            cgImage.preserveAspect = false;
            var fitter = cgImage.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = cgImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectRatio = cgImage.sprite != null
                ? cgImage.sprite.rect.width / cgImage.sprite.rect.height : 16f / 9f;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private static Config.DialogueTextPresentation ResolveTextPresentation(DialogueNodeData node)
        {
            if (node.TextPresentation != Config.DialogueTextPresentation.Automatic) return node.TextPresentation;
            // Opening prose uses the protagonist as its authoring voice, but it is
            // still narration unless the node carries an explicit portrait snapshot.
            return string.IsNullOrEmpty(node.SpeakerId) || node.Portraits.Length == 0
                ? Config.DialogueTextPresentation.Narration : Config.DialogueTextPresentation.Speech;
        }

        private void ApplyCgTint(bool spoken)
        {
            if (cgImage == null) return;
            cgImage.color = spoken ? new Color(.55f, .55f, .55f, 1f) : Color.white;
        }

        private void ApplyPortraits(DialogueNodeData node)
        {
            // A node owns the complete portrait state. This prevents an environment
            // description from leaking the previous speaker into the next shot.
            foreach (var slot in portraitSlots)
            {
                if (slot == null || slot.PortraitImage == null) continue;
                slot.CurrentCharacterId = string.Empty;
                slot.SpeakingSprite = null;
                slot.InactiveSprite = null;
                slot.Nameplate = null;
                slot.PortraitImage.sprite = null;
                slot.PortraitImage.gameObject.SetActive(false);
                SetDimmed(slot.DimOverlay, false);
                var position = slot.SlotId == "left" ? LeftPortraitX : RightPortraitX;
                slot.PortraitImage.rectTransform.anchoredPosition =
                    new Vector2(position, slot.PortraitImage.rectTransform.anchoredPosition.y);
            }

            foreach (var portrait in node.Portraits)
            {
                var slot = FindSlot(portrait.SlotId);
                if (slot == null || slot.PortraitImage == null)
                {
                    continue;
                }

                slot.PortraitImage.sprite = portrait.Portrait;
                slot.SpeakingSprite = portrait.Portrait;
                slot.InactiveSprite = portrait.InactivePortrait;
                slot.Nameplate = portrait.Nameplate;
                slot.PortraitImage.color = portrait.Portrait != null ? Color.white : portrait.PlaceholderColor;
                slot.PortraitImage.gameObject.SetActive(portrait.Visible);
                slot.CurrentCharacterId = portrait.CharacterId;
            }

            Sprite speakerNameplate = null;
            foreach (var slot in portraitSlots)
            {
                if (slot != null && slot.PortraitImage != null)
                {
                    bool visible = slot.PortraitImage.gameObject.activeSelf;
                    bool dimmed = visible && ShouldDimCharacter(node, slot.CurrentCharacterId);
                    if (slot.SpeakingSprite != null)
                    {
                        slot.PortraitImage.sprite = dimmed && slot.InactiveSprite != null
                            ? slot.InactiveSprite : slot.SpeakingSprite;
                        if (useNativePortraitSize) slot.PortraitImage.SetNativeSize();
                    }

                    // Authored inactive sprites include shading with the correct silhouette.
                    SetDimmed(slot.DimOverlay, dimmed && slot.InactiveSprite == null);
                    if (visible && !string.IsNullOrEmpty(node.SpeakerId) && slot.CurrentCharacterId == node.SpeakerId)
                    {
                        speakerNameplate = slot.Nameplate;
                    }
                }
            }

            if (speakerNameImage != null)
            {
                speakerNameImage.sprite = speakerNameplate;
                speakerNameImage.gameObject.SetActive(speakerNameplate != null);
                var namePosition = speakerNameImage.rectTransform.anchoredPosition;
                namePosition.x = node.SpeakerId == "andre" ? -724f : 724f;
                speakerNameImage.rectTransform.anchoredPosition = namePosition;
            }

            if (ResolveTextPresentation(node) != Config.DialogueTextPresentation.Speech &&
                node.Portraits.Length == 1 && node.Portraits[0].CharacterId == "andre")
            {
                foreach (var slot in portraitSlots)
                {
                    if (slot == null || slot.PortraitImage == null || !slot.PortraitImage.gameObject.activeSelf)
                        continue;
                    var position = slot.PortraitImage.rectTransform.anchoredPosition;
                    position.x = 0f;
                    slot.PortraitImage.rectTransform.anchoredPosition = position;
                }
            }
            if (dialogueFrame != null)
            {
                var frameSprite = ResolveTextPresentation(node) == Config.DialogueTextPresentation.Speech
                    ? spokenFrameSprite : narrationFrameSprite;
                if (frameSprite != null)
                {
                    dialogueFrame.sprite = frameSprite;
                }
            }
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

        private void HandleTypewriterCompletionRequested(DialogueTypewriterCompletionRequestedEvent evt)
        {
            if (!isTyping || evt.DialogueId != typewriterDialogueId || evt.NodeIndex != typewriterNodeIndex)
            {
                return;
            }

            CompleteTypewriter();
            PublishTypewriterState(false);
        }

        private void StartTypewriter(DialogueNodeData node)
        {
            if (typewriterRoutine != null)
            {
                StopCoroutine(typewriterRoutine);
                typewriterRoutine = null;
            }

            typewriterText = node.Text ?? string.Empty;
            typewriterDialogueId = node.DialogueId;
            typewriterNodeIndex = node.NodeIndex;
            isTyping = useTypewriter && typewriterText.Length > 0;

            if (!isTyping)
            {
                if (dialogueText != null)
                {
                    dialogueText.text = ApplyBaseInlineStyle(typewriterText);
                }
                UpdateSpecialText(typewriterText);
                PublishTypewriterState(false);
                return;
            }

            if (dialogueText != null)
            {
                dialogueText.text = string.Empty;
            }
            UpdateSpecialText(string.Empty);
            PublishTypewriterState(true);
            typewriterRoutine = StartCoroutine(TypewriterRoutine());
        }

        private IEnumerator TypewriterRoutine()
        {
            var visibleCount = 0;
            var visibleTotal = CountVisibleCharacters(typewriterText);
            var characterDelay = new WaitForSecondsRealtime(1f / Mathf.Max(1f, charactersPerSecond));
            while (visibleCount < visibleTotal && isTyping)
            {
                visibleCount++;
                if (dialogueText != null)
                {
                    var visible = BuildVisibleRichText(typewriterText, visibleCount);
                    dialogueText.text = ApplyBaseInlineStyle(visible);
                    UpdateSpecialText(visible);
                }
                yield return characterDelay;
            }

            // Clear the coroutine reference before completing naturally so
            // CompleteTypewriter does not attempt to stop its own coroutine.
            typewriterRoutine = null;
            CompleteTypewriter();
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
                dialogueText.text = ApplyBaseInlineStyle(typewriterText);
            }
            UpdateSpecialText(typewriterText);
        }

        private void UpdateSpecialText(string text)
        {
            if (specialText == null) return;
            const string open = "<size=32><i><color=#FFFFFF>";
            const string close = "</color></i></size>";
            var start = text.IndexOf(open, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf(close, start + open.Length, StringComparison.Ordinal);
            specialText.gameObject.SetActive(start >= 0 && end >= 0);
            if (start < 0 || end < 0) return;
            var position = specialText.GetComponent<DialogueInlinePosition>();
            if (position == null) position = specialText.gameObject.AddComponent<DialogueInlinePosition>();
            var tracking = specialText.GetComponent<DialogueTracking>();
            if (tracking != null) tracking.enabled = false;
            position.Body = dialogueText;
            position.StartCharacter = CountVisibleCharacters(text.Substring(0, start));
            specialText.text = text.Substring(start + open.Length, end - start - open.Length);
            specialText.SetVerticesDirty();
        }

        private static string ApplyBaseInlineStyle(string text)
        {
            return (text ?? string.Empty).Replace("<size=32><i><color=#FFFFFF>", "<size=32><color=#FFFFFF00>")
                .Replace("</color></i></size>", "</color></size>");
        }

        private static string ApplySpecialInlineStyle(string text)
        {
            const string open = "<size=32><i><color=#FFFFFF>";
            const string close = "</color></i></size>";
            var start = text.IndexOf(open, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf(close, start + open.Length, StringComparison.Ordinal);
            if (start < 0 || end < 0) return string.Empty;
            var prefix = text.Substring(0, start);
            var special = text.Substring(start + open.Length, end - start - open.Length);
            return "<color=#FFFFFF00>" + prefix + "</color>" + open + special + close;
        }

        private bool ShouldPresentNode(DialogueNodeData node)
        {
            if (node == null)
            {
                return false;
            }

            if (requiredOpenPanel != null && !requiredOpenPanel.IsOpen)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(dialogueIdFilter) && node.DialogueId != dialogueIdFilter)
            {
                return false;
            }

            return !narrationOnly || string.IsNullOrEmpty(node.SpeakerId);
        }

        private bool ShouldHideForIgnoredNode(DialogueNodeData node)
        {
            if (node == null || (!string.IsNullOrEmpty(dialogueIdFilter) && node.DialogueId != dialogueIdFilter))
            {
                return false;
            }

            return (requiredOpenPanel != null && !requiredOpenPanel.IsOpen)
                || (narrationOnly && !string.IsNullOrEmpty(node.SpeakerId));
        }

        private void StopTypewriterForIgnoredNode(DialogueNodeData node)
        {
            if (typewriterRoutine != null)
            {
                StopCoroutine(typewriterRoutine);
                typewriterRoutine = null;
            }

            isTyping = false;
            typewriterDialogueId = node.DialogueId;
            typewriterNodeIndex = node.NodeIndex;
            PublishTypewriterState(false);
        }

        private void HandleDialogueFinished(DialogueFinishedEvent evt)
        {
            if (!string.IsNullOrEmpty(dialogueIdFilter) && evt.DialogueId != dialogueIdFilter)
            {
                return;
            }

            HidePresentationRoot();
        }

        private void HidePresentationRoot()
        {
            if (presentationRoot != null && presentationRoot.activeSelf)
            {
                presentationRoot.SetActive(false);
            }
        }

        private void ApplyDialogueTextStyle()
        {
            if (dialogueText == null)
            {
                return;
            }

            // Content chooses a semantic presentation; the View owns its typography.
            dialogueText.resizeTextForBestFit = false;
            dialogueText.fontSize = Mathf.Max(1, dialogueFontSize);
            if (useAuthoredTextStyles)
            {
                bool speech = currentTextPresentation == Config.DialogueTextPresentation.Speech;
                bool quotation = currentTextPresentation == Config.DialogueTextPresentation.Quotation;
                var font = speech || quotation ? speechFont : narrationFont;
                if (font != null) dialogueText.font = font;
                // FZZJ-LZXTFSJW is the authored italic face; avoid adding faux italic.
                dialogueText.fontStyle = FontStyle.Normal;
                dialogueText.fontSize = speech && !quotation ? speechFontSize : narrationFontSize;
                // Unity multiplies the font's own line metrics, which differ between
                // these two fonts. Normalize to the reference's 48/40 canvas-unit leading.
                float nativeLineHeight = dialogueText.font != null
                    ? dialogueText.font.lineHeight * (float)dialogueText.fontSize / Mathf.Max(1, dialogueText.font.fontSize)
                    : dialogueText.fontSize;
                dialogueText.lineSpacing = (speech && !quotation ? 48f : 40f) / Mathf.Max(1f, nativeLineHeight);
                dialogueText.alignment = quotation && currentCenterText ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
                dialogueText.color = quotation ? QuotationTextColor : speech ? Color.black : new Color32(0xE3, 0xD7, 0xB2, 0xFF);
                var tracking = dialogueText.GetComponent<DialogueTracking>();
                if (tracking != null) tracking.Tracking = speech && !quotation ? -100f : 12f;
            }
            else
            {
                dialogueText.alignment = TextAnchor.UpperLeft;
                dialogueText.color = Color.white;
            }
            dialogueText.supportRichText = true;
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static string BuildVisibleRichText(string source, int visibleCharacters)
        {
            if (string.IsNullOrEmpty(source) || visibleCharacters >= CountVisibleCharacters(source))
                return source ?? string.Empty;

            var result = new System.Text.StringBuilder(source.Length);
            var openTags = new System.Collections.Generic.Stack<string>();
            var visible = 0;
            for (var i = 0; i < source.Length && visible < visibleCharacters;)
            {
                if (source[i] == '<')
                {
                    var end = source.IndexOf('>', i);
                    if (end < 0) break;
                    var tag = source.Substring(i, end - i + 1);
                    result.Append(tag);
                    if (tag.Length > 2 && tag[1] != '/')
                    {
                        var nameEnd = tag.IndexOfAny(new[] { ' ', '=', '>' }, 1);
                        openTags.Push(nameEnd > 1 ? tag.Substring(1, nameEnd - 1) : tag.Substring(1, tag.Length - 2));
                    }
                    else if (tag.StartsWith("</", StringComparison.Ordinal) && openTags.Count > 0)
                    {
                        openTags.Pop();
                    }
                    i = end + 1;
                    continue;
                }

                result.Append(source[i++]);
                visible++;
            }

            while (openTags.Count > 0)
                result.Append("</").Append(openTags.Pop()).Append('>');
            return result.ToString();
        }

        private static int CountVisibleCharacters(string source)
        {
            var count = 0;
            var inTag = false;
            foreach (var character in source)
            {
                if (character == '<') inTag = true;
                else if (character == '>') inTag = false;
                else if (!inTag) count++;
            }
            return count;
        }

        private bool currentCenterText;

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
