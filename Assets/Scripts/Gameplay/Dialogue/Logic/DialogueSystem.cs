using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Dialogue.Config;
using MemorialArchive.Gameplay.Dialogue.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Dialogue.Logic
{
    public sealed class DialogueSystem : IGameSystem
    {
        private GameContext context;
        private DialogueSequenceConfig sequence;
        private int nodeIndex = -1;
        private int lastAdvanceFrame = -1;
        private DialoguePlaybackState state = DialoguePlaybackState.Idle;

        public bool IsPlaying => sequence != null && state != DialoguePlaybackState.Idle && state != DialoguePlaybackState.Finished;
        public bool IsInputModeActive => IsPlaying && state != DialoguePlaybackState.BlockingEffect;
        public string CurrentDialogueId => sequence != null ? sequence.DialogueId : string.Empty;
        public int CurrentNodeIndex => nodeIndex;

        public void Initialize(GameContext gameContext)
        {
            context = gameContext;
            context.Events.Subscribe<DialoguePlayRequestedEvent>(HandlePlayRequested);
            context.Events.Subscribe<DialogueAdvancePressedEvent>(HandleAdvancePressed);
            context.Events.Subscribe<DialogueBlockingEffectFinishedEvent>(HandleBlockingEffectFinished);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<DialoguePlayRequestedEvent>(HandlePlayRequested);
                context.Events.Unsubscribe<DialogueAdvancePressedEvent>(HandleAdvancePressed);
                context.Events.Unsubscribe<DialogueBlockingEffectFinishedEvent>(HandleBlockingEffectFinished);
            }

            ResetPlayback();
            context = null;
        }

        private void HandlePlayRequested(DialoguePlayRequestedEvent evt)
        {
            var requestedSequence = context.Configs.GetDialogueSequence(evt.DialogueId);
            if (requestedSequence == null)
            {
                Debug.LogError($"Dialogue sequence not found: {evt.DialogueId}");
                return;
            }

            if (requestedSequence.Nodes == null || requestedSequence.Nodes.Length == 0)
            {
                Debug.LogError($"Dialogue sequence has no nodes: {evt.DialogueId}");
                return;
            }

            sequence = requestedSequence;
            nodeIndex = 0;
            PresentCurrentNode();
        }

        private void HandleAdvancePressed(DialogueAdvancePressedEvent evt)
        {
            if (!IsInputModeActive || lastAdvanceFrame == Time.frameCount)
            {
                return;
            }

            lastAdvanceFrame = Time.frameCount;

            if (nodeIndex >= sequence.Nodes.Length - 1)
            {
                FinishPlayback();
                return;
            }

            nodeIndex++;
            PresentCurrentNode();
        }

        private void HandleBlockingEffectFinished(DialogueBlockingEffectFinishedEvent evt)
        {
            if (state != DialoguePlaybackState.BlockingEffect || evt.DialogueId != CurrentDialogueId || evt.NodeIndex != nodeIndex)
            {
                return;
            }

            state = DialoguePlaybackState.Presenting;
            PublishPlaybackState();
        }

        private void PresentCurrentNode()
        {
            var config = sequence.Nodes[nodeIndex];
            if (config == null)
            {
                Debug.LogError($"Dialogue sequence '{CurrentDialogueId}' has an empty node at index {nodeIndex}.");
                FinishPlayback();
                return;
            }

            var effects = ResolveEffects(config.EffectIds);
            state = config.BlocksAdvance && effects.Length > 0
                ? DialoguePlaybackState.BlockingEffect
                : DialoguePlaybackState.Presenting;

            var data = new DialogueNodeData(CurrentDialogueId, nodeIndex, config, effects);
            context.Events.Publish(new DialogueNodePresentedEvent(data));
            PublishPlaybackState();
        }

        private DialogueEffectConfig[] ResolveEffects(string[] effectIds)
        {
            if (effectIds == null || effectIds.Length == 0)
            {
                return System.Array.Empty<DialogueEffectConfig>();
            }

            var results = new List<DialogueEffectConfig>(effectIds.Length);
            foreach (var effectId in effectIds)
            {
                if (string.IsNullOrEmpty(effectId))
                {
                    continue;
                }

                var effect = context.Configs.GetDialogueEffect(effectId);
                if (effect == null)
                {
                    Debug.LogWarning($"Dialogue effect not found: {effectId}");
                    continue;
                }

                results.Add(effect);
            }

            return results.ToArray();
        }

        private void FinishPlayback()
        {
            var dialogueId = CurrentDialogueId;
            state = DialoguePlaybackState.Finished;
            context.Events.Publish(new DialogueFinishedEvent(dialogueId));
            PublishPlaybackState();
            ResetPlayback();
        }

        private void ResetPlayback()
        {
            sequence = null;
            nodeIndex = -1;
            lastAdvanceFrame = -1;
            state = DialoguePlaybackState.Idle;
        }

        private void PublishPlaybackState()
        {
            context?.Events.Publish(new DialoguePlaybackStateChangedEvent(CurrentDialogueId, state, IsInputModeActive));
        }
    }
}
