using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Dialogue.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Dialogue.View
{
    public sealed class DialogueInputReader : MonoBehaviour
    {
        private void Update()
        {
            var root = GameRoot.Instance;
            var dialogueSystem = root != null ? root.GetSystem<DialogueSystem>() : null;
            if (root?.Context == null || dialogueSystem == null || !dialogueSystem.IsInputModeActive)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                root.Context.Events.Publish(new DialogueAdvancePressedEvent());
            }
        }
    }
}
