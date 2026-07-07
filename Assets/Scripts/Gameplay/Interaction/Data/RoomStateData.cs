using System;
using System.Collections.Generic;

namespace MemorialArchive.Gameplay.Interaction.Data
{
    [Serializable]
    public sealed class RoomStateData
    {
        public string roomId;
        public bool visited;
        public bool monstersSpawned;
        public List<string> completedInteractionIds = new List<string>();
    }
}
