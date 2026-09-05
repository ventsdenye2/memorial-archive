using MemorialArchive.Framework.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public sealed class SettingsPanel : BasePanel
    {
        [SerializeField] private Slider masterVolumeSlider;
        public override void Open()
        {
            base.Open();
            if (masterVolumeSlider == null) masterVolumeSlider = GetComponentInChildren<Slider>(true);
            if (masterVolumeSlider == null) CreateVolumeSlider();
            masterVolumeSlider.minValue = 0; masterVolumeSlider.maxValue = 1;
            masterVolumeSlider.wholeNumbers = false;
            masterVolumeSlider.SetValueWithoutNotify(AudioSystem.Current?.Playback?.MasterVolume ?? 1f);
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }
        public void SetMasterVolume(float value) => AudioSystem.Current?.Playback?.SetMasterVolume(value);
        public override void Close() { PlayerPrefs.Save(); base.Close(); }
        public void SetSfxVolume(float value) => AudioSystem.Current?.Playback?.SetBusVolume(AudioBus.Sfx, value);
        public void SetMusicVolume(float value) => AudioSystem.Current?.Playback?.SetBusVolume(AudioBus.Music, value);
        public void SetAmbienceVolume(float value) => AudioSystem.Current?.Playback?.SetBusVolume(AudioBus.Ambience, value);
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
        private void OnDestroy() { if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume); }
    }
}
