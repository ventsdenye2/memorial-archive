using System;
using System.Collections.Generic;

namespace MemorialArchive.Framework.Save
{
    [Serializable]
    public sealed class SaveData
    {
        public int saveVersion = 1;
        public string saveTime;
        public string currentSceneId;
        public string currentRoomId;
        public List<SaveModuleData> modules = new List<SaveModuleData>();
    }

    [Serializable]
    public sealed class SaveModuleData
    {
        public string moduleKey;
        public string json;
    }

    [Serializable]
    public sealed class SaveSlotInfo
    {
        public int slotIndex;
        public bool hasSave;
        public string saveTime;
        public string currentSceneId;
    }
}
