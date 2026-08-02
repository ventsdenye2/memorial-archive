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
        private string focusedContainerId;
private GameContext context;

        public bool IsGameplayInputBlocked
        {
            get
            {
                foreach (var panel in panelStack)
                {
                    if (panel != null && panel.IsOpen && panel.PanelId != PanelId.Hud)
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
            context.Events.Subscribe<ContainerFocusChangedEvent>(HandleContainerFocusChanged);
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
                context.Events.Unsubscribe<ContainerFocusChangedEvent>(HandleContainerFocusChanged);
context.Events.Unsubscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
                context.Events.Unsubscribe<ContainerClosedEvent>(HandleContainerClosed);
                context.Events.Unsubscribe<CharacterDiedEvent>(HandleCharacterDied);
            }

            CloseAll();
            focusedContainerId = null;
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
    if (panelId == PanelId.Inventory && !string.IsNullOrEmpty(focusedContainerId))
    {
        context?.Events.Publish(new OpenContainerRequestedEvent(focusedContainerId));
        return FindPanel(PanelId.Inventory);
    }

    if (panelId == PanelId.Inventory)
    {
        // Container availability is determined by the current ContainerPoint
        // focus, not by a previously opened inventory session.
        context?.Events.Publish(new ContainerClosedEvent());
    }

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

    if (panelId == PanelId.Hud)
    {
        return OpenDirect(panel);
    }

    // The Esc panel is a hard modal: only Esc may close it.
    if (IsOpen(PanelId.System) && panelId != PanelId.System)
    {
        return null;
    }

    CloseForExclusiveOpen(panelId);
    return OpenDirect(panel);
}

public void Close(PanelId panelId)
{
    if (panelId == PanelId.Inventory && IsContainerGroupOpen())
    {
        CloseContainerGroup();
        return;
    }

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
    var hadContainerGroup = IsContainerGroupOpen();
    while (panelStack.Count > 0)
    {
        var panel = panelStack.Pop();
        if (panel != null && panel.IsOpen)
        {
            panel.Close();
        }
    }

    var hud = FindPanel(PanelId.Hud);
    if (hud != null && hud.IsOpen)
    {
        hud.Close();
        context?.Events.Publish(new PanelClosedEvent(PanelId.Hud));
    }

    if (hadContainerGroup)
    {
        context?.Events.Publish(new ContainerClosedEvent());
    }

    ApplyPauseState();
}

public void CloseTop()
{
    if (IsContainerGroupOpen())
    {
        CloseContainerGroup();
        return;
    }

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
        if (panel != null && panel.IsOpen && panel.PanelId != PanelId.Hud)
        {
            Time.timeScale = 0f;
            return;
        }
    }

    Time.timeScale = 1f;
}

private void HandleOpenInventoryPressed(OpenInventoryPressedEvent evt)
{
    if (IsOpen(PanelId.Inventory))
    {
        Close(PanelId.Inventory);
        return;
    }

    Open(PanelId.Inventory);
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
    if (IsOpen(PanelId.System))
    {
        return;
    }

    OpenContainerGroup();
}

private void HandleContainerClosed(ContainerClosedEvent evt)
{
    CloseContainerGroup(false);
}

        private void HandleCharacterDied(CharacterDiedEvent evt)
        {
            Open(PanelId.Load);
        }

private void HandleContainerFocusChanged(ContainerFocusChangedEvent evt)
{
    focusedContainerId = evt.ContainerId;
}

private BasePanel OpenDirect(BasePanel panel)
{
    if (panel == null)
    {
        return null;
    }

    panel.Open();
    if (panel.PanelId != PanelId.Hud)
    {
        panelStack.Push(panel);
    }
    context?.Events.Publish(new PanelOpenedEvent(panel.PanelId));
    ApplyPauseState();
    return panel;
}

private void CloseForExclusiveOpen(PanelId openingPanelId)
{
    if (openingPanelId == PanelId.Hud)
    {
        return;
    }

    if (IsContainerGroupOpen())
    {
        CloseContainerGroup();
    }

    var openPanels = panelStack.ToArray();
    foreach (var openPanel in openPanels)
    {
        if (openPanel != null && openPanel.IsOpen && openPanel.PanelId != openingPanelId && openPanel.PanelId != PanelId.Hud)
        {
            openPanel.Close();
            context?.Events.Publish(new PanelClosedEvent(openPanel.PanelId));
        }
    }

    panelStack.Clear();
    ApplyPauseState();
}

private void OpenContainerGroup()
{
    if (IsContainerGroupOpen())
    {
        return;
    }

    // InventoryPanel owns the SceneContainer, backpack and shortcut-bar views.
    // The legacy Container/ShortcutBar panels are separate placeholder overlays
    // and must not be opened as part of this composite inventory screen.
    CloseForExclusiveOpen(PanelId.Inventory);
    OpenDirect(FindPanel(PanelId.Inventory));
}

private bool IsContainerGroupOpen()
{
    return !string.IsNullOrEmpty(focusedContainerId) && IsOpen(PanelId.Inventory);
}

private void CloseContainerGroup(bool notifyInventorySystem = true)
{
    var groupPanels = new[] { PanelId.Container, PanelId.Inventory, PanelId.ShortcutBar };
    foreach (var panelId in groupPanels)
    {
        var panel = FindPanel(panelId);
        if (panel != null && panel.IsOpen)
        {
            panel.Close();
            RebuildStackWithout(panel);
            context?.Events.Publish(new PanelClosedEvent(panelId));
        }
    }

    if (notifyInventorySystem)
    {
        context?.Events.Publish(new ContainerClosedEvent());
    }

    ApplyPauseState();
}

    }
}
