using System;
using System.Collections.Generic;

namespace MemorialArchive.Gameplay.Story.Data
{
    [Serializable]
    public sealed class StorySaveData
    {
        public List<string> unlockedStoryIds = new List<string>();
        public List<string> unlockedNoteIds = new List<string>();
        public List<string> playedStoryIds = new List<string>();
    }
}
