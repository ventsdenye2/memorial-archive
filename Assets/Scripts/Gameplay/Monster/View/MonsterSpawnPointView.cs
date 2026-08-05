using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Monster.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.View
{
    public sealed class MonsterSpawnPointView : MonoBehaviour
    {
        [SerializeField] private string spawnPointId;
        [SerializeField] private string roomId;
        [SerializeField] private int monsterId;
        [SerializeField] private int initialHealth = 100;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Transform spawnParent;

        private GameObject spawnedInstance;

        public string SpawnPointId => spawnPointId;
        public string RoomId => roomId;
        public int MonsterId => monsterId;
        public int InitialHealth => initialHealth;
        public GameObject MonsterPrefab => monsterPrefab;
        public GameObject SpawnedInstance => spawnedInstance;

        private void OnEnable()
        {
            GameRoot.Instance?.GetSystem<MonsterSystem>()?.RegisterSpawnPoint(this);
        }

        private void OnDisable()
        {
            GameRoot.Instance?.GetSystem<MonsterSystem>()?.UnregisterSpawnPoint(this);
        }

        public string SpawnOrRefresh()
        {
            if (spawnedInstance != null)
            {
                Destroy(spawnedInstance);
            }

            if (monsterPrefab != null)
            {
                spawnedInstance = Instantiate(monsterPrefab, transform.position, transform.rotation, spawnParent);
                var instanceId = $"{spawnPointId}_{spawnedInstance.GetInstanceID()}";
                var target = spawnedInstance.GetComponentInChildren<MonsterTargetView>();
                if (target == null || target.GetComponent<Collider2D>() == null)
                {
                    Debug.LogError($"Monster prefab '{monsterPrefab.name}' must contain MonsterTargetView and Collider2D.", this);
                    Destroy(spawnedInstance);
                    spawnedInstance = null;
                    return string.Empty;
                }

                target.SetRuntimeIdentity(instanceId, monsterId, initialHealth);
                return instanceId;
            }

            Debug.LogError($"Monster spawn point '{spawnPointId}' has no prefab assigned.", this);
            return string.Empty;
        }
    }
}
