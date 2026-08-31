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
        /// 叠层对象接入区域视图、在每个光晕中心放置灯具与黑暗层，并注册
        /// 到 GameConfigDatabase。可重复执行（幂等）。
        /// 走廊特殊灯具的横向位置按策划《灯具分布图》配置；房间特殊灯具与
        /// 普通灯位置直接从美术光晕贴图的透明度轮廓中识别。
        /// </summary>
    public static class Stage4LightingBootstrap
    {
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";
        private const string LightingConfigFolder = "Assets/GameConfigs/Lighting";
        private const string LightSourceConfigFolder = "Assets/GameConfigs/Lighting/LightSources";
        private const string LightingPrefabFolder = "Assets/Prefabs/Lighting";
        private const string LightFixturePrefabPath = LightingPrefabFolder + "/LightFixture.prefab";
        private const string LightingPlaceholderFolder = "Assets/Art/Placeholders";
        private const string LightFixturePlaceholderPath = LightingPlaceholderFolder + "/LightFixturePlaceholder.png";
        private const string DarknessMaterialPath = "Assets/GameConfigs/Lighting/DarknessOverlayMaterial.mat";
        private const string GlobalConfigPath = "Assets/GameConfigs/Lighting/LightingGlobalConfig.asset";
        private const string DarknessShaderName = "Memorial Archive/Lighting/Darkness Overlay";

        private struct ScenePlan
        {
            public string SceneName;
            public string RegionId;
            public bool PlaceSpecial;
            /// <summary>特殊灯具在场景横向范围（避开两侧边界后的区间）内的位置，0=最左 1=最右。</summary>
            public float SpecialXFraction;
        }

        private static readonly ScenePlan[] ScenePlans =
        {
            // 前厅并入 1F 走廊区域（corridor_1f），该区域的特殊灯具放在前厅。
            // 走廊特殊灯具的横向位置来自策划《灯具分布图》（4 个紫色菱形）：
            // 1F 前厅展览馆正中、2F 走廊中部、3F 走廊右端（禁闭室侧）、4F 走廊左端（露台侧）。
            new ScenePlan { SceneName = "FrontHall", RegionId = "corridor_1f", PlaceSpecial = true, SpecialXFraction = 0.45f },
            new ScenePlan { SceneName = "Floor_1F", RegionId = "corridor_1f", PlaceSpecial = false },
            new ScenePlan { SceneName = "Floor_2F", RegionId = "corridor_2f", PlaceSpecial = true, SpecialXFraction = 0.5f },
            new ScenePlan { SceneName = "Floor_3F", RegionId = "corridor_3f", PlaceSpecial = true, SpecialXFraction = 0.9f },
            new ScenePlan { SceneName = "Floor_4F", RegionId = "corridor_4f", PlaceSpecial = true, SpecialXFraction = 0.12f },
            new ScenePlan { SceneName = "Room_Toilet", RegionId = "room_toilet", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_Office", RegionId = "room_office", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_ArchiveA", RegionId = "room_archive_a", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_ArchiveB", RegionId = "room_archive_b", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_ArchiveC", RegionId = "room_archive_c", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_TreatmentA", RegionId = "room_treatment_a", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_TreatmentB", RegionId = "room_treatment_b", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_Reception", RegionId = "room_reception", PlaceSpecial = true, SpecialXFraction = 1f },
            new ScenePlan { SceneName = "Room_Director", RegionId = "room_director", PlaceSpecial = true, SpecialXFraction = 1f }
        };

        [MenuItem("Tools/Memorial Archive/Build Stage 4 Lighting")]
        public static void BuildStage4Lighting()
        {
            EnsureFolder(LightingConfigFolder);
            EnsureFolder(LightSourceConfigFolder);
            EnsureFolder(LightingPrefabFolder);

            var globalConfig = BuildGlobalConfig();
            BuildLightFixturePrefab();
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

        [MenuItem("Tools/Memorial Archive/Build Light Fixture Prefab")]
        public static void BuildLightFixturePrefabAsset()
        {
            EnsureFolder(LightingPrefabFolder);
            var prefab = BuildLightFixturePrefab();
            var migratedCount = MigrateGeneratedFixturesToPrefab(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log($"Light fixture prefab built: {LightFixturePrefabPath}; migrated scene fixtures: {migratedCount}.");
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

            var glowCenters = FindGlowCenters();
            glowCenters.Sort((left, right) =>
            {
                var xComparison = left.x.CompareTo(right.x);
                return xComparison != 0 ? xComparison : right.y.CompareTo(left.y);
            });

            for (var index = 0; index < glowCenters.Count; index++)
            {
                var lightId = $"light_{plan.RegionId}_{plan.SceneName.ToLower()}_n{index + 1:00}";
                CreateFixture(lightId, plan.RegionId, false, glowCenters[index], 6f);
                configs.Add(GetOrCreateLightConfig(lightId, plan.RegionId, false, 6f));
            }

            Debug.Log($"[Stage4Lighting] {plan.SceneName}: 从光晕贴图识别并放置 {glowCenters.Count} 盏普通灯。");

            return configs;
        }

        private static List<Vector3> FindGlowCenters()
        {
            var centers = new List<Vector3>();
            foreach (var rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootObject.GetComponent<LightRegionView>() == null)
                {
                    continue;
                }

                foreach (var renderer in rootObject.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    centers.AddRange(ExtractGlowCenters(renderer));
                }
            }

            return centers;
        }

        /// <summary>
        /// 对光晕透明度轮廓做近似距离变换，再以非极大值抑制取每个圆形光晕的中心。
        /// 即便相邻光晕互相搭接，也能保留各自的局部最大内切圆中心。
        /// </summary>
        private static List<Vector3> ExtractGlowCenters(SpriteRenderer renderer)
        {
            var results = new List<Vector3>();
            var sprite = renderer.sprite;
            if (sprite == null)
            {
                return results;
            }

            var assetPath = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                return results;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(assetPath), false))
                {
                    return results;
                }

                var width = texture.width;
                var height = texture.height;
                var pixels = texture.GetPixels32();
                var maxAlpha = 0;
                for (var index = 0; index < pixels.Length; index++)
                {
                    maxAlpha = Mathf.Max(maxAlpha, pixels[index].a);
                }

                var alphaThreshold = Mathf.Max(8, Mathf.RoundToInt(maxAlpha * 0.05f));
                var paddedWidth = width + 2;
                var paddedHeight = height + 2;
                var distance = new float[paddedWidth * paddedHeight];
                const float infinity = 100000f;
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        distance[(y + 1) * paddedWidth + x + 1] =
                            pixels[y * width + x].a >= alphaThreshold ? infinity : 0f;
                    }
                }

                const float diagonal = 1.41421356f;
                for (var y = 1; y < paddedHeight - 1; y++)
                {
                    for (var x = 1; x < paddedWidth - 1; x++)
                    {
                        var index = y * paddedWidth + x;
                        if (distance[index] <= 0f)
                        {
                            continue;
                        }

                        distance[index] = Mathf.Min(distance[index], distance[index - 1] + 1f);
                        distance[index] = Mathf.Min(distance[index], distance[index - paddedWidth] + 1f);
                        distance[index] = Mathf.Min(distance[index], distance[index - paddedWidth - 1] + diagonal);
                        distance[index] = Mathf.Min(distance[index], distance[index - paddedWidth + 1] + diagonal);
                    }
                }

                for (var y = paddedHeight - 2; y >= 1; y--)
                {
                    for (var x = paddedWidth - 2; x >= 1; x--)
                    {
                        var index = y * paddedWidth + x;
                        if (distance[index] <= 0f)
                        {
                            continue;
                        }

                        distance[index] = Mathf.Min(distance[index], distance[index + 1] + 1f);
                        distance[index] = Mathf.Min(distance[index], distance[index + paddedWidth] + 1f);
                        distance[index] = Mathf.Min(distance[index], distance[index + paddedWidth + 1] + diagonal);
                        distance[index] = Mathf.Min(distance[index], distance[index + paddedWidth - 1] + diagonal);
                    }
                }

                var localMaximum = MaximumFilter(distance, paddedWidth, paddedHeight, 50);
                var peaks = new List<Vector3>();
                for (var y = 1; y < paddedHeight - 1; y++)
                {
                    for (var x = 1; x < paddedWidth - 1; x++)
                    {
                        var index = y * paddedWidth + x;
                        var value = distance[index];
                        if (value < 28f || value < localMaximum[index] - 0.001f)
                        {
                            continue;
                        }

                        peaks.Add(new Vector3(x, y, value));
                    }
                }

                peaks.Sort((left, right) => right.z.CompareTo(left.z));
                var acceptedPeaks = new List<Vector3>();
                foreach (var peak in peaks)
                {
                    var overlapsExistingPeak = false;
                    foreach (var accepted in acceptedPeaks)
                    {
                        var minimumSeparation = Mathf.Max(55f, accepted.z * 1.15f);
                        var deltaX = peak.x - accepted.x;
                        var deltaY = peak.y - accepted.y;
                        if (deltaX * deltaX + deltaY * deltaY < minimumSeparation * minimumSeparation)
                        {
                            overlapsExistingPeak = true;
                            break;
                        }
                    }

                    if (overlapsExistingPeak)
                    {
                        continue;
                    }

                    acceptedPeaks.Add(peak);
                    var pixelX = peak.x - 1f;
                    var pixelY = peak.y - 1f;
                    if (renderer.flipX)
                    {
                        pixelX = width - 1f - pixelX;
                    }

                    if (renderer.flipY)
                    {
                        pixelY = height - 1f - pixelY;
                    }

                    var local = new Vector3(
                        (pixelX - sprite.pivot.x) / sprite.pixelsPerUnit,
                        (pixelY - sprite.pivot.y) / sprite.pixelsPerUnit,
                        0f);
                    var world = renderer.transform.TransformPoint(local);
                    world.z = 0f;
                    results.Add(world);

                    if (acceptedPeaks.Count >= 32)
                    {
                        break;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return results;
        }

        /// <summary>
        /// O(width*height) 的方形最大值过滤。先按行、再按列做分块前缀/后缀最大值，
        /// 用于排除同一光晕斜坡上的次级峰值，窗口半径 50px 与美术图的最小光晕间距匹配。
        /// </summary>
        private static float[] MaximumFilter(float[] source, int width, int height, int radius)
        {
            var horizontal = new float[source.Length];
            var result = new float[source.Length];
            var maxLength = Mathf.Max(width, height);
            var line = new float[maxLength];
            var filteredLine = new float[maxLength];

            for (var y = 0; y < height; y++)
            {
                Array.Copy(source, y * width, line, 0, width);
                MaximumFilterLine(line, filteredLine, width, radius);
                Array.Copy(filteredLine, 0, horizontal, y * width, width);
            }

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    line[y] = horizontal[y * width + x];
                }

                MaximumFilterLine(line, filteredLine, height, radius);
                for (var y = 0; y < height; y++)
                {
                    result[y * width + x] = filteredLine[y];
                }
            }

            return result;
        }

        private static void MaximumFilterLine(float[] source, float[] destination, int length, int radius)
        {
            var blockSize = radius * 2 + 1;
            var prefix = new float[length];
            var suffix = new float[length];

            for (var index = 0; index < length; index++)
            {
                prefix[index] = index % blockSize == 0
                    ? source[index]
                    : Mathf.Max(prefix[index - 1], source[index]);
            }

            for (var index = length - 1; index >= 0; index--)
            {
                suffix[index] = index == length - 1 || (index + 1) % blockSize == 0
                    ? source[index]
                    : Mathf.Max(suffix[index + 1], source[index]);
            }

            for (var index = 0; index < length; index++)
            {
                var start = Mathf.Max(0, index - radius);
                var end = Mathf.Min(length - 1, index + radius);
                destination[index] = Mathf.Max(suffix[start], prefix[end]);
            }
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
                var fixturePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightFixturePrefabPath) ??
                                    BuildLightFixturePrefab();
                fixture = (GameObject)PrefabUtility.InstantiatePrefab(
                    fixturePrefab,
                    SceneManager.GetActiveScene());
                fixture.name = lightId;
                fixture.transform.position = position;
            }

            ConfigureFixture(fixture, lightId, isSpecial);
        }

        private static void ConfigureFixture(GameObject fixture, string lightId, bool isSpecial)
        {
            var spriteRenderer = fixture.GetComponent<SpriteRenderer>();
            if (isSpecial)
            {
                if (spriteRenderer == null)
                {
                    spriteRenderer = fixture.AddComponent<SpriteRenderer>();
                }

                spriteRenderer.sprite = LoadPlaceholderSprite();
                spriteRenderer.sortingOrder = 10;
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.color = new Color(0.9f, 0.75f, 0.35f);
            }
            else if (spriteRenderer != null)
            {
                // 普通灯只保留交互与光源逻辑，画面由场景原有灯具和光晕贴图提供。
                UnityEngine.Object.DestroyImmediate(spriteRenderer);
            }

            fixture.transform.localScale = isSpecial ? new Vector3(1.4f, 0.9f, 1f) : Vector3.one;

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

        private static int MigrateGeneratedFixturesToPrefab(GameObject prefab)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("存在未保存的场景修改，请先保存后再迁移灯具预制体。");
                }
            }

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var migratedCount = 0;
            try
            {
                var visitedScenes = new HashSet<string>();
                foreach (var plan in ScenePlans)
                {
                    if (!visitedScenes.Add(plan.SceneName))
                    {
                        continue;
                    }

                    var scenePath = $"Assets/Scenes/{plan.SceneName}.unity";
                    if (!File.Exists(scenePath))
                    {
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var fixtures = new List<LightSourceView>();
                    foreach (var rootObject in scene.GetRootGameObjects())
                    {
                        fixtures.AddRange(rootObject.GetComponentsInChildren<LightSourceView>(true));
                    }

                    var sceneChanged = false;
                    foreach (var lightView in fixtures)
                    {
                        var fixture = lightView.gameObject;
                        if (PrefabUtility.IsPartOfPrefabInstance(fixture) || !IsGeneratedPlaceholderFixture(fixture))
                        {
                            continue;
                        }

                        var interactionPoint = fixture.GetComponent<InteractionPointView>();
                        var lightId = interactionPoint != null && !string.IsNullOrEmpty(interactionPoint.InteractionId)
                            ? interactionPoint.InteractionId
                            : fixture.name;
                        var lightConfig = AssetDatabase.LoadAssetAtPath<LightSourceConfig>(
                            $"{LightSourceConfigFolder}/{lightId}.asset");
                        var isSpecial = lightConfig != null
                            ? lightConfig.IsSpecial
                            : fixture.transform.localScale.x > 1.1f;

                        var parent = fixture.transform.parent;
                        var siblingIndex = fixture.transform.GetSiblingIndex();
                        var localPosition = fixture.transform.localPosition;
                        var localRotation = fixture.transform.localRotation;
                        var localScale = fixture.transform.localScale;
                        var activeSelf = fixture.activeSelf;

                        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        replacement.name = fixture.name;
                        replacement.transform.SetParent(parent, false);
                        replacement.transform.SetSiblingIndex(siblingIndex);
                        replacement.transform.localPosition = localPosition;
                        replacement.transform.localRotation = localRotation;
                        replacement.transform.localScale = localScale;
                        replacement.SetActive(activeSelf);
                        ConfigureFixture(replacement, lightId, isSpecial);

                        UnityEngine.Object.DestroyImmediate(fixture);
                        migratedCount++;
                        sceneChanged = true;
                    }

                    if (sceneChanged)
                    {
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            return migratedCount;
        }

        private static bool IsGeneratedPlaceholderFixture(GameObject fixture)
        {
            var spriteRenderer = fixture.GetComponent<SpriteRenderer>();
            return fixture.transform.childCount == 0 &&
                   (spriteRenderer == null || spriteRenderer.sprite == LoadPlaceholderSprite()) &&
                   fixture.GetComponent<BoxCollider2D>() != null &&
                   fixture.GetComponent<InteractionPointView>() != null;
        }

        private static GameObject BuildLightFixturePrefab()
        {
            EnsureFolder(LightingPrefabFolder);
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightFixturePrefabPath);
            var editingExisting = existingPrefab != null;
            var root = editingExisting
                ? PrefabUtility.LoadPrefabContents(LightFixturePrefabPath)
                : new GameObject("LightFixture");

            try
            {
                root.name = "LightFixture";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                var spriteRenderer = root.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    UnityEngine.Object.DestroyImmediate(spriteRenderer);
                }

                var collider = root.GetComponent<BoxCollider2D>();
                if (collider == null)
                {
                    collider = root.AddComponent<BoxCollider2D>();
                }

                collider.isTrigger = true;
                collider.size = new Vector2(1.6f, 12f);
                collider.offset = new Vector2(0f, -2f);

                var lightView = root.GetComponent<LightSourceView>();
                if (lightView == null)
                {
                    lightView = root.AddComponent<LightSourceView>();
                }

                SetSerializedString(lightView, "lightId", string.Empty);

                var interactionPoint = root.GetComponent<InteractionPointView>();
                if (interactionPoint == null)
                {
                    interactionPoint = root.AddComponent<InteractionPointView>();
                }

                var interactionSerialized = new SerializedObject(interactionPoint);
                interactionSerialized.FindProperty("interactionId").stringValue = string.Empty;
                interactionSerialized.FindProperty("interactionType").intValue = (int)InteractionType.LightSource;
                interactionSerialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, LightFixturePrefabPath);
            }
            finally
            {
                if (editingExisting)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
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
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LightFixturePlaceholderPath);
            if (sprite != null)
            {
                return sprite;
            }

            EnsureFolder(LightingPlaceholderFolder);

            const int size = 32;
            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var solid = new Color32(255, 255, 255, 255);
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = clear;
            }

            // 纯白轮廓由 SpriteRenderer 着色：上方吊线、梯形灯罩和下方灯泡。
            FillRect(pixels, size, 15, 25, 2, 6, solid);
            for (var y = 12; y <= 24; y++)
            {
                var halfWidth = 3 + (24 - y) / 2;
                FillRect(pixels, size, 16 - halfWidth, y, halfWidth * 2 + 1, 1, solid);
            }

            FillRect(pixels, size, 13, 9, 7, 3, solid);
            FillRect(pixels, size, 11, 7, 11, 2, solid);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.name = "LightFixturePlaceholder";
                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                var fullPath = Path.GetFullPath(LightFixturePlaceholderPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(LightFixturePlaceholderPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(LightFixturePlaceholderPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"无法导入灯具占位贴图：{LightFixturePlaceholderPath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LightFixturePlaceholderPath);
            if (sprite == null)
            {
                throw new InvalidOperationException($"灯具占位贴图未生成 Sprite：{LightFixturePlaceholderPath}");
            }

            return sprite;
        }

        private static void FillRect(Color32[] pixels, int textureWidth, int x, int y, int width, int height, Color32 color)
        {
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    pixels[(y + row) * textureWidth + x + column] = color;
                }
            }
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
