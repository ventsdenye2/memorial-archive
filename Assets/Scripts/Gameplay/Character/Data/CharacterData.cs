using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Data
{
    public enum CharacterActionState
    {
        Normal,
        Attack1,
        Attack2,
        Attack3,
        Blocking,
        Aiming,
        Dodging,
        Staggered,
        Weak,
        Dead
    }

    [Serializable]
    public sealed class CharacterTimedEffectData
    {
        public string effectId;
        public float remainingSeconds;
        public float staminaCostMultiplier = 1f;
        public float meleeDamageMultiplier = 1f;
        public bool restoreHealthAtExpiry;
        public float healthBeforeUse;
        public bool exhaustAtExpiry;
    }

    [Serializable]
    public sealed class CharacterData
    {
        public int attributeId;
        public int stateId;
        public float health = 3f;
        public int stamina = 30;
        public string currentRoomId;
        public Vector2 position;
        public bool isDead;
        public bool isBleeding;
        public bool isPoisoned;
        public List<CharacterTimedEffectData> activeEffects = new List<CharacterTimedEffectData>();
    }
}
