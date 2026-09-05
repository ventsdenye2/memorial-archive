using UnityEngine;

namespace MemorialArchive.Framework.Audio
{
    /// <summary>UnityEvent / animation-event entry point for interactions authored later.</summary>
    public sealed class AudioCueEmitter : MonoBehaviour
    {
        [SerializeField] private string cueId;
        private AudioSource loop;
        public void Play() => AudioSystem.Play(cueId);
        public void PlayCue(string id) => AudioSystem.Play(id);
        public void StartLoop()
        {
            StopLoop();
            loop = AudioSystem.Current?.Playback?.Play(cueId, transform);
        }
        public void StopLoop() { AudioSystem.Current?.Playback?.Stop(loop); loop = null; }
        private void OnDisable() => StopLoop();
        public void DiaryPickedUp() => AudioSystem.Play("sfx_scene_diary_pickup");
        public void QuestToggled() => AudioSystem.Play("sfx_ui_quest_toggle");
    }
}
