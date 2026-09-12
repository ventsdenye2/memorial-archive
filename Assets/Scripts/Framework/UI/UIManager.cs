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
        private readonly HashSet<PanelId> persistentPanelIds = new HashSet<PanelId>();
        private GameContext context;
        private string focusedContainerId;
        private readonly HashSet<object> pauseOwners = new HashSet<object>();

        public void SetPauseOwner(object owner, bool paused)
        {
            if (paused) pauseOwners.Add(owner); else pauseOwners.Remove(owner);
            ApplyPauseState();
        }

        public bool IsGameplayInputBlocked
        {
            get
            {
                if (GameRoot.Instance?.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideSystem>()?.BlocksInput == true) return true;
                foreach (var panel in panelStack)
                {
                    if (panel != null && panel.IsOpen && !IsNonModalOverlay(panel.PanelId))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Initialize(GameContext gameContext)
        {
            context = gameContext;
            context.Events.Subscribe<OpenInventoryPressedEvent>(HandleOpenInventoryPressed);
            context.Events.Subscribe<OpenDiaryPressedEvent>(HandleOpenDiaryPressed);
            context.Events.Subscribe<OpenMapPressedEvent>(HandleOpenMapPressed);
            context.Events.Subscribe<PausePressedEvent>(HandlePausePressed);
            context.Events.Subscribe<ContainerFocusChangedEvent>(HandleContainerFocusChanged);
            context.Events.Subscribe<OpenContainerRequestedEvent>(HandleOpenContainerRequested);
            context.Events.Subscribe<ContainerClosedEvent>(HandleContainerClosed);
            context.Events.Subscribe<StairTravelRequestedEvent>(HandleStairTravelRequested);
            context.Events.Subscribe<RoomTravelConfirmationRequestedEvent>(HandleRoomTravelConfirmationRequested);
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
                context.Events.Unsubscribe<StairTravelRequestedEvent>(HandleStairTravelRequested);
                context.Events.Unsubscribe<RoomTravelConfirmationRequestedEvent>(HandleRoomTravelConfirmationRequested);
                context.Events.Unsubscribe<CharacterDiedEvent>(HandleCharacterDied);
            }

            CloseAll();
            focusedContainerId = null;
            context = null;
        }

        public void RegisterPersistentPanel(BasePanel panel)
        {
            RegisterScenePanels(new[] { panel });
            persistentPanelIds.Add(panel.PanelId);
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

                if (persistentPanelIds.Contains(panel.PanelId) && FindPanel(panel.PanelId) != panel)
                {
                    panel.Close();
                    continue;
                }

                foreach (var existing in panels.ToArray())
                {
                    if (existing == null || existing == panel || existing.PanelId != panel.PanelId)
                    {
                        continue;
                    }

                    existing.Close();
                    RebuildStackWithout(existing);
                }

                panels.RemoveAll(existing => existing == null || existing.PanelId == panel.PanelId);
                panels.Add(panel);
            }

            ApplyPauseState();
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

                if (panel.IsOpen)
                {
                    panel.Close();
                    RebuildStackWithout(panel);
                    context?.Events.Publish(new PanelClosedEvent(panel.PanelId));
                }

                panels.Remove(panel);
            }

            ApplyPauseState();
        }

        public BasePanel Open(PanelId panelId)
        {
            if (IsOpen(PanelId.Narrative) && panelId != PanelId.Narrative) return null;
            if (panelId == PanelId.Inventory && !string.IsNullOrEmpty(focusedContainerId))
            {
                context?.Events.Publish(new OpenContainerRequestedEvent(focusedContainerId));
                return FindPanel(PanelId.Inventory);
            }

            if (panelId == PanelId.Inventory)
            {
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

            if (IsNonModalOverlay(panelId))
            {
                return OpenDirect(panel);
            }

            // Esc is a hard modal. It must be closed before any other panel opens.
            if (IsOpen(PanelId.System) && panelId != PanelId.System)
            {
                return null;
            }

            CloseForExclusiveOpen(panelId);
            return OpenDirect(panel);
        }

        public BasePanel OpenFromSystem(PanelId panelId)
        {
            if (panelId != PanelId.Settings && panelId != PanelId.Load)
            {
                Debug.LogWarning($"System panel cannot open: {panelId}");
                return null;
            }

            if (!IsOpen(PanelId.System))
            {
                Debug.LogWarning($"Cannot open {panelId} from System because the System panel is closed.");
                return null;
            }

            Close(PanelId.System);
            return Open(panelId);
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
            if (IsOpen(panelId))
            {
                Close(panelId);
            }
            else
            {
                Open(panelId);
            }
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

            // A panel can be opened by its authored lifecycle (for example a
            // scene-start panel) without ever entering the modal stack.  A
            // scene transition must still close that panel; otherwise its
            // visual state survives the transition and can leave the UI in a
            // stale modal state after loading a save.
            foreach (var panel in panels.ToArray())
            {
                if (panel == null || !panel.IsOpen)
                {
                    continue;
                }

                panel.Close();
                if (panel.PanelId == PanelId.Hud)
                {
                    context?.Events.Publish(new PanelClosedEvent(PanelId.Hud));
                }
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

        private BasePanel FindPanel(PanelId panelId) =>
            panels.Find(panel => panel != null && panel.PanelId == panelId);

        private BasePanel OpenDirect(BasePanel panel)
        {
            if (panel == null)
            {
                return null;
            }

            panel.Open();
            if (!IsNonModalOverlay(panel.PanelId))
            {
                panelStack.Push(panel);
            }

            context?.Events.Publish(new PanelOpenedEvent(panel.PanelId));
            ApplyPauseState();
            return panel;
        }

        private void CloseForExclusiveOpen(PanelId openingPanelId)
        {
            if (IsNonModalOverlay(openingPanelId))
            {
                return;
            }

            if (IsContainerGroupOpen())
            {
                CloseContainerGroup();
            }

            foreach (var openPanel in panelStack.ToArray())
            {
                if (openPanel == null || !openPanel.IsOpen || openPanel.PanelId == openingPanelId)
                {
                    continue;
                }

                openPanel.Close();
                context?.Events.Publish(new PanelClosedEvent(openPanel.PanelId));
            }

            panelStack.Clear();
            ApplyPauseState();
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
            if (pauseOwners.Count > 0) { Time.timeScale = 0f; return; }
            foreach (var panel in panelStack)
            {
                if (panel != null && panel.IsOpen && !IsNonModalOverlay(panel.PanelId))
                {
                    Time.timeScale = 0f;
                    return;
                }
            }

            Time.timeScale = 1f;
        }

        /// <summary>
        /// 非模态覆盖层：不进入模态栈、不暂停游戏、不阻塞游戏输入、不被其它面板顶掉。
        /// HUD 与 GuideOverlay 属于此类。
        /// </summary>
        private static bool IsNonModalOverlay(PanelId panelId)
        {
            return panelId == PanelId.Hud || panelId == PanelId.GuideOverlay;
        }

        private void HandleOpenInventoryPressed(OpenInventoryPressedEvent evt) => Toggle(PanelId.Inventory);
        private void HandleOpenDiaryPressed(OpenDiaryPressedEvent evt)
        { if (GameRoot.Instance?.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem>()?.MapUnlocked != false) Toggle(PanelId.Diary); }
        private void HandleOpenMapPressed(OpenMapPressedEvent evt)
        { if (GameRoot.Instance?.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem>()?.MapUnlocked != false) Toggle(PanelId.Map); }
        private void HandlePausePressed(PausePressedEvent evt) => Toggle(PanelId.System);
        private void HandleCharacterDied(CharacterDiedEvent evt) => Open(PanelId.Load);

        private void HandleContainerFocusChanged(ContainerFocusChangedEvent evt)
        {
            focusedContainerId = evt.ContainerId;
        }

        private void HandleOpenContainerRequested(OpenContainerRequestedEvent evt)
        {
            if (!IsOpen(PanelId.System))
            {
                OpenContainerGroup();
            }
        }

        private void HandleContainerClosed(ContainerClosedEvent evt)
        {
            CloseContainerGroup(false);
        }

        private void HandleStairTravelRequested(StairTravelRequestedEvent evt)
        {
            if (IsOpen(PanelId.System))
            {
                return;
            }

            var panel = Open(PanelId.StairTravel) as StairTravelPanel;
            panel?.Show(evt);
        }

        private void HandleRoomTravelConfirmationRequested(RoomTravelConfirmationRequestedEvent evt)
        {
            if (IsOpen(PanelId.System))
            {
                return;
            }

            var panel = Open(PanelId.RoomTravelConfirm) as RoomTravelConfirmPanel;
            panel?.Show(evt);
        }

        private void OpenContainerGroup()
        {
            if (IsContainerGroupOpen())
            {
                return;
            }

            // InventoryPanel owns the SceneContainer, backpack and shortcut bar.
            CloseForExclusiveOpen(PanelId.Inventory);
            var opened = OpenDirect(FindPanel(PanelId.Inventory));
            if (opened != null)
                MemorialArchive.Framework.Audio.AudioSystem.Play("sfx_scene_container_open");
        }

        private bool IsContainerGroupOpen() =>
            !string.IsNullOrEmpty(focusedContainerId) && IsOpen(PanelId.Inventory);

        private void CloseContainerGroup(bool notifyInventorySystem = true)
        {
            foreach (var panelId in new[] { PanelId.Container, PanelId.Inventory, PanelId.ShortcutBar })
            {
                var panel = FindPanel(panelId);
                if (panel == null || !panel.IsOpen)
                {
                    continue;
                }

                panel.Close();
                RebuildStackWithout(panel);
                context?.Events.Publish(new PanelClosedEvent(panelId));
            }

            if (notifyInventorySystem)
            {
                context?.Events.Publish(new ContainerClosedEvent());
            }

            ApplyPauseState();
        }
    }
}
