using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Stage1;
using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public sealed class CommonPanelActions : MonoBehaviour
    {
        [SerializeField] private PanelId ownerPanelId;

        public void CloseOwner()
        {
            GameRoot.Instance?.Context?.UI?.Close(ownerPanelId);
        }

        public void CloseTop()
        {
            GameRoot.Instance?.Context?.UI?.CloseTop();
        }

        public void OpenSettings() => GameRoot.Instance?.Context?.UI?.Open(PanelId.Settings);
        public void OpenSave() => GameRoot.Instance?.Context?.UI?.Open(PanelId.Save);
        public void OpenLoad() => GameRoot.Instance?.Context?.UI?.Open(PanelId.Load);

        public void ContinueGame()
        {
            GameRoot.Instance?.Context?.UI?.Close(PanelId.System);
        }

        public void ReturnToMainMenu()
        {
            GameRoot.Instance?.Context?.UI?.CloseAll();
            GameRoot.Instance?.Context?.Events.Publish(
                new SceneTransitionRequestedEvent(Stage1Ids.MainMenuSceneName, string.Empty));
        }

        public void CloseContainerGroup()
        {
            GameRoot.Instance?.Context?.Events.Publish(new ContainerClosedEvent());
        }

        public void SaveSlot0() => RequestSave(0);
        public void SaveSlot1() => RequestSave(1);
        public void SaveSlot2() => RequestSave(2);

        private static void RequestSave(int slotIndex)
        {
            GameRoot.Instance?.Context?.Events.Publish(new SaveRequestedEvent(slotIndex));
        }
    }
}
