namespace MemorialArchive.Framework.Core
{
    public interface IGameSystem
    {
        void Initialize(GameContext context);
        void Dispose();
    }

    /// <summary>Clears runtime state that must not carry into a newly started game.</summary>
    public interface INewGameResettable
    {
        void ResetForNewGame();
    }
}
