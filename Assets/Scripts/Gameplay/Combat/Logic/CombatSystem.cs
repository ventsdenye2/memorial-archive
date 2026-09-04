using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Item.Config;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.Logic
{
    public sealed class CombatSystem : IGameSystem, ITickableSystem, INewGameResettable
    {
        public const int FirearmMagazineCapacity = 6;

        private sealed class PlayerBlockDamageModifier : IDamageModifier
        {
            private const float SuccessfulBlockStaminaCost = 5f;
            private readonly ICharacterCombatStateProvider character;

            public PlayerBlockDamageModifier(ICharacterCombatStateProvider character) => this.character = character;
            public bool AppliesTo(string targetId) => targetId == CombatTargetIds.Player;

            public DamageResult Modify(DamageRequest request, DamageResult currentResult)
            {
                if (character == null || !character.IsBlocking || currentResult.FinalDamage <= 0f)
                {
                    return currentResult;
                }

                var finalDamage = character.HasShieldEquipped ? 0f : currentResult.FinalDamage * 0.5f;
                character.ConsumeSuccessfulBlockStamina(SuccessfulBlockStaminaCost);
                return new DamageResult(finalDamage, true, currentResult.KilledTarget);
            }
        }

        private sealed class PendingAttack
        {
            public AttackContext Context;
            public float RemainingSeconds;
            public readonly HashSet<string> HitTargetKeys = new HashSet<string>();
        }

        private readonly Dictionary<int, PendingAttack> pendingAttacks = new Dictionary<int, PendingAttack>();
        private readonly List<IDamageModifier> damageModifiers = new List<IDamageModifier>();
        private GameContext context;
        private InventoryItemInstance selectedItem;
        private bool isAiming;
        private Vector2 aimWorldPosition;
        private Vector2 playerPosition;
        private int nextAttackInstanceId;
        private float meleeDamageMultiplier = 1f;
        private readonly IDamageModifier playerBlockDamageModifier;
        private readonly InventorySystem inventory;

        public CombatSystem(
            ICharacterCombatStateProvider characterCombatState = null,
            InventorySystem inventorySystem = null)
        {
            inventory = inventorySystem;
            if (characterCombatState != null)
            {
                playerBlockDamageModifier = new PlayerBlockDamageModifier(characterCombatState);
            }
        }

        public bool IsAiming => isAiming;
        public Vector2 AimWorldPosition => aimWorldPosition;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            context.Events.Subscribe<AimInputEvent>(HandleAimInput);
            context.Events.Subscribe<CharacterAttackRequestedEvent>(HandleCharacterAttackRequested);
            context.Events.Subscribe<ReloadPressedEvent>(HandleReloadPressed);
            context.Events.Subscribe<AmmoReloadRequestedEvent>(HandleAmmoReloadRequested);
            context.Events.Subscribe<CombatHitReportedEvent>(HandleCombatHitReported);
            context.Events.Subscribe<CombatAttackLifetimeRequestedEvent>(HandleCombatAttackLifetimeRequested);
            context.Events.Subscribe<DamageRequestedEvent>(HandleDamageRequested);
            context.Events.Subscribe<PlayerPositionChangedEvent>(HandlePlayerPositionChanged);
            context.Events.Subscribe<CharacterCombatModifiersChangedEvent>(HandleCharacterCombatModifiersChanged);
            EnsureBuiltInDamageModifiers();
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
                context.Events.Unsubscribe<AimInputEvent>(HandleAimInput);
                context.Events.Unsubscribe<CharacterAttackRequestedEvent>(HandleCharacterAttackRequested);
                context.Events.Unsubscribe<ReloadPressedEvent>(HandleReloadPressed);
                context.Events.Unsubscribe<AmmoReloadRequestedEvent>(HandleAmmoReloadRequested);
                context.Events.Unsubscribe<CombatHitReportedEvent>(HandleCombatHitReported);
                context.Events.Unsubscribe<CombatAttackLifetimeRequestedEvent>(HandleCombatAttackLifetimeRequested);
                context.Events.Unsubscribe<DamageRequestedEvent>(HandleDamageRequested);
                context.Events.Unsubscribe<PlayerPositionChangedEvent>(HandlePlayerPositionChanged);
                context.Events.Unsubscribe<CharacterCombatModifiersChangedEvent>(HandleCharacterCombatModifiersChanged);
            }

            context = null;
            selectedItem = null;
            isAiming = false;
            aimWorldPosition = Vector2.zero;
            playerPosition = Vector2.zero;
            meleeDamageMultiplier = 1f;
            pendingAttacks.Clear();
            damageModifiers.Clear();
        }

        public void ResetForNewGame()
        {
            selectedItem = null;
            isAiming = false;
            aimWorldPosition = Vector2.zero;
            playerPosition = Vector2.zero;
            meleeDamageMultiplier = 1f;
            nextAttackInstanceId = 0;
            pendingAttacks.Clear();
            damageModifiers.Clear();
            EnsureBuiltInDamageModifiers();
        }

        public void RegisterDamageModifier(IDamageModifier modifier)
        {
            if (modifier != null && !damageModifiers.Contains(modifier))
            {
                damageModifiers.Add(modifier);
            }
        }

        public void UnregisterDamageModifier(IDamageModifier modifier)
        {
            damageModifiers.Remove(modifier);
        }

        private void EnsureBuiltInDamageModifiers()
        {
            if (playerBlockDamageModifier != null && !damageModifiers.Contains(playerBlockDamageModifier))
            {
                damageModifiers.Add(playerBlockDamageModifier);
            }
        }

        public void Tick(float deltaTime)
        {
            if (pendingAttacks.Count == 0)
            {
                return;
            }

            var expiredIds = new List<int>();
            foreach (var pair in pendingAttacks)
            {
                pair.Value.RemainingSeconds -= deltaTime;
                if (pair.Value.RemainingSeconds <= 0f)
                {
                    expiredIds.Add(pair.Key);
                }
            }

            foreach (var id in expiredIds)
            {
                pendingAttacks.Remove(id);
            }
        }

        private void HandleSelectedItemChanged(SelectedItemChangedEvent evt)
        {
            selectedItem = evt.Item;
            NormalizeSelectedFirearmMagazine();
            PublishSelectedFirearmAmmo();
        }

        private void HandleAimInput(AimInputEvent evt)
        {
            isAiming = evt.IsAiming;
            aimWorldPosition = evt.PointerWorldPosition;
        }

        private void HandlePlayerPositionChanged(PlayerPositionChangedEvent evt)
        {
            playerPosition = evt.Position;
        }

        private void HandleCharacterCombatModifiersChanged(CharacterCombatModifiersChangedEvent evt)
        {
            meleeDamageMultiplier = evt.MeleeDamageMultiplier;
        }

        private void HandleCharacterAttackRequested(CharacterAttackRequestedEvent evt)
        {
            if (selectedItem == null || selectedItem.itemId != evt.ItemId)
            {
                RejectAttack(evt, "The requested weapon is no longer selected.");
                return;
            }

            // Keep the selected weapon snapshot local.  Consuming a stack can
            // publish inventory/equipment events synchronously and may change
            // the CombatSystem's selected-item cache.
            var weapon = selectedItem;
            var config = context.Configs.GetItem(weapon.itemId);
            if (config == null || config.Category != ItemCategory.Weapon)
            {
                RejectAttack(evt, "The selected item is not a weapon.");
                return;
            }

            if (config.CombatAttackKind == CombatAttackKind.Firearm &&
                !TryConsumeMagazineRound(weapon, config))
            {
                RejectAttack(evt, "The firearm magazine is empty.");
                return;
            }

            var attackId = ++nextAttackInstanceId;
            var attack = new AttackContext(
                attackId,
                CombatTargetIds.Player,
                weapon.instanceId,
                config.ItemId,
                config.CombatAttackKind,
                config.DamageType,
                config.Damage * (config.CombatAttackKind == CombatAttackKind.Melee ? meleeDamageMultiplier : 1f),
                config.AttackRange,
                playerPosition,
                evt.Direction,
                config.AttackActiveSeconds,
                evt.ComboStage,
                evt.HasTargetWorldPosition,
                evt.TargetWorldPosition);

            pendingAttacks.Add(attackId, new PendingAttack
            {
                Context = attack,
                // Gives View code a small grace interval after its animation
                // window closes, without allowing stale reports indefinitely.
                RemainingSeconds = attack.ActiveSeconds + 0.5f
            });

            context.Events.Publish(new PlayerAttackRequestedEvent(attack));
            context.Events.Publish(new AttackStartedEvent(attack));
        }

        private void HandleReloadPressed(ReloadPressedEvent evt)
        {
            RequestReload();
        }

        private void HandleAmmoReloadRequested(AmmoReloadRequestedEvent evt)
        {
            // ItemEffectSystem raises this only when the player explicitly uses
            // an ammo stack.  It is a one-round compatibility path; R below is
            // the only fill-to-capacity path, so the same input cannot consume
            // six rounds or consume the stack a second time.
            RequestReloadFromAmmoItem(evt.AmmoItemId, evt.AmmoInstanceId);
        }

        /// <summary>
        /// Shared damage entry point.  It is intentionally Unity-free so it
        /// can be called by monster logic or unit-tested without a scene.
        /// Player weapon Views should publish CombatHitReportedEvent instead.
        /// </summary>
        public DamageResult ResolveDamage(DamageRequest request)
        {
            if (string.IsNullOrEmpty(request.TargetId))
            {
                return new DamageResult(0, false, false);
            }

            // Defence, block, shield and armour modifiers are applied here,
            // never inside a hitbox/projectile.  A snapshot allows a modifier
            // to unregister itself safely while processing a fatal hit.
            var result = new DamageResult(request.RawDamage, false, false);
            var modifiers = damageModifiers.ToArray();
            foreach (var modifier in modifiers)
            {
                if (modifier != null && modifier.AppliesTo(request.TargetId))
                {
                    result = modifier.Modify(request, result);
                }
            }

            context.Events.Publish(new DamageAppliedEvent(request, result));
            return result;
        }

        private void HandleCombatHitReported(CombatHitReportedEvent evt)
        {
            var report = evt.Report;
            var targetKey = $"{report.TargetId}:{report.HitSequence}";
            if (!pendingAttacks.TryGetValue(report.AttackInstanceId, out var pending) ||
                string.IsNullOrEmpty(report.TargetId) ||
                !pending.HitTargetKeys.Add(targetKey))
            {
                return;
            }

            var attack = pending.Context;
            ResolveDamage(new DamageRequest(
                attack.AttackInstanceId,
                attack.AttackerId,
                report.TargetId,
                attack.WeaponItemId,
                attack.DamageType,
                attack.BaseDamage,
                report.HitPoint));
        }

        private void HandleCombatAttackLifetimeRequested(CombatAttackLifetimeRequestedEvent evt)
        {
            if (pendingAttacks.TryGetValue(evt.AttackInstanceId, out var pending))
            {
                pending.RemainingSeconds = Mathf.Max(pending.RemainingSeconds, evt.MinimumRemainingSeconds);
            }
        }

        private void HandleDamageRequested(DamageRequestedEvent evt)
        {
            ResolveDamage(evt.Request);
        }

        private bool TryConsumeMagazineRound(InventoryItemInstance weapon, ItemConfig config)
        {
            if (weapon == null || config == null || config.Category != ItemCategory.Weapon ||
                config.CombatAttackKind != CombatAttackKind.Firearm)
            {
                return false;
            }

            var loaded = Mathf.Clamp(weapon.loadedAmmo, 0, FirearmMagazineCapacity);
            if (loaded <= 0)
            {
                // Old saves and hand-authored instances may contain a negative
                // value; normalize it without ever allowing a shot to proceed.
                weapon.loadedAmmo = 0;
                return false;
            }

            weapon.loadedAmmo = loaded - 1;
            PublishFirearmAmmoChanged(weapon);
            return true;
        }

        private void NormalizeSelectedFirearmMagazine()
        {
            if (selectedItem == null || context == null)
            {
                return;
            }

            var config = context.Configs.GetItem(selectedItem.itemId);
            if (config == null || config.Category != ItemCategory.Weapon ||
                config.CombatAttackKind != CombatAttackKind.Firearm)
            {
                return;
            }

            selectedItem.loadedAmmo = Mathf.Clamp(selectedItem.loadedAmmo, 0, FirearmMagazineCapacity);
        }

        private void PublishSelectedFirearmAmmo()
        {
            if (context == null)
            {
                return;
            }

            if (selectedItem == null)
            {
                context.Events.Publish(new FirearmAmmoChangedEvent(string.Empty, 0, 0, FirearmMagazineCapacity));
                return;
            }

            var config = context.Configs.GetItem(selectedItem.itemId);
            if (config == null || config.Category != ItemCategory.Weapon ||
                config.CombatAttackKind != CombatAttackKind.Firearm)
            {
                context.Events.Publish(new FirearmAmmoChangedEvent(string.Empty, 0, 0, FirearmMagazineCapacity));
                return;
            }

            PublishFirearmAmmoChanged(selectedItem);
        }

        private void PublishFirearmAmmoChanged(InventoryItemInstance weapon)
        {
            if (context == null || weapon == null)
            {
                return;
            }

            context.Events.Publish(new FirearmAmmoChangedEvent(
                weapon.instanceId,
                weapon.itemId,
                Mathf.Clamp(weapon.loadedAmmo, 0, FirearmMagazineCapacity),
                FirearmMagazineCapacity));
        }

        private void RequestReloadFromAmmoItem(int ammoItemId, string ammoInstanceId)
        {
            if (ammoItemId <= 0)
            {
                return;
            }

            // The old item-use flow selects the ammo stack, so find the first
            // equipped firearm for that explicit one-round action.  R itself
            // never falls back to another shortcut slot.
            var weapon = GetSelectedFirearm();
            if (weapon == null && inventory != null)
            {
                foreach (var placement in inventory.GetPlayerPlacements(InventoryContainerKind.ShortcutBar))
                {
                    var candidate = placement?.item;
                    if (IsFirearm(candidate))
                    {
                        weapon = candidate;
                        break;
                    }
                }
            }

            if (weapon == null)
            {
                context.Events.Publish(new ItemUseFailedEvent(ammoItemId, "No equipped firearm can use this ammunition."));
                return;
            }

            RequestReload(weapon, ammoItemId, ammoInstanceId, true);
        }

        private void RequestReload()
        {
            RequestReload(selectedItem, 0, null, false);
        }

        private void RequestReload(
            InventoryItemInstance weapon,
            int requestedAmmoItemId,
            string requestedAmmoInstanceId,
            bool consumeSingleRound)
        {
            if (weapon == null)
            {
                PublishAttackFailed(0, "No firearm is selected for reloading.");
                return;
            }

            var config = context.Configs.GetItem(weapon.itemId);
            if (config == null || config.Category != ItemCategory.Weapon || config.CombatAttackKind != CombatAttackKind.Firearm)
            {
                PublishAttackFailed(weapon.itemId, "Reload requires a selected firearm.");
                return;
            }

            var loadedBefore = Mathf.Clamp(weapon.loadedAmmo, 0, FirearmMagazineCapacity);
            weapon.loadedAmmo = loadedBefore;
            var missing = FirearmMagazineCapacity - loadedBefore;
            if (missing <= 0)
            {
                return;
            }

            if (inventory == null)
            {
                PublishAttackFailed(weapon.itemId, "No inventory is available for reloading.");
                return;
            }

            var unitsToLoad = consumeSingleRound ? 1 : missing;
            var consumed = requestedAmmoItemId > 0
                ? inventory.TryConsumeCompatibleAmmo(
                    config.ItemId,
                    unitsToLoad,
                    requestedAmmoInstanceId,
                    requestedAmmoItemId,
                    out _)
                : inventory.TryConsumeCompatibleAmmo(config.ItemId, unitsToLoad, out _);
            if (consumed <= 0)
            {
                if (requestedAmmoItemId > 0)
                {
                    context.Events.Publish(new ItemUseFailedEvent(
                        requestedAmmoItemId,
                        "The ammunition is not compatible with the equipped firearm."));
                }
                else
                {
                    PublishAttackFailed(weapon.itemId, "No compatible ammunition is available.");
                }

                return;
            }

            // InventorySystem has already removed exactly the units it
            // reported.  The weapon instance is the same object retained by
            // its placement, so this survives switching and save capture.
            weapon.loadedAmmo = Mathf.Clamp(loadedBefore + consumed, 0, FirearmMagazineCapacity);
            PublishFirearmAmmoChanged(weapon);
            context.Events.Publish(new ReloadRequestedEvent(weapon));
        }

        private InventoryItemInstance GetSelectedFirearm()
        {
            return IsFirearm(selectedItem) ? selectedItem : null;
        }

        private bool IsFirearm(InventoryItemInstance item)
        {
            if (item == null || context == null)
            {
                return false;
            }

            var config = context.Configs.GetItem(item.itemId);
            return config != null && config.Category == ItemCategory.Weapon &&
                config.CombatAttackKind == CombatAttackKind.Firearm;
        }

        private void PublishAttackFailed(int weaponItemId, string reason)
        {
            context.Events.Publish(new AttackFailedEvent(weaponItemId, reason));
        }

        private void RejectAttack(CharacterAttackRequestedEvent evt, string reason)
        {
            evt.Result?.Reject(reason);
            PublishAttackFailed(evt.ItemId, reason);
        }
    }
}
