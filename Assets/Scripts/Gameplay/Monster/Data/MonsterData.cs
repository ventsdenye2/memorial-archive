using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.Data
{
    [Serializable]
    public sealed class MonsterRuntimeData
    {
        public string instanceId;
        public int monsterId;
        public string spawnPointId;
        public int health;
        public Vector2 position;
        public bool isAlive = true;
    }

    [Serializable]
    public sealed class MonsterSaveData
    {
        public List<MonsterRuntimeData> monsters = new List<MonsterRuntimeData>();
    }
}
