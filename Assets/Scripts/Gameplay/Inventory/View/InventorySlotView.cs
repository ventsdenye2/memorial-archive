using MemorialArchive.Gameplay.Inventory.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Inventory.View
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private InventoryContainerKind containerKind = InventoryContainerKind.Backpack;
        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private int slotIndex = -1;

        public InventoryContainerKind ContainerKind => containerKind;
        public int X => x;
        public int Y => y;
        public int SlotIndex => slotIndex;
    }
}
