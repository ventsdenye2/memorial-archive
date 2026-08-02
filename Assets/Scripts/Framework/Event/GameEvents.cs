using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Framework.UI;
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

    // 仅由输入层发布的右键原始意图；CharacterSystem 根据当前装备和状态决定瞄准或格挡。
    public readonly struct SecondaryActionInputEvent
    {
        public SecondaryActionInputEvent(bool isHeld, Vector2 pointerWorldPosition)
        {
            IsHeld = isHeld;
            PointerWorldPosition = pointerWorldPosition;
        }

        public bool IsHeld { get; }
        public Vector2 PointerWorldPosition { get; }
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

    public readonly struct ContainerFocusChangedEvent
    {
        public ContainerFocusChangedEvent(string containerId) => ContainerId = containerId;
        public string ContainerId { get; }
    }

public readonly struct ContainerClosedEvent { }

    public readonly struct PanelOpenedEvent
    {
        public PanelOpenedEvent(PanelId panelId) => PanelId = panelId;
        public PanelId PanelId { get; }
    }

    public readonly struct PanelClosedEvent
    {
        public PanelClosedEvent(PanelId panelId) => PanelId = panelId;
        public PanelId PanelId { get; }
    }

    public readonly struct SaveCompletedEvent
    {
        public SaveCompletedEvent(int slotIndex, bool overwroteExisting)
        {
            SlotIndex = slotIndex;
            OverwroteExisting = overwroteExisting;
        }

        public int SlotIndex { get; }
        public bool OverwroteExisting { get; }
    }

    public readonly struct SaveFailedEvent
    {
        public SaveFailedEvent(int slotIndex, string reason)
        {
            SlotIndex = slotIndex;
            Reason = reason;
        }

        public int SlotIndex { get; }
        public string Reason { get; }
    }

    public readonly struct InventoryMoveFailedEvent
    {
        public InventoryMoveFailedEvent(string instanceId, string reason)
        {
            InstanceId = instanceId;
            Reason = reason;
        }

        public string InstanceId { get; }
        public string Reason { get; }
    }

    public readonly struct DodgeRequestedEvent
    {
        public DodgeRequestedEvent(Vector2 direction, float distanceDesignUnits, float durationSeconds)
        {
            Direction = direction;
            DistanceDesignUnits = distanceDesignUnits;
            DurationSeconds = durationSeconds;
        }

        public Vector2 Direction { get; }
        public float DistanceDesignUnits { get; }
        public float DurationSeconds { get; }
    }

    // View 层同步：Spine dodge 播放期间，PlayerMotor 暂停普通位移。
    public readonly struct DodgeAnimationStateChangedEvent
    {
        public DodgeAnimationStateChangedEvent(bool isPlaying) => IsPlaying = isPlaying;
        public bool IsPlaying { get; }
    }

    // dodge 播放结束后一次性结算到 Player/Rigidbody2D 的水平坐标增量。
    public readonly struct DodgePositionDeltaEvent
    {
        public DodgePositionDeltaEvent(Vector2 worldDelta) => WorldDelta = worldDelta;
        public Vector2 WorldDelta { get; }
    }

    public readonly struct Stage1GameplayStartedEvent { }

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

    public readonly struct StairTravelRequestedEvent
    {
        public StairTravelRequestedEvent(
            string message,
            string upSceneId,
            string upSpawnPointId,
            string downSceneId,
            string downSpawnPointId)
        {
            Message = message;
            UpSceneId = upSceneId;
            UpSpawnPointId = upSpawnPointId;
            DownSceneId = downSceneId;
            DownSpawnPointId = downSpawnPointId;
        }

        public string Message { get; }
        public string UpSceneId { get; }
        public string UpSpawnPointId { get; }
        public string DownSceneId { get; }
        public string DownSpawnPointId { get; }
        public bool CanGoUp => !string.IsNullOrEmpty(UpSceneId);
        public bool CanGoDown => !string.IsNullOrEmpty(DownSceneId);
    }

    public readonly struct InventoryChangedEvent { }
    public readonly struct ShortcutChangedEvent { }

    public readonly struct SelectedItemChangedEvent
    {
        public SelectedItemChangedEvent(InventoryItemInstance item) => Item = item;
        public InventoryItemInstance Item { get; }
    }

    public readonly struct CharacterEquipmentChangedEvent
    {
        public CharacterEquipmentChangedEvent(int primaryItemId, OffhandType offhandType)
        {
            PrimaryItemId = primaryItemId;
            OffhandType = offhandType;
        }

        public int PrimaryItemId { get; }
        public OffhandType OffhandType { get; }
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

    /// <summary>
    /// Published by the player View after physics movement.  CombatSystem uses
    /// this snapshot only to freeze an attack origin/direction; it never reads
    /// or controls the full CharacterSystem directly.
    /// </summary>
    public readonly struct PlayerPositionChangedEvent
    {
        public PlayerPositionChangedEvent(Vector2 position) => Position = position;
        public Vector2 Position { get; }
    }

    public readonly struct CharacterDiedEvent
    {
        public CharacterDiedEvent(float deathAnimationSeconds) => DeathAnimationSeconds = deathAnimationSeconds;
        public float DeathAnimationSeconds { get; }
    }

    public readonly struct CharacterActionStateChangedEvent
    {
        public CharacterActionStateChangedEvent(CharacterActionState state) => State = state;
        public CharacterActionState State { get; }
    }

    /// <summary>角色动画 View 在非循环动作结束时回传，仅用于角色状态机解锁。</summary>
    public readonly struct CharacterActionAnimationCompletedEvent
    {
        public CharacterActionAnimationCompletedEvent(CharacterActionState state)
        {
            State = state;
        }

        public CharacterActionState State { get; }
    }

    /// <summary>
    /// 角色状态机已通过本次攻击的动作与体力校验。CombatSystem 接收此事件
    /// 创建唯一攻击实例；武器 View 只在收到 AttackStartedEvent 后执行命中检测。
    /// </summary>
    public readonly struct CharacterAttackRequestedEvent
    {
        public CharacterAttackRequestedEvent(int itemId, int comboStage, Vector2 direction)
        {
            ItemId = itemId;
            ComboStage = comboStage;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }
        public int ItemId { get; }
        public int ComboStage { get; }
        public Vector2 Direction { get; }
    }

    public readonly struct CharacterDamageReceivedEvent
    {
        public CharacterDamageReceivedEvent(float finalDamage) => FinalDamage = Mathf.Max(0f, finalDamage);
        public float FinalDamage { get; }
    }

    public readonly struct PlayerAttackRequestedEvent
    {
        public PlayerAttackRequestedEvent(AttackContext attack) => Attack = attack;
        public AttackContext Attack { get; }
    }

    /// <summary>Asks CharacterSystem to validate and pay the shared action cost.</summary>
    public readonly struct CombatActionRequestedEvent
    {
        public CombatActionRequestedEvent(int attackInstanceId, float staminaCost)
        {
            AttackInstanceId = attackInstanceId;
            StaminaCost = staminaCost;
        }

        public int AttackInstanceId { get; }
        public float StaminaCost { get; }
    }

    public readonly struct CombatActionApprovedEvent
    {
        public CombatActionApprovedEvent(int attackInstanceId) => AttackInstanceId = attackInstanceId;
        public int AttackInstanceId { get; }
    }

    public readonly struct CombatActionRejectedEvent
    {
        public CombatActionRejectedEvent(int attackInstanceId, string reason)
        {
            AttackInstanceId = attackInstanceId;
            Reason = reason;
        }

        public int AttackInstanceId { get; }
        public string Reason { get; }
    }

    /// <summary>Consumed by a melee, firearm, or throwable View according to AttackKind.</summary>
    public readonly struct AttackStartedEvent
    {
        public AttackStartedEvent(AttackContext attack) => Attack = attack;
        public AttackContext Attack { get; }
    }

    public readonly struct AttackFailedEvent
    {
        public AttackFailedEvent(int weaponItemId, string reason)
        {
            WeaponItemId = weaponItemId;
            Reason = reason;
        }

        public int WeaponItemId { get; }
        public string Reason { get; }
    }

    /// <summary>Published by any Unity View that has confirmed a hit.</summary>
    public readonly struct CombatHitReportedEvent
    {
        public CombatHitReportedEvent(CombatHitReport report) => Report = report;
        public CombatHitReport Report { get; }
    }

    /// <summary>
    /// 投掷物等异步攻击在开始时延长自己的命中报告有效期。只延长已存在的
    /// AttackContext，不创建额外伤害入口。
    /// </summary>
    public readonly struct CombatAttackLifetimeRequestedEvent
    {
        public CombatAttackLifetimeRequestedEvent(int attackInstanceId, float minimumRemainingSeconds)
        {
            AttackInstanceId = attackInstanceId;
            MinimumRemainingSeconds = Mathf.Max(0.01f, minimumRemainingSeconds);
        }

        public int AttackInstanceId { get; }
        public float MinimumRemainingSeconds { get; }
    }

    public readonly struct DamageRequestedEvent
    {
        public DamageRequestedEvent(DamageRequest request) => Request = request;
        public DamageRequest Request { get; }
    }

    public readonly struct DamageAppliedEvent
    {
        public DamageAppliedEvent(string targetId, float amount)
        {
            Request = new DamageRequest(0, string.Empty, targetId, 0, DamageType.Physical, amount, Vector2.zero);
            Result = new DamageResult(amount, false, false);
        }

        public DamageAppliedEvent(DamageRequest request, DamageResult result)
        {
            Request = request;
            Result = result;
        }

        public DamageRequest Request { get; }
        public DamageResult Result { get; }
        public string TargetId => Request.TargetId;
        public float Amount => Result.FinalDamage;
    }

    public readonly struct ReloadRequestedEvent
    {
        public ReloadRequestedEvent(InventoryItemInstance weapon) => Weapon = weapon;
        public InventoryItemInstance Weapon { get; }
    }

    public readonly struct MonsterDiedEvent
    {
        public MonsterDiedEvent(string monsterInstanceId) => MonsterInstanceId = monsterInstanceId;
        public string MonsterInstanceId { get; }
    }

    public readonly struct MonsterDamagedEvent
    {
        public MonsterDamagedEvent(string monsterInstanceId, float amount, float remainingHealth, bool triggersHurt)
        {
            MonsterInstanceId = monsterInstanceId;
            Amount = amount;
            RemainingHealth = remainingHealth;
            TriggersHurt = triggersHurt;
        }

        public string MonsterInstanceId { get; }
        public float Amount { get; }
        public float RemainingHealth { get; }
        public bool TriggersHurt { get; }
    }

    public readonly struct MonsterStateChangedEvent
    {
        public MonsterStateChangedEvent(string monsterInstanceId, MonsterActionState state)
        {
            MonsterInstanceId = monsterInstanceId;
            State = state;
        }

        public string MonsterInstanceId { get; }
        public MonsterActionState State { get; }
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
