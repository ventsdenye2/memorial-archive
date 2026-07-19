namespace MemorialArchive.Framework.Core
{
    public interface IGameSystem
    {
        void Initialize(GameContext context);
        void Dispose();
    }
}
