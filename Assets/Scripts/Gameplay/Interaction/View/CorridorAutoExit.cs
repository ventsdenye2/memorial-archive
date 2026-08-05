using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Gameplay.Interaction.View
{
    /// <summary>
    /// Same-floor corridor auto-transition: when the player enters this trigger at a
    /// corridor edge, the adjacent corridor scene loads immediately (no F, no dialog).
    /// Stairs use InteractionPointView + confirmation instead.
    /// </summary>
    public sealed class CorridorAutoExit : MonoBehaviour
    {
        [SerializeField] private string targetSceneId;
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private string playerTag = "Player";

        private bool triggered;

        public void Configure(string sceneId, string spawnPointId)
        {
            targetSceneId = sceneId;
            targetSpawnPointId = spawnPointId;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryTransition(other);
        }

        private void TryTransition(Collider2D other)
        {
            if (triggered || !other.CompareTag(playerTag))
            {
                return;
            }

            var root = GameRoot.Instance;
            if (root == null || root.Context == null ||
                string.IsNullOrEmpty(targetSceneId) || string.IsNullOrEmpty(targetSpawnPointId))
            {
                return;
            }

            triggered = true;
            root.Context.Events.Publish(new SceneTransitionRequestedEvent(targetSceneId, targetSpawnPointId));
        }
    }
}
