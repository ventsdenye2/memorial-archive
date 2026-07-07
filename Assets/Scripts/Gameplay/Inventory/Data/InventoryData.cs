using System;
using System.Collections.Generic;

namespace MemorialArchive.Gameplay.Inventory.Data
{
    public enum InventoryContainerKind
    {
        Backpack,
        ShortcutBar,
        Offhand,
        SceneContainer
    }

    [Serializable]
    public sealed class InventoryItemPlacement
    {
        public InventoryItemInstance item = new InventoryItemInstance();
        public InventoryContainerKind containerKind = InventoryContainerKind.Backpack;
        public string containerId;
        public int x;
        public int y;
        public int width = 1;
        public int height = 1;
        public int slotIndex = -1;
    }

    [Serializable]
    public sealed class InventoryData
    {
        public int selectedShortcutIndex = -1;
        public List<InventoryItemPlacement> playerItems = new List<InventoryItemPlacement>();
    }

    [Serializable]
    public sealed class SceneContainerData
    {
        public string containerId;
        public List<InventoryItemPlacement> items = new List<InventoryItemPlacement>();
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public InventoryData playerInventory = new InventoryData();
        public List<SceneContainerData> sceneContainers = new List<SceneContainerData>();
    }
}
