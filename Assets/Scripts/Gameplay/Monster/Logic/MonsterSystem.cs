using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Monster.View;
using MemorialArchive.Gameplay.Combat.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.Logic
{
    public sealed class MonsterSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
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
            context.Events.Subscribe<DamageAppliedEvent>(HandleDamageApplied);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);
                context.Events.Unsubscribe<RoomEnteredEvent>(HandleRoomEntered);
                context.Events.Unsubscribe<DamageAppliedEvent>(HandleDamageApplied);
            }

            context = null;
            spawnPoints.Clear();
            saveData = new MonsterSaveData();
        }

        public void ResetForNewGame()
        {
            saveData = new MonsterSaveData();
        }

        public void RegisterSpawnPoint(MonsterSpawnPointView spawnPoint)
        {
            if (spawnPoint != null && !spawnPoints.Contains(spawnPoint))
            {
                spawnPoints.Add(spawnPoint);
                RefreshSpawnPoint(spawnPoint);
            }
        }

        public void UnregisterSpawnPoint(MonsterSpawnPointView spawnPoint)
        {
            spawnPoints.Remove(spawnPoint);
        }

        /// <summary>
        /// 由生成后的 MonsterTargetView 注册运行时实例。怪物生命仅由本系统
        /// 在收到 DamageAppliedEvent 后修改，View 不保存也不扣减生命值。
        /// </summary>
        public void RegisterSpawnedMonster(string instanceId, int monsterId, int initialHealth, Vector2 position)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            var runtime = saveData.monsters.Find(monster => monster != null && monster.instanceId == instanceId);
            if (runtime == null)
            {
                runtime = new MonsterRuntimeData { instanceId = instanceId };
                saveData.monsters.Add(runtime);
            }

            runtime.monsterId = monsterId;
            runtime.health = Mathf.Max(1, initialHealth);
            runtime.position = position;
            runtime.isAlive = true;
            runtime.state = MonsterActionState.Idle;
            runtime.hurtCooldownRemaining = 0f;
        }

        public void UpdateRuntimeState(string instanceId, Vector2 position, MonsterActionState state)
        {
            var runtime = FindMonster(instanceId);
            if (runtime == null || !runtime.isAlive)
            {
                return;
            }

            runtime.position = position;
            runtime.state = state;
        }

        public void Tick(float deltaTime)
        {
            foreach (var monster in saveData.monsters)
            {
                if (monster != null && monster.hurtCooldownRemaining > 0f)
                {
                    monster.hurtCooldownRemaining = Mathf.Max(0f, monster.hurtCooldownRemaining - deltaTime);
                }
            }
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

                RefreshSpawnPoint(spawnPoint);
            }
        }

        private void RefreshSpawnPoint(MonsterSpawnPointView spawnPoint)
        {
            if (spawnPoint == null)
            {
                return;
            }

            var instanceId = spawnPoint.SpawnOrRefresh();
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            var runtime = FindMonster(instanceId);
            if (runtime == null)
            {
                RegisterSpawnedMonster(instanceId, spawnPoint.MonsterId, spawnPoint.InitialHealth, spawnPoint.transform.position);
                runtime = FindMonster(instanceId);
            }

            if (runtime != null)
            {
                runtime.spawnPointId = spawnPoint.SpawnPointId;
            }
        }

        private void HandleDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.Amount <= 0 || string.IsNullOrEmpty(evt.TargetId) || evt.TargetId == CombatTargetIds.Player)
            {
                return;
            }

            var target = FindMonster(evt.TargetId);
            if (target == null || !target.isAlive)
            {
                return;
            }

            target.health = Mathf.Max(0f, target.health - evt.Amount);
            var died = target.health <= 0f;
            var triggersHurt = false;
            if (!died && target.hurtCooldownRemaining <= 0f)
            {
                var config = context.Configs.GetMonster(target.monsterId);
                target.hurtCooldownRemaining = config != null ? config.HurtCooldown : 1f;
                target.state = MonsterActionState.Hurt;
                triggersHurt = true;
            }

            context.Events.Publish(new MonsterDamagedEvent(target.instanceId, evt.Amount, target.health, triggersHurt));
            if (died)
            {
                target.isAlive = false;
                target.state = MonsterActionState.Dead;
                context.Events.Publish(new MonsterDiedEvent(target.instanceId));
            }
        }

        private MonsterRuntimeData FindMonster(string instanceId)
        {
            return string.IsNullOrEmpty(instanceId)
                ? null
                : saveData.monsters.Find(monster => monster != null && monster.instanceId == instanceId);
        }
    }
}
