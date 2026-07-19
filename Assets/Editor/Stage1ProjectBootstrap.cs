#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Camera.View;
using MemorialArchive.Gameplay.Character.Config;
using MemorialArchive.Gameplay.Character.View;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Inventory.View;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Stage1;
using MemorialArchive.Gameplay.Story.Config;
using MemorialArchive.Gameplay.Story.View;
using Spine.Unity;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MemorialArchive.Editor
{
    public static class Stage1ProjectBootstrap
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";
        private const string PrefabRoot = "Assets/Prefabs";

        private static Font defaultFont;

        [MenuItem("Tools/Memorial Archive/Build Stage 1 Baseline")]
        public static void BuildStage1Baseline()
        {
            CleanupFailedTemporaryObjects();
            EnsureFolders();
            var database = BuildConfigs();
            var prefabs = BuildPrefabs();
            BuildMainMenuScene(database, prefabs);
            BuildSampleScene(database, prefabs);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Stage 1 baseline built. Open MainMenu.unity and run the acceptance route.");
        }

        public static void BuildStage1BaselineBatch()
        {
            try
            {
                BuildStage1Baseline();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         "Assets/GameConfigs/Character", "Assets/GameConfigs/Interaction", "Assets/GameConfigs/Items",
                         "Assets/GameConfigs/Story", "Assets/Prefabs/System", "Assets/Prefabs/UI",
                         "Assets/Prefabs/Character", "Assets/Prefabs/Camera", "Assets/Prefabs/Interaction"
                     })
            {
                EnsureFolder(folder);
            }
        }

        private static GameConfigDatabase BuildConfigs()
        {
            var player = GetOrCreate<CharacterAttributeConfig>("Assets/GameConfigs/Character/Stage1Player.asset");
            Set(player, "attributeId", Stage1Ids.PlayerAttributeId);
            Set(player, "displayName", "Stage 1 Player");
            Set(player, "maxHealth", 3);
            Set(player, "maxStamina", 30);
            Set(player, "walkSpeed", 100f);
            Set(player, "runSpeed", 300f);
            Set(player, "runStaminaCostPerSecond", 2f);
            Set(player, "staminaRecoveryPerSecond", 1f);
            Set(player, "dodgeStaminaCost", 6f);
            Set(player, "dodgeDistance", 120f);
            Set(player, "dodgeDurationSeconds", 0.18f);

            var normalState = GetOrCreate<CharacterStateConfig>("Assets/GameConfigs/Character/Stage1NormalState.asset");
            Set(normalState, "stateId", Stage1Ids.PlayerNormalStateId);
            Set(normalState, "stateType", (int)CharacterStateType.Normal);
            Set(normalState, "displayName", "Normal");

            var story = GetOrCreate<StoryEntryConfig>("Assets/GameConfigs/Story/Stage1OpeningStory.asset");
            Set(story, "storyId", Stage1Ids.OpeningStory);
            Set(story, "presentationType", (int)StoryPresentationType.BlackScreen);
            Set(story, "returnToMainMenuWhenFinished", false);
            Set(story, "content", OpeningStoryText);

            var items = new[]
            {
                CreateItem(1002, "制式刺刀", ItemCategory.Weapon, InventoryFootprint.OneByOne, 1, true, false, OffhandType.None, false, 0, ""),
                CreateItem(1003, "消防斧", ItemCategory.Weapon, InventoryFootprint.OneByTwoHorizontal, 1, true, false, OffhandType.None, false, 0, ""),
                CreateItem(1012, "瓶装气泡水", ItemCategory.Consumable, InventoryFootprint.OneByOne, 1, true, false, OffhandType.None, false, 0, "restore_full_stamina", true),
                CreateItem(1020, "手枪子弹", ItemCategory.Ammo, InventoryFootprint.OneByOne, 30, true, false, OffhandType.None, true, 1007, "", true),
                CreateItem(1006, "手提灯", ItemCategory.Offhand, InventoryFootprint.OneByOne, 1, false, true, OffhandType.Lantern, false, 0, "lantern_light"),
                CreateItem(1024, "办公室钥匙", ItemCategory.Key, InventoryFootprint.OneByOne, 1, false, false, OffhandType.None, false, 0, "office_key")
            };

            var interactions = new[]
            {
                CreateInteraction(Stage1Ids.ContainerPoint01, InteractionType.Container, Stage1Ids.DemoContainer01),
                CreateInteraction(Stage1Ids.ContainerPoint02, InteractionType.Container, Stage1Ids.DemoContainer02),
                CreateInteraction(Stage1Ids.SavePoint01, InteractionType.SavePoint, null),
                CreateInteraction(Stage1Ids.InspectPoint01, InteractionType.Inspect, null),
                CreateInteraction(Stage1Ids.EntrancePoint01, InteractionType.SceneExit, null)
            };

            var database = GetOrCreate<GameConfigDatabase>(DatabasePath);
            SetArray(database, "items", items);
            SetArray(database, "characterAttributes", new[] { player });
            SetArray(database, "characterStates", new[] { normalState });
            SetArray(database, "interactions", interactions);
            SetArray(database, "stories", new[] { story });
            return database;
        }

        private static Dictionary<string, GameObject> BuildPrefabs()
        {
            var prefabs = new Dictionary<string, GameObject>();
            prefabs["GameRoot"] = BuildGameRootPrefab();
            prefabs["Player"] = BuildPlayerPrefab();
            prefabs["MainCamera"] = BuildCameraPrefab();
            prefabs["MainMenu"] = BuildMainMenuPanel();
            prefabs["Confirm"] = BuildConfirmPanel();
            prefabs["Story"] = BuildStoryPanel();
            prefabs["Hud"] = BuildHudPanel();
            prefabs["System"] = BuildSimplePanel<SystemPanel>("SystemPanel", PanelId.System, true, "系统", true);
            prefabs["Settings"] = BuildSimplePanel<SettingsPanel>("SettingsPanel", PanelId.Settings, true, "设置（第一阶段：分辨率占位）", true);
            prefabs["Diary"] = BuildSimplePanel<DiaryPanel>("DiaryPanel", PanelId.Diary, false, "日记 / 纸条（占位）", true);
            prefabs["Map"] = BuildSimplePanel<MapPanel>("MapPanel", PanelId.Map, false, "地图（占位）", true);
            prefabs["Inventory"] = BuildInventoryPanel();
            prefabs["Container"] = BuildSimplePanel<ContainerPanel>("ContainerPanel", PanelId.Container, false, "场景容器 2x2", false);
            prefabs["Shortcut"] = BuildSimplePanel<ShortcutBarPanel>("ShortcutBarPanel", PanelId.ShortcutBar, false, "快捷栏 1 / 2 / 3", false);
            prefabs["Save"] = BuildSavePanel();
            prefabs["Load"] = BuildSimplePanel<LoadPanel>("LoadPanel", PanelId.Load, true, "读取存档（第一阶段占位）", true);
            BuildInventorySlotPrefab();
            BuildInteractionPrefabs();
            NormalizePanelPrefabVisibility(prefabs);
            NormalizeSharedUiOwnership(prefabs);
            return prefabs;
        }

        private static GameObject BuildGameRootPrefab()
        {
            const string path = PrefabRoot + "/System/GameRoot.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("GameRoot");
            var ui = root.AddComponent<UIManager>();
            var gameRoot = root.AddComponent<GameRoot>();
            Set(gameRoot, "configDatabase", AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(DatabasePath));
            Set(gameRoot, "uiManager", ui);
            Set(gameRoot, "dontDestroyOnLoad", true);
            return SavePrefab(root, path);
        }

        private static GameObject BuildPlayerPrefab()
        {
            const string path = PrefabRoot + "/Character/Player.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Player");
            root.tag = "Player";
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.color = new Color(0.25f, 0.75f, 0.95f, 1f);
            root.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            root.AddComponent<CapsuleCollider2D>();
            root.AddComponent<GameplayInputReader>();
            root.AddComponent<PlayerMotor>();
            AttachPlayerSpine(root);
            return SavePrefab(root, path);
        }

        // 给 Player 装配 Spine 角色（PlayerVisual 子 GO + SkeletonAnimation + CharacterAnimationView）。
        // 美术资产按动作拆成独立 SkeletonDataAsset，CharacterAnimationView 在运行时切换。
        private static void AttachPlayerSpine(GameObject root)
        {
            const string actionRoot = "Assets/Actions/Player01";
            var idleData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(actionRoot + "/Idle/player_02_Idle_split_SkeletonData.asset");
            var walkData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(actionRoot + "/Walk/2player_01_walk_split_SkeletonData.asset");
            var runData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(actionRoot + "/Run/player_01_run_split_SkeletonData.asset");
            var dodgeData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(actionRoot + "/Fight_Throw_Block_Death_Dodge/player_01_fight_split_SkeletonData.asset");
            if (idleData == null || walkData == null || runData == null || dodgeData == null)
            {
                UnityEngine.Debug.LogWarning("Stage1ProjectBootstrap: Spine SkeletonDataAsset missing, Player will fall back to placeholder sprite.");
                return;
            }

            // SpriteRenderer 仅作占位回退；接入 Spine 后禁用，避免双层显示。
            var placeholderRenderer = root.GetComponent<SpriteRenderer>();
            if (placeholderRenderer != null)
            {
                placeholderRenderer.enabled = false;
            }

            var visualGo = new GameObject("PlayerVisual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localScale = new Vector3(1f, 1f, 1f);

            var skeletonAnimation = visualGo.AddComponent<SkeletonAnimation>();
            skeletonAnimation.skeletonDataAsset = idleData;
            // 不调用 AnimationName setter（它内部会 GetSkeletonData 查找动画，未 Initialize 时抛 NPE）。
            // 直接序列化底层 _animationName 字段，让 SkeletonAnimation 在 Awake/Initialize 时自己播放。
            var skeletonSo = new SerializedObject(skeletonAnimation);
            skeletonSo.FindProperty("_animationName").stringValue = "idle";
            skeletonSo.FindProperty("loop").boolValue = true;
            skeletonSo.ApplyModifiedPropertiesWithoutUndo();

            var animView = root.AddComponent<CharacterAnimationView>();
            var so = new SerializedObject(animView);
            so.FindProperty("skeletonAnimation").objectReferenceValue = skeletonAnimation;
            so.FindProperty("idleData").objectReferenceValue = idleData;
            so.FindProperty("walkData").objectReferenceValue = walkData;
            so.FindProperty("runData").objectReferenceValue = runData;
            so.FindProperty("dodgeData").objectReferenceValue = dodgeData;
            so.FindProperty("characterScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildCameraPrefab()
        {
            const string path = PrefabRoot + "/Camera/MainCamera.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("MainCamera");
            root.tag = "MainCamera";
            var camera = root.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            // 不再用 Skybox 默认值；设为 SolidColor + 黑，避免边缘短暂露出 Unity 默认蓝。
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            root.transform.position = new Vector3(0f, 0f, -10f);
            root.AddComponent<AudioListener>();
            // CameraFollowView 保留为空壳（Validator 要求恰好 1 个），实际跟随交给 Cinemachine。
            root.AddComponent<CameraFollowView>();
            AttachCinemachineRig(root);
            return SavePrefab(root, path);
        }

        // 给 MainCamera 装 Cinemachine：Brain 在相机本体，VirtualCamera + FramingTransposer + Confiner2D 在子 GO。
        // Confiner 的 BoundingShape2D 在 BuildSampleScene 里注入（场景侧的 Collider2D 引用）。
        private static void AttachCinemachineRig(GameObject cameraRoot)
        {
            var brain = cameraRoot.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.5f);

            var vcamGo = new GameObject("CM_vcam_Player");
            vcamGo.transform.SetParent(cameraRoot.transform, false);
            vcamGo.transform.localPosition = Vector3.zero;

            var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority = 10;
            vcam.m_Lens.Orthographic = true;
            vcam.m_Lens.OrthographicSize = 5.4f;

            // Body: FramingTransposer（2D 友好，无 dead zone = 贴身跟随）
            var body = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            body.m_DeadZoneWidth = 0f;
            body.m_DeadZoneHeight = 0f;
            body.m_XDamping = 0.5f;
            body.m_YDamping = 0.5f;
            body.m_CenterOnActivate = true;

            // Extension: Confiner2D（2D 专用；要求 BoundingShape2D 是 PolygonCollider2D 或 CompositeCollider2D，
            // BoxCollider2D 不被支持因为 Confiner2D 算法依赖 Collider2D.path 顶点）
            vcamGo.AddComponent<CinemachineConfiner2D>();
        }

        private static GameObject BuildMainMenuPanel()
        {
            const string path = PrefabRoot + "/UI/MainMenuPanel.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<MainMenuPanel>("MainMenuPanel", PanelId.MainMenu, false, false);
            var flow = root.AddComponent<MainMenuFlowController>();
            CreateLabel(root.transform, "Title", "MEMORIAL ARCHIVE", new Vector2(0f, 220f), 34);
            AddButton(root.transform, "NewGameButton", "新游戏", new Vector2(0f, 100f), flow.NewGame);
            AddButton(root.transform, "ContinueButton", "继续游戏", new Vector2(0f, 45f), flow.ContinueGamePlaceholder);
            AddButton(root.transform, "LoadButton", "读取存档", new Vector2(0f, -10f), flow.LoadGamePlaceholder);
            AddButton(root.transform, "SettingsButton", "设置", new Vector2(0f, -65f), flow.OpenSettings);
            AddButton(root.transform, "ExitButton", "退出游戏", new Vector2(0f, -120f), flow.ExitGame);
            return SavePrefab(root, path);
        }

        private static GameObject BuildConfirmPanel()
        {
            const string path = PrefabRoot + "/UI/NewGameConfirmPanel.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<ConfirmDialogPanel>("NewGameConfirmPanel", PanelId.ConfirmDialog, true, true);
            var flow = root.AddComponent<MainMenuFlowController>();
            CreateLabel(root.transform, "Message", "确认开始新游戏？", new Vector2(0f, 35f), 24);
            AddButton(root.transform, "ConfirmButton", "确认", new Vector2(-90f, -45f), flow.ConfirmNewGame);
            AddButton(root.transform, "CancelButton", "取消", new Vector2(90f, -45f), flow.CancelNewGame);
            return SavePrefab(root, path);
        }

        private static GameObject BuildStoryPanel()
        {
            const string path = PrefabRoot + "/UI/BlackScreenStoryPanel.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<BlackScreenStoryPanel>("BlackScreenStoryPanel", PanelId.BlackScreenStory, true, true);
            root.GetComponent<Image>().color = Color.black;
            var text = CreateLabel(root.transform, "StoryText", string.Empty, Vector2.zero, 22);
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(1100f, 520f);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            var controller = root.AddComponent<BlackScreenStoryController>();
            Set(controller, "storyText", text);
            return SavePrefab(root, path);
        }

        private static GameObject BuildHudPanel()
        {
            const string path = PrefabRoot + "/UI/GameplayHUD.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<HUDPanel>("GameplayHUD", PanelId.Hud, false, true);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var actions = root.AddComponent<HUDButtonActions>();
            CreateLabel(root.transform, "Health", "生命 3 / 3", new Vector2(-480f, 300f), 18);
            CreateLabel(root.transform, "Stamina", "体力 30 / 30", new Vector2(-480f, 270f), 18);
            CreateLabel(root.transform, "Hint", "系统提示", new Vector2(0f, -300f), 18);
            AddButton(root.transform, "SystemButton", "系统", new Vector2(480f, 300f), actions.OpenSystem, new Vector2(90f, 36f));
            AddButton(root.transform, "InventoryButton", "背包", new Vector2(380f, 300f), actions.OpenInventory, new Vector2(90f, 36f));
            AddButton(root.transform, "DiaryButton", "日记", new Vector2(280f, 300f), actions.OpenDiary, new Vector2(90f, 36f));
            AddButton(root.transform, "MapButton", "地图", new Vector2(180f, 300f), actions.OpenMap, new Vector2(90f, 36f));
            return SavePrefab(root, path);
        }

        private static GameObject BuildInventoryPanel()
        {
            const string path = PrefabRoot + "/UI/InventoryPanel.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<InventoryPanel>("InventoryPanel", PanelId.Inventory, false, true);
            var actions = root.AddComponent<CommonPanelActions>();
            Set(actions, "ownerPanelId", (int)PanelId.Inventory);
            CreateLabel(root.transform, "SceneContainerGrid_2x2", "场景容器 2x2\n（C 接入格子）", new Vector2(-300f, 60f), 22);
            CreateLabel(root.transform, "BackpackGrid_3x3", "背包 3x3\n（C 接入格子）", new Vector2(0f, 60f), 22);
            CreateLabel(root.transform, "ShortcutBar_3", "快捷栏 3 格", new Vector2(0f, -190f), 20);
            CreateLabel(root.transform, "OffhandSlot_1", "副手栏 1 格", new Vector2(300f, 60f), 20);
            AddButton(root.transform, "CloseButton", "关闭", new Vector2(430f, 250f), actions.CloseContainerGroup, new Vector2(90f, 36f));
            return SavePrefab(root, path);
        }

        private static GameObject BuildSavePanel()
        {
            const string path = PrefabRoot + "/UI/SavePanel.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<SavePanel>("SavePanel", PanelId.Save, true, true);
            var actions = root.AddComponent<CommonPanelActions>();
            Set(actions, "ownerPanelId", (int)PanelId.Save);
            CreateLabel(root.transform, "Title", "保存：3 个内存槽，可覆盖，不写磁盘", new Vector2(0f, 150f), 22);
            AddButton(root.transform, "Slot0", "槽位 1（slotIndex=0）", new Vector2(0f, 70f), actions.SaveSlot0, new Vector2(300f, 42f));
            AddButton(root.transform, "Slot1", "槽位 2（slotIndex=1）", new Vector2(0f, 15f), actions.SaveSlot1, new Vector2(300f, 42f));
            AddButton(root.transform, "Slot2", "槽位 3（slotIndex=2）", new Vector2(0f, -40f), actions.SaveSlot2, new Vector2(300f, 42f));
            AddButton(root.transform, "CloseButton", "关闭", new Vector2(0f, -120f), actions.CloseOwner);
            return SavePrefab(root, path);
        }

        private static GameObject BuildSimplePanel<T>(string name, PanelId id, bool pauses, string title, bool closeButton)
            where T : BasePanel
        {
            var path = PrefabRoot + "/UI/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = CreatePanelRoot<T>(name, id, pauses, true);
            CreateLabel(root.transform, "Title", title, new Vector2(0f, 80f), 24);
            if (closeButton)
            {
                var actions = root.AddComponent<CommonPanelActions>();
                Set(actions, "ownerPanelId", (int)id);
                AddButton(root.transform, "CloseButton", id == PanelId.System ? "继续" : "关闭", new Vector2(0f, -80f), id == PanelId.System ? actions.ContinueGame : actions.CloseOwner);
            }
            return SavePrefab(root, path);
        }

        private static void BuildInventorySlotPrefab()
        {
            const string path = PrefabRoot + "/UI/InventorySlot.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var root = new GameObject("InventorySlot", typeof(RectTransform), typeof(Image), typeof(InventorySlotView));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(72f, 72f);
            root.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 0.85f);
            SavePrefab(root, path);
        }

        private static void BuildInteractionPrefabs()
        {
            foreach (var pair in new[]
                     {
                         ("ContainerPoint", InteractionType.Container), ("SavePoint", InteractionType.SavePoint),
                         ("InspectPoint", InteractionType.Inspect), ("EntrancePoint", InteractionType.SceneExit)
                     })
            {
                var path = PrefabRoot + "/Interaction/" + pair.Item1 + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var root = new GameObject(pair.Item1);
                var collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 1.2f;
                var view = root.AddComponent<InteractionPointView>();
                Set(view, "interactionType", (int)pair.Item2);
                SavePrefab(root, path);
            }
        }

        private static void BuildMainMenuScene(GameConfigDatabase database, Dictionary<string, GameObject> prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null)
            {
                var existingScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
                EnsureMainMenuCamera(existingScene);
                EditorSceneManager.SaveScene(existingScene, MainMenuScenePath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(prefabs["GameRoot"], scene);
            new GameObject("MainMenuFlowController").AddComponent<MainMenuFlowController>();
            var canvas = CreateCanvas("Canvas");
            var menuRoot = new GameObject("MainMenuRoot", typeof(RectTransform));
            menuRoot.transform.SetParent(canvas.transform, false);
            Stretch(menuRoot.GetComponent<RectTransform>());
            var menu = InstantiatePanel(prefabs["MainMenu"], menuRoot.transform);
            var confirm = InstantiatePanel(prefabs["Confirm"], menuRoot.transform);
            var settings = InstantiatePanel(prefabs["Settings"], menuRoot.transform);
            var load = InstantiatePanel(prefabs["Load"], menuRoot.transform);
            AddRegistry(canvas, menu, confirm, settings, load);
            CreateEventSystem();
            EnsureMainMenuCamera(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void EnsureMainMenuCamera(Scene scene)
        {
            var cameraRoot = FindRoot(scene, "MainMenuCamera");
            if (cameraRoot == null)
            {
                cameraRoot = new GameObject("MainMenuCamera");
                SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                cameraRoot.transform.position = new Vector3(0f, 0f, -10f);
            }

            cameraRoot.tag = "MainCamera";
            var camera = GetOrAddComponent<UnityEngine.Camera>(cameraRoot);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            GetOrAddComponent<AudioListener>(cameraRoot);
        }

        private static void BuildSampleScene(GameConfigDatabase database, Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            if (FindRoot(scene, "GameRoot") == null) PrefabUtility.InstantiatePrefab(prefabs["GameRoot"], scene);
            var demoRoot = FindRoot(scene, "Stage1DemoRoot") ?? new GameObject("Stage1DemoRoot");
            var roomGeometry = FindChildOrCreate(demoRoot.transform, "RoomGeometry");
            var player = FindChildOrInstantiate(demoRoot.transform, "Player", prefabs["Player"]);
            player.transform.localPosition = new Vector3(-3.5f, -1.5f, 0f);
            var cameraObject = ResolveSampleSceneCamera(scene, demoRoot.transform, prefabs["MainCamera"]);
            var follow = cameraObject.GetComponent<CameraFollowView>();
            if (follow != null) follow.SetTarget(player.transform);
            // Cinemachine vcam.Follow 也指向 Player，并注入 Confiner 边界（与"休息室"背景 19.2x10.8 对齐）。
            ConfigureCinemachine(cameraObject, player.transform, demoRoot.transform);
            var interactions = FindChildOrCreate(demoRoot.transform, "InteractionPoints");
            CreateInteractionPoint(interactions.transform, "ContainerPoint_01", Stage1Ids.ContainerPoint01, InteractionType.Container, Stage1Ids.DemoContainer01, new Vector2(-1.5f, 1f), new[] { (1002, 0, 0, 1), (1003, 1, 0, 1), (1012, 0, 1, 1) });
            CreateInteractionPoint(interactions.transform, "ContainerPoint_02", Stage1Ids.ContainerPoint02, InteractionType.Container, Stage1Ids.DemoContainer02, new Vector2(1.5f, 1f), new[] { (1020, 0, 0, 6), (1006, 1, 0, 1), (1024, 0, 1, 1) });
            CreateInteractionPoint(interactions.transform, "SavePoint_01", Stage1Ids.SavePoint01, InteractionType.SavePoint, null, new Vector2(3.5f, -1.5f), null);
            CreateInteractionPoint(interactions.transform, "InspectPoint_01", Stage1Ids.InspectPoint01, InteractionType.Inspect, null, new Vector2(0f, -1.5f), null);
            CreateInteractionPoint(interactions.transform, "EntrancePoint_01", Stage1Ids.EntrancePoint01, InteractionType.SceneExit, null, new Vector2(4.5f, 1.5f), null);
            var spawn = FindChildOrCreate(demoRoot.transform, "SpawnPoint");
            spawn.transform.localPosition = player.transform.localPosition;
            var named = GetOrAddComponent<Stage1NamedPoint>(spawn);
            Set(named, "pointId", Stage1Ids.SpawnPoint);
            CreateRoomBounds(roomGeometry.transform);

            var canvas = FindRoot(scene, "Canvas") ?? CreateCanvas("Canvas");
            var hud = FindPanelOrInstantiate<HUDPanel>(canvas.transform, prefabs["Hud"]);
            var system = FindPanelOrInstantiate<SystemPanel>(canvas.transform, prefabs["System"]);
            var settings = FindPanelOrInstantiate<SettingsPanel>(canvas.transform, prefabs["Settings"]);
            var inventory = FindPanelOrInstantiate<InventoryPanel>(canvas.transform, prefabs["Inventory"]);
            var container = FindPanelOrInstantiate<ContainerPanel>(canvas.transform, prefabs["Container"]);
            var shortcut = FindPanelOrInstantiate<ShortcutBarPanel>(canvas.transform, prefabs["Shortcut"]);
            var diary = FindPanelOrInstantiate<DiaryPanel>(canvas.transform, prefabs["Diary"]);
            var map = FindPanelOrInstantiate<MapPanel>(canvas.transform, prefabs["Map"]);
            var save = FindPanelOrInstantiate<SavePanel>(canvas.transform, prefabs["Save"]);
            var load = FindPanelOrInstantiate<LoadPanel>(canvas.transform, prefabs["Load"]);
            var blackRoot = FindRoot(scene, "BlackScreenStoryRoot") ?? CreateCanvas("BlackScreenStoryRoot");
            var story = FindPanelOrInstantiate<BlackScreenStoryPanel>(blackRoot.transform, prefabs["Story"]);
            var registry = AddRegistry(canvas, hud, system, settings, inventory, container, shortcut, diary, map, save, load, story);

            var flowObject = FindRoot(scene, "Stage1FlowController") ?? new GameObject("Stage1FlowController");
            var flow = GetOrAddComponent<Stage1FlowController>(flowObject);
            Set(flow, "blackScreenStoryRoot", blackRoot);
            Set(flow, "stage1DemoRoot", demoRoot);
            var validator = GetOrAddComponent<Stage1DemoSceneValidator>(flowObject);
            Set(validator, "blackScreenStoryRoot", blackRoot);
            Set(validator, "stage1DemoRoot", demoRoot);
            Set(validator, "panelRegistry", registry);
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null) CreateEventSystem();
            blackRoot.SetActive(true);
            demoRoot.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SampleScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(SampleScenePath, true)
            };
        }

        private static ItemConfig CreateItem(int id, string name, ItemCategory category, InventoryFootprint footprint, int maxStack,
            bool shortcut, bool offhand, OffhandType offhandType, bool canPlaceAmmo, int compatibleWeaponId, string effectId, bool canUse = false)
        {
            var asset = GetOrCreate<ItemConfig>($"Assets/GameConfigs/Items/{id}_{name}.asset");
            Set(asset, "itemId", id); Set(asset, "itemName", name); Set(asset, "category", (int)category);
            Set(asset, "backpackFootprint", (int)footprint); Set(asset, "maxStack", maxStack); Set(asset, "canUse", canUse);
            Set(asset, "canEquipToShortcut", shortcut); Set(asset, "canEquipToOffhand", offhand); Set(asset, "offhandType", (int)offhandType);
            Set(asset, "canPlaceAmmo", canPlaceAmmo); Set(asset, "compatibleWeaponItemId", compatibleWeaponId); Set(asset, "effectId", effectId);
            return asset;
        }

        private static InteractionConfig CreateInteraction(string id, InteractionType type, string containerId)
        {
            var asset = GetOrCreate<InteractionConfig>($"Assets/GameConfigs/Interaction/{id}.asset");
            Set(asset, "interactionId", id); Set(asset, "interactionType", (int)type); Set(asset, "displayName", id);
            Set(asset, "sceneId", Stage1Ids.GameplaySceneName); Set(asset, "containerId", containerId ?? string.Empty);
            return asset;
        }

        private static void CreateInteractionPoint(Transform parent, string name, string id, InteractionType type, string containerId,
            Vector2 position, (int itemId, int x, int y, int quantity)[] seeds)
        {
            var root = parent.Find(name)?.gameObject ?? new GameObject(name);
            root.transform.SetParent(parent, false); root.transform.localPosition = position;
            var collider = GetOrAddComponent<CircleCollider2D>(root);
            collider.isTrigger = true; collider.radius = 1.1f;
            var view = GetOrAddComponent<InteractionPointView>(root);
            Set(view, "interactionId", id); Set(view, "interactionType", (int)type);
            if (seeds == null) return;
            var seedView = GetOrAddComponent<SceneContainerSeedView>(root);
            Set(seedView, "containerId", containerId);
            var so = new SerializedObject(seedView); var array = so.FindProperty("initialItems"); array.arraySize = seeds.Length;
            for (var i = 0; i < seeds.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("itemId").intValue = seeds[i].itemId;
                element.FindPropertyRelative("x").intValue = seeds[i].x;
                element.FindPropertyRelative("y").intValue = seeds[i].y;
                element.FindPropertyRelative("quantity").intValue = seeds[i].quantity;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateRoomBounds(Transform parent)
        {
            CreateWall(parent, "Wall_Left", new Vector2(-5.5f, 0f), new Vector2(0.3f, 8f));
            CreateWall(parent, "Wall_Right", new Vector2(5.5f, 0f), new Vector2(0.3f, 8f));
            CreateWall(parent, "Wall_Top", new Vector2(0f, 3.8f), new Vector2(11f, 0.3f));
            CreateWall(parent, "Wall_Bottom", new Vector2(0f, -3.8f), new Vector2(11f, 0.3f));
            CreateWall(parent, "TestObstacle", new Vector2(0f, 0f), new Vector2(1.2f, 2.2f));
        }

        private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = parent.Find(name)?.gameObject ?? new GameObject(name);
            wall.transform.SetParent(parent, false); wall.transform.localPosition = position;
            var collider = GetOrAddComponent<BoxCollider2D>(wall); collider.size = size;
        }

        // 给 Cinemachine vcam 注入 Follow + Confiner 边界。
        // 边界 BoxCollider2D 独立 GameObject（不依赖可能不存在的背景 sprite），尺寸与"休息室"背景 19.2x10.8 对齐，
        // 保证相机视野（ortho 5.4 → 高 10.8）在任何角落都不超出背景画面、不露蓝边。
        private static void ConfigureCinemachine(GameObject cameraObject, Transform playerTransform, Transform demoRoot)
        {
            var vcam = cameraObject.GetComponentInChildren<CinemachineVirtualCamera>(true);
            if (vcam == null)
            {
                Debug.LogWarning("Stage1ProjectBootstrap: CinemachineVirtualCamera not found on MainCamera, skipping Confiner setup.");
                return;
            }

            vcam.Follow = playerTransform;

            var geometryHolder = FindChildOrCreate(demoRoot, "RoomGeometry");
            var confinerGo = geometryHolder.transform.Find("CameraConfiner")?.gameObject ?? new GameObject("CameraConfiner");
            confinerGo.transform.SetParent(geometryHolder.transform, false);
            confinerGo.transform.localPosition = Vector3.zero;

            // CinemachineConfiner2D 要求 BoundingShape2D 是 PolygonCollider2D 或 CompositeCollider2D
            //（它读 Collider2D.GetPath(int) 顶点；BoxCollider2D 无 path，运行时会失败）。
            // 这里手构一个 4 顶点矩形 Polygon，与"休息室"背景 19.2x10.8 对齐。
            var bounds = GetOrAddComponent<PolygonCollider2D>(confinerGo);
            bounds.isTrigger = true;
            bounds.SetPath(0, new Vector2[]
            {
                new Vector2(-9.6f, -5.4f),
                new Vector2( 9.6f, -5.4f),
                new Vector2( 9.6f,  5.4f),
                new Vector2(-9.6f,  5.4f)
            });

            var confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner != null)
            {
                var so = new SerializedObject(confiner);
                so.FindProperty("m_BoundingShape2D").objectReferenceValue = bounds;
                so.ApplyModifiedPropertiesWithoutUndo();
                confiner.InvalidateCache(); // 重新计算边界缓存
            }
        }

        private static GameObject CreatePanelRoot<T>(string name, PanelId id, bool pauses, bool startClosed) where T : BasePanel
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(T));
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.92f);
            var panel = root.GetComponent<T>();
            Set(panel, "panelId", (int)id); Set(panel, "pausesGame", pauses); Set(panel, "canvasGroup", root.GetComponent<CanvasGroup>()); Set(panel, "startClosed", startClosed);
            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = startClosed ? 0f : 1f;
            canvasGroup.interactable = !startClosed;
            canvasGroup.blocksRaycasts = !startClosed;
            return root;
        }

        private static void NormalizePanelPrefabVisibility(Dictionary<string, GameObject> prefabs)
        {
            foreach (var prefab in prefabs.Values)
            {
                if (prefab == null || prefab.GetComponent<BasePanel>() == null) continue;
                var path = AssetDatabase.GetAssetPath(prefab);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var panel = contents.GetComponent<BasePanel>();
                    var group = contents.GetComponent<CanvasGroup>();
                    if (panel == null || group == null) continue;
                    var open = panel.PanelId == PanelId.MainMenu;
                    group.alpha = open ? 1f : 0f;
                    group.interactable = open;
                    group.blocksRaycasts = open;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static void NormalizeSharedUiOwnership(Dictionary<string, GameObject> prefabs)
        {
            NormalizeOwnedChildren(prefabs["Inventory"], new[] { "CloseButton" }, "PanelChromeRoot", new[]
            {
                "SceneContainerGrid_2x2", "BackpackGrid_3x3", "ShortcutBar_3", "OffhandSlot_1"
            }, "InventoryContentRoot");

            NormalizeOwnedChildren(prefabs["Hud"], new[]
            {
                "Health", "Stamina", "Hint", "SystemButton", "InventoryButton", "DiaryButton", "MapButton"
            }, "HudChromeRoot", Array.Empty<string>(), "ShortcutMount");
        }

        private static void NormalizeOwnedChildren(GameObject prefab, string[] chromeChildren, string chromeRootName,
            string[] contentChildren, string contentRootName)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var chromeRoot = FindChildOrCreate(contents.transform, chromeRootName);
                var contentRoot = FindChildOrCreate(contents.transform, contentRootName);
                foreach (var childName in chromeChildren)
                {
                    var child = contents.transform.Find(childName);
                    if (child != null) child.SetParent(chromeRoot.transform, false);
                }

                foreach (var childName in contentChildren)
                {
                    var child = contents.transform.Find(childName);
                    if (child != null) child.SetParent(contentRoot.transform, false);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject InstantiatePanel(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Stretch(instance.GetComponent<RectTransform>());
            return instance;
        }

        private static T FindPanelOrInstantiate<T>(Transform parent, GameObject prefab) where T : BasePanel
        {
            var existing = parent.GetComponentInChildren<T>(true);
            return existing != null ? existing : InstantiatePanel(prefab, parent).GetComponent<T>();
        }

        private static ScenePanelRegistry AddRegistry(GameObject owner, params BasePanel[] panels)
        {
            var registry = GetOrAddComponent<ScenePanelRegistry>(owner);
            var so = new SerializedObject(registry); var array = so.FindProperty("panels"); array.arraySize = panels.Length;
            for (var i = 0; i < panels.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return registry;
        }

        private static ScenePanelRegistry AddRegistry(GameObject owner, params GameObject[] panelObjects)
        {
            var panels = new BasePanel[panelObjects.Length];
            for (var i = 0; i < panelObjects.Length; i++) panels[i] = panelObjects[i].GetComponent<BasePanel>();
            return AddRegistry(owner, panels);
        }

        private static GameObject CreateCanvas(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f);
            return root;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Text CreateLabel(Transform parent, string name, string value, Vector2 position, int fontSize)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text)); root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(520f, 100f); rect.anchoredPosition = position;
            var text = root.GetComponent<Text>(); text.font = DefaultFont; text.text = value; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            return text;
        }

        private static Button AddButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = size ?? new Vector2(220f, 44f); rect.anchoredPosition = position;
            root.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.24f, 0.95f);
            var text = CreateLabel(root.transform, "Label", label, Vector2.zero, 18); Stretch(text.rectTransform);
            var button = root.GetComponent<Button>(); UnityEventTools.AddPersistentListener(button.onClick, action); return button;
        }

        private static Font DefaultFont => defaultFont != null ? defaultFont : defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private static GameObject FindRoot(Scene scene, string name) { foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root; return null; }
        private static GameObject FindChildOrCreate(Transform parent, string name) { var child = parent.Find(name); if (child != null) return child.gameObject; var root = new GameObject(name); root.transform.SetParent(parent, false); return root; }
        private static GameObject FindChildOrInstantiate(Transform parent, string name, GameObject prefab) { var child = parent.Find(name); if (child != null) return child.gameObject; var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); instance.name = name; return instance; }

        private static GameObject ResolveSampleSceneCamera(Scene scene, Transform demoRoot, GameObject cameraPrefab)
        {
            var originalCamera = FindRoot(scene, "Main Camera") ?? FindRoot(scene, "MainCamera");
            var generatedCamera = demoRoot.Find("MainCamera");

            // 如果旧相机没有 CinemachineBrain（历史遗留 prefab 实例），销毁它，让流程走到下方从最新 prefab 实例化。
            // 这能保证场景里的 MainCamera 永远跟 MainCamera.prefab 同步（包括 vcam 子 GO、Confiner extension 等）。
            if (originalCamera != null && originalCamera.GetComponent<CinemachineBrain>() == null)
            {
                UnityEngine.Object.DestroyImmediate(originalCamera);
                originalCamera = null;
            }

            if (originalCamera != null)
            {
                if (generatedCamera != null && generatedCamera.gameObject != originalCamera)
                {
                    UnityEngine.Object.DestroyImmediate(generatedCamera.gameObject);
                }

                originalCamera.name = "MainCamera";
                originalCamera.tag = "MainCamera";
                originalCamera.transform.SetParent(null, true);
                GetOrAddComponent<CameraFollowView>(originalCamera);
                return originalCamera;
            }

            if (generatedCamera != null)
            {
                generatedCamera.SetParent(null, true);
                return generatedCamera.gameObject;
            }

            var created = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
            created.name = "MainCamera";
            return created;
        }

        private static T GetOrAddComponent<T>(GameObject owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static void Set(UnityEngine.Object target, string propertyName, object value)
        {
            var so = new SerializedObject(target); var property = so.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case int intValue when property.propertyType == SerializedPropertyType.Enum: property.enumValueIndex = intValue; break;
                case int intValue: property.intValue = intValue; break;
                case float floatValue: property.floatValue = floatValue; break;
                case bool boolValue: property.boolValue = boolValue; break;
                case string stringValue: property.stringValue = stringValue; break;
                case UnityEngine.Object objectValue: property.objectReferenceValue = objectValue; break;
                default: throw new NotSupportedException($"Unsupported serialized value type: {value.GetType().Name}");
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }

        private static void SetArray<T>(UnityEngine.Object target, string propertyName, T[] values) where T : UnityEngine.Object
        {
            var so = new SerializedObject(target); var property = so.FindProperty(propertyName); property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/'); var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void CleanupFailedTemporaryObjects()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root.name == "MainMenuPanel" && PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.NotAPrefab)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private const string OpeningStoryText = "在终年大雾笼罩的伦敦旧城区深处，矗立着一栋沉默的建筑。▼\n" +
            "在市政厅的公开地图上，它只是一座普通的私人档案馆。在官方的描述中，那里不过是用来堆放霉烂旧书、过期病历与废弃信件的荒凉处所。▼\n" +
            "然而，▼\n在旧城区的暗巷里，关于它的传闻从未停止过。▼\n" +
            "那些神色惊恐的巡夜人发誓，他们曾隔着铁窗，看到成群的穿着苍白病号服的怪人，用一种近乎折断关节的诡异姿态，神色癫狂地向后倒着跑。▼\n" +
            "有人在酒馆里低声散布，说那座档案馆是一座活人囚笼。深夜里，总有密不透风的黑色马车从西区驶来，车里抬下的不是疯掉的伯爵，就是穿着丝绸长裙却满头鲜血的贵族小姐。▼\n" +
            "当然，大不列颠的法律需要真凭实据，而这些传言，早就随着知情者的失踪而无处可查了。▼\n" +
            "直到多年前的某个夜晚，那栋大楼里似乎爆发了一场无法挽回的变故。▼\n" +
            "传言说里面所有的人都在那一夜凭空蒸发了，随后政府便永久关停了这里，像躲避瘟疫一样抹掉了它在官方的所有记录。▼\n" +
            "一堵冰冷的青砖墙彻底砌死了它的正门，自那以后，那座档案馆沦为雾都人人避之不及的禁忌。▼\n" +
            "人们说，每当深夜路过那栋建筑，湿冷的墙壁后面会传来不属于活人的细碎低语、冰冷的翻书声、还有……▼";
    }
}
#endif
