using System;
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
    public sealed class CharacterData
    {
        public int attributeId;
        public int stateId;
        public float health = 3f;
        public int stamina = 30;
        public string currentRoomId;
        public Vector2 position;
        public bool isDead;
    }
}
