using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Item.Data;

namespace MemorialArchive.Gameplay.Item.Logic
{
    public sealed class ItemEffectSystem : IGameSystem
    {
        private GameContext context;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<ItemUseRequestedEvent>(HandleItemUseRequested);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<ItemUseRequestedEvent>(HandleItemUseRequested);
            }

            context = null;
        }

        private void HandleItemUseRequested(ItemUseRequestedEvent evt)
        {
            if (evt.Item == null)
            {
                return;
            }

            var config = context.Configs.GetItem(evt.Item.itemId);
            if (config == null || !config.CanUse)
            {
                context.Events.Publish(new ItemUseFailedEvent(evt.Item.itemId, "This item cannot be used."));
                return;
            }

            if (config.CanPlaceAmmo)
            {
                context.Events.Publish(new AmmoReloadRequestedEvent(config.ItemId));
                return;
            }

            if (!ItemEffectData.TryCreate(config.EffectId, out var effect))
            {
                context.Events.Publish(new ItemUseFailedEvent(config.ItemId, "This item has no implemented runtime effect."));
                return;
            }

            context.Events.Publish(new ItemEffectAppliedEvent(config.ItemId, config.EffectId));
            context.Events.Publish(new CharacterItemEffectRequestedEvent(config.ItemId, effect));
            if (config.IsConsumable)
            {
                context.Events.Publish(new InventoryItemConsumeRequestedEvent(evt.Item.instanceId, config.ItemId, config.EffectId));
            }
        }
    }
}
