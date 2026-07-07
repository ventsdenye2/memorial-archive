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

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<OpenInventoryPressedEvent>(HandleOpenInventoryPressed);
            context.Events.Subscribe<OpenDiaryPressedEvent>(HandleOpenDiaryPressed);
            context.Events.Subscribe<OpenMapPressedEvent>(HandleOpenMapPressed);
            context.Events.Subscribe<PausePressedEvent>(HandlePausePressed);
            context.Events.Subscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
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
                context.Events.Unsubscribe<CharacterDiedEvent>(HandleCharacterDied);
            }

            context = null;
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
            Open(PanelId.Inventory);
        }

        private void HandleOpenDiaryPressed(OpenDiaryPressedEvent evt)
        {
            Open(PanelId.Diary);
        }

        private void HandleOpenMapPressed(OpenMapPressedEvent evt)
        {
            Open(PanelId.Map);
        }

        private void HandlePausePressed(PausePressedEvent evt)
        {
            Open(PanelId.System);
        }

        private void HandleOpenContainerRequested(OpenContainerRequestedEvent evt)
        {
            Open(PanelId.Container);
            Open(PanelId.Inventory);
            Open(PanelId.ShortcutBar);
        }

        private void HandleCharacterDied(CharacterDiedEvent evt)
        {
            Open(PanelId.Load);
        }
    }
}
