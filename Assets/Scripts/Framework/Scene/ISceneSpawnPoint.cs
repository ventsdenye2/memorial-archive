using UnityEngine;

namespace MemorialArchive.Framework.Scene
{
    public interface ISceneSpawnPoint
    {
        string PointId { get; }
        Vector3 Position { get; }
    }
}
