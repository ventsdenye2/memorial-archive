using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Framework.Scene
{
    public sealed class SceneFlowManager : IGameSystem
    {
        private GameContext context;
        private string pendingSpawnPointId;
        private bool isLoading;

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
            isLoading = false;
            context = null;
        }

        private void HandleSceneTransitionRequested(SceneTransitionRequestedEvent evt)
        {
            if (isLoading || string.IsNullOrEmpty(evt.SceneId))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(evt.SceneId))
            {
                Debug.LogError($"Scene is not available in build settings: {evt.SceneId}");
                return;
            }

            isLoading = true;
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

        private void HandleUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            if (!string.IsNullOrEmpty(pendingSpawnPointId))
            {
                ResolveSpawnPoint(pendingSpawnPointId);
            }

            pendingSpawnPointId = null;
            isLoading = false;
            context?.Events.Publish(new SceneLoadedEvent(scene.name));
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
    }
}
