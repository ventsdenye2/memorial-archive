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
        Dead,
        // 目标在攻击距离内，但攻击间隔尚未结束。这个状态不移动，动画表现为待机。
        // 放在已有枚举值末尾，避免旧存档中的状态数值发生变化。
        AttackCooldown
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
