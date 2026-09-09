using System;
using UnityEngine;
using MemorialArchive.Gameplay.Dialogue.Config;

namespace MemorialArchive.Gameplay.Dialogue.Data
{
    public enum DialoguePlaybackState
    {
        Idle,
        Presenting,
        BlockingEffect,
        Finished
    }

    [Serializable]
    public sealed class DialoguePortraitData
    {
        public string CharacterId { get; }
        public Sprite Portrait { get; }
        public Sprite InactivePortrait { get; }
        public Sprite Nameplate { get; }
        public string SlotId { get; }
        public Color PlaceholderColor { get; }
        public bool Visible { get; }

        public DialoguePortraitData(DialoguePortraitConfig config)
        {
            CharacterId = config != null ? config.CharacterId : string.Empty;
            Portrait = config != null ? config.Portrait : null;
            InactivePortrait = config != null ? config.InactivePortrait : null;
            Nameplate = config != null ? config.Nameplate : null;
            SlotId = config != null ? config.SlotId : string.Empty;
            PlaceholderColor = config != null ? config.PlaceholderColor : Color.white;
            Visible = config != null && config.Visible;
        }
    }

    public sealed class DialogueNodeData
    {
        public string DialogueId { get; }
        public int NodeIndex { get; }
        public string NodeId { get; }
        public string SpeakerId { get; }
        public DialogueTextPresentation TextPresentation { get; }
        public string Text { get; }
        public DialogueCgCommand CgCommand { get; }
        public Sprite CgSprite { get; }
        public DialoguePortraitData[] Portraits { get; }
        public DialogueEffectConfig[] Effects { get; }
        public bool BlocksAdvance { get; }

        public DialogueNodeData(
            string dialogueId,
            int nodeIndex,
            DialogueNodeConfig config,
            DialogueEffectConfig[] effects)
        {
            DialogueId = dialogueId;
            NodeIndex = nodeIndex;
            NodeId = config != null ? config.NodeId : string.Empty;
            SpeakerId = config != null ? config.SpeakerId : string.Empty;
            TextPresentation = config != null ? config.TextPresentation : DialogueTextPresentation.Automatic;
            Text = config != null ? config.Text : string.Empty;
            CgCommand = config != null ? config.CgCommand : DialogueCgCommand.Keep;
            CgSprite = config != null ? config.CgSprite : null;
            BlocksAdvance = config != null && config.BlocksAdvance;

            var portraitConfigs = config != null ? config.Portraits : null;
            Portraits = new DialoguePortraitData[portraitConfigs != null ? portraitConfigs.Length : 0];
            for (var i = 0; i < Portraits.Length; i++)
            {
                Portraits[i] = new DialoguePortraitData(portraitConfigs[i]);
            }

            Effects = effects ?? Array.Empty<DialogueEffectConfig>();
        }
    }
}
