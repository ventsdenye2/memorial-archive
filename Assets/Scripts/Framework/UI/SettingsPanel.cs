using MemorialArchive.Framework.Audio;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace MemorialArchive.Framework.UI
{
    public sealed class SettingsPanel : BasePanel
    {
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider backgroundVolumeSlider;
        [SerializeField] private Slider gameVolumeSlider;
        [SerializeField] private Text backgroundVolumeValueLabel;
        [SerializeField] private Text gameVolumeValueLabel;
        [SerializeField] private Text resolutionLabel;
        [SerializeField] private Button fullscreenButton;
        [SerializeField] private Button windowedButton;
        [SerializeField] private Sprite fullscreenNormalSprite;
        [SerializeField] private Sprite fullscreenSelectedSprite;
        [SerializeField] private Sprite windowedNormalSprite;
        [SerializeField] private Sprite windowedSelectedSprite;

        public override void Open()
        {
            base.Open();
            if (backgroundVolumeSlider == null) backgroundVolumeSlider = FindSlider("BackgroundVolume");
            if (gameVolumeSlider == null) gameVolumeSlider = FindSlider("GameVolume");

            if (backgroundVolumeSlider != null || gameVolumeSlider != null)
            {
                BindSlider(backgroundVolumeSlider, AudioBus.Music, SetBackgroundVolume);
                BindSlider(gameVolumeSlider, AudioBus.Sfx, SetGameVolume);
            }
            else
            {
                if (masterVolumeSlider == null) masterVolumeSlider = GetComponentInChildren<Slider>(true);
                if (masterVolumeSlider == null) CreateVolumeSlider();
                BindSlider(masterVolumeSlider, AudioBus.UI, SetMasterVolume);
            }

            RefreshResolutionLabel();
            RefreshModeVisuals();
        }

        public void SetMasterVolume(float value) => AudioSystem.Current?.Playback?.SetMasterVolume(value);
        public override void Close() { PlayerPrefs.Save(); base.Close(); }
        public void SetSfxVolume(float value) => AudioSystem.Current?.Playback?.SetBusVolume(AudioBus.Sfx, value);
        public void SetMusicVolume(float value) => AudioSystem.Current?.Playback?.SetBusVolume(AudioBus.Music, value);
        public void SetAmbienceVolume(float value) => AudioSystem.Current?.Playback?.SetBusVolume(AudioBus.Ambience, value);

        public void SetBackgroundVolume(float value)
        {
            SetMusicVolume(value);
            SetVolumeLabel(backgroundVolumeValueLabel, value);
        }

        public void SetGameVolume(float value)
        {
            SetSfxVolume(value);
            SetVolumeLabel(gameVolumeValueLabel, value);
        }

        public void SetFullscreen()
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            RefreshModeVisuals();
        }

        public void SetWindowed()
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            RefreshModeVisuals();
        }

        public void CycleResolution()
        {
            var resolutions = GetDistinctResolutions();
            if (resolutions == null || resolutions.Length == 0)
            {
                RefreshResolutionLabel();
                return;
            }

            var currentIndex = 0;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < resolutions.Length; i++)
            {
                var widthDistance = resolutions[i].width - Screen.width;
                var heightDistance = resolutions[i].height - Screen.height;
                var distance = (long)widthDistance * widthDistance + (long)heightDistance * heightDistance;
                if (distance < bestDistance)
                {
                    currentIndex = i;
                    bestDistance = distance;
                }
            }

            var next = resolutions[(currentIndex + 1) % resolutions.Length];
            Screen.SetResolution(next.width, next.height, Screen.fullScreenMode, next.refreshRateRatio);
            // SetResolution applies on a later player update. Show the
            // requested size immediately, then Open() refreshes it from the
            // actual Screen dimensions the next time this panel is shown.
            if (resolutionLabel != null)
            {
                resolutionLabel.text = FormatResolution(next.width, next.height);
            }
        }

        private static Resolution[] GetDistinctResolutions()
        {
            var source = Screen.resolutions;
            if (source == null || source.Length == 0) return source;

            var distinct = new List<Resolution>(source.Length);
            var seen = new HashSet<string>();
            foreach (var resolution in source)
            {
                var key = resolution.width + "x" + resolution.height;
                if (seen.Add(key)) distinct.Add(resolution);
            }

            return distinct.ToArray();
        }

        private void CreateVolumeSlider()
        {
            // Existing placeholder prefab has only a title and close button.
            var control = DefaultControls.CreateSlider(new DefaultControls.Resources());
            control.name = "MasterVolume";
            control.transform.SetParent(transform, false);
            var rect = control.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(360f, 24f);
            masterVolumeSlider = control.GetComponent<Slider>();
            var labelObject = new GameObject("VolumeLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(control.transform, false);
            var label = labelObject.GetComponent<Text>();
            var existingLabel = GetComponentInChildren<Text>(true);
            label.font = existingLabel != null ? existingLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "总音量"; label.fontSize = 22; label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(360f, 36f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        }

        private Slider FindSlider(string objectName)
        {
            var child = transform.Find(objectName);
            return child != null ? child.GetComponent<Slider>() : null;
        }

        private void BindSlider(Slider slider, AudioBus bus, UnityAction<float> callback)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            var playback = AudioSystem.Current?.Playback;
            var value = bus == AudioBus.UI
                ? (playback?.MasterVolume ?? 1f)
                : (playback?.GetBusVolume(bus) ?? 1f);
            slider.SetValueWithoutNotify(value);
            if (bus == AudioBus.Music) SetVolumeLabel(backgroundVolumeValueLabel, value);
            if (bus == AudioBus.Sfx) SetVolumeLabel(gameVolumeValueLabel, value);
            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private static void SetVolumeLabel(Text label, float value)
        {
            if (label != null) label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString();
        }

        private void RefreshResolutionLabel()
        {
            if (resolutionLabel != null) resolutionLabel.text = FormatResolution(Screen.width, Screen.height);
        }

        private static string FormatResolution(int width, int height) => $"{width}*{height} px";

        private void RefreshModeVisuals()
        {
            var isFullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            SetModeSprite(fullscreenButton, isFullscreen, fullscreenNormalSprite, fullscreenSelectedSprite);
            SetModeSprite(windowedButton, !isFullscreen, windowedNormalSprite, windowedSelectedSprite);
        }

        private static void SetModeSprite(Button button, bool selected, Sprite normal, Sprite highlighted)
        {
            if (button == null || button.targetGraphic == null) return;
            var image = button.targetGraphic as Image;
            if (image == null) return;
            image.sprite = selected ? highlighted : normal;
            image.overrideSprite = null;
        }

        private void OnDestroy()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            if (backgroundVolumeSlider != null) backgroundVolumeSlider.onValueChanged.RemoveListener(SetBackgroundVolume);
            if (gameVolumeSlider != null) gameVolumeSlider.onValueChanged.RemoveListener(SetGameVolume);
        }
    }
}
