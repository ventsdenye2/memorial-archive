#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Combat.Logic;
using MemorialArchive.Gameplay.Monster.Config;
using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Monster.Logic;
using MemorialArchive.Gameplay.Monster.View;
using MemorialArchive.Gameplay.Stage1;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
    public static class Stage2MonsterBootstrap
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";

        [MenuItem("Tools/Memorial Archive/Build Stage 2 Monsters")]
        public static void BuildStage2Monsters()
        {
            EnsureFolder("Assets/GameConfigs/Monsters");
            EnsureFolder("Assets/Prefabs/Monster");

            var melee = BuildMonsterConfig(
                "Assets/GameConfigs/Monsters/Stage1MeleeMonster.asset",
                Stage1Ids.MeleeMonsterId,
                "怪物 1（近战）",
                1500,
                1,
                200f,
                8f,
                1.2f,
                1f,
                2.5f,
                MonsterAttackMode.Melee);
            var ranged = BuildMonsterConfig(
                "Assets/GameConfigs/Monsters/Stage1RangedMonster.asset",
                Stage1Ids.RangedMonsterId,
                "怪物 2（远程）",
                1000,
                1,
                180f,
                8f,
                5f,
                2f,
                2f,
                MonsterAttackMode.Ranged);

            RegisterConfigs(melee, ranged);
            var meleePrefab = BuildMonsterPrefab(
                "Assets/Prefabs/Monster/Stage1MeleeMonster.prefab",
                melee,
                "Assets/Actions/Enemies/Enemy01/Enemy_01 _action_SkeletonData.asset",
                "Enemy_01_idle",
                "Enemy_01_walk",
                "Enemy_01_attack01",
                "Enemy_01_hurt",
                "Enemy_01_die");
            var rangedPrefab = BuildMonsterPrefab(
                "Assets/Prefabs/Monster/Stage1RangedMonster.prefab",
                ranged,
                "Assets/Actions/Enemies/Enemy02/Enemy_02_action_SkeletonData.asset",
                "Enemy_02_Idle",
                "Enemy_02_walk",
                "Enemy_02_attack",
                "Enemy_02_hurt",
                "Enemy_02_die");

            ConfigureScene(melee, meleePrefab, ranged, rangedPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateStage2Monsters();
            Debug.Log("Stage 2 monster AI, prefabs, configs and spawn points built successfully.");
        }

        public static void BuildStage2MonstersBatch()
        {
            try
            {
                BuildStage2Monsters();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Memorial Archive/Validate Stage 2 Monsters")]
        public static void ValidateStage2Monsters()
        {
            AssertDecisionPolicy();
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(DatabasePath);
            if (database == null || database.GetType() == null)
            {
                throw new InvalidOperationException("GameConfigDatabase is missing.");
            }

            RequireConfig(database, Stage1Ids.MeleeMonsterId, 1500, 200f, 1.2f, 1f, MonsterAttackMode.Melee);
            RequireConfig(database, Stage1Ids.RangedMonsterId, 1000, 180f, 5f, 2f, MonsterAttackMode.Ranged);
            AssertDamagePipeline(database);
            AssertNewGameCharacterReset(database);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var spawnPoints = UnityEngine.Object.FindObjectsOfType<MonsterSpawnPointView>(true);
            if (spawnPoints.Length != 2)
            {
                throw new InvalidOperationException($"Expected 2 monster spawn points, found {spawnPoints.Length}.");
            }

            var demoRoot = FindRoot(scene, "Stage1DemoRoot");
            Require(demoRoot != null, "Stage1DemoRoot is missing from SampleScene.");
            foreach (var spawnPoint in spawnPoints)
            {
                var prefab = spawnPoint.MonsterPrefab;
                if (prefab == null || prefab.GetComponent<MonsterAIView>() == null ||
                    prefab.GetComponent<MonsterTargetView>() == null || prefab.GetComponent<Collider2D>() == null)
                {
                    throw new InvalidOperationException($"Spawn point '{spawnPoint.SpawnPointId}' has an invalid monster prefab.");
                }

                AssertMonsterPresentation(prefab);
                var expectedGroundY = FindGroundY(demoRoot.transform, spawnPoint.transform.parent);
                Require(Mathf.Abs(spawnPoint.transform.localPosition.y - expectedGroundY) <= 0.001f,
                    $"Spawn point '{spawnPoint.SpawnPointId}' is not aligned with the side-scrolling floor.");
            }

            if (!scene.IsValid())
            {
                throw new InvalidOperationException("SampleScene could not be opened for validation.");
            }
        }

        private static void AssertMonsterPresentation(GameObject prefab)
        {
            var visual = prefab.transform.Find("Visual");
            var skeleton = visual != null ? visual.GetComponent<SkeletonAnimation>() : null;
            var animationView = prefab.GetComponent<MonsterAnimationView>();
            var collider = prefab.GetComponent<BoxCollider2D>();
            Require(skeleton != null && animationView != null && collider != null,
                $"Monster prefab '{prefab.name}' is missing presentation components.");

            var animationSerialized = new SerializedObject(animationView);
            var idleAnimation = animationSerialized.FindProperty("idleAnimation").stringValue;
            var expectedVisualY = CalculateGroundingOffset(skeleton.skeletonDataAsset, idleAnimation);
            Require(Mathf.Abs(visual.localPosition.y - expectedVisualY) <= 0.001f,
                $"Monster prefab '{prefab.name}' visual is not grounded at its root.");
            Require(Mathf.Abs(collider.offset.y - collider.size.y * 0.5f) <= 0.001f,
                $"Monster prefab '{prefab.name}' collider is not grounded at its root.");

            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var runtimeSkeleton = instance.GetComponentInChildren<SkeletonAnimation>(true);
                var runtimeAnimation = instance.GetComponent<MonsterAnimationView>();
                runtimeSkeleton.Initialize(true);
                runtimeAnimation.SetFacing(-1f);
                Require(runtimeSkeleton.skeleton.ScaleX > 0f,
                    $"Monster prefab '{prefab.name}' must preserve its source-art orientation when facing left.");
                runtimeAnimation.SetFacing(1f);
                Require(runtimeSkeleton.skeleton.ScaleX < 0f,
                    $"Monster prefab '{prefab.name}' must flip its source art when facing right.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static MonsterConfig BuildMonsterConfig(
            string path,
            int id,
            string displayName,
            int health,
            int damage,
            float moveSpeed,
            float detectionRange,
            float attackRange,
            float attackInterval,
            float deathSeconds,
            MonsterAttackMode mode)
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MonsterConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            var serialized = new SerializedObject(config);
            Set(serialized, "monsterId", id);
            Set(serialized, "monsterName", displayName);
            Set(serialized, "maxHealth", health);
            Set(serialized, "attackDamage", damage);
            Set(serialized, "moveSpeed", moveSpeed);
            Set(serialized, "detectionRange", detectionRange);
            Set(serialized, "attackRange", attackRange);
            Set(serialized, "attackInterval", attackInterval);
            Set(serialized, "decisionInterval", 0.15f);
            Set(serialized, "hurtDuration", 0.3f);
            Set(serialized, "hurtCooldown", 1f);
            Set(serialized, "deathPresentationSeconds", deathSeconds);
            Set(serialized, "attackMode", (int)mode);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void RegisterConfigs(MonsterConfig melee, MonsterConfig ranged)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(DatabasePath);
            if (database == null)
            {
                throw new InvalidOperationException("GameConfigDatabase is missing.");
            }

            var configs = new List<MonsterConfig>();
            foreach (var existing in database.Monsters)
            {
                if (existing != null && existing.MonsterId != melee.MonsterId && existing.MonsterId != ranged.MonsterId)
                {
                    configs.Add(existing);
                }
            }

            configs.Add(melee);
            configs.Add(ranged);
            var serialized = new SerializedObject(database);
            var array = serialized.FindProperty("monsters");
            array.arraySize = configs.Count;
            for (var index = 0; index < configs.Count; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = configs[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static GameObject BuildMonsterPrefab(
            string path,
            MonsterConfig config,
            string skeletonPath,
            string idle,
            string walk,
            string attack,
            string hurt,
            string death)
        {
            var skeletonData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(skeletonPath);
            if (skeletonData == null)
            {
                throw new InvalidOperationException($"Skeleton data is missing: {skeletonPath}");
            }

            var root = new GameObject(config.AttackMode == MonsterAttackMode.Melee ? "Stage1MeleeMonster" : "Stage1RangedMonster");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.2f, 1.8f);
            collider.offset = new Vector2(0f, collider.size.y * 0.5f);

            var target = root.AddComponent<MonsterTargetView>();
            var targetSerialized = new SerializedObject(target);
            Set(targetSerialized, "monsterId", config.MonsterId);
            Set(targetSerialized, "initialHealth", config.MaxHealth);
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, CalculateGroundingOffset(skeletonData, idle), 0f);
            var skeleton = visual.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = skeletonData;
            var skeletonSerialized = new SerializedObject(skeleton);
            Set(skeletonSerialized, "_animationName", idle);
            Set(skeletonSerialized, "loop", true);
            skeletonSerialized.ApplyModifiedPropertiesWithoutUndo();

            var animation = root.AddComponent<MonsterAnimationView>();
            var animationSerialized = new SerializedObject(animation);
            Set(animationSerialized, "skeletonAnimation", skeleton);
            Set(animationSerialized, "idleAnimation", idle);
            Set(animationSerialized, "walkAnimation", walk);
            Set(animationSerialized, "attackAnimation", attack);
            Set(animationSerialized, "hurtAnimation", hurt);
            Set(animationSerialized, "deathAnimation", death);
            Set(animationSerialized, "artFacesRight", false);
            animationSerialized.ApplyModifiedPropertiesWithoutUndo();

            var ai = root.AddComponent<MonsterAIView>();
            var aiSerialized = new SerializedObject(ai);
            Set(aiSerialized, "body", body);
            Set(aiSerialized, "targetView", target);
            Set(aiSerialized, "animationView", animation);
            aiSerialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static float CalculateGroundingOffset(SkeletonDataAsset skeletonDataAsset, string idleAnimation)
        {
            var skeletonData = skeletonDataAsset.GetSkeletonData(true);
            if (skeletonData == null)
            {
                throw new InvalidOperationException($"Could not load skeleton data from '{skeletonDataAsset.name}'.");
            }

            var animation = skeletonData.FindAnimation(idleAnimation);
            if (animation == null)
            {
                throw new InvalidOperationException(
                    $"Idle animation '{idleAnimation}' is missing from '{skeletonDataAsset.name}'.");
            }

            var skeleton = new Spine.Skeleton(skeletonData);
            skeleton.SetToSetupPose();
            animation.Apply(skeleton, 0f, 0f, false, null, 1f, Spine.MixBlend.Replace, Spine.MixDirection.In);
            skeleton.UpdateWorldTransform();

            float[] vertexBuffer = null;
            skeleton.GetBounds(out _, out var minimumY, out _, out var height, ref vertexBuffer);
            if (height <= 0f)
            {
                throw new InvalidOperationException(
                    $"Idle animation '{idleAnimation}' has no visible bounds in '{skeletonDataAsset.name}'.");
            }

            return -minimumY;
        }

        private static void ConfigureScene(
            MonsterConfig melee,
            GameObject meleePrefab,
            MonsterConfig ranged,
            GameObject rangedPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var demoRoot = FindRoot(scene, "Stage1DemoRoot");
            if (demoRoot == null)
            {
                throw new InvalidOperationException("Stage1DemoRoot is missing from SampleScene.");
            }

            var holder = FindChildOrCreate(demoRoot.transform, "MonsterSpawnPoints");
            var groundY = FindGroundY(demoRoot.transform, holder.transform);
            ConfigureSpawnPoint(holder.transform, "MeleeMonsterSpawnPoint", Stage1Ids.MeleeMonsterSpawnPoint,
                melee, meleePrefab, new Vector2(0.5f, groundY));
            ConfigureSpawnPoint(holder.transform, "RangedMonsterSpawnPoint", Stage1Ids.RangedMonsterSpawnPoint,
                ranged, rangedPrefab, new Vector2(4.5f, groundY));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static float FindGroundY(Transform demoRoot, Transform spawnParent)
        {
            var bottomWall = FindDescendant(demoRoot, "Wall_Bottom");
            var collider = bottomWall != null ? bottomWall.GetComponent<BoxCollider2D>() : null;
            if (collider == null)
            {
                throw new InvalidOperationException("Wall_Bottom with a BoxCollider2D is required to ground monsters.");
            }

            var localTop = new Vector3(
                collider.offset.x,
                collider.offset.y + collider.size.y * 0.5f,
                0f);
            var worldTop = bottomWall.TransformPoint(localTop);
            return spawnParent.InverseTransformPoint(worldTop).y;
        }

        private static void ConfigureSpawnPoint(
            Transform parent,
            string name,
            string spawnPointId,
            MonsterConfig config,
            GameObject prefab,
            Vector2 position)
        {
            var owner = FindChildOrCreate(parent, name);
            owner.transform.localPosition = position;
            var view = owner.GetComponent<MonsterSpawnPointView>() ?? owner.AddComponent<MonsterSpawnPointView>();
            var serialized = new SerializedObject(view);
            Set(serialized, "spawnPointId", spawnPointId);
            Set(serialized, "roomId", string.Empty);
            Set(serialized, "monsterId", config.MonsterId);
            Set(serialized, "initialHealth", config.MaxHealth);
            Set(serialized, "monsterPrefab", prefab);
            Set(serialized, "spawnParent", parent);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertDecisionPolicy()
        {
            Require(MonsterDecisionPolicy.Decide(false, true, 0f, 8f, 1.2f, true) == MonsterActionState.Idle,
                "AI without a player must idle.");
            Require(MonsterDecisionPolicy.Decide(true, true, 1f, 8f, 1.2f, true) == MonsterActionState.Attacking,
                "AI in range with a ready cooldown must attack.");
            Require(MonsterDecisionPolicy.Decide(true, true, 1f, 8f, 1.2f, false) == MonsterActionState.Idle,
                "AI in range while cooling down must wait.");
            Require(MonsterDecisionPolicy.Decide(true, true, 4f, 8f, 1.2f, true) == MonsterActionState.Chasing,
                "AI in detection range must chase.");
            Require(MonsterDecisionPolicy.Decide(true, true, 9f, 8f, 1.2f, true) == MonsterActionState.Idle,
                "AI outside detection range must idle.");
        }

        private static void AssertDamagePipeline(GameConfigDatabase database)
        {
            var events = new EventBus();
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, new SaveManager(), null, new SceneFlowManager());
            var combat = new CombatSystem();
            var monsters = new MonsterSystem();
            var died = false;
            events.Subscribe<MonsterDiedEvent>(evt => died |= evt.MonsterInstanceId == "validation_monster");

            configs.Initialize(context);
            combat.Initialize(context);
            monsters.Initialize(context);
            monsters.RegisterSpawnedMonster("validation_monster", Stage1Ids.MeleeMonsterId, 10, Vector2.zero);
            events.Publish(new DamageRequestedEvent(new DamageRequest(
                1,
                CombatTargetIds.Player,
                "validation_monster",
                0,
                DamageType.Physical,
                4,
                Vector2.zero)));

            var save = monsters.CaptureSaveData() as MonsterSaveData;
            Require(save != null && save.monsters.Count == 1 && Mathf.Approximately(save.monsters[0].health, 6f),
                "DamageRequestedEvent must resolve through CombatSystem before MonsterSystem changes health.");
            events.Publish(new DamageRequestedEvent(new DamageRequest(
                2,
                CombatTargetIds.Player,
                "validation_monster",
                0,
                DamageType.Physical,
                6,
                Vector2.zero)));
            Require(died && !save.monsters[0].isAlive && save.monsters[0].state == MonsterActionState.Dead,
                "Fatal resolved damage must publish MonsterDiedEvent and stop the monster.");

            monsters.Dispose();
            combat.Dispose();
            configs.Dispose();
        }

        private static void AssertNewGameCharacterReset(GameConfigDatabase database)
        {
            var events = new EventBus();
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, new SaveManager(), null, new SceneFlowManager());
            var character = new CharacterSystem();

            configs.Initialize(context);
            character.Initialize(context);
            events.Publish(new DamageAppliedEvent(CombatTargetIds.Player, 999f));
            Require(character.Data.isDead && character.ActionState == CharacterActionState.Dead,
                "Fatal damage must put the character into the persistent death state.");

            events.Publish(new MoveInputEvent(Vector2.left));
            Require(character.MoveDirection == Vector2.zero,
                "A dead character must reject movement before the session reset.");

            character.ResetForNewGame();
            events.Publish(new MoveInputEvent(Vector2.left));
            Require(!character.Data.isDead && character.Data.health > 0f &&
                    character.ActionState == CharacterActionState.Normal && character.MoveDirection == Vector2.left,
                "Starting a new game must revive the character and restore keyboard movement.");

            character.Dispose();
            configs.Dispose();
        }

        private static void RequireConfig(GameConfigDatabase database, int id, int health, float speed,
            float range, float interval, MonsterAttackMode mode)
        {
            MonsterConfig found = null;
            foreach (var config in database.Monsters)
            {
                if (config != null && config.MonsterId == id)
                {
                    found = config;
                    break;
                }
            }

            Require(found != null, $"MonsterConfig {id} is not registered.");
            Require(found.MaxHealth == health && Mathf.Approximately(found.MoveSpeed, speed) &&
                    Mathf.Approximately(found.AttackRange, range) && Mathf.Approximately(found.AttackInterval, interval) &&
                    found.AttackMode == mode,
                $"MonsterConfig {id} does not match the state table.");
            Require(found.DecisionInterval >= 0.1f && found.DecisionInterval <= 0.2f,
                $"MonsterConfig {id} decision interval must be 0.1-0.2 seconds.");
        }

        private static void Set(SerializedObject serialized, string propertyName, object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property {propertyName}.");
            }

            switch (value)
            {
                case int intValue when property.propertyType == SerializedPropertyType.Enum:
                    property.enumValueIndex = intValue;
                    break;
                case int intValue:
                    property.intValue = intValue;
                    break;
                case float floatValue:
                    property.floatValue = floatValue;
                    break;
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                case string stringValue:
                    property.stringValue = stringValue;
                    break;
                case UnityEngine.Object objectValue:
                    property.objectReferenceValue = objectValue;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported serialized value: {value?.GetType().Name ?? "null"}");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static GameObject FindChildOrCreate(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var owner = new GameObject(name);
            owner.transform.SetParent(parent, false);
            return owner;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDescendant(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var separator = path.LastIndexOf('/');
            var parent = path.Substring(0, separator);
            var name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
