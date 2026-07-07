using System;
using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Inventory.View
{
    public sealed class SceneContainerSeedView : MonoBehaviour
    {
        [Serializable]
        private sealed class SceneItemSeed
        {
            public int itemId;
            public int quantity = 1;
            public int x;
            public int y;
        }

        [SerializeField] private string containerId;
        [SerializeField] private SceneItemSeed[] initialItems;

        private void Start()
        {
            var inventorySystem = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventorySystem == null || string.IsNullOrEmpty(containerId))
            {
                return;
            }

            var container = inventorySystem.GetOrCreateSceneContainer(containerId);
            if (container == null || container.items.Count > 0 || initialItems == null)
            {
                return;
            }

            foreach (var seed in initialItems)
            {
                if (seed == null || seed.itemId <= 0)
                {
                    continue;
                }

                container.items.Add(new InventoryItemPlacement
                {
                    item = new InventoryItemInstance
                    {
                        instanceId = Guid.NewGuid().ToString("N"),
                        itemId = seed.itemId,
                        quantity = Mathf.Max(1, seed.quantity)
                    },
                    containerKind = InventoryContainerKind.SceneContainer,
                    containerId = containerId,
                    x = seed.x,
                    y = seed.y,
                    width = 1,
                    height = 1,
                    slotIndex = -1
                });
            }
        }
    }
}
