using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Inventory.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.Logic
{
    public sealed class CombatSystem : IGameSystem
    {
        private GameContext context;
        private InventoryItemInstance selectedItem;
        private bool isAiming;
        private Vector2 aimWorldPosition;

        public bool IsAiming => isAiming;
        public Vector2 AimWorldPosition => aimWorldPosition;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            context.Events.Subscribe<AimInputEvent>(HandleAimInput);
            context.Events.Subscribe<PrimaryActionPressedEvent>(HandlePrimaryActionPressed);
            context.Events.Subscribe<ReloadPressedEvent>(HandleReloadPressed);
            context.Events.Subscribe<AmmoReloadRequestedEvent>(HandleAmmoReloadRequested);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
                context.Events.Unsubscribe<AimInputEvent>(HandleAimInput);
                context.Events.Unsubscribe<PrimaryActionPressedEvent>(HandlePrimaryActionPressed);
                context.Events.Unsubscribe<ReloadPressedEvent>(HandleReloadPressed);
                context.Events.Unsubscribe<AmmoReloadRequestedEvent>(HandleAmmoReloadRequested);
            }

            context = null;
            selectedItem = null;
            isAiming = false;
            aimWorldPosition = Vector2.zero;
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

        private void HandlePrimaryActionPressed(PrimaryActionPressedEvent evt)
        {
            if (selectedItem == null)
            {
                return;
            }

            context.Events.Publish(new PlayerAttackRequestedEvent());
        }

        private void HandleReloadPressed(ReloadPressedEvent evt)
        {
            Debug.Log("Reload key pressed. Ammo item use still decides whether a compatible weapon can reload.");
        }

        private void HandleAmmoReloadRequested(AmmoReloadRequestedEvent evt)
        {
            Debug.Log($"Ammo reload requested. ammoItemId={evt.AmmoItemId}");
        }
    }
}
