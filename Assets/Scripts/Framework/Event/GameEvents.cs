using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Item.Data;
using MemorialArchive.Gameplay.Lighting.Data;
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

    public enum PrimaryActionPhase { Started, Updated, Released, Canceled }

    /// <summary>左键完整生命周期；投掷物用它实现按住瞄准、松开投掷。</summary>
    public readonly struct PrimaryActionPhaseEvent
    {
        public PrimaryActionPhaseEvent(PrimaryActionPhase phase, Vector2 pointerWorldPosition)
        {
            Phase = phase;
            PointerWorldPosition = pointerWorldPosition;
        }
        public PrimaryActionPhase Phase { get; }
        public Vector2 PointerWorldPosition { get; }
    }

    public readonly struct ThrowableAimChangedEvent
    {
        public ThrowableAimChangedEvent(bool isAiming, int itemId, Vector2 targetWorldPosition)
        {
            IsAiming = isAiming;
            ItemId = itemId;
            TargetWorldPosition = targetWorldPosition;
        }
        public bool IsAiming { get; }
        public int ItemId { get; }
        public Vector2 TargetWorldPosition { get; }
    }

    public readonly struct DodgePressedEvent { }
    public readonly struct InteractPressedEvent { }
    public readonly struct LanternTogglePressedEvent { }
    public readonly struct PrimaryActionPressedEvent { }
    public readonly struct ReloadPressedEvent { }
    public readonly struct OpenInventoryPressedEvent { }
    public readonly struct OpenDiaryPressedEvent { }
    public readonly struct OpenMapPressedEvent { }
    public readonly struct PausePressedEvent { }

    // 当前版本的临时调试入口；正式版发布前移除。
    public readonly struct DebugModeToggledEvent { }
    public readonly struct DebugDarknessOverlayToggleRequestedEvent { }

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

    public readonly struct LoadRequestedEvent
    {
        public LoadRequestedEvent(int slotIndex) => SlotIndex = slotIndex;
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

    public readonly struct LoadCompletedEvent
    {
        public LoadCompletedEvent(int slotIndex) => SlotIndex = slotIndex;
        public int SlotIndex { get; }
    }

    public readonly struct LoadFailedEvent
    {
        public LoadFailedEvent(int slotIndex, string reason)
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

    // Dodge 位移期间同步视觉抛物线进度；真实 Y 坐标仍锁在地面线上。
    public readonly struct DodgeMotionProgressEvent
    {
        public DodgeMotionProgressEvent(float normalizedProgress) => NormalizedProgress = normalizedProgress;
        public float NormalizedProgress { get; }
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

    // ---- 光照系统事件 ----

    /// <summary>交互系统分发灯具交互：InteractionSystem → LightingSystem。</summary>
    public readonly struct LightSourceInteractRequestedEvent
    {
        public LightSourceInteractRequestedEvent(string lightId) => LightId = lightId;
        public string LightId { get; }
    }

    /// <summary>单盏灯亮灭变化；RemainingSeconds 小于等于 0 视为永久点亮。</summary>
    public readonly struct LightStateChangedEvent
    {
        public LightStateChangedEvent(string lightId, bool isOn, float remainingSeconds)
        {
            LightId = lightId;
            IsOn = isOn;
            RemainingSeconds = remainingSeconds;
        }

        public string LightId { get; }
        public bool IsOn { get; }
        public float RemainingSeconds { get; }
    }

    /// <summary>区域点亮状态变化（特殊灯具交互后发布）。</summary>
    public readonly struct RegionLightsStateChangedEvent
    {
        public RegionLightsStateChangedEvent(string regionId, bool isLit)
        {
            RegionId = regionId;
            IsLit = isLit;
        }

        public string RegionId { get; }
        public bool IsLit { get; }
    }

    /// <summary>手提灯燃料或光强阶段变化。</summary>
    public readonly struct LanternFuelChangedEvent
    {
        public LanternFuelChangedEvent(float remainingSeconds, float totalSeconds, LanternStage stage)
        {
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
            Stage = stage;
        }

        public float RemainingSeconds { get; }
        public float TotalSeconds { get; }
        public LanternStage Stage { get; }
    }

    public readonly struct LanternLitChangedEvent
    {
        public LanternLitChangedEvent(bool isLit) => IsLit = isLit;
        public bool IsLit { get; }
    }

    public readonly struct LanternToggleFailedEvent
    {
        public LanternToggleFailedEvent(string reason) => Reason = reason;
        public string Reason { get; }
    }

    public readonly struct LightInteractionFailedEvent
    {
        public LightInteractionFailedEvent(string lightId, string reason)
        {
            LightId = lightId;
            Reason = reason;
        }

        public string LightId { get; }
        public string Reason { get; }
    }

    /// <summary>黑暗中非场景切换类交互被拦截，供提示 UI 后续接入。</summary>
    public readonly struct InteractionBlockedInDarkEvent
    {
        public InteractionBlockedInDarkEvent(InteractionType interactionType) => InteractionType = interactionType;
        public InteractionType InteractionType { get; }
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

    public readonly struct RoomTravelConfirmationRequestedEvent
    {
        public RoomTravelConfirmationRequestedEvent(string message, string sceneId, string spawnPointId)
        {
            Message = message;
            SceneId = sceneId;
            SpawnPointId = spawnPointId;
        }

        public string Message { get; }
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

    /// <summary>Requests consumption of one concrete inventory item instance.</summary>
    public readonly struct InventoryItemConsumeRequestedEvent
    {
        public InventoryItemConsumeRequestedEvent(string instanceId, int itemId = 0, string effectId = null)
        {
            InstanceId = instanceId;
            ItemId = itemId;
            EffectId = effectId;
        }

        public string InstanceId { get; }
        public int ItemId { get; }
        public string EffectId { get; }
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

    /// <summary>Typed character effect created by ItemEffectSystem from an item configuration.</summary>
    public readonly struct CharacterItemEffectRequestedEvent
    {
        public CharacterItemEffectRequestedEvent(int itemId, ItemEffectData effect)
        {
            ItemId = itemId;
            Effect = effect;
        }

        public int ItemId { get; }
        public ItemEffectData Effect { get; }
    }

    /// <summary>Notifies presentation after a consumable instance was actually removed from inventory.</summary>
    public readonly struct ConsumableUsedEvent
    {
        public ConsumableUsedEvent(int itemId, string effectId)
        {
            ItemId = itemId;
            EffectId = effectId;
        }

        public int ItemId { get; }
        public string EffectId { get; }
    }

    /// <summary>Character-owned temporary modifiers consumed by CombatSystem.</summary>
    public readonly struct CharacterCombatModifiersChangedEvent
    {
        public CharacterCombatModifiersChangedEvent(float meleeDamageMultiplier)
        {
            MeleeDamageMultiplier = Mathf.Max(0f, meleeDamageMultiplier);
        }

        public float MeleeDamageMultiplier { get; }
    }

    public readonly struct AmmoReloadRequestedEvent
    {
        public AmmoReloadRequestedEvent(int ammoItemId, string ammoInstanceId = null)
        {
            AmmoItemId = ammoItemId;
            AmmoInstanceId = ammoInstanceId;
        }

        public int AmmoItemId { get; }
        public string AmmoInstanceId { get; }
    }

    /// <summary>Published after a firearm instance's loaded rounds change.</summary>
    public readonly struct FirearmAmmoChangedEvent
    {
        public FirearmAmmoChangedEvent(string weaponInstanceId, int weaponItemId, int loadedAmmo, int capacity)
        {
            WeaponInstanceId = weaponInstanceId;
            WeaponItemId = weaponItemId;
            LoadedAmmo = Mathf.Max(0, loadedAmmo);
            Capacity = Mathf.Max(1, capacity);
        }

        public string WeaponInstanceId { get; }
        public int WeaponItemId { get; }
        public int LoadedAmmo { get; }
        public int Capacity { get; }
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

    /// <summary>Emitted at authored Spine foot-contact frames; independent of movement speed.</summary>
    public readonly struct PlayerFootstepEvent
    {
        public PlayerFootstepEvent(bool running) => Running = running;
        public bool Running { get; }
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

    /// <summary>角色动画 View 回报装备武器动画的实际播放生命周期。</summary>
    public readonly struct CharacterEquipAnimationStateChangedEvent
    {
        public CharacterEquipAnimationStateChangedEvent(bool isPlaying) => IsPlaying = isPlaying;
        public bool IsPlaying { get; }
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
    /// Synchronous approval shared by CharacterSystem and CombatSystem.  The
    /// character publishes an attack request before entering its attack state;
    /// CombatSystem can reject it (for example, when a firearm has no ammo)
    /// without briefly playing an attack animation.
    /// </summary>
    public sealed class CharacterAttackRequestResult
    {
        public bool Approved { get; private set; } = true;
        public string Reason { get; private set; }

        public void Reject(string reason)
        {
            Approved = false;
            Reason = string.IsNullOrEmpty(reason) ? "Attack request was rejected." : reason;
        }
    }

    /// <summary>
    /// 角色状态机已通过本次攻击的动作与体力校验。CombatSystem 接收此事件
    /// 创建唯一攻击实例；近战/投掷 View 由 AttackStartedEvent 驱动，枪械则
    /// 等待 CharacterAnimationView 发布 FirearmShotFrameEvent 后执行命中检测。
    /// CombatSystem may synchronously reject the request before the character
    /// commits its visual/action state.
    /// </summary>
    public readonly struct CharacterAttackRequestedEvent
    {
        public CharacterAttackRequestedEvent(int itemId, int comboStage, Vector2 direction)
            : this(itemId, comboStage, direction, false, Vector2.zero, null) { }

        public CharacterAttackRequestedEvent(int itemId, int comboStage, Vector2 direction, Vector2 targetWorldPosition)
            : this(itemId, comboStage, direction, true, targetWorldPosition, null) { }

        private CharacterAttackRequestedEvent(
            int itemId,
            int comboStage,
            Vector2 direction,
            bool hasTargetWorldPosition,
            Vector2 targetWorldPosition,
            CharacterAttackRequestResult result)
        {
            ItemId = itemId;
            ComboStage = comboStage;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            HasTargetWorldPosition = hasTargetWorldPosition;
            TargetWorldPosition = targetWorldPosition;
            Result = result ?? new CharacterAttackRequestResult();
        }
        public int ItemId { get; }
        public int ComboStage { get; }
        public Vector2 Direction { get; }
        public bool HasTargetWorldPosition { get; }
        public Vector2 TargetWorldPosition { get; }
        public CharacterAttackRequestResult Result { get; }
    }

    public readonly struct CharacterDamageReceivedEvent
    {
        public CharacterDamageReceivedEvent(float finalDamage, bool wasBlocked = false)
        {
            FinalDamage = Mathf.Max(0f, finalDamage);
            WasBlocked = wasBlocked;
        }
        public float FinalDamage { get; }
        public bool WasBlocked { get; }
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

    /// <summary>
    /// Published by CharacterAnimationView at the authored firearm shot frame.
    /// The payload keeps the approved attack identity and release target while
    /// the firearm view resolves the current muzzle position for the hit query.
    /// </summary>
    public readonly struct FirearmShotFrameEvent
    {
        public FirearmShotFrameEvent(AttackContext attack) => Attack = attack;
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

    public readonly struct GrenadeExplodedEvent
    {
        public GrenadeExplodedEvent(Vector2 position) => Position = position;
        public Vector2 Position { get; }
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

    public readonly struct GuideStepStartedEvent
    {
        public GuideStepStartedEvent(string stepId) => StepId = stepId;
        public string StepId { get; }
    }

    public readonly struct GuideStepCompletedEvent
    {
        public GuideStepCompletedEvent(string stepId) => StepId = stepId;
        public string StepId { get; }
    }

    public readonly struct GuideStepHiddenEvent
    {
        public GuideStepHiddenEvent(string stepId) => StepId = stepId;
        public string StepId { get; }
    }

    public readonly struct GuideSequenceActiveChangedEvent
    {
        public GuideSequenceActiveChangedEvent(bool isActive) => IsActive = isActive;
        public bool IsActive { get; }
    }

    public readonly struct DialoguePlayRequestedEvent
    {
        public DialoguePlayRequestedEvent(string dialogueId) => DialogueId = dialogueId;
        public string DialogueId { get; }
    }

    public readonly struct DialogueAdvancePressedEvent { }

    public readonly struct DialogueTypewriterCompletionRequestedEvent
    {
        public DialogueTypewriterCompletionRequestedEvent(string dialogueId, int nodeIndex)
        {
            DialogueId = dialogueId;
            NodeIndex = nodeIndex;
        }

        public string DialogueId { get; }
        public int NodeIndex { get; }
    }

    public readonly struct DialogueTypewriterStateChangedEvent
    {
        public DialogueTypewriterStateChangedEvent(string dialogueId, int nodeIndex, bool isTyping)
        {
            DialogueId = dialogueId;
            NodeIndex = nodeIndex;
            IsTyping = isTyping;
        }

        public string DialogueId { get; }
        public int NodeIndex { get; }
        public bool IsTyping { get; }
    }

    public readonly struct DialogueNodePresentedEvent
    {
        public DialogueNodePresentedEvent(MemorialArchive.Gameplay.Dialogue.Data.DialogueNodeData node) => Node = node;
        public MemorialArchive.Gameplay.Dialogue.Data.DialogueNodeData Node { get; }
    }

    public readonly struct DialogueBlockingEffectFinishedEvent
    {
        public DialogueBlockingEffectFinishedEvent(string dialogueId, int nodeIndex)
        {
            DialogueId = dialogueId;
            NodeIndex = nodeIndex;
        }

        public string DialogueId { get; }
        public int NodeIndex { get; }
    }

    public readonly struct DialogueFinishedEvent
    {
        public DialogueFinishedEvent(string dialogueId) => DialogueId = dialogueId;
        public string DialogueId { get; }
    }

    public readonly struct DialoguePlaybackStateChangedEvent
    {
        public DialoguePlaybackStateChangedEvent(
            string dialogueId,
            MemorialArchive.Gameplay.Dialogue.Data.DialoguePlaybackState state,
            bool canAdvance)
        {
            DialogueId = dialogueId;
            State = state;
            CanAdvance = canAdvance;
        }

        public string DialogueId { get; }
        public MemorialArchive.Gameplay.Dialogue.Data.DialoguePlaybackState State { get; }
        public bool CanAdvance { get; }
    }
}
