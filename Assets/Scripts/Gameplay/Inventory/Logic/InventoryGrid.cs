using System.Collections.Generic;
using MemorialArchive.Gameplay.Inventory.Data;

namespace MemorialArchive.Gameplay.Inventory.Logic
{
    public sealed class InventoryGrid
    {
        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public bool CanPlace(InventoryItemPlacement candidate, IEnumerable<InventoryItemPlacement> existingItems, params string[] ignoredInstanceIds)
        {
            if (candidate == null || candidate.width <= 0 || candidate.height <= 0)
            {
                return false;
            }

            if (candidate.x < 0 || candidate.y < 0)
            {
                return false;
            }

            if (candidate.x + candidate.width > Width || candidate.y + candidate.height > Height)
            {
                return false;
            }

            if (existingItems == null)
            {
                return true;
            }

            foreach (var existing in existingItems)
            {
                if (existing == null || existing.item == null)
                {
                    continue;
                }

                if (ShouldIgnore(existing.item.instanceId, ignoredInstanceIds))
                {
                    continue;
                }

                if (Overlaps(candidate, existing))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldIgnore(string instanceId, string[] ignoredInstanceIds)
        {
            if (string.IsNullOrEmpty(instanceId) || ignoredInstanceIds == null)
            {
                return false;
            }

            foreach (var ignoredId in ignoredInstanceIds)
            {
                if (!string.IsNullOrEmpty(ignoredId) && ignoredId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Overlaps(InventoryItemPlacement a, InventoryItemPlacement b)
        {
            return a.x < b.x + b.width &&
                   a.x + a.width > b.x &&
                   a.y < b.y + b.height &&
                   a.y + a.height > b.y;
        }
    }
}
