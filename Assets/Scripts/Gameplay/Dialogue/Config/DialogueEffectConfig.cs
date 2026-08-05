using UnityEngine;

namespace MemorialArchive.Gameplay.Dialogue.Config
{
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Dialogue Effect")]
    public sealed class DialogueEffectConfig : ScriptableObject
    {
        [SerializeField] private string effectId;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private Color screenFlashColor = Color.white;
        [SerializeField, Min(0)] private int flashCount = 1;
        [SerializeField, Min(0f)] private float flashInDuration = 0.04f;
        [SerializeField, Min(0f)] private float flashOutDuration = 0.08f;

        public string EffectId => effectId;
        public AudioClip AudioClip => audioClip;
        public Color ScreenFlashColor => screenFlashColor;
        public int FlashCount => flashCount;
        public float FlashInDuration => flashInDuration;
        public float FlashOutDuration => flashOutDuration;
    }
}
