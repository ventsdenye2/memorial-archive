using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Interaction.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Interaction.View
{
    public sealed class InteractionPointView : MonoBehaviour
    {
        [SerializeField] private string interactionId;
        [SerializeField] private InteractionType interactionType = InteractionType.Inspect;
        [SerializeField] private string playerTag = "Player";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                PublishFocus(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                PublishFocus(false);
            }
        }

        private void PublishFocus(bool hasFocus)
        {
            var root = GameRoot.Instance;
            if (root == null || root.Context == null)
            {
                return;
            }

            root.Context.Events.Publish(new InteractionFocusChangedEvent(interactionId, interactionType, hasFocus));
        }
    }
}
