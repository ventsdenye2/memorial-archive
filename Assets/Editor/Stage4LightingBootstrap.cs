#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MemorialArchive.Framework.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Lighting.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
        /// <summary>
        /// 第四阶段光照系统引导脚本：创建光照配置资产、把美术预置的“灯光”
        /// 叠层对象接入区域视图、在 gameplay 场景放置占位灯具与黑暗层，并注册
        /// 到 GameConfigDatabase。可重复执行（幂等）。
        /// 走廊特殊灯具的横向位置按策划《灯具分布图》配置；房间特殊灯具与
        /// 普通灯分布图未标注，仍为占位，待美术定位后按 lightId 平移调整。
        /// </summary>
    public static class Stage4LightingBootstrap
    {
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";
        private const string LightingConfigFolder = "Assets/GameConfigs/Lighting";
        private const string LightSourceConfigFolder = "Assets/GameConfigs/Lighting/LightSources";
        private const string DarknessMaterialPath = "Assets/GameConfigs/Lighting/DarknessOverlayMaterial.mat";
        private const string GlobalConfigPath = "Assets/GameConfigs/Lighting/LightingGlobalConfig.asset";
        private const string DarknessShaderName = "Memorial Archive/Lighting/Darkness Overlay";

        private struct ScenePlan
        {
            public string SceneName;
            public string RegionId;
            public bool PlaceSpecial;
            public int NormalLampCount;

            /// <summary>特殊灯具在场景横向范围（避开两侧边界后的区间）内的位置，0=最左 1=最右。</summary>
            public float SpecialXFraction;
        }

        private static readonly ScenePlan[] ScenePlans =
        {
            // 前厅并入 1F 走廊区域（corridor_1f），该区域的特殊灯具放在前厅。
            // 走廊特殊灯具的横向位置来自策划《灯具分布图》（4 个紫色菱形）：
            // 1F 前厅展览馆正中、2F 走廊中部、3F 走廊右端（禁闭室侧）、4F 走廊左端（露台侧）。
            new ScenePlan { SceneName = "FrontHall", RegionId = "corridor_1f", PlaceSpecial = true, NormalLampCount = 2, SpecialXFraction = 0.45f },
            new ScenePlan { SceneName = "Floor_1F", RegionId = "corridor_1f", PlaceSpecial = false, NormalLampCount = 3 },
            new ScenePlan { SceneName = "Floor_2F", RegionId = "corridor_2f", PlaceSpecial = true, NormalLampCount = 3, SpecialXFraction = 0.5f },
            new ScenePlan { SceneName = "Floor_3F", RegionId = "corridor_3f", PlaceSpecial = true, NormalLampCount = 3, SpecialXFraction = 0.9f },
            new ScenePlan { SceneName = "Floor_4F", RegionId = "corridor_4f", PlaceSpecial = true, NormalLampCount = 2, SpecialXFraction = 0.12f },
            // 房间内特殊灯具与普通灯均未出现在分布图上（§6：房间灯具“由美术自己设定位置”），
            // 以下数值仍是占位，待美术定位图后按 lightId 调整。
            new ScenePlan { SceneName = "Room_Toilet", RegionId = "room_toilet", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_Office", RegionId = "room_office", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_ArchiveA", RegionId = "room_archive_a", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_ArchiveB", RegionId = "room_archive_b", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_ArchiveC", RegionId = "room_archive_c", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_TreatmentA", RegionId = "room_treatment_a", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_TreatmentB", RegionId = "room_treatment_b", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_Reception", RegionId = "room_reception", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_Director", RegionId = "room_director", PlaceSpecial = true, NormalLampCount = 1, SpecialXFraction = 1f }
        };

        [MenuItem("Tools/Memorial Archive/Build Stage 4 Lighting")]
        public static void BuildStage4Lighting()
        {
            EnsureFolder(LightingConfigFolder);
            EnsureFolder(LightSourceConfigFolder);

            var globalConfig = BuildGlobalConfig();
            ClearLightSourceConfigs();

            var createdConfigs = new List<LightSourceConfig>();

            foreach (var plan in ScenePlans)
            {
                createdConfigs.AddRange(BuildScene(plan, globalConfig));
            }

            RegisterConfigsInDatabase(globalConfig, createdConfigs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Stage 4 lighting built. Light source configs: {createdConfigs.Count}.");
        }

        public static void BuildStage4LightingBatch()
        {
            try
            {
                BuildStage4Lighting();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static LightingGlobalConfig BuildGlobalConfig()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DarknessMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find(DarknessShaderName);
                if (shader == null)
                {
                    throw new InvalidOperationException($"找不到 shader：{DarknessShaderName}");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, DarknessMaterialPath);
            }

            var config = AssetDatabase.LoadAssetAtPath<LightingGlobalConfig>(GlobalConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LightingGlobalConfig>();
                AssetDatabase.CreateAsset(config, GlobalConfigPath);
            }

            var configObject = new SerializedObject(config);
            var materialProperty = configObject.FindProperty("darknessMaterial");
            if (materialProperty.objectReferenceValue == null)
            {
                materialProperty.objectReferenceValue = material;
                configObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return config;
        }

        private static List<LightSourceConfig> BuildScene(ScenePlan plan, LightingGlobalConfig globalConfig)
        {
            var scenePath = $"Assets/Scenes/{plan.SceneName}.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Stage4Lighting] 场景不存在，跳过：{scenePath}");
                return new List<LightSourceConfig>();
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var configs = new List<LightSourceConfig>();

            try
            {
                RemoveExistingFixtures(scene);
                TagRegionOverlays(scene, plan.RegionId);
                EnsureDarknessOverlay(globalConfig);
                configs.AddRange(PlaceFixtures(plan));
                EditorSceneManager.SaveScene(scene);
            }
            catch (Exception)
            {
                // 失败时不保存该场景，避免半成品写入。
                throw;
            }

            return configs;
        }

        /// <summary>清理本场景已放置的占位灯具；lightId 规则变化时重跑不会残留旧对象。</summary>
        private static void RemoveExistingFixtures(Scene scene)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name.StartsWith("light_", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(rootObject);
                }
            }
        }

        private static void TagRegionOverlays(Scene scene, string regionId)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (!IsLightingOverlayName(rootObject.name))
                {
                    continue;
                }

                var regionView = rootObject.GetComponent<LightRegionView>();
                if (regionView == null)
                {
                    regionView = rootObject.AddComponent<LightRegionView>();
                }

                SetSerializedString(regionView, "regionId", regionId);
            }
        }

        /// <summary>
        /// 美术的灯光叠层命名不统一：多数叫“灯光Scene_XX_Background_Far”，
        /// 接待室叫“光Scene_20_Background_Far”（少一个“灯”字）。
        /// </summary>
        private static bool IsLightingOverlayName(string objectName)
        {
            return objectName.IndexOf("灯光", StringComparison.Ordinal) >= 0
                || objectName.StartsWith("光Scene", StringComparison.Ordinal);
        }

        private static void EnsureDarknessOverlay(LightingGlobalConfig globalConfig)
        {
            var overlayObject = GameObject.Find("DarknessOverlay");
            if (overlayObject == null)
            {
                overlayObject = new GameObject("DarknessOverlay");
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(
                    overlayObject, SceneManager.GetActiveScene());
            }

            var overlay = overlayObject.GetComponent<DarknessOverlayView>();
            if (overlay == null)
            {
                overlay = overlayObject.AddComponent<DarknessOverlayView>();
            }

            var overlayObjectSerialized = new SerializedObject(overlay);
            overlayObjectSerialized.FindProperty("config").objectReferenceValue = globalConfig;
            overlayObjectSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<LightSourceConfig> PlaceFixtures(ScenePlan plan)
        {
            var configs = new List<LightSourceConfig>();
            var bounds = ResolveSceneBounds();

            if (plan.PlaceSpecial)
            {
                var lightId = $"light_{plan.RegionId}_special";
                var x = Mathf.Lerp(bounds.min.x + 2f, bounds.max.x - 2.5f, plan.SpecialXFraction);
                var position = new Vector3(x, bounds.max.y - 1.2f, 0f);
                CreateFixture(lightId, plan.RegionId, true, position, 4f);
                configs.Add(GetOrCreateLightConfig(lightId, plan.RegionId, true, 4f));
            }

            for (var index = 0; index < plan.NormalLampCount; index++)
            {
                // 普通灯 lightId 必须全局唯一：同一区域可能跨多个场景（如 corridor_1f
                // 覆盖 FrontHall 与 Floor_1F），撞 ID 会导致跨场景串灯。
                var lightId = $"light_{plan.RegionId}_{plan.SceneName.ToLower()}_n{index + 1:00}";
                var t = plan.NormalLampCount == 1 ? 0.5f : 0.2f + 0.6f * index / (plan.NormalLampCount - 1f);
                var x = Mathf.Lerp(bounds.min.x + 3f, bounds.max.x - 6f, t);
                var position = new Vector3(x, bounds.max.y - 1.2f, 0f);
                CreateFixture(lightId, plan.RegionId, false, position, 3f);
                configs.Add(GetOrCreateLightConfig(lightId, plan.RegionId, false, 3f));
            }

            return configs;
        }

        /// <summary>
        /// 场景内可能存在多个同名 CameraConfiner（走廊分段切换用），GameObject.Find
        /// 只会命中层级里第一个，导致"整层中部"落在某一段的中部——这里取全部
        /// CameraConfiner 世界边界的并集作为整层走廊范围。
        /// </summary>
        private static Bounds ResolveSceneBounds()
        {
            var union = new Bounds();
            var found = false;
            foreach (var rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var transform in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (!string.Equals(transform.name, "CameraConfiner", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var collider = transform.GetComponent<Collider2D>();
                    if (collider == null || collider.bounds.size == Vector3.zero)
                    {
                        continue;
                    }

                    if (!found)
                    {
                        union = collider.bounds;
                        found = true;
                    }
                    else
                    {
                        union.Encapsulate(collider.bounds);
                    }
                }
            }

            if (found)
            {
                return union;
            }

            return new Bounds(Vector3.zero, new Vector3(16f, 8f, 1f));
        }

        private static void CreateFixture(string lightId, string regionId, bool isSpecial, Vector3 position, float radius)
        {
            var fixture = GameObject.Find(lightId);
            if (fixture == null)
            {
                fixture = new GameObject(lightId);
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(
                    fixture, SceneManager.GetActiveScene());
                fixture.transform.position = position;
            }

            var spriteRenderer = fixture.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = fixture.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = LoadPlaceholderSprite();
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.color = isSpecial ? new Color(0.9f, 0.75f, 0.35f) : new Color(0.55f, 0.55f, 0.55f);
            fixture.transform.localScale = isSpecial ? new Vector3(1.4f, 0.9f, 1f) : new Vector3(1f, 0.7f, 1f);

            var collider = fixture.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = fixture.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            collider.size = new Vector2(1.6f, 12f);
            collider.offset = new Vector2(0f, -2f);

            var lightView = fixture.GetComponent<LightSourceView>();
            if (lightView == null)
            {
                lightView = fixture.AddComponent<LightSourceView>();
            }

            SetSerializedString(lightView, "lightId", lightId);

            var interactionPoint = fixture.GetComponent<InteractionPointView>();
            if (interactionPoint == null)
            {
                interactionPoint = fixture.AddComponent<InteractionPointView>();
            }

            var interactionSerialized = new SerializedObject(interactionPoint);
            interactionSerialized.FindProperty("interactionId").stringValue = lightId;
            interactionSerialized.FindProperty("interactionType").intValue = (int)InteractionType.LightSource;
            interactionSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LightSourceConfig GetOrCreateLightConfig(string lightId, string regionId, bool isSpecial, float radius)
        {
            var path = $"{LightSourceConfigFolder}/{lightId}.asset";
            var config = AssetDatabase.LoadAssetAtPath<LightSourceConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LightSourceConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            var configSerialized = new SerializedObject(config);
            configSerialized.FindProperty("lightId").stringValue = lightId;
            configSerialized.FindProperty("regionId").stringValue = regionId;
            configSerialized.FindProperty("isSpecial").boolValue = isSpecial;
            configSerialized.FindProperty("radius").floatValue = radius;
            configSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void RegisterConfigsInDatabase(LightingGlobalConfig globalConfig, List<LightSourceConfig> configs)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(DatabasePath);
            if (database == null)
            {
                throw new InvalidOperationException($"找不到 GameConfigDatabase：{DatabasePath}");
            }

            // 以目录中的全部 LightSourceConfig 为准，保证重复执行后数组完整。
            var guids = AssetDatabase.FindAssets("t:LightSourceConfig", new[] { LightSourceConfigFolder });
            var allConfigs = new List<LightSourceConfig>();
            foreach (var guid in guids)
            {
                var config = AssetDatabase.LoadAssetAtPath<LightSourceConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null)
                {
                    allConfigs.Add(config);
                }
            }

            var databaseSerialized = new SerializedObject(database);
            databaseSerialized.FindProperty("lightingGlobal").objectReferenceValue = globalConfig;

            var lightSourcesProperty = databaseSerialized.FindProperty("lightSources");
            lightSourcesProperty.arraySize = allConfigs.Count;
            for (var index = 0; index < allConfigs.Count; index++)
            {
                lightSourcesProperty.GetArrayElementAtIndex(index).objectReferenceValue = allConfigs[index];
            }

            databaseSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void ClearLightSourceConfigs()
        {
            var guids = AssetDatabase.FindAssets("t:LightSourceConfig", new[] { LightSourceConfigFolder });
            foreach (var guid in guids)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static Sprite LoadPlaceholderSprite()
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Square.png");
            if (sprite != null)
            {
                return sprite;
            }

            return AssetDatabase.GetBuiltinExtraResource<Sprite>("4-Sprite.png");
        }

        private static void SetSerializedString(Component component, string propertyName, string value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
