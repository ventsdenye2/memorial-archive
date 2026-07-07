using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Puzzle.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Puzzle.Logic
{
    public sealed class PuzzleSystem : IGameSystem, ISaveModule
    {
        private readonly Dictionary<string, PuzzleRuntimeData> puzzles = new Dictionary<string, PuzzleRuntimeData>();
        private GameContext context;

        public string ModuleKey => "puzzles";

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<PuzzleInteractRequestedEvent>(HandlePuzzleInteractRequested);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<PuzzleInteractRequestedEvent>(HandlePuzzleInteractRequested);
            }

            context = null;
            puzzles.Clear();
        }

        public object CaptureSaveData()
        {
            return new PuzzleSaveData { puzzles = new List<PuzzleRuntimeData>(puzzles.Values) };
        }

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var saveData = JsonUtility.FromJson<PuzzleSaveData>(json);
            if (saveData == null || saveData.puzzles == null)
            {
                return;
            }

            puzzles.Clear();
            foreach (var puzzle in saveData.puzzles)
            {
                if (puzzle != null && !string.IsNullOrEmpty(puzzle.puzzleId))
                {
                    puzzles[puzzle.puzzleId] = puzzle;
                }
            }
        }

        private void HandlePuzzleInteractRequested(PuzzleInteractRequestedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.PuzzleId))
            {
                return;
            }

            var puzzle = GetOrCreate(evt.PuzzleId);
            if (puzzle.solved)
            {
                return;
            }

            puzzle.solved = true;
            context.Events.Publish(new PuzzleSolvedEvent(evt.PuzzleId));
        }

        private PuzzleRuntimeData GetOrCreate(string puzzleId)
        {
            if (!puzzles.TryGetValue(puzzleId, out var puzzle))
            {
                puzzle = new PuzzleRuntimeData { puzzleId = puzzleId };
                puzzles[puzzleId] = puzzle;
            }

            return puzzle;
        }
    }
}
