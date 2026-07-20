using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public sealed class UIManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private List<BasePanel> panels = new List<BasePanel>();

        private readonly Stack<BasePanel> panelStack = new Stack<BasePanel>();
        private GameContext context;

        public bool IsGameplayInputBlocked
        {
            get
            {
                foreach (var panel in panelStack)
                {
                    if (panel != null && panel.IsOpen && panel.PausesGame)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<OpenInventoryPressedEvent>(HandleOpenInventoryPressed);
            context.Events.Subscribe<OpenDiaryPressedEvent>(HandleOpenDiaryPressed);
            context.Events.Subscribe<OpenMapPressedEvent>(HandleOpenMapPressed);
            context.Events.Subscribe<PausePressedEvent>(HandlePausePressed);
            context.Events.Subscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
            context.Events.Subscribe<ContainerClosedEvent>(HandleContainerClosed);
            context.Events.Subscribe<CharacterDiedEvent>(HandleCharacterDied);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<OpenInventoryPressedEvent>(HandleOpenInventoryPressed);
                context.Events.Unsubscribe<OpenDiaryPressedEvent>(HandleOpenDiaryPressed);
                context.Events.Unsubscribe<OpenMapPressedEvent>(HandleOpenMapPressed);
                context.Events.Unsubscribe<PausePressedEvent>(HandlePausePressed);
                context.Events.Unsubscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
                context.Events.Unsubscribe<ContainerClosedEvent>(HandleContainerClosed);
                context.Events.Unsubscribe<CharacterDiedEvent>(HandleCharacterDied);
            }

            CloseAll();
            context = null;
        }

        public void RegisterScenePanels(IEnumerable<BasePanel> scenePanels)
        {
            if (scenePanels == null)
            {
                return;
            }

            foreach (var panel in scenePanels)
            {
                if (panel == null)
                {
                    continue;
                }

                panels.RemoveAll(existing => existing == null || existing.PanelId == panel.PanelId);
                panels.Add(panel);
            }
        }

        public void UnregisterScenePanels(IEnumerable<BasePanel> scenePanels)
        {
            if (scenePanels == null)
            {
                return;
            }

            foreach (var panel in scenePanels)
            {
                if (panel == null)
                {
                    continue;
                }

                Close(panel.PanelId);
                panels.Remove(panel);
            }
        }

        public BasePanel Open(PanelId panelId)
        {
            var panel = FindPanel(panelId);
            if (panel == null)
            {
                Debug.LogWarning($"Panel not registered: {panelId}");
                return null;
            }

            if (panel.IsOpen)
            {
                return panel;
            }

            panel.Open();
            panelStack.Push(panel);
            context?.Events.Publish(new PanelOpenedEvent(panelId));
            ApplyPauseState();
            return panel;
        }

        public void Close(PanelId panelId)
        {
            var panel = FindPanel(panelId);
            if (panel == null)
            {
                return;
            }

            panel.Close();
            RebuildStackWithout(panel);
            context?.Events.Publish(new PanelClosedEvent(panelId));
            ApplyPauseState();
        }

        public void Toggle(PanelId panelId)
        {
            var panel = FindPanel(panelId);
            if (panel != null && panel.IsOpen)
            {
                Close(panelId);
                return;
            }

            Open(panelId);
        }

        public bool IsOpen(PanelId panelId)
        {
            var panel = FindPanel(panelId);
            return panel != null && panel.IsOpen;
        }

        public void CloseAll()
        {
            while (panelStack.Count > 0)
            {
                var panel = panelStack.Pop();
                if (panel != null && panel.IsOpen)
                {
                    panel.Close();
                }
            }

            ApplyPauseState();
        }

        public void CloseTop()
        {
            if (panelStack.Count == 0)
            {
                return;
            }

            var panel = panelStack.Pop();
            panel.Close();
            context?.Events.Publish(new PanelClosedEvent(panel.PanelId));
            ApplyPauseState();
        }

        private BasePanel FindPanel(PanelId panelId)
        {
            return panels.Find(panel => panel != null && panel.PanelId == panelId);
        }

        private void RebuildStackWithout(BasePanel removedPanel)
        {
            var remaining = new Stack<BasePanel>();
            while (panelStack.Count > 0)
            {
                var panel = panelStack.Pop();
                if (panel != removedPanel)
                {
                    remaining.Push(panel);
                }
            }

            while (remaining.Count > 0)
            {
                panelStack.Push(remaining.Pop());
            }
        }

        private void ApplyPauseState()
        {
            foreach (var panel in panelStack)
            {
                if (panel != null && panel.IsOpen && panel.PausesGame)
                {
                    Time.timeScale = 0f;
                    return;
                }
            }

            Time.timeScale = 1f;
        }

        private void HandleOpenInventoryPressed(OpenInventoryPressedEvent evt)
        {
            Toggle(PanelId.Inventory);
        }

        private void HandleOpenDiaryPressed(OpenDiaryPressedEvent evt)
        {
            Toggle(PanelId.Diary);
        }

        private void HandleOpenMapPressed(OpenMapPressedEvent evt)
        {
            Toggle(PanelId.Map);
        }

        private void HandlePausePressed(PausePressedEvent evt)
        {
            Toggle(PanelId.System);
        }

        private void HandleOpenContainerRequested(OpenContainerRequestedEvent evt)
        {
            // InventoryPanel is now the complete container UI: it renders both
            // the scene container and the player's backpack.  The legacy
            // ContainerPanel and ShortcutBarPanel are separate placeholder
            // overlays and must not be opened here, otherwise they obscure the
            // authored inventory layout.
            Open(PanelId.Inventory);
        }

        private void HandleContainerClosed(ContainerClosedEvent evt)
        {
            Close(PanelId.Inventory);
        }

        private void HandleCharacterDied(CharacterDiedEvent evt)
        {
            Open(PanelId.Load);
        }
    }
}
