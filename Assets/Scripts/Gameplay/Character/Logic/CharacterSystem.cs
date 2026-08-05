using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Character.Config;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Stage1;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Logic
{
    /// <summary>纯 C# 角色状态规则；不引用 CombatSystem，也不做命中或伤害结算。</summary>
    public sealed class CharacterSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        private CharacterData data = new CharacterData(); private GameContext context; private CharacterAttributeConfig attributes;
        private Vector2 moveDirection, facingDirection = Vector2.right, aimDirection = Vector2.right;
        private float exactStamina, dodgeCooldown, dodgeInvincible, staggerRemaining, staggerCooldown, weakRemaining;
        private int primaryItemId, comboStage; private OffhandType offhandType;
        private CharacterActionState state = CharacterActionState.Normal;
        public string ModuleKey => "character"; public CharacterData Data => data; public Vector2 MoveDirection => moveDirection;
        // 当前版本的临时调试状态；不写入存档，正式版发布前移除。
        private bool debugModeEnabled;
        public bool IsRunning { get; private set; } public CharacterActionState ActionState => state;
        public bool IsBlocking => state == CharacterActionState.Blocking; public bool IsAiming => state == CharacterActionState.Aiming;
        public Vector2 AimDirection => aimDirection;
        public float StaminaCostMultiplier => GetStaminaCostMultiplier();
        public float MeleeDamageMultiplier => GetMeleeDamageMultiplier();
        public float CurrentMoveSpeed { get { if (attributes == null) return 0; var speed = IsRunning ? attributes.RunSpeed : attributes.WalkSpeed; if (data.health <= 1) speed *= .8f; if (offhandType == OffhandType.Splint) speed *= .8f; return Mathf.Clamp(speed, 60, 300); } }

        public void Initialize(GameContext value)
        {
            context = value; attributes = context.Configs.GetCharacterAttribute(Stage1Ids.PlayerAttributeId);
            context.Events.Subscribe<MoveInputEvent>(OnMove); context.Events.Subscribe<RunInputEvent>(OnRun); context.Events.Subscribe<SecondaryActionInputEvent>(OnSecondary);
            context.Events.Subscribe<DodgePressedEvent>(OnDodge); context.Events.Subscribe<PrimaryActionPressedEvent>(OnPrimary);
            context.Events.Subscribe<DodgeAnimationStateChangedEvent>(OnDodgeAnimationState);
            context.Events.Subscribe<CharacterActionAnimationCompletedEvent>(OnAnimationCompleted); context.Events.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            context.Events.Subscribe<CharacterEquipmentChangedEvent>(OnEquipment); context.Events.Subscribe<CharacterItemEffectRequestedEvent>(OnItemEffectRequested); context.Events.Subscribe<DebugModeToggledEvent>(OnDebugModeToggled); ResetForNewGame();
        }

        public void ResetForNewGame()
        {
            data = new CharacterData
            {
                attributeId = Stage1Ids.PlayerAttributeId,
                stateId = Stage1Ids.PlayerNormalStateId,
                health = attributes?.MaxHealth ?? 3,
                stamina = attributes?.MaxStamina ?? 30,
                isDead = false,
                position = Vector2.zero
            };
            moveDirection = Vector2.zero;
            facingDirection = Vector2.right;
            aimDirection = Vector2.right;
            exactStamina = data.stamina;
            dodgeCooldown = 0f;
            dodgeInvincible = 0f;
            staggerRemaining = 0f;
            staggerCooldown = 0f;
            weakRemaining = 0f;
            primaryItemId = 0;
            debugModeEnabled = false;
            comboStage = 0;
            offhandType = OffhandType.None;
            IsRunning = false;
            state = CharacterActionState.Normal;
            PublishState();
            context?.Events.Publish(new CharacterStatsChangedEvent());
            PublishCombatModifiers();
        }
        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<MoveInputEvent>(OnMove); context.Events.Unsubscribe<RunInputEvent>(OnRun); context.Events.Unsubscribe<SecondaryActionInputEvent>(OnSecondary);
                context.Events.Unsubscribe<DodgePressedEvent>(OnDodge); context.Events.Unsubscribe<PrimaryActionPressedEvent>(OnPrimary); context.Events.Unsubscribe<DodgeAnimationStateChangedEvent>(OnDodgeAnimationState);
                context.Events.Unsubscribe<CharacterActionAnimationCompletedEvent>(OnAnimationCompleted); context.Events.Unsubscribe<DamageAppliedEvent>(OnDamageApplied); context.Events.Unsubscribe<CharacterEquipmentChangedEvent>(OnEquipment); context.Events.Unsubscribe<CharacterItemEffectRequestedEvent>(OnItemEffectRequested); context.Events.Unsubscribe<DebugModeToggledEvent>(OnDebugModeToggled);
            }
            context = null;
        }
        public void Tick(float dt)
        {
            var forceStaminaToZero = TickTimedEffects(dt);
            if (data.isDead) return; dodgeCooldown = Mathf.Max(0, dodgeCooldown - dt); dodgeInvincible = Mathf.Max(0, dodgeInvincible - dt); staggerCooldown = Mathf.Max(0, staggerCooldown - dt);
            if (state == CharacterActionState.Staggered && (staggerRemaining -= dt) <= 0) SetState(weakRemaining > 0 ? CharacterActionState.Weak : CharacterActionState.Normal);
            if (weakRemaining > 0 && (weakRemaining -= dt) <= 0 && state == CharacterActionState.Weak) SetState(CharacterActionState.Normal);
            if (debugModeEnabled)
            {
                data.health = attributes?.MaxHealth ?? 3;
                exactStamina = attributes?.MaxStamina ?? 30;
            }
            var drains = IsRunning && moveDirection.sqrMagnitude > 0 || IsBlocking;
            if (forceStaminaToZero)
            {
                exactStamina = 0f;
            }
            else
            {
                var staminaRate = drains
                    ? -(attributes?.RunStaminaCostPerSecond ?? 2f) * StaminaCostMultiplier
                    : attributes?.StaminaRecoveryPerSecond ?? 1f;
                exactStamina = Mathf.Clamp(exactStamina + (debugModeEnabled ? 0f : staminaRate) * dt, 0, attributes?.MaxStamina ?? 30);
            }
            if (exactStamina <= 0 && weakRemaining <= 0) { weakRemaining = 5; IsRunning = false; SetState(CharacterActionState.Weak); }
            var before = data.stamina; data.stamina = Mathf.CeilToInt(exactStamina); if (before != data.stamina) context.Events.Publish(new CharacterStatsChangedEvent());
        }
        private void OnMove(MoveInputEvent e) { if (BlocksMovement()) { moveDirection = Vector2.zero; return; } moveDirection = new Vector2(Mathf.Clamp(e.Direction.x, -1, 1), 0); if (moveDirection.sqrMagnitude > 0) facingDirection = moveDirection.normalized; }
        private void OnRun(RunInputEvent e) { IsRunning = e.IsRunning && state == CharacterActionState.Normal && exactStamina > 0; }
        private void OnEquipment(CharacterEquipmentChangedEvent e) { primaryItemId = e.PrimaryItemId; offhandType = e.OffhandType; comboStage = 0; }
        private void OnSecondary(SecondaryActionInputEvent e)
        {
            if (BlocksActions()) { PublishSecondary(false, false, e.PointerWorldPosition); return; }
            var config = context.Configs.GetItem(primaryItemId); var aim = e.IsHeld && config != null && (config.CombatAttackKind == CombatAttackKind.Firearm || config.CombatAttackKind == CombatAttackKind.Throwable);
            var block = e.IsHeld && !aim && ((config != null && config.CombatAttackKind == CombatAttackKind.Melee) || offhandType == OffhandType.Shield);
            SetState(aim ? CharacterActionState.Aiming : block ? CharacterActionState.Blocking : CharacterActionState.Normal); PublishSecondary(block, aim, e.PointerWorldPosition);
            if (aim) { var d = e.PointerWorldPosition - data.position; if (d.sqrMagnitude > 0) aimDirection = d.normalized; }
        }
        private void OnPrimary(PrimaryActionPressedEvent e)
        {
            if (BlocksActions() || IsBlocking || primaryItemId <= 0) return; var config = context.Configs.GetItem(primaryItemId); if (config == null || config.Category != ItemCategory.Weapon) return;
            var staminaCost = config.StaminaCost * StaminaCostMultiplier;
            if (exactStamina < staminaCost) return;
            if (!debugModeEnabled) exactStamina -= staminaCost;
            var direction = state == CharacterActionState.Aiming ? aimDirection : facingDirection;
            if (config.CombatAttackKind == CombatAttackKind.Melee) { if (state == CharacterActionState.Attack3) return; comboStage = comboStage % 3 + 1; SetState(comboStage == 1 ? CharacterActionState.Attack1 : comboStage == 2 ? CharacterActionState.Attack2 : CharacterActionState.Attack3); context.Events.Publish(new CharacterAttackRequestedEvent(primaryItemId, comboStage, direction)); }
            else { SetState(CharacterActionState.Attack1); context.Events.Publish(new CharacterAttackRequestedEvent(primaryItemId, 0, direction)); }
        }
        private void OnDodge(DodgePressedEvent e) { var staminaCost = (attributes?.DodgeStaminaCost ?? 6f) * StaminaCostMultiplier; if (attributes == null || BlocksActions() || dodgeCooldown > 0 || exactStamina < staminaCost) return; if (!debugModeEnabled) exactStamina -= staminaCost; IsRunning = false; PublishSecondary(false, false, data.position); dodgeCooldown = attributes.DodgeCooldownSeconds; dodgeInvincible = attributes.DodgeDurationSeconds; SetState(CharacterActionState.Dodging); context.Events.Publish(new DodgeRequestedEvent(moveDirection.sqrMagnitude > 0 ? moveDirection.normalized : -facingDirection, attributes.DodgeDistance, attributes.DodgeDurationSeconds)); }
        private void OnDodgeAnimationState(DodgeAnimationStateChangedEvent e) { if (!e.IsPlaying && state == CharacterActionState.Dodging) SetState(weakRemaining > 0 ? CharacterActionState.Weak : CharacterActionState.Normal); }
        private void OnAnimationCompleted(CharacterActionAnimationCompletedEvent e) { if (e.State == state) { if (state == CharacterActionState.Attack3) comboStage = 0; SetState(weakRemaining > 0 ? CharacterActionState.Weak : CharacterActionState.Normal); } }
        private void OnDamageApplied(DamageAppliedEvent e) { if (e.TargetId == CombatTargetIds.Player) ApplyFinalDamage(e.Amount); }

        private void OnItemEffectRequested(CharacterItemEffectRequestedEvent evt)
        {
            var effect = evt.Effect;
            if (effect == null || data.isDead)
            {
                return;
            }

            var healthBeforeUse = data.health;
            var statsChanged = false;
            if (effect.RestoreFullHealth)
            {
                var fullHealth = attributes?.MaxHealth ?? 3;
                statsChanged = !Mathf.Approximately(data.health, fullHealth);
                data.health = fullHealth;
            }
            else if (effect.HealthRestore > 0f)
            {
                var restoredHealth = Mathf.Clamp(data.health + effect.HealthRestore, 0f, attributes?.MaxHealth ?? 3);
                statsChanged = !Mathf.Approximately(data.health, restoredHealth);
                data.health = restoredHealth;
            }

            if (effect.RestoreFullStamina)
            {
                var fullStamina = attributes?.MaxStamina ?? 30;
                statsChanged |= !Mathf.Approximately(exactStamina, fullStamina);
                exactStamina = fullStamina;
                data.stamina = Mathf.CeilToInt(exactStamina);
            }

            if (effect.CuresBleeding && data.isBleeding)
            {
                data.isBleeding = false;
                statsChanged = true;
            }

            if (effect.CuresPoison && data.isPoisoned)
            {
                data.isPoisoned = false;
                statsChanged = true;
            }

            var previousMeleeMultiplier = MeleeDamageMultiplier;
            if (effect.HasTimedModifier)
            {
                EnsureActiveEffects();
                data.activeEffects.RemoveAll(active => active != null && active.effectId == effect.EffectId);
                data.activeEffects.Add(new CharacterTimedEffectData
                {
                    effectId = effect.EffectId,
                    remainingSeconds = effect.DurationSeconds,
                    staminaCostMultiplier = effect.StaminaCostMultiplier,
                    meleeDamageMultiplier = effect.MeleeDamageMultiplier,
                    restoreHealthAtExpiry = effect.RestoreHealthAtExpiry,
                    healthBeforeUse = healthBeforeUse,
                    exhaustAtExpiry = effect.ExhaustAtExpiry
                });
                statsChanged = true;
            }

            if (statsChanged)
            {
                context.Events.Publish(new CharacterStatsChangedEvent());
            }

            if (!Mathf.Approximately(previousMeleeMultiplier, MeleeDamageMultiplier))
            {
                PublishCombatModifiers();
            }
        }

        private bool TickTimedEffects(float deltaTime)
        {
            EnsureActiveEffects();
            var previousMeleeMultiplier = MeleeDamageMultiplier;
            var statsChanged = false;
            var forceStaminaToZero = false;
            for (var index = data.activeEffects.Count - 1; index >= 0; index--)
            {
                var active = data.activeEffects[index];
                if (active == null || (active.remainingSeconds -= deltaTime) > 0f)
                {
                    continue;
                }

                if (active.restoreHealthAtExpiry)
                {
                    var restoredHealth = Mathf.Clamp(active.healthBeforeUse, 0f, attributes?.MaxHealth ?? 3);
                    statsChanged |= !Mathf.Approximately(data.health, restoredHealth);
                    data.health = restoredHealth;
                }

                if (active.exhaustAtExpiry)
                {
                    forceStaminaToZero = true;
                    statsChanged |= !Mathf.Approximately(exactStamina, 0f);
                }

                data.activeEffects.RemoveAt(index);
            }

            if (statsChanged)
            {
                context.Events.Publish(new CharacterStatsChangedEvent());
            }

            if (!Mathf.Approximately(previousMeleeMultiplier, MeleeDamageMultiplier))
            {
                PublishCombatModifiers();
            }

            return forceStaminaToZero;
        }

        private float GetStaminaCostMultiplier()
        {
            EnsureActiveEffects();
            var multiplier = 1f;
            foreach (var active in data.activeEffects)
            {
                if (active != null && active.remainingSeconds > 0f)
                {
                    multiplier = Mathf.Min(multiplier, Mathf.Clamp01(active.staminaCostMultiplier));
                }
            }

            return multiplier;
        }

        private float GetMeleeDamageMultiplier()
        {
            EnsureActiveEffects();
            var multiplier = 1f;
            foreach (var active in data.activeEffects)
            {
                if (active != null && active.remainingSeconds > 0f)
                {
                    multiplier = Mathf.Max(multiplier, Mathf.Max(0f, active.meleeDamageMultiplier));
                }
            }

            return multiplier;
        }

        private void EnsureActiveEffects()
        {
            if (data.activeEffects == null)
            {
                data.activeEffects = new List<CharacterTimedEffectData>();
            }
        }

        private void PublishCombatModifiers()
        {
            context?.Events.Publish(new CharacterCombatModifiersChangedEvent(MeleeDamageMultiplier));
        }

        private void ApplyFinalDamage(float damage) { if (debugModeEnabled || data.isDead || dodgeInvincible > 0) return; data.health = Mathf.Max(0, data.health - damage); context.Events.Publish(new CharacterStatsChangedEvent()); if (data.health <= 0) { data.isDead = true; SetState(CharacterActionState.Dead); context.Events.Publish(new CharacterDiedEvent(1.2f)); return; } if (staggerCooldown <= 0) { staggerRemaining = .3f; staggerCooldown = 1; IsRunning = false; PublishSecondary(false, false, data.position); SetState(CharacterActionState.Staggered); } }
        private void OnDebugModeToggled(DebugModeToggledEvent e)
        {
            debugModeEnabled = !debugModeEnabled;
            if (debugModeEnabled)
            {
                data.health = attributes?.MaxHealth ?? 3;
                exactStamina = attributes?.MaxStamina ?? 30;
                data.stamina = Mathf.CeilToInt(exactStamina);
                weakRemaining = 0f;
                if (state == CharacterActionState.Weak) SetState(CharacterActionState.Normal);
            }
            context.Events.Publish(new CharacterStatsChangedEvent());
        }
        private bool BlocksActions() => data.isDead || state == CharacterActionState.Staggered || state == CharacterActionState.Dodging || state == CharacterActionState.Weak || state == CharacterActionState.Attack3;
        private bool BlocksMovement() => data.isDead || state == CharacterActionState.Staggered || state == CharacterActionState.Dodging;
        private void SetState(CharacterActionState value) { if (state == value) return; state = value; PublishState(); }
        private void PublishState() => context?.Events.Publish(new CharacterActionStateChangedEvent(state));
        private void PublishSecondary(bool block, bool aim, Vector2 point) { context.Events.Publish(new BlockInputEvent(block)); context.Events.Publish(new AimInputEvent(aim, point)); }
        public object CaptureSaveData() => data;

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            data = JsonUtility.FromJson<CharacterData>(json) ?? data;
            EnsureActiveEffects();
            exactStamina = data.stamina;
            PublishCombatModifiers();
        }
    }
}
