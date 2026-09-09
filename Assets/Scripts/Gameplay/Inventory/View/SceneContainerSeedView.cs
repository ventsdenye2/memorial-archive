using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Inventory.View
{
    public sealed class SceneContainerSeedView : MonoBehaviour
    {
        [Serializable]
        public sealed class SceneItemSeed
        {
            public int itemId;
            public int quantity = 1;
            public int x;
            public int y;
        }

        [SerializeField] private string containerId;
        [SerializeField] private SceneItemSeed[] initialItems;
        [SerializeField] private int[] randomItemPool;
        [SerializeField, Range(0, 4)] private int randomItemCount = 1;

        public string ContainerId => containerId;
        public SceneItemSeed[] InitialItems => initialItems;

        private void Start()
        {
            var inventorySystem = GameRoot.Instance?.GetSystem<InventorySystem>();
            if (inventorySystem == null || string.IsNullOrEmpty(containerId))
            {
                return;
            }

            if (inventorySystem.IsSceneContainerInitialized(containerId))
            {
                return;
            }

            var occupied = new bool[InventorySystem.SceneContainerWidth, InventorySystem.SceneContainerHeight];
            var seeds = new List<SceneItemSeed>(initialItems ?? Array.Empty<SceneItemSeed>());
            var placements = new List<InventoryItemPlacement>();
            foreach (var seed in seeds)
            {
                if (seed == null || seed.itemId <= 0 ||
                    seed.x < 0 || seed.x >= InventorySystem.SceneContainerWidth ||
                    seed.y < 0 || seed.y >= InventorySystem.SceneContainerHeight || occupied[seed.x, seed.y])
                {
                    Debug.LogError($"Invalid or overlapping seed in container {containerId}.", this);
                    return;
                }

                occupied[seed.x, seed.y] = true;

                placements.Add(new InventoryItemPlacement
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

            if (randomItemPool != null && randomItemPool.Length > 0)
            {
                var remaining = Mathf.Clamp(randomItemCount, 0, 4);
                for (var y = 0; y < InventorySystem.SceneContainerHeight && remaining > 0; y++)
                for (var x = 0; x < InventorySystem.SceneContainerWidth && remaining > 0; x++)
                {
                    if (occupied[x, y]) continue;
                    placements.Add(new InventoryItemPlacement
                    {
                        item = new InventoryItemInstance
                        {
                            instanceId = Guid.NewGuid().ToString("N"),
                            itemId = randomItemPool[UnityEngine.Random.Range(0, randomItemPool.Length)],
                            quantity = 1
                        },
                        x = x,
                        y = y
                    });
                    remaining--;
                }
            }

            if (!inventorySystem.TryInitializeSceneContainer(containerId, placements))
            {
                Debug.LogError($"Container {containerId} has invalid seed configuration.", this);
            }
        }
    }
}
