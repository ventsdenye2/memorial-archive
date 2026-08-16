using System;
using UnityEngine;

namespace MemorialArchive.Gameplay.Dialogue.Config
{
    public enum DialogueCgCommand
    {
        Keep,
        Set,
        Clear
    }

    [Serializable]
    public sealed class DialoguePortraitConfig
    {
        [SerializeField] private string characterId;
        [SerializeField] private Color placeholderColor = Color.white;
        [SerializeField] private Sprite portrait;
        [SerializeField] private string slotId;
        [SerializeField] private bool visible = true;

        public string CharacterId => characterId;
        public Sprite Portrait => portrait;
        public Color PlaceholderColor => placeholderColor;
        public string SlotId => slotId;
        public bool Visible => visible;
    }

    [Serializable]
    public sealed class DialogueNodeConfig
    {
        [SerializeField] private string nodeId;
        [SerializeField] private string speakerId;
        [TextArea(3, 8)]
        [SerializeField] private string text;
        [SerializeField] private DialogueCgCommand cgCommand = DialogueCgCommand.Keep;
        [SerializeField] private Sprite cgSprite;
        [SerializeField] private DialoguePortraitConfig[] portraits = Array.Empty<DialoguePortraitConfig>();
        [SerializeField] private string[] effectIds = Array.Empty<string>();
        [SerializeField] private bool blocksAdvance;

        public string NodeId => nodeId;
        public string SpeakerId => speakerId;
        public string Text => text;
        public DialogueCgCommand CgCommand => cgCommand;
        public Sprite CgSprite => cgSprite;
        public DialoguePortraitConfig[] Portraits => portraits;
        public string[] EffectIds => effectIds;
        public bool BlocksAdvance => blocksAdvance;
    }

    [CreateAssetMenu(menuName = "Memorial Archive/Config/Dialogue Sequence")]
    public sealed class DialogueSequenceConfig : ScriptableObject
    {
        [SerializeField] private string dialogueId;
        [SerializeField] private DialogueNodeConfig[] nodes = Array.Empty<DialogueNodeConfig>();

        public string DialogueId => dialogueId;
        public DialogueNodeConfig[] Nodes => nodes;
    }
}
