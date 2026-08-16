#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Guide.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// 第三阶段引导系统的一次性生成器：创建「操作引导」序列及其 8 个步骤配置，
    /// 注册到 GameConfigDatabase.guideSequences，并生成 GuideOverlayPanel 预制体。
    /// 正常开发不要重复运行；资产由本工具产出后由 C 在场景中接入。
    /// 菜单：Tools/Memorial Archive/Build Stage 3 Guide。
    /// </summary>
    public static class Stage3GuideBootstrap
    {
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";
        private const string GuideConfigFolder = "Assets/GameConfigs/Guide";
        private const string OperationSequencePath = "Assets/GameConfigs/Guide/操作引导.asset";
        private const string OverlayPrefabPath = "Assets/Prefabs/UI/GuideOverlayPanel.prefab";

        private const string FrontHallScene = "FrontHall";
        private const string MoveHintGroup = "entry_hints";
        private const int LanternItemId = 1006;

        [MenuItem("Tools/Memorial Archive/Build Stage 3 Guide")]
        public static void BuildStage3Guide()
        {
            EnsureFolder(GuideConfigFolder);
            EnsureFolder("Assets/Prefabs/UI");

            // 操作引导 8 步（对照需求文档第 2.1 页面详情 + 2.2 系统流程）。
            var moveHint = BuildStep("op_move_hint", "移动", "按下 A / D 进行移动", 1f, 3f,
                10, MoveHintGroup, GuideCompleteCondition.PlayerMoved, 0, true);
            var inventoryHint = BuildStep("op_inventory_hint", "背包", "背包中储存着你的物品，点击或按下 TAB 键打开", 1f, 3f,
                11, MoveHintGroup, GuideCompleteCondition.InventoryOpened, 0, true);

            // 背包打开后引导装备手提灯；条件=道具已装备，不绑定快捷栏/副手槽位。
            var lanternEquip = BuildStep("op_lantern_equip", "装备手提灯", "将手提灯装备以照亮黑暗", 0.5f, 0f,
                20, string.Empty, GuideCompleteCondition.ItemEquipped, LanternItemId, true);

            // 奔跑提示：3 秒自动隐藏。
            var runHint = BuildStep("op_run_hint", "奔跑", "长按 Shift 可以进行奔跑", 1f, 3f,
                30, string.Empty, GuideCompleteCondition.None, 0, false);

            // 体力提示：玩家触发奔跑后弹出，3 秒隐藏。
            var staminaHint = BuildStep("op_stamina_hint", "体力", "运动时消耗体力，请适当休息，避免体力消耗殆尽", 0f, 3f,
                40, string.Empty, GuideCompleteCondition.PlayerRunning, 0, false);

            var sequence = BuildSequence(OperationSequencePath, "operation", FrontHallScene,
                moveHint, inventoryHint, lanternEquip, runHint, staminaHint);
            RegisterSequence(sequence);
            BuildOverlayPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Stage 3 guide sequence, step configs and overlay prefab built successfully.");
        }

        private static GuideStepConfig BuildStep(
            string stepId, string title, string description,
            float displayDelay, float autoHideDelay,
            int sortOrder, string parallelGroupId,
            GuideCompleteCondition condition, int conditionArg, bool required)
        {
            var path = $"{GuideConfigFolder}/{stepId}.asset";
            var config = AssetDatabase.LoadAssetAtPath<GuideStepConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GuideStepConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            var serialized = new SerializedObject(config);
            Set(serialized, "stepId", stepId);
            Set(serialized, "title", title);
            Set(serialized, "description", description);
            Set(serialized, "sprite", (Sprite)null);
            Set(serialized, "parallelGroupId", parallelGroupId);
            Set(serialized, "displayDelay", displayDelay);
            Set(serialized, "autoHideDelay", autoHideDelay);
            Set(serialized, "sortOrder", sortOrder);
            Set(serialized, "required", required);
            Set(serialized, "completeCondition", (int)condition);
            Set(serialized, "completeConditionArg", conditionArg);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static GuideSequenceConfig BuildSequence(string path, string sequenceId, string triggerScene, params GuideStepConfig[] steps)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<GuideSequenceConfig>(path);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<GuideSequenceConfig>();
                AssetDatabase.CreateAsset(sequence, path);
            }

            var serialized = new SerializedObject(sequence);
            Set(serialized, "sequenceId", sequenceId);
            Set(serialized, "triggerSceneId", triggerScene);
            var stepsProperty = serialized.FindProperty("steps");
            stepsProperty.arraySize = steps.Length;
            for (var i = 0; i < steps.Length; i++)
            {
                stepsProperty.GetArrayElementAtIndex(i).objectReferenceValue = steps[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static void RegisterSequence(GuideSequenceConfig sequence)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(DatabasePath);
            if (database == null)
            {
                throw new InvalidOperationException("GameConfigDatabase is missing.");
            }

            var configs = new List<GuideSequenceConfig>();
            foreach (var existing in database.GuideSequences)
            {
                if (existing != null && existing.SequenceId != sequence.SequenceId)
                {
                    configs.Add(existing);
                }
            }

            configs.Add(sequence);
            // 先标记 dirty 并保存一次，确保数据库资产已序列化出 guideSequences 字段（旧资产可能缺该字段）。
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            var serialized = new SerializedObject(database);
            var array = serialized.FindProperty("guideSequences");
            if (array == null)
            {
                throw new InvalidOperationException(
                    "GameConfigDatabase.asset 缺少 guideSequences 序列化字段，请重新打开 Unity 让其重新序列化后重试。");
            }

            array.arraySize = configs.Count;
            for (var index = 0; index < configs.Count; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = configs[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void BuildOverlayPrefab()
        {
            var root = new GameObject("GuideOverlayPanel");
            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            root.AddComponent<RectTransform>();
            root.AddComponent<VerticalLayoutGroup>();

            // 引导提示条目模板：Title / Description / Icon，默认隐藏，由 Panel 在运行时实例化。
            var entry = new GameObject("EntryPrefab");
            entry.transform.SetParent(root.transform, false);
            var entryLayout = entry.AddComponent<VerticalLayoutGroup>();
            entryLayout.childAlignment = TextAnchor.UpperLeft;

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(entry.transform, false);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "引导标题";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var descObj = new GameObject("Description");
            descObj.transform.SetParent(entry.transform, false);
            var descText = descObj.AddComponent<Text>();
            descText.text = "引导描述";
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(entry.transform, false);
            iconObj.AddComponent<Image>();

            var panel = root.AddComponent<GuideOverlayPanel>();
            var serialized = new SerializedObject(panel);
            Set(serialized, "panelId", (int)PanelId.GuideOverlay);
            Set(serialized, "pausesGame", false);
            Set(serialized, "canvasGroup", canvasGroup);
            Set(serialized, "startClosed", true);
            Set(serialized, "entryPrefab", entry);
            Set(serialized, "entryContainer", (RectTransform)root.transform);
            Set(serialized, "maxVisibleEntries", 4);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            entry.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, OverlayPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void Set(SerializedObject serialized, string name, int value)
        {
            serialized.FindProperty(name).intValue = value;
        }

        private static void Set(SerializedObject serialized, string name, float value)
        {
            serialized.FindProperty(name).floatValue = value;
        }

        private static void Set(SerializedObject serialized, string name, bool value)
        {
            serialized.FindProperty(name).boolValue = value;
        }

        private static void Set(SerializedObject serialized, string name, string value)
        {
            serialized.FindProperty(name).stringValue = value ?? string.Empty;
        }

        private static void Set(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
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
