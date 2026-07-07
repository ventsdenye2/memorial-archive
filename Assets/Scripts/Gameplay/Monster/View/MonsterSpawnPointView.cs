using System;
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
                return $"{spawnPointId}_{spawnedInstance.GetInstanceID()}";
            }

            return $"{spawnPointId}_{Guid.NewGuid():N}";
        }
    }
}
