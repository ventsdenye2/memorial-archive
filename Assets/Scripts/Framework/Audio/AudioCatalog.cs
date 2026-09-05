using System;
using UnityEngine;

namespace MemorialArchive.Framework.Audio
{
    public enum AudioBus { Sfx, UI, Ambience, Music }
    [Serializable] public sealed class AudioCue
    {
        public string id;
        public AudioClip[] clips;
        public AudioBus bus;
        public bool loop;
        public bool sequential;
        [Range(0, 1)] public float volume = 0.7f;
        [Min(0)] public float cooldown = 0.06f;
    }
    [Serializable] public sealed class SceneAudioProfile
    {
        public string scene;
        public string ambience;
        public string music;
        public string surface = "wood";
    }
    [CreateAssetMenu(menuName = "Memorial Archive/Audio/Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public AudioCue[] cues;
        public SceneAudioProfile[] scenes;
    }
}
