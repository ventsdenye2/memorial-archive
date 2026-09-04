using System;

namespace MemorialArchive.Gameplay.Inventory.Data
{
    [Serializable]
    public sealed class InventoryItemInstance
    {
        public string instanceId;
        public int itemId;
        public int quantity = 1;
        // Runtime ammunition stored on this weapon instance.  Keeping it on
        // the same serializable object preserves a partially loaded magazine
        // when the weapon is unequipped, moved, or saved and restored.
        public int loadedAmmo;

        public InventoryItemInstance Clone()
        {
            return new InventoryItemInstance
            {
                instanceId = instanceId,
                itemId = itemId,
                quantity = quantity,
                loadedAmmo = loadedAmmo
            };
        }
    }
}
