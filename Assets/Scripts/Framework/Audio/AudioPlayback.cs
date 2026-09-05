using System.Collections.Generic;
using UnityEngine;

namespace MemorialArchive.Framework.Audio
{
    // Sources are pooled; loop handles belong to the caller and die with their owner.
    public sealed class AudioPlayback : MonoBehaviour
    {
        private sealed class Voice
        {
            public AudioSource source;
            public AudioCue cue;
            public Transform owner;
            public bool owned;
            public float positionX;
            public float gain;
            public bool stopping;
        }
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<string, AudioCue> cues = new Dictionary<string, AudioCue>();
        private readonly Dictionary<string, float> nextPlay = new Dictionary<string, float>();
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        public Vector2 ListenerPosition { get; set; }
        public float MasterVolume { get; private set; }
        private readonly float[] volumes = new float[4];

        public void Initialize(AudioCatalog catalog)
        {
            MasterVolume = PlayerPrefs.GetFloat("audio.master", 1f);
            for (int i = 0; i < volumes.Length; i++) volumes[i] = PlayerPrefs.GetFloat("audio.bus." + i, 1f);
            if (catalog != null && catalog.cues != null)
                foreach (var cue in catalog.cues) if (!string.IsNullOrEmpty(cue.id)) cues[cue.id] = cue;
        }
        public void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat("audio.master", MasterVolume); }
        public void SetBusVolume(AudioBus bus, float value) { volumes[(int)bus] = Mathf.Clamp01(value); PlayerPrefs.SetFloat("audio.bus." + (int)bus, volumes[(int)bus]); }
        public float GetBusVolume(AudioBus bus) => volumes[(int)bus];
        public AudioSource Play(string id, Transform owner = null)
        {
            if (string.IsNullOrEmpty(id) || !cues.TryGetValue(id, out var cue) || cue.clips == null || cue.clips.Length == 0) return null;
            // UI remains responsive while gameplay is paused.
            if (cue.bus == AudioBus.Sfx && Time.timeScale <= 0f) return null;
            if (nextPlay.TryGetValue(id, out var next) && Time.unscaledTime < next) return null;
            int variant = cue.sequential
                ? (lastVariant.TryGetValue(id, out var last) ? (last + 1) % cue.clips.Length : 0)
                : Random.Range(0, cue.clips.Length);
            if (cue.clips.Length > 1 && lastVariant.TryGetValue(id, out var previous) && previous == variant) variant = (variant + 1) % cue.clips.Length;
            if (cue.clips[variant] == null) return null;
            Voice voice = voices.Find(v => v.cue == null);
            if (voice == null)
            {
                if (voices.Count >= 32) return null;
                voice = new Voice { source = gameObject.AddComponent<AudioSource>() };
                voice.source.playOnAwake = false;
                voice.source.spatialBlend = 0f;
                voices.Add(voice);
            }
            lastVariant[id] = variant;
            nextPlay[id] = Time.unscaledTime + cue.cooldown;
            voice.cue = cue; voice.owner = owner; voice.owned = owner != null;
            voice.positionX = owner != null ? owner.position.x : 0f;
            voice.stopping = false; voice.gain = cue.loop ? 0f : 1f;
            voice.source.clip = cue.clips[variant]; voice.source.loop = cue.loop;
            voice.source.volume = Gain(voice); voice.source.Play();
            return voice.source;
        }
        public void Stop(AudioSource source)
        {
            var voice = voices.Find(v => v.source == source && v.cue != null);
            if (voice != null) voice.stopping = true;
        }
        public void StopSceneVoices()
        {
            foreach (var voice in voices) if (voice.cue != null && voice.cue.bus != AudioBus.UI) Release(voice);
            nextPlay.Clear();
        }
        private float Gain(Voice voice)
        {
            if (voice.owner != null) voice.positionX = voice.owner.position.x;
            float distance = voice.owned ? Mathf.Abs(voice.positionX - ListenerPosition.x) : 0f;
            return MasterVolume * volumes[(int)voice.cue.bus] * voice.cue.volume * voice.gain * Mathf.Clamp01(1f - distance / 12f);
        }
        private static void Release(Voice voice) { voice.source.Stop(); voice.source.clip = null; voice.cue = null; voice.owner = null; }
        private void Update()
        {
            foreach (var voice in voices)
            {
                if (voice.cue == null) continue;
                if (voice.owned && voice.cue.loop && (voice.owner == null || !voice.owner.gameObject.activeInHierarchy)) { Release(voice); continue; }
                bool paused = Time.timeScale <= 0 && voice.cue.bus == AudioBus.Sfx;
                if (paused) { voice.source.Pause(); continue; }
                voice.source.UnPause();
                voice.gain = Mathf.MoveTowards(voice.gain, voice.stopping ? 0f : 1f, Time.unscaledDeltaTime * 3f);
                if (voice.stopping && voice.gain <= 0 || !voice.source.isPlaying) { Release(voice); continue; }
                voice.source.volume = Gain(voice);
            }
        }
    }
}
