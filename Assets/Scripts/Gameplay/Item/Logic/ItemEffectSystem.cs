using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Gameplay.Item.Logic
{
    public sealed class ItemEffectSystem : IGameSystem
    {
        private GameContext context;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<ItemEffectAppliedEvent>(HandleItemEffectApplied);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<ItemEffectAppliedEvent>(HandleItemEffectApplied);
            }

            context = null;
        }

        private void HandleItemEffectApplied(ItemEffectAppliedEvent evt)
        {
            Debug.Log($"Item effect applied. itemId={evt.ItemId}, effectId={evt.EffectId}");
        }
    }
}
