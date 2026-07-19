using System;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Data
{
    [Serializable]
    public sealed class CharacterData
    {
        public int attributeId;
        public int stateId;
        public int health = 3;
        public int stamina = 30;
        public string currentRoomId;
        public Vector2 position;
        public bool isDead;
    }
}
