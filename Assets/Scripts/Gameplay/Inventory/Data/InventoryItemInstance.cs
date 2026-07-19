using System;

namespace MemorialArchive.Gameplay.Inventory.Data
{
    [Serializable]
    public sealed class InventoryItemInstance
    {
        public string instanceId;
        public int itemId;
        public int quantity = 1;

        public InventoryItemInstance Clone()
        {
            return new InventoryItemInstance
            {
                instanceId = instanceId,
                itemId = itemId,
                quantity = quantity
            };
        }
    }
}
