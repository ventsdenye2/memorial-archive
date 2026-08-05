using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Framework.Save
{
    public sealed class SaveManager : IGameSystem
    {
        public const int Stage1SlotCount = 3;

        private readonly List<ISaveModule> modules = new List<ISaveModule>();
        private readonly Dictionary<int, SaveData> inMemorySlots = new Dictionary<int, SaveData>();
        private GameContext context;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<SaveRequestedEvent>(HandleSaveRequested);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SaveRequestedEvent>(HandleSaveRequested);
            }

            modules.Clear();
            inMemorySlots.Clear();
            context = null;
        }

        public bool HasSlot(int slotIndex) => inMemorySlots.ContainsKey(slotIndex);

        public SaveData GetSlot(int slotIndex)
        {
            inMemorySlots.TryGetValue(slotIndex, out var saveData);
            return saveData;
        }

        public void RegisterModule(ISaveModule module)
        {
            if (module != null && !modules.Contains(module))
            {
                modules.Add(module);
            }
        }

        public SaveData CaptureSaveData()
        {
            var saveData = new SaveData
            {
                saveTime = DateTime.Now.ToString("O")
            };

            foreach (var module in modules)
            {
                var moduleData = module.CaptureSaveData();
                saveData.modules.Add(new SaveModuleData
                {
                    moduleKey = module.ModuleKey,
                    json = JsonUtility.ToJson(moduleData)
                });
            }

            return saveData;
        }

        public void RestoreSaveData(SaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            foreach (var module in modules)
            {
                var stored = saveData.modules.Find(entry => entry.moduleKey == module.ModuleKey);
                if (stored != null)
                {
                    module.RestoreSaveData(stored.json);
                }
            }
        }

        private void HandleSaveRequested(SaveRequestedEvent saveRequested)
        {
            if (saveRequested.SlotIndex < 0 || saveRequested.SlotIndex >= Stage1SlotCount)
            {
                var reason = $"Slot index must be between 0 and {Stage1SlotCount - 1}.";
                Debug.LogWarning(reason);
                context.Events.Publish(new SaveFailedEvent(saveRequested.SlotIndex, reason));
                return;
            }

            var overwroteExisting = inMemorySlots.ContainsKey(saveRequested.SlotIndex);
            var saveData = CaptureSaveData();
            inMemorySlots[saveRequested.SlotIndex] = saveData;
            Debug.Log($"Save requested for slot {saveRequested.SlotIndex}. Captured {saveData.modules.Count} modules. Overwrite={overwroteExisting}.");
            context.Events.Publish(new SaveCompletedEvent(saveRequested.SlotIndex, overwroteExisting));
        }
    }
}
