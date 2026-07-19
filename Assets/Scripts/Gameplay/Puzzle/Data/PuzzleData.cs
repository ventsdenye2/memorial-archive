using System;
using System.Collections.Generic;

namespace MemorialArchive.Gameplay.Puzzle.Data
{
    [Serializable]
    public sealed class PuzzleRuntimeData
    {
        public string puzzleId;
        public bool solved;
        public int failedCount;
    }

    [Serializable]
    public sealed class PuzzleSaveData
    {
        public List<PuzzleRuntimeData> puzzles = new List<PuzzleRuntimeData>();
    }
}
