using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Stage1;
using UnityEngine;

namespace MemorialArchive.Framework.Scene
{
    public sealed class MainMenuFlowController : MonoBehaviour
    {
        public void NewGame()
        {
            GameRoot.Instance?.Context?.UI?.Open(PanelId.ConfirmDialog);
        }

        public void CancelNewGame()
        {
            GameRoot.Instance?.Context?.UI?.Close(PanelId.ConfirmDialog);
        }

        public void ConfirmNewGame()
        {
            var context = GameRoot.Instance?.Context;
            if (context == null)
            {
                Debug.LogError("Cannot start a new game because GameRoot is not initialized.");
                return;
            }

            context.UI.CloseAll();
            context.Events.Publish(new SceneTransitionRequestedEvent(Stage1Ids.GameplaySceneName, Stage1Ids.SpawnPoint));
        }

        public void ContinueGamePlaceholder()
        {
            Debug.Log("Continue game placeholder");
            GameRoot.Instance?.Context?.UI?.Open(PanelId.Load);
        }

        public void LoadGamePlaceholder()
        {
            Debug.Log("Load game placeholder");
            GameRoot.Instance?.Context?.UI?.Open(PanelId.Load);
        }

        public void OpenSettings()
        {
            GameRoot.Instance?.Context?.UI?.Open(PanelId.Settings);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Exit game requested (Editor placeholder).");
#else
            Application.Quit();
#endif
        }
    }
}
