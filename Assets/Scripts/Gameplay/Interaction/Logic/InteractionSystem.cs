using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Lighting.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Interaction.Logic
{
    public sealed class InteractionSystem : IGameSystem, ISaveModule, INewGameResettable
    {
        private readonly Dictionary<string, InteractionRuntimeData> interactions = new Dictionary<string, InteractionRuntimeData>();
        private readonly Dictionary<string, RoomStateData> rooms = new Dictionary<string, RoomStateData>();
        private readonly Dictionary<string, FocusCandidate> activeFocus = new Dictionary<string, FocusCandidate>();
        private readonly LightingSystem lighting;
        private GameContext context;
        private string focusedInteractionId;
        private InteractionType focusedInteractionType = InteractionType.None;
        private long focusOrder;

        private sealed class FocusCandidate
        {
            public InteractionType type;
            public long order;
        }

        public string ModuleKey => "interactions";

        public InteractionSystem(LightingSystem lighting = null)
        {
            this.lighting = lighting;
        }

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<InteractionFocusChangedEvent>(HandleInteractionFocusChanged);
            context.Events.Subscribe<InteractPressedEvent>(HandleInteractPressed);
            context.Events.Subscribe<RoomEnteredEvent>(HandleRoomEntered);
            context.Events.Subscribe<SceneLoadedEvent>(HandleSceneLoaded);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<InteractionFocusChangedEvent>(HandleInteractionFocusChanged);
                context.Events.Unsubscribe<InteractPressedEvent>(HandleInteractPressed);
                context.Events.Unsubscribe<RoomEnteredEvent>(HandleRoomEntered);
                context.Events.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);
            }

            context = null;
            interactions.Clear();
            rooms.Clear();
            ClearFocus();
        }

        public void ResetForNewGame()
        {
            interactions.Clear();
            rooms.Clear();
            ClearFocus();
        }

        public void RegisterInteraction(string interactionId, InteractionType interactionType, string linkedContainerId = null)
        {
            if (string.IsNullOrEmpty(interactionId))
            {
                return;
            }

            if (!interactions.TryGetValue(interactionId, out var data))
            {
                data = new InteractionRuntimeData { interactionId = interactionId };
                interactions[interactionId] = data;
            }

            data.interactionType = interactionType;
            data.linkedContainerId = linkedContainerId;
        }

        public object CaptureSaveData()
        {
            return new InteractionSaveData
            {
                interactions = new List<InteractionRuntimeData>(interactions.Values),
                rooms = new List<RoomStateData>(rooms.Values)
            };
        }

        public void RestoreSaveData(string json)
        {
            ClearFocus();
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var saveData = JsonUtility.FromJson<InteractionSaveData>(json);
            if (saveData == null)
            {
                return;
            }

            interactions.Clear();
            rooms.Clear();

            if (saveData.interactions != null)
            {
                foreach (var interaction in saveData.interactions)
                {
                    if (interaction != null && !string.IsNullOrEmpty(interaction.interactionId))
                    {
                        interactions[interaction.interactionId] = interaction;
                    }
                }
            }

            if (saveData.rooms != null)
            {
                foreach (var room in saveData.rooms)
                {
                    if (room != null && !string.IsNullOrEmpty(room.roomId))
                    {
                        rooms[room.roomId] = room;
                    }
                }
            }

        }

        private void HandleInteractionFocusChanged(InteractionFocusChangedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.InteractionId)) return;

            if (evt.HasFocus)
            {
                activeFocus[evt.InteractionId] = new FocusCandidate { type = evt.InteractionType, order = ++focusOrder };
                RegisterInteraction(evt.InteractionId, evt.InteractionType);
            }
            else
            {
                activeFocus.Remove(evt.InteractionId);
            }

            RefreshFocusedInteraction();
        }

        private void RefreshFocusedInteraction()
        {
            string selectedId = null;
            FocusCandidate selected = null;
            foreach (var pair in activeFocus)
            {
                if (selected == null || IsBetterFocus(pair.Key, pair.Value, selectedId, selected))
                {
                    selectedId = pair.Key;
                    selected = pair.Value;
                }
            }

            focusedInteractionId = selectedId;
            focusedInteractionType = selected != null ? selected.type : InteractionType.None;
            if (selected == null || selected.type != InteractionType.Container)
            {
                context.Events.Publish(new ContainerFocusChangedEvent(null));
                return;
            }

            var config = context.Configs?.GetInteraction(selectedId);
            context.Events.Publish(new ContainerFocusChangedEvent(GetContainerId(config, selectedId)));
        }

        private bool IsBetterFocus(string candidateId, FocusCandidate candidate, string currentId, FocusCandidate current)
        {
            var candidateLight = candidate.type == InteractionType.LightSource;
            var currentLight = current.type == InteractionType.LightSource;
            if (candidateLight && currentLight)
            {
                var candidateSpecial = IsSpecialLight(candidateId);
                var currentSpecial = IsSpecialLight(currentId);
                if (candidateSpecial != currentSpecial) return candidateSpecial;
            }
            return candidate.order > current.order;
        }

        private bool IsSpecialLight(string candidateId)
        {
            var config = context?.Configs?.GetLightSource(candidateId);
            return config != null ? config.IsSpecial : lighting != null && lighting.IsSpecialLight(candidateId);
        }

        private void HandleInteractPressed(InteractPressedEvent evt)
        {
            if (string.IsNullOrEmpty(focusedInteractionId))
            {
                return;
            }

            var runtimeData = GetRuntimeData(focusedInteractionId);
            if (runtimeData != null && !runtimeData.isEnabled)
            {
                return;
            }

            var config = context.Configs.GetInteraction(focusedInteractionId);
            var type = config != null ? config.InteractionType : focusedInteractionType;

            // 恢复型交互不能被黑暗自身锁死：出口可脱离危险，灯具可恢复照明。
            if (!InteractionLightingPolicy.CanAttemptInDarkness(type) &&
                lighting != null &&
                !lighting.IsPlayerInLight())
            {
                context.Events.Publish(new InteractionBlockedInDarkEvent(type));
                return;
            }

            DispatchInteraction(config, type);
        }

        private void DispatchInteraction(InteractionConfig config, InteractionType type)
        {
            var interactionId = config != null ? config.InteractionId : focusedInteractionId;
            switch (type)
            {
                case InteractionType.Container:
                    context.Events.Publish(new OpenContainerRequestedEvent(GetContainerId(config, interactionId)));
                    break;
                case InteractionType.ItemPickup:
                    // ItemPickup is not a scene-container interaction. Its
                    // dedicated pickup behavior can be added independently.
                    break;
                case InteractionType.Inspect:
                    context.Events.Publish(new InspectRequestedEvent(interactionId));
                    break;
                case InteractionType.Door:
                    context.Events.Publish(new DoorInteractRequestedEvent(interactionId));
                    break;
                case InteractionType.Puzzle:
                    context.Events.Publish(new PuzzleInteractRequestedEvent(config != null ? config.PuzzleId : interactionId));
                    break;
                case InteractionType.SavePoint:
                    MemorialArchive.Framework.Audio.AudioSystem.Play("sfx_scene_save_phone");
                    context.UI.Open(PanelId.Save);
                    break;
                case InteractionType.SceneExit:
                    if (config != null && config.HasStairDestinations)
                    {
                        context.Events.Publish(new StairTravelRequestedEvent(
                            config.StairPrompt,
                            config.StairUpSceneId,
                            config.StairUpSpawnPointId,
                            config.StairDownSceneId,
                            config.StairDownSpawnPointId));
                    }
                    else if (config != null)
                    {
                        if (config.RequiresConfirmation)
                        {
                            context.Events.Publish(new RoomTravelConfirmationRequestedEvent(
                                config.ConfirmationMessage,
                                config.TransitionSceneId,
                                config.TransitionSpawnPointId));
                        }
                        else
                        {
                            context.Events.Publish(new SceneTransitionRequestedEvent(config.TransitionSceneId, config.TransitionSpawnPointId));
                    }
                        }
                    break;
                case InteractionType.LightSource:
                    context.Events.Publish(new LightSourceInteractRequestedEvent(interactionId));
                    break;
                case InteractionType.NotePickup:
                    context.Events.Publish(new NoteUnlockedEvent(config != null ? config.NoteId : interactionId));
                    context.Events.Publish(new DiaryUpdatedEvent());
                    MarkCompleted(interactionId);
                    break;
                case InteractionType.HideSpot:
                case InteractionType.Npc:
                default:
                    context.Events.Publish(new InspectRequestedEvent(interactionId));
                    break;
            }
        }

        private void HandleRoomEntered(RoomEnteredEvent evt)
        {
            var roomId = evt.RoomId;
            if (string.IsNullOrEmpty(roomId))
            {
                return;
            }

            if (!rooms.TryGetValue(roomId, out var room))
            {
                room = new RoomStateData { roomId = roomId };
                rooms[roomId] = room;
            }

            room.visited = true;
        }

        private void HandleSceneLoaded(SceneLoadedEvent evt)
        {
            ClearFocus();
            context.Events.Publish(new ContainerFocusChangedEvent(null));
        }

        private void ClearFocus()
        {
            activeFocus.Clear();
            focusOrder = 0;
            focusedInteractionId = null;
            focusedInteractionType = InteractionType.None;
        }

        private void MarkCompleted(string interactionId)
        {
            var data = GetRuntimeData(interactionId);
            if (data != null)
            {
                data.isCompleted = true;
            }
        }

        private InteractionRuntimeData GetRuntimeData(string interactionId)
        {
            if (string.IsNullOrEmpty(interactionId))
            {
                return null;
            }

            interactions.TryGetValue(interactionId, out var data);
            return data;
        }

        private string GetContainerId(InteractionConfig config, string interactionId)
        {
            if (config != null && !string.IsNullOrEmpty(config.ContainerId))
            {
                return config.ContainerId;
            }

            var runtimeData = GetRuntimeData(interactionId);
            if (runtimeData != null && !string.IsNullOrEmpty(runtimeData.linkedContainerId))
            {
                return runtimeData.linkedContainerId;
            }

            return interactionId;
        }
    }
}
