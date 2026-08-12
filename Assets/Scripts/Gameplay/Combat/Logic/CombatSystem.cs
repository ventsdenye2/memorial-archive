using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Item.Config;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.Logic
{
    public sealed class CombatSystem : IGameSystem, ITickableSystem, INewGameResettable
    {
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

        public CombatSystem(ICharacterCombatStateProvider characterCombatState = null)
        {
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
                PublishAttackFailed(evt.ItemId, "The requested weapon is no longer selected.");
                return;
            }

            var config = context.Configs.GetItem(selectedItem.itemId);
            if (config == null || config.Category != ItemCategory.Weapon)
            {
                PublishAttackFailed(selectedItem.itemId, "The selected item is not a weapon.");
                return;
            }

            var attackId = ++nextAttackInstanceId;
            var attack = new AttackContext(
                attackId,
                CombatTargetIds.Player,
                selectedItem.instanceId,
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
            RequestReload();
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

        private void RequestReload()
        {
            if (selectedItem == null)
            {
                PublishAttackFailed(0, "No firearm is selected for reloading.");
                return;
            }

            var config = context.Configs.GetItem(selectedItem.itemId);
            if (config == null || config.Category != ItemCategory.Weapon || config.CombatAttackKind != CombatAttackKind.Firearm)
            {
                PublishAttackFailed(selectedItem.itemId, "Reload requires a selected firearm.");
                return;
            }

            // Ammo counts and magazine state belong to the firearm developer.
            // This shared event is the only common reload trigger they need.
            context.Events.Publish(new ReloadRequestedEvent(selectedItem));
        }

        private void PublishAttackFailed(int weaponItemId, string reason)
        {
            context.Events.Publish(new AttackFailedEvent(weaponItemId, reason));
        }
    }
}
