using UnityEngine;

namespace MemorialArchive.Framework.Scene
{
    public sealed class SceneSpawnPoint : MonoBehaviour, ISceneSpawnPoint
    {
        [SerializeField] private string pointId;

        public string PointId => pointId;
        public Vector3 Position => transform.position;
    }
}
