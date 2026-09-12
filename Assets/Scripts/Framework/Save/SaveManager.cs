using System;
using System.Collections.Generic;
using System.IO;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Framework.Save
{
    public sealed class SaveManager : IGameSystem
    {
        public const int Stage1SlotCount = 3;

        private readonly List<ISaveModule> modules = new List<ISaveModule>();
        private readonly Dictionary<int, SaveData> inMemorySlots = new Dictionary<int, SaveData>();
        private GameContext context;
        private string currentRoomId;
        private readonly string storageDirectory;

        public SaveManager(string storageDirectory = null)
        {
            this.storageDirectory = storageDirectory;
        }

        private string SaveDirectory => storageDirectory ?? Application.persistentDataPath;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<SaveRequestedEvent>(HandleSaveRequested);
            context.Events.Subscribe<LoadRequestedEvent>(HandleLoadRequested);
            context.Events.Subscribe<RoomEnteredEvent>(HandleRoomEntered);
            LoadPersistentSlots();
            Debug.Log($"Save directory: {SaveDirectory}. Loaded {inMemorySlots.Count} slots.");
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SaveRequestedEvent>(HandleSaveRequested);
                context.Events.Unsubscribe<LoadRequestedEvent>(HandleLoadRequested);
                context.Events.Unsubscribe<RoomEnteredEvent>(HandleRoomEntered);
            }

            modules.Clear();
            inMemorySlots.Clear();
            currentRoomId = null;
            context = null;
        }

        public bool HasSlot(int slotIndex) => inMemorySlots.ContainsKey(slotIndex);

        public SaveData GetSlot(int slotIndex)
        {
            inMemorySlots.TryGetValue(slotIndex, out var saveData);
            return saveData;
        }

        public SaveSlotInfo GetSlotInfo(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || !inMemorySlots.TryGetValue(slotIndex, out var saveData) || saveData == null)
            {
                return new SaveSlotInfo { slotIndex = slotIndex, hasSave = false };
            }

            return new SaveSlotInfo
            {
                slotIndex = slotIndex,
                hasSave = true,
                saveTime = saveData.saveTime,
                currentSceneId = saveData.currentSceneId
            };
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
                saveTime = DateTime.Now.ToString("O"),
                currentSceneId = SceneManager.GetActiveScene().name,
                currentRoomId = currentRoomId
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

            try
            {
                var overwroteExisting = inMemorySlots.ContainsKey(saveRequested.SlotIndex);
                var saveData = CaptureSaveData();
                WriteSlot(saveRequested.SlotIndex, saveData);
                inMemorySlots[saveRequested.SlotIndex] = saveData;
                Debug.Log($"Save requested for slot {saveRequested.SlotIndex}. Captured {saveData.modules.Count} modules. Overwrite={overwroteExisting}.");
                context.Events.Publish(new SaveCompletedEvent(saveRequested.SlotIndex, overwroteExisting));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                context.Events.Publish(new SaveFailedEvent(saveRequested.SlotIndex, exception.Message));
            }
        }

        private void HandleLoadRequested(LoadRequestedEvent loadRequested)
        {
            if (!IsValidSlot(loadRequested.SlotIndex))
            {
                context.Events.Publish(new LoadFailedEvent(loadRequested.SlotIndex, $"Slot index must be between 0 and {Stage1SlotCount - 1}."));
                return;
            }

            if (!inMemorySlots.TryGetValue(loadRequested.SlotIndex, out var saveData) || saveData == null)
            {
                context.Events.Publish(new LoadFailedEvent(loadRequested.SlotIndex, "该存档槽为空。"));
                return;
            }

            if (string.IsNullOrEmpty(saveData.currentSceneId) || !Application.CanStreamedLevelBeLoaded(saveData.currentSceneId))
            {
                context.Events.Publish(new LoadFailedEvent(loadRequested.SlotIndex, "存档对应的场景不可用。"));
                return;
            }

            try
            {
                context.UI?.CloseAll();
                RestoreSaveData(saveData);
                currentRoomId = saveData.currentRoomId;
                context.Scenes.LoadSavedScene(saveData.currentSceneId, GetSavedScenePosition());
                context.Events.Publish(new LoadCompletedEvent(loadRequested.SlotIndex));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                context.Events.Publish(new LoadFailedEvent(loadRequested.SlotIndex, exception.Message));
            }
        }

        private void HandleRoomEntered(RoomEnteredEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.RoomId))
            {
                currentRoomId = evt.RoomId;
            }
        }

        private Vector3 GetSavedScenePosition()
        {
            foreach (var module in modules)
            {
                if (module is ISaveScenePositionProvider positionProvider)
                {
                    return positionProvider.SavedScenePosition;
                }
            }

            return Vector3.zero;
        }

        private void LoadPersistentSlots()
        {
            for (var slotIndex = 0; slotIndex < Stage1SlotCount; slotIndex++)
            {
                var path = GetSlotPath(slotIndex);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var saveData = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (saveData != null)
                    {
                        inMemorySlots[slotIndex] = saveData;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Unable to read save slot {slotIndex}: {exception.Message}");
                }
            }
        }

        private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < Stage1SlotCount;

        private string GetSlotPath(int slotIndex) =>
            Path.Combine(SaveDirectory, $"save_slot_{slotIndex}.json");

        private void WriteSlot(int slotIndex, SaveData saveData)
        {
            var directory = SaveDirectory;
            Directory.CreateDirectory(directory);
            var path = GetSlotPath(slotIndex);
            var temporaryPath = path + ".tmp";
            // Finish writing before replacing the last valid save.
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(saveData, true));
            if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
            else File.Move(temporaryPath, path);
        }
    }
}
