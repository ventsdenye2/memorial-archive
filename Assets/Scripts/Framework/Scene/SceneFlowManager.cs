using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemorialArchive.Gameplay.Inventory.Logic;

namespace MemorialArchive.Framework.Scene
{
    public sealed class SceneFlowManager : IGameSystem
    {
        private GameContext context;
        private string pendingSpawnPointId;
        private bool hasPendingSavedPosition;
        private Vector3 pendingSavedPosition;
        private bool isLoading;
        private bool playDoorOnArrival;

        public void Initialize(GameContext gameContext)
        {
            context = gameContext;
            context.Events.Subscribe<SceneTransitionRequestedEvent>(HandleSceneTransitionRequested);
            SceneManager.sceneLoaded += HandleUnitySceneLoaded;
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SceneTransitionRequestedEvent>(HandleSceneTransitionRequested);
            }

            SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
            pendingSpawnPointId = null;
            hasPendingSavedPosition = false;
            isLoading = false;
            context = null;
        }

        private void HandleSceneTransitionRequested(SceneTransitionRequestedEvent evt)
        {
            evt = new SceneTransitionRequestedEvent(FirstFloorSceneLayout.ResolveScene(evt.SceneId), evt.SpawnPointId);
            if (isLoading || string.IsNullOrEmpty(evt.SceneId))
            {
                return;
            }

            // Doors and stair-panel selections share this access check. Save restoration bypasses it.
            var requiredItemId = context.Configs.GetRequiredSceneKey(SceneManager.GetActiveScene().name, evt.SceneId);
            var inventory = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (requiredItemId > 0 && (inventory == null ||
                !inventory.PlayerInventory.playerItems.Exists(p => p?.item != null && p.item.itemId == requiredItemId && p.item.quantity > 0)))
            {
                var itemName = context.Configs.GetItem(requiredItemId)?.ItemName ?? "钥匙";
                context.Events.Publish(new SceneAccessDeniedEvent(evt.SceneId, $"需要{itemName}"));
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(evt.SceneId))
            {
                Debug.LogError($"Scene is not available in build settings: {evt.SceneId}");
                return;
            }

            isLoading = true;
            playDoorOnArrival = evt.SceneId.StartsWith("Room_") || SceneManager.GetActiveScene().name.StartsWith("Room_");
            pendingSpawnPointId = evt.SpawnPointId;
            try
            {
                SceneManager.LoadScene(evt.SceneId);
            }
            catch (System.Exception exception)
            {
                pendingSpawnPointId = null;
                isLoading = false;
                Debug.LogException(exception);
            }
        }

        public void LoadSavedScene(string sceneId, Vector3 savedPosition)
        {
            savedPosition = FirstFloorSceneLayout.ResolveSavedPosition(sceneId, savedPosition);
            sceneId = FirstFloorSceneLayout.ResolveScene(sceneId);
            if (isLoading)
            {
                throw new System.InvalidOperationException("A scene transition is already in progress.");
            }

            if (string.IsNullOrEmpty(sceneId) || !Application.CanStreamedLevelBeLoaded(sceneId))
            {
                throw new System.ArgumentException($"Scene is not available in build settings: {sceneId}", nameof(sceneId));
            }

            isLoading = true;
            playDoorOnArrival = false;
            pendingSpawnPointId = null;
            pendingSavedPosition = savedPosition;
            hasPendingSavedPosition = true;
            try
            {
                SceneManager.LoadScene(sceneId);
            }
            catch
            {
                hasPendingSavedPosition = false;
                isLoading = false;
                throw;
            }
        }

        private void HandleUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            if (!string.IsNullOrEmpty(pendingSpawnPointId))
            {
                ResolveSpawnPoint(pendingSpawnPointId);
            }

            if (hasPendingSavedPosition)
            {
                ResolveSavedPosition(pendingSavedPosition);
            }

            pendingSpawnPointId = null;
            hasPendingSavedPosition = false;
            isLoading = false;
            context?.Events.Publish(new SceneLoadedEvent(scene.name));
            if (playDoorOnArrival)
                MemorialArchive.Framework.Audio.AudioSystem.Play("sfx_scene_door_open");
            playDoorOnArrival = false;
        }

        private static void ResolveSpawnPoint(string spawnPointId)
        {
            ISceneSpawnPoint targetPoint = null;
            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour is ISceneSpawnPoint point && point.PointId == spawnPointId)
                {
                    targetPoint = point;
                    break;
                }
            }

            if (targetPoint == null)
            {
                Debug.LogError($"Scene spawn point not found: {spawnPointId}");
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            var spawnTarget = player != null ? player.GetComponent<ISceneSpawnTarget>() : null;
            if (spawnTarget == null)
            {
                Debug.LogError($"No ISceneSpawnTarget found for spawn point: {spawnPointId}");
                return;
            }

            spawnTarget.MoveToSceneSpawn(targetPoint.Position);
        }

        private static void ResolveSavedPosition(Vector3 savedPosition)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var spawnTarget = player != null ? player.GetComponent<ISceneSpawnTarget>() : null;
            if (spawnTarget == null)
            {
                Debug.LogError("No ISceneSpawnTarget found while restoring a saved position.");
                return;
            }

            spawnTarget.MoveToSceneSpawn(savedPosition);
        }
    }
}
