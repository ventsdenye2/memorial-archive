using UnityEngine;

namespace MemorialArchive.Gameplay.Stage1
{
    public sealed class Stage1NamedPoint : MonoBehaviour
    {
        [SerializeField] private string pointId;
        public string PointId => pointId;
    }
}
