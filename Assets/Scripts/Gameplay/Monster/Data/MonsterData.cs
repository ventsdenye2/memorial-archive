using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.Data
{
    public enum MonsterActionState
    {
        Idle,
        Chasing,
        Attacking,
        Hurt,
        Dead
    }

    [Serializable]
    public sealed class MonsterRuntimeData
    {
        public string instanceId;
        public int monsterId;
        public string spawnPointId;
        public float health;
        public Vector2 position;
        public bool isAlive = true;
        public MonsterActionState state;
        public float hurtCooldownRemaining;
    }

    [Serializable]
    public sealed class MonsterSaveData
    {
        public List<MonsterRuntimeData> monsters = new List<MonsterRuntimeData>();
    }
}
