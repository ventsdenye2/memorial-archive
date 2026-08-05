using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using UnityEngine;

namespace MemorialArchive.Gameplay.Dialogue.View
{
    public sealed class DialogueSceneController : MonoBehaviour
    {
        [SerializeField] private string dialogueId;
        [SerializeField] private string nextSceneId;
        [SerializeField] private string nextSpawnPointId;
        private bool subscribed;

        private void OnEnable()
        {
            subscribed = false;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                GameRoot.Instance?.Context?.Events.Unsubscribe<DialogueFinishedEvent>(HandleDialogueFinished);
                subscribed = false;
            }
        }

        private IEnumerator Start()
        {
            while (GameRoot.Instance == null || GameRoot.Instance.Context == null)
            {
                yield return null;
            }

            var context = GameRoot.Instance.Context;
            context.Events.Subscribe<DialogueFinishedEvent>(HandleDialogueFinished);
            subscribed = true;
            context?.UI?.Open(PanelId.Dialogue);
            if (!string.IsNullOrEmpty(dialogueId))
            {
                context?.Events.Publish(new DialoguePlayRequestedEvent(dialogueId));
            }
        }

        private void HandleDialogueFinished(DialogueFinishedEvent evt)
        {
            if (evt.DialogueId != dialogueId || string.IsNullOrEmpty(nextSceneId))
            {
                return;
            }

            GameRoot.Instance?.Context?.Events.Publish(
                new SceneTransitionRequestedEvent(nextSceneId, nextSpawnPointId));
        }
    }
}
