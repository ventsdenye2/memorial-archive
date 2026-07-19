using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Gameplay.Interaction.View
{
    public sealed class RoomMarkerView : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField] private string playerTag = "Player";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                GameRoot.Instance?.Context?.Events.Publish(new RoomEnteredEvent(roomId));
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                GameRoot.Instance?.Context?.Events.Publish(new RoomExitedEvent(roomId));
            }
        }
    }
}
