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
        private readonly LightingSystem lighting;
        private GameContext context;
        private string focusedInteractionId;
        private InteractionType focusedInteractionType = InteractionType.None;

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
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<InteractionFocusChangedEvent>(HandleInteractionFocusChanged);
                context.Events.Unsubscribe<InteractPressedEvent>(HandleInteractPressed);
                context.Events.Unsubscribe<RoomEnteredEvent>(HandleRoomEntered);
            }

            context = null;
            interactions.Clear();
            rooms.Clear();
            focusedInteractionId = null;
            focusedInteractionType = InteractionType.None;
        }

        public void ResetForNewGame()
        {
            interactions.Clear();
            rooms.Clear();
            focusedInteractionId = null;
            focusedInteractionType = InteractionType.None;
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
    if (evt.HasFocus)
    {
        focusedInteractionId = evt.InteractionId;
        focusedInteractionType = evt.InteractionType;
        RegisterInteraction(evt.InteractionId, evt.InteractionType);
        if (evt.InteractionType == InteractionType.Container)
        {
            var config = context.Configs.GetInteraction(evt.InteractionId);
            context.Events.Publish(new ContainerFocusChangedEvent(GetContainerId(config, evt.InteractionId)));
        }
        else
        {
            // Only ContainerPoint focus is allowed to expose SceneContainer.
            context.Events.Publish(new ContainerFocusChangedEvent(null));
        }
        return;
    }

    if (focusedInteractionId == evt.InteractionId)
    {
        focusedInteractionId = null;
        focusedInteractionType = InteractionType.None;
        context.Events.Publish(new ContainerFocusChangedEvent(null));
    }
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

            // 黑暗门禁：无光源时除场景切换（含楼梯）外全部拦截。
            if (type != InteractionType.SceneExit && lighting != null && !lighting.IsPlayerInLight())
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
