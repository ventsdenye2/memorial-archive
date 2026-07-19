using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Monster.View;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.Logic
{
    public sealed class MonsterSystem : IGameSystem, ISaveModule
    {
        private readonly List<MonsterSpawnPointView> spawnPoints = new List<MonsterSpawnPointView>();
        private MonsterSaveData saveData = new MonsterSaveData();
        private GameContext context;

        public string ModuleKey => "monsters";

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<SceneLoadedEvent>(HandleSceneLoaded);
            context.Events.Subscribe<RoomEnteredEvent>(HandleRoomEntered);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);
                context.Events.Unsubscribe<RoomEnteredEvent>(HandleRoomEntered);
            }

            context = null;
            spawnPoints.Clear();
            saveData = new MonsterSaveData();
        }

        public void RegisterSpawnPoint(MonsterSpawnPointView spawnPoint)
        {
            if (spawnPoint != null && !spawnPoints.Contains(spawnPoint))
            {
                spawnPoints.Add(spawnPoint);
            }
        }

        public void UnregisterSpawnPoint(MonsterSpawnPointView spawnPoint)
        {
            spawnPoints.Remove(spawnPoint);
        }

        public object CaptureSaveData()
        {
            return saveData;
        }

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var restored = JsonUtility.FromJson<MonsterSaveData>(json);
            if (restored != null)
            {
                saveData = restored;
            }
        }

        private void HandleSceneLoaded(SceneLoadedEvent evt)
        {
            RefreshSpawnPoints(null);
        }

        private void HandleRoomEntered(RoomEnteredEvent evt)
        {
            RefreshSpawnPoints(evt.RoomId);
        }

        private void RefreshSpawnPoints(string roomId)
        {
            saveData.monsters.Clear();
            foreach (var spawnPoint in spawnPoints)
            {
                if (spawnPoint == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(roomId) && spawnPoint.RoomId != roomId)
                {
                    continue;
                }

                var monster = spawnPoint.SpawnOrRefresh();
                saveData.monsters.Add(new MonsterRuntimeData
                {
                    instanceId = monster,
                    monsterId = spawnPoint.MonsterId,
                    spawnPointId = spawnPoint.SpawnPointId,
                    health = spawnPoint.InitialHealth,
                    position = spawnPoint.transform.position,
                    isAlive = true
                });
            }
        }
    }
}
