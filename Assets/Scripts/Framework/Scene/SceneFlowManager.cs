using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Framework.Scene
{
    public sealed class SceneFlowManager : IGameSystem
    {
        private GameContext context;

        public void Initialize(GameContext context)
        {
            this.context = context;
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

            context = null;
        }

        private void HandleSceneTransitionRequested(SceneTransitionRequestedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.SceneId))
            {
                return;
            }

            SceneManager.LoadScene(evt.SceneId);
        }

        private void HandleUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            context?.Events.Publish(new SceneLoadedEvent(scene.name));
        }
    }
}
