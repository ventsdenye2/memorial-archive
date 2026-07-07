using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using UnityEngine;

namespace MemorialArchive.Framework.Event
{
    public readonly struct MoveInputEvent
    {
        public MoveInputEvent(Vector2 direction) => Direction = direction;
        public Vector2 Direction { get; }
    }

    public readonly struct RunInputEvent
    {
        public RunInputEvent(bool isRunning) => IsRunning = isRunning;
        public bool IsRunning { get; }
    }

    public readonly struct BlockInputEvent
    {
        public BlockInputEvent(bool isBlocking) => IsBlocking = isBlocking;
        public bool IsBlocking { get; }
    }

    public readonly struct AimInputEvent
    {
        public AimInputEvent(bool isAiming, Vector2 pointerWorldPosition)
        {
            IsAiming = isAiming;
            PointerWorldPosition = pointerWorldPosition;
        }

        public bool IsAiming { get; }
        public Vector2 PointerWorldPosition { get; }
    }

    public readonly struct DodgePressedEvent { }
    public readonly struct InteractPressedEvent { }
    public readonly struct PrimaryActionPressedEvent { }
    public readonly struct ReloadPressedEvent { }
    public readonly struct OpenInventoryPressedEvent { }
    public readonly struct OpenDiaryPressedEvent { }
    public readonly struct OpenMapPressedEvent { }
    public readonly struct PausePressedEvent { }

    public readonly struct ShortcutEquipPressedEvent
    {
        public ShortcutEquipPressedEvent(int slotIndex) => SlotIndex = slotIndex;
        public int SlotIndex { get; }
    }

    public readonly struct InteractionFocusChangedEvent
    {
        public InteractionFocusChangedEvent(string interactionId, InteractionType interactionType, bool hasFocus)
        {
            InteractionId = interactionId;
            InteractionType = interactionType;
            HasFocus = hasFocus;
        }

        public string InteractionId { get; }
        public InteractionType InteractionType { get; }
        public bool HasFocus { get; }
    }

    public readonly struct SaveRequestedEvent
    {
        public SaveRequestedEvent(int slotIndex) => SlotIndex = slotIndex;
        public int SlotIndex { get; }
    }

    public readonly struct OpenContainerRequestedEvent
    {
        public OpenContainerRequestedEvent(string containerId) => ContainerId = containerId;
        public string ContainerId { get; }
    }

    public readonly struct InspectRequestedEvent
    {
        public InspectRequestedEvent(string inspectId) => InspectId = inspectId;
        public string InspectId { get; }
    }

    public readonly struct DoorInteractRequestedEvent
    {
        public DoorInteractRequestedEvent(string doorId) => DoorId = doorId;
        public string DoorId { get; }
    }

    public readonly struct PuzzleInteractRequestedEvent
    {
        public PuzzleInteractRequestedEvent(string puzzleId) => PuzzleId = puzzleId;
        public string PuzzleId { get; }
    }

    public readonly struct SceneTransitionRequestedEvent
    {
        public SceneTransitionRequestedEvent(string sceneId, string spawnPointId)
        {
            SceneId = sceneId;
            SpawnPointId = spawnPointId;
        }

        public string SceneId { get; }
        public string SpawnPointId { get; }
    }

    public readonly struct InventoryChangedEvent { }
    public readonly struct ShortcutChangedEvent { }

    public readonly struct SelectedItemChangedEvent
    {
        public SelectedItemChangedEvent(InventoryItemInstance item) => Item = item;
        public InventoryItemInstance Item { get; }
    }

    public readonly struct ItemUseRequestedEvent
    {
        public ItemUseRequestedEvent(InventoryItemInstance item)
        {
            Item = item;
        }

        public InventoryItemInstance Item { get; }
    }

    public readonly struct ItemUseFailedEvent
    {
        public ItemUseFailedEvent(int itemId, string reason)
        {
            ItemId = itemId;
            Reason = reason;
        }

        public int ItemId { get; }
        public string Reason { get; }
    }

    public readonly struct ItemEffectAppliedEvent
    {
        public ItemEffectAppliedEvent(int itemId, string effectId)
        {
            ItemId = itemId;
            EffectId = effectId;
        }

        public int ItemId { get; }
        public string EffectId { get; }
    }

    public readonly struct AmmoReloadRequestedEvent
    {
        public AmmoReloadRequestedEvent(int ammoItemId) => AmmoItemId = ammoItemId;
        public int AmmoItemId { get; }
    }

    public readonly struct CharacterStatsChangedEvent { }

    public readonly struct CharacterDiedEvent
    {
        public CharacterDiedEvent(float deathAnimationSeconds) => DeathAnimationSeconds = deathAnimationSeconds;
        public float DeathAnimationSeconds { get; }
    }

    public readonly struct PlayerAttackRequestedEvent { }
    public readonly struct DamageAppliedEvent
    {
        public DamageAppliedEvent(string targetId, int amount)
        {
            TargetId = targetId;
            Amount = amount;
        }

        public string TargetId { get; }
        public int Amount { get; }
    }

    public readonly struct MonsterDiedEvent
    {
        public MonsterDiedEvent(string monsterInstanceId) => MonsterInstanceId = monsterInstanceId;
        public string MonsterInstanceId { get; }
    }

    public readonly struct RoomEnteredEvent
    {
        public RoomEnteredEvent(string roomId) => RoomId = roomId;
        public string RoomId { get; }
    }

    public readonly struct RoomExitedEvent
    {
        public RoomExitedEvent(string roomId) => RoomId = roomId;
        public string RoomId { get; }
    }

    public readonly struct SceneLoadedEvent
    {
        public SceneLoadedEvent(string sceneId)
        {
            SceneId = sceneId;
        }

        public string SceneId { get; }
    }

    public readonly struct DoorUnlockedEvent
    {
        public DoorUnlockedEvent(string doorId) => DoorId = doorId;
        public string DoorId { get; }
    }

    public readonly struct PuzzleSolvedEvent
    {
        public PuzzleSolvedEvent(string puzzleId) => PuzzleId = puzzleId;
        public string PuzzleId { get; }
    }

    public readonly struct PuzzleFailedEvent
    {
        public PuzzleFailedEvent(string puzzleId) => PuzzleId = puzzleId;
        public string PuzzleId { get; }
    }

    public readonly struct StoryUnlockedEvent
    {
        public StoryUnlockedEvent(string storyId) => StoryId = storyId;
        public string StoryId { get; }
    }

    public readonly struct NoteUnlockedEvent
    {
        public NoteUnlockedEvent(string noteId) => NoteId = noteId;
        public string NoteId { get; }
    }

    public readonly struct DiaryUpdatedEvent { }

    public readonly struct BlackScreenStoryStartedEvent
    {
        public BlackScreenStoryStartedEvent(string storyId) => StoryId = storyId;
        public string StoryId { get; }
    }

    public readonly struct BlackScreenStoryFinishedEvent
    {
        public BlackScreenStoryFinishedEvent(string storyId) => StoryId = storyId;
        public string StoryId { get; }
    }
}
