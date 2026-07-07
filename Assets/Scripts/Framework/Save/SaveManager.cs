using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Framework.Save
{
    public sealed class SaveManager : IGameSystem
    {
        private readonly List<ISaveModule> modules = new List<ISaveModule>();
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
            context = null;
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
            var saveData = CaptureSaveData();
            Debug.Log($"Save requested for slot {saveRequested.SlotIndex}. Captured {saveData.modules.Count} modules.");
        }
    }
}
