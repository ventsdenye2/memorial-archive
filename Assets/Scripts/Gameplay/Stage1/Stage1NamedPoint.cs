using MemorialArchive.Framework.Scene;
using UnityEngine;

namespace MemorialArchive.Gameplay.Stage1
{
    public sealed class Stage1NamedPoint : MonoBehaviour, ISceneSpawnPoint
    {
        [SerializeField] private string pointId;
        public string PointId => pointId;
        public Vector3 Position => transform.position;
    }
}
