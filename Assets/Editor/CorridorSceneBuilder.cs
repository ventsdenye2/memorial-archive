#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Camera.View;
using MemorialArchive.Gameplay.Character.View;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Inventory.View;
using MemorialArchive.Gameplay.Monster.View;
using MemorialArchive.Gameplay.Stage3;
using MemorialArchive.Gameplay.Story.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Batch-creates the corridor gameplay scenes and the standalone opening-story scene.
    ///
    /// Every gameplay scene (not MainMenu) shares the same structure:
    ///   - GameRoot (from prefab, DontDestroyOnLoad)
    ///   - [SceneName]Root   (like Stage1DemoRoot)
    ///     - RoomGeometry    (walls + camera confiner)
    ///     - Player          (from Player prefab)
    ///     - InteractionPoints
    ///     - SpawnPoints     (framework SceneSpawnPoint)
    ///     - MonsterSpawnPoints
    ///   - MainCamera         (from prefab, with Cinemachine rig)
    ///   - Canvas             (ScreenSpaceOverlay, 1920×1080, with ScenePanelRegistry)
    ///   - EventSystem
    ///
    /// GameRoot with DontDestroyOnLoad persists from MainMenu, so the corridor scenes
    /// do NOT create a new one — the existing GameRoot is merely re-parented into the
    /// new scene by Unity.
    /// </summary>
    public static class CorridorSceneBuilder
    {
        private const string ArtRoot = "Assets/Art/Scenes";
        private const string SceneRoot = "Assets/Scenes";
        private const string ConfigRoot = "Assets/GameConfigs";
        private const string GoRootPrefabPath = "Assets/Prefabs/System/GameRoot.prefab";
        private const string CamPrefabPath = "Assets/Prefabs/Camera/MainCamera.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/Character/Player.prefab";
        private const string UiPrefabRoot = "Assets/Prefabs/UI";
        private const string DatabasePath = "Assets/GameConfigs/GameConfigDatabase.asset";

        // Background sprite size: 19.2 × 10.8 world units (matching "休息室" in SampleScene)
        private const float BgHalfW = 9.6f;
        private const float BgHalfH = 5.4f;
        private const float EdgeSpawnX = BgHalfW - 2.4f;
        // Player walks along the bottom line (Y is frozen at this height in PlayerMotor).
        private const float WalkY = -5.2f;

        private sealed class CorridorDef
        {
            public string SceneId;
            public string DisplayName;
            public string ArtSubPath;
            public string CompositePng;
            public string LeftSceneId;
            public string RightSceneId;
            public string StairsUpSceneId;
            public string StairsDownSceneId;
            public bool HasStairs;
        }

        private static readonly CorridorDef[] Corridors =
        {
            // ── Floor 1 ──
            new CorridorDef { SceneId = "Corridor_1F_1", DisplayName = "一层走廊 1.1",
                ArtSubPath = "走廊1楼/走廊1", CompositePng = "走廊1.png",
                RightSceneId = "Corridor_1F_2" },
            new CorridorDef { SceneId = "Corridor_1F_2", DisplayName = "一层走廊 1.2",
                ArtSubPath = "走廊1楼/走廊1.2", CompositePng = "走廊1.2.png",
                LeftSceneId = "Corridor_1F_1", RightSceneId = "Corridor_1F_3" },
            new CorridorDef { SceneId = "Corridor_1F_3", DisplayName = "一层走廊 1.3",
                ArtSubPath = "走廊1楼/走廊1.3", CompositePng = "走廊1.3.png",
                LeftSceneId = "Corridor_1F_2", RightSceneId = "Corridor_1F_4",
                StairsUpSceneId = "Corridor_2F_3", HasStairs = true },
            new CorridorDef { SceneId = "Corridor_1F_4", DisplayName = "一层走廊 1.4",
                ArtSubPath = "走廊1楼/走廊1.4", CompositePng = "走廊1.4.png",
                LeftSceneId = "Corridor_1F_3" },
            // ── Floor 2 ──
            new CorridorDef { SceneId = "Corridor_2F_1", DisplayName = "二层走廊 2.1",
                ArtSubPath = "走廊2楼/走廊2", CompositePng = "走廊2.png",
                RightSceneId = "Corridor_2F_2",
                StairsUpSceneId = "Corridor_3F_1", HasStairs = true },
            new CorridorDef { SceneId = "Corridor_2F_2", DisplayName = "二层走廊 2.2",
                ArtSubPath = "走廊2楼/走廊2.2", CompositePng = "走廊2.2.png",
                LeftSceneId = "Corridor_2F_1", RightSceneId = "Corridor_2F_3" },
            new CorridorDef { SceneId = "Corridor_2F_3", DisplayName = "二层走廊 2.3",
                ArtSubPath = "走廊2楼/走廊2.3", CompositePng = "走廊2.3.png",
                LeftSceneId = "Corridor_2F_2", RightSceneId = "Corridor_2F_4",
                StairsDownSceneId = "Corridor_1F_3", HasStairs = true },
            new CorridorDef { SceneId = "Corridor_2F_4", DisplayName = "二层走廊 2.4",
                ArtSubPath = "走廊2楼/走廊2.4", CompositePng = "走廊2.4.png",
                LeftSceneId = "Corridor_2F_3" },
            // ── Floor 3 ──
            new CorridorDef { SceneId = "Corridor_3F_1", DisplayName = "三层走廊 3.1",
                ArtSubPath = "走廊3楼/走廊3,1", CompositePng = "走廊3，1、.png",
                RightSceneId = "Corridor_3F_2",
                StairsUpSceneId = "Corridor_4F_1", StairsDownSceneId = "Corridor_2F_1", HasStairs = true },
            new CorridorDef { SceneId = "Corridor_3F_2", DisplayName = "三层走廊 3.2",
                ArtSubPath = "走廊3楼/走廊3,2", CompositePng = "走廊3.2.png",
                LeftSceneId = "Corridor_3F_1", RightSceneId = "Corridor_3F_3" },
            new CorridorDef { SceneId = "Corridor_3F_3", DisplayName = "三层走廊 3.3",
                ArtSubPath = "走廊3楼/走廊3,3", CompositePng = "走廊3 3.png",
                LeftSceneId = "Corridor_3F_2", RightSceneId = "Corridor_3F_4" },
            new CorridorDef { SceneId = "Corridor_3F_4", DisplayName = "三层走廊 3.4",
                ArtSubPath = "走廊3楼/走廊3,4", CompositePng = "走廊3 4.png",
                LeftSceneId = "Corridor_3F_3" },
            // ── Floor 4 (partial — no stair to floor 4 yet) ──
            new CorridorDef { SceneId = "Corridor_4F_1", DisplayName = "四层走廊 4.1",
                ArtSubPath = "走廊4楼/走廊4.1", CompositePng = "走廊4 1.png",
                RightSceneId = "Corridor_4F_2",
                StairsDownSceneId = "Corridor_3F_1", HasStairs = true },
            new CorridorDef { SceneId = "Corridor_4F_2", DisplayName = "四层走廊 4.2",
                ArtSubPath = "走廊4楼/走廊4.2", CompositePng = "走廊4.2.png",
                LeftSceneId = "Corridor_4F_1" },
        };

        [MenuItem("Tools/Memorial Archive/Build All Corridor Scenes")]
        public static void BuildAllCorridors()
        {
            EnsureFolders();
            EnsureStairTravelPanelPrefab();
            EnsureBlackScreenStoryPanelPrefab();
            var database = EnsureDatabase();
            BuildInteractionConfigs(database);
            AssetDatabase.SaveAssets();
            BuildAllScenes();
            BuildOpeningStoryScene();
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built {Corridors.Length} corridor scenes.");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { "Assets/GameConfigs/Interaction", "Assets/Scenes" })
                MkDir(dir);
        }

        private static void MkDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var p = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var n = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(p)) MkDir(p);
            AssetDatabase.CreateFolder(p, n);
        }

        private static GameConfigDatabase EnsureDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(DatabasePath);
            if (db == null) { db = ScriptableObject.CreateInstance<GameConfigDatabase>(); AssetDatabase.CreateAsset(db, DatabasePath); }
            return db;
        }

        // ──────────────────────── Interaction configs ─────────────────────────
        private sealed class StairInfo
        {
            public string Id;
            public string UpScene;
            public string UpSpawn;
            public string DownScene;
            public string DownSpawn;
        }

        private static StairInfo GetStair(CorridorDef d)
        {
            if (!d.HasStairs)
            {
                return null;
            }

            return new StairInfo
            {
                Id = $"{d.SceneId}_stairs",
                UpScene = d.StairsUpSceneId,
                UpSpawn = string.IsNullOrEmpty(d.StairsUpSceneId) ? string.Empty : $"{d.StairsUpSceneId}_spawn_stairs",
                DownScene = d.StairsDownSceneId,
                DownSpawn = string.IsNullOrEmpty(d.StairsDownSceneId) ? string.Empty : $"{d.StairsDownSceneId}_spawn_stairs"
            };
        }

        private static void BuildInteractionConfigs(GameConfigDatabase db)
        {
            var ids = new HashSet<string>();
            foreach (var d in Corridors)
            {
                var stair = GetStair(d);
                if (stair != null) ids.Add(stair.Id);
            }

            // Remove stale
            foreach (var a in FindAssets<InteractionConfig>("Assets/GameConfigs/Interaction"))
                if (!ids.Contains(a.InteractionId) && a.InteractionId.StartsWith("Corridor_"))
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(a));

            // Create/update
            foreach (var d in Corridors)
            {
                var stair = GetStair(d);
                if (stair != null) EnsureConfig(stair, d);
            }

            // Sync database array
            var assets = FindAssets<InteractionConfig>("Assets/GameConfigs/Interaction");
            var so = new SerializedObject(db);
            var arr = so.FindProperty("interactions");
            arr.arraySize = assets.Length;
            for (var i = 0; i < assets.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
        }

        private static void EnsureConfig(StairInfo stair, CorridorDef d)
        {
            var path = $"{ConfigRoot}/Interaction/{stair.Id}.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<InteractionConfig>(path);
            if (cfg == null) { cfg = ScriptableObject.CreateInstance<InteractionConfig>(); cfg.name = stair.Id; AssetDatabase.CreateAsset(cfg, path); }
            var so = new SerializedObject(cfg);
            so.FindProperty("interactionId").stringValue = stair.Id;
            so.FindProperty("interactionType").enumValueIndex = (int)InteractionType.SceneExit;
            so.FindProperty("displayName").stringValue = "楼梯";
            so.FindProperty("transitionSceneId").stringValue = string.Empty;
            so.FindProperty("transitionSpawnPointId").stringValue = string.Empty;
            so.FindProperty("sceneId").stringValue = d.SceneId;
            so.FindProperty("stairPrompt").stringValue = "请选择前往楼层";
            so.FindProperty("stairUpSceneId").stringValue = stair.UpScene ?? string.Empty;
            so.FindProperty("stairUpSpawnPointId").stringValue = stair.UpSpawn ?? string.Empty;
            so.FindProperty("stairDownSceneId").stringValue = stair.DownScene ?? string.Empty;
            so.FindProperty("stairDownSpawnPointId").stringValue = stair.DownSpawn ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg);
        }

        private static T[] FindAssets<T>(string folder) where T : Object
        {
            var l = new List<T>();
            foreach (var g in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var o = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (o != null) l.Add(o);
            }
            return l.ToArray();
        }

        // ──────────────────────── Scene building ─────────────────────────────
        private static void BuildAllScenes() { foreach (var d in Corridors) BuildOne(d); }

        private static void BuildOne(CorridorDef d)
        {
            var sp = $"{SceneRoot}/{d.SceneId}.unity";
            Scene sc = AssetDatabase.LoadAssetAtPath<SceneAsset>(sp) != null
                ? EditorSceneManager.OpenScene(sp, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rootName = $"{d.SceneId}Root";

            // Clean up the old "CorridorRoot" naming from earlier builder versions
            RemoveRootObject(sc, "CorridorRoot");

            // 1. Scene root (like Stage1DemoRoot)
            var sceneRoot = FindOrMakeRoot(sc, rootName);

            // 2. Background sprite — same z/sorting as SampleScene "休息室"
            var bgGo = FindOrMakeChild(sceneRoot.transform, "Background");
            bgGo.transform.localPosition = Vector3.zero;
            var sr = bgGo.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                var old = bgGo.GetComponent<Renderer>(); if (old != null) Object.DestroyImmediate(old);
                sr = bgGo.AddComponent<SpriteRenderer>();
            }
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{d.ArtSubPath}/{d.CompositePng}");
            sr.sortingOrder = -10;

            // 3. RoomGeometry (walls + camera confiner, like SampleScene)
            var geom = FindOrMakeChild(sceneRoot.transform, "RoomGeometry");
            ClearKids(geom);

            // Camera confiner — PolygonCollider2D matching bg 19.2×10.8
            var confGo = FindOrMakeChild(geom.transform, "CameraConfiner");
            RemoveComps<Collider2D>(confGo);
            var poly = confGo.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.SetPath(0, new[] { new Vector2(-BgHalfW, -BgHalfH), new Vector2(BgHalfW, -BgHalfH), new Vector2(BgHalfW, BgHalfH), new Vector2(-BgHalfW, BgHalfH) });

            // Walls — same positions as SampleScene
            MakeWall(geom, "Wall_Left", new Vector2(-BgHalfW - 0.15f, 0f), new Vector2(0.3f, BgHalfH * 2f));
            MakeWall(geom, "Wall_Right", new Vector2(BgHalfW + 0.15f, 0f), new Vector2(0.3f, BgHalfH * 2f));
            MakeWall(geom, "Wall_Top", new Vector2(0f, BgHalfH + 0.15f), new Vector2(BgHalfW * 2f, 0.3f));
            MakeWall(geom, "Wall_Bottom", new Vector2(0f, -BgHalfH - 0.15f), new Vector2(BgHalfW * 2f, 0.3f));

            // Door openings — remove wall at edges that connect to adjacent corridors
            // Left connection = remove left wall block
            if (!string.IsNullOrEmpty(d.LeftSceneId))
                KillWall(geom, "Wall_Left");
            if (!string.IsNullOrEmpty(d.RightSceneId))
                KillWall(geom, "Wall_Right");

            // 4. Player (same localPos as SampleScene: -3.5, -5.2 relative to root)
            KillChild(sceneRoot.transform, "Player");
            var player = InstPrefab(PlayerPrefabPath, sceneRoot.transform, "Player");
            if (player != null) { player.transform.localPosition = new Vector3(-3.5f, WalkY, 0f); var b = player.GetComponent<Rigidbody2D>(); if (b != null) { b.position = player.transform.position; b.velocity = Vector2.zero; } }

            // 5. InteractionPoints — same-floor exits auto-transition on trigger enter;
            //    stairs use InteractionPointView + confirmation dialog.
            var ipRoot = FindOrMakeChild(sceneRoot.transform, "InteractionPoints");
            ClearKids(ipRoot);
            if (!string.IsNullOrEmpty(d.LeftSceneId))
            {
                MakeAutoExit(
                    ipRoot.transform,
                    $"{d.SceneId}_exit_left",
                    d.LeftSceneId,
                    $"{d.LeftSceneId}_spawn_right",
                    new Vector2(-BgHalfW + 0.4f, WalkY));
            }

            if (!string.IsNullOrEmpty(d.RightSceneId))
            {
                MakeAutoExit(
                    ipRoot.transform,
                    $"{d.SceneId}_exit_right",
                    d.RightSceneId,
                    $"{d.RightSceneId}_spawn_left",
                    new Vector2(BgHalfW - 0.4f, WalkY));
            }

            var stair = GetStair(d);
            if (stair != null)
            {
                MakePoint(ipRoot.transform, stair.Id, new Vector2(StairX(d), WalkY));
            }

            // 6. SpawnPoints — edge arrivals stay safely outside the opposite edge
            //    trigger; stair arrivals share one point beside the same stair.
            KillChild(sceneRoot.transform, "SpawnPoints");
            var spRoot = NewChild(sceneRoot.transform, "SpawnPoints");
            MakeSpawn(spRoot.transform, $"{d.SceneId}_spawn_left", new Vector3(-EdgeSpawnX, WalkY, 0f));
            MakeSpawn(spRoot.transform, $"{d.SceneId}_spawn_right", new Vector3(EdgeSpawnX, WalkY, 0f));
            if (d.HasStairs)
            {
                var stairX = StairX(d);
                MakeSpawn(spRoot.transform, $"{d.SceneId}_spawn_stairs", new Vector3(stairX + 0.8f, WalkY, 0f));
            }
            // Initial spawn (new game entry) — left side
            MakeSpawn(spRoot.transform, $"{d.SceneId}_spawn_initial", new Vector3(-EdgeSpawnX, WalkY, 0f));

            // 7. MonsterSpawnPoints (empty inactive container, like SampleScene)
            var msRoot = FindOrMakeChild(sceneRoot.transform, "MonsterSpawnPoints");
            msRoot.SetActive(false);
            ClearKids(msRoot);

            // 8. MainCamera (from prefab)
            RemoveRootObject(sc, "MainCamera"); RemoveRootObject(sc, "Main Camera");
            var cam = InstPrefab(CamPrefabPath, null, "MainCamera");
            if (cam != null) { SceneManager.MoveGameObjectToScene(cam, sc); cam.tag = "MainCamera"; ConfigCam(sc, cam, confGo); }

            // 9. GameRoot — NOT added to corridor scenes. The persistent GameRoot from
            //    MainMenu is DontDestroyOnLoad and survives into every corridor scene.
            //    (FindObjectOfType is scene-agnostic and would find another scene's
            //    GameRoot during batch building, causing duplicate UIManagers.)
            RemoveRootObject(sc, "GameRoot");

            // 10. Canvas (ScreenSpaceOverlay, 1920×1080)
            RemoveRootObject(sc, "Canvas");
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, sc);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            // UI panels (like SampleScene: Hud, Inventory, Container, Shortcut, Diary, Map, System, Settings, Save, Load)
            var panels = BuildPanels(canvasGo.transform);

            // Corridor scenes contain gameplay UI only. Opening story presentation
            // belongs exclusively to the dedicated OpeningStory scene.
            RemoveRootObject(sc, "BlackScreenStoryRoot");
            var reg = canvasGo.GetComponent<ScenePanelRegistry>() ?? canvasGo.AddComponent<ScenePanelRegistry>();
            var regSo = new SerializedObject(reg);
            var arr = regSo.FindProperty("panels");
            arr.arraySize = panels.Count;
            for (var i = 0; i < panels.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            regSo.ApplyModifiedPropertiesWithoutUndo();

            // No story flow controller is allowed in a corridor scene.
            while (RemoveRootObject(sc, "FlowController")) { }
            while (RemoveRootObject(sc, "Stage1FlowController")) { }

            // 13. EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(es, sc);
            }

            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc, sp);
            Debug.Log($"  Built: {d.SceneId}");
        }

        // ── Helpers ──

        private static GameObject FindOrMakeRoot(Scene sc, string n)
        {
            foreach (var r in sc.GetRootGameObjects()) if (r.name == n) return r;
            var go = new GameObject(n);
            SceneManager.MoveGameObjectToScene(go, sc);
            return go;
        }

        private static GameObject FindOrMakeChild(Transform p, string n)
        {
            var c = p.Find(n);
            return c != null ? c.gameObject : NewChild(p, n);
        }

        private static GameObject NewChild(Transform p, string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            return go;
        }

        private static void ClearKids(GameObject go)
        {
            for (var i = go.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }

        private static void KillChild(Transform p, string n)
        {
            var c = p.Find(n);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        private static void KillWall(GameObject geom, string n)
        {
            var c = geom.transform.Find(n);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        private static void RemoveComps<T>(GameObject go) where T : Component
        {
            foreach (var c in go.GetComponents<T>()) Object.DestroyImmediate(c);
        }

        private static bool RemoveRootObject(Scene sc, string n)
        {
            foreach (var r in sc.GetRootGameObjects())
                if (r.name == n) { Object.DestroyImmediate(r); return true; }
            return false;
        }

        private static GameObject InstPrefab(string path, Transform parent, string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) { Debug.LogError($"Prefab not found: {path}"); return null; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            inst.name = name;
            return inst;
        }

        private static void MakeWall(GameObject geom, string n, Vector2 pos, Vector2 size)
        {
            var go = NewChild(geom.transform, n);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        private static void MakePoint(Transform p, string cid, Vector2 pos)
        {
            var go = NewChild(p, cid);
            go.transform.localPosition = pos;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.2f;
            var v = go.AddComponent<InteractionPointView>();
            var so = new SerializedObject(v);
            so.FindProperty("interactionId").stringValue = cid;
            so.FindProperty("interactionType").enumValueIndex = (int)InteractionType.SceneExit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MakeAutoExit(Transform p, string cid, string targetScene, string targetSpawn, Vector2 pos)
        {
            var go = NewChild(p, cid);
            go.transform.localPosition = pos;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.2f;
            var autoExit = go.AddComponent<CorridorAutoExit>();
            var so = new SerializedObject(autoExit);
            so.FindProperty("targetSceneId").stringValue = targetScene;
            so.FindProperty("targetSpawnPointId").stringValue = targetSpawn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MakeSpawn(Transform p, string pointId, Vector3 pos)
        {
            var go = NewChild(p, pointId);
            go.transform.localPosition = pos;
            var np = go.AddComponent<SceneSpawnPoint>();
            var so = new SerializedObject(np);
            so.FindProperty("pointId").stringValue = pointId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Stair X position (world) for each stair corridor, read from the art analysis.
        private static float StairX(CorridorDef d)
        {
            switch (d.SceneId)
            {
                case "Corridor_1F_3": return 3.7f;   // stair cluster x 3.0~4.5
                case "Corridor_2F_1": return -0.0f;  // stair cluster x -0.6~0.6
                case "Corridor_2F_3": return -3.0f;  // stair cluster x -3.7~-2.5
                case "Corridor_3F_1": return 0.0f;   // stair cluster spans wide, use center
                case "Corridor_4F_1": return 0.0f;
                default: return 0f;
            }
        }

        private static void ConfigCam(Scene sc, GameObject camGo, GameObject confGo)
        {
            var vcam = camGo.GetComponentInChildren<CinemachineVirtualCamera>(true);
            if (vcam == null)
            {
                var vGo = new GameObject("CM_vcam_Player");
                vGo.transform.SetParent(camGo.transform, false);
                vGo.transform.localPosition = Vector3.zero;
                vcam = vGo.AddComponent<CinemachineVirtualCamera>();
                vcam.Priority = 10;
                vcam.m_Lens.Orthographic = true;
                vcam.m_Lens.OrthographicSize = 5.4f;
                var body = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
                body.m_DeadZoneWidth = 0f; body.m_DeadZoneHeight = 0f;
                body.m_XDamping = 0.5f; body.m_YDamping = 0.5f; body.m_CenterOnActivate = true;
            }

            // Find Player in scene to bind Follow
            foreach (var r in sc.GetRootGameObjects())
            {
                var p = r.transform.Find("Player");
                if (p != null) { vcam.Follow = p; break; }
                // Player may be nested under the scene root
                foreach (Transform child in r.transform)
                {
                    if (child.name == "Player") { vcam.Follow = child; break; }
                    var nested = child.Find("Player");
                    if (nested != null) { vcam.Follow = nested; break; }
                }
            }

            var confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner == null) confiner = vcam.gameObject.AddComponent<CinemachineConfiner2D>();
            var cso = new SerializedObject(confiner);
            cso.FindProperty("m_BoundingShape2D").objectReferenceValue = confGo?.GetComponent<PolygonCollider2D>();
            cso.ApplyModifiedPropertiesWithoutUndo();
            if (confGo != null) confiner.InvalidateCache();
        }

        // ── UI panels (identical to SampleScene Canvas children) ──
        private static readonly (string name, PanelId id)[] PanelSpecs =
        {
            ("GameplayHUD", PanelId.Hud),
            ("InventoryPanel", PanelId.Inventory),
            ("DiaryPanel", PanelId.Diary),
            ("MapPanel", PanelId.Map),
            ("SystemPanel", PanelId.System),
            ("SettingsPanel", PanelId.Settings),
            ("SavePanel", PanelId.Save),
            ("LoadPanel", PanelId.Load),
            ("StairTravelPanel", PanelId.StairTravel),
        };

        private static List<BasePanel> BuildPanels(Transform canvasT)
        {
            var list = new List<BasePanel>();
            foreach (var (name, id) in PanelSpecs)
            {
                var existing = canvasT.Find(name);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                var prefabPath = $"{UiPrefabRoot}/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) { Debug.LogWarning($"UI prefab missing: {prefabPath}"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasT);
                inst.name = name;
                var bp = inst.GetComponent<BasePanel>();
                if (bp == null) { Debug.LogWarning($"No BasePanel on {name}"); continue; }
                var so = new SerializedObject(bp);
                so.FindProperty("panelId").enumValueIndex = (int)id;
                so.FindProperty("startClosed").boolValue = id != PanelId.Hud;
                so.ApplyModifiedPropertiesWithoutUndo();
                list.Add(bp);
            }
            return list;
        }

        private static void EnsureStairTravelPanelPrefab()
        {
            var path = $"{UiPrefabRoot}/StairTravelPanel.prefab";
            var root = new GameObject(
                "StairTravelPanel",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(StairTravelPanel));

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var body = NewUiObject("Dialog", root.transform, new Vector2(560f, 330f), Vector2.zero);
            var bodyImage = body.AddComponent<Image>();
            bodyImage.color = new Color(0.22f, 0.14f, 0.11f, 1f);

            var message = NewUiText("Message", body.transform, new Vector2(500f, 100f), new Vector2(0f, 75f), "请选择目的楼层", 30);
            var up = NewUiButton("UpButton", body.transform, new Vector2(150f, 64f), new Vector2(-170f, -70f), "上楼");
            var down = NewUiButton("DownButton", body.transform, new Vector2(150f, 64f), new Vector2(0f, -70f), "下楼");
            var cancel = NewUiButton("CancelButton", body.transform, new Vector2(150f, 64f), new Vector2(170f, -70f), "取消");

            var panel = root.GetComponent<StairTravelPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelId").enumValueIndex = (int)PanelId.StairTravel;
            so.FindProperty("pausesGame").boolValue = true;
            so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("startClosed").boolValue = true;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("upButton").objectReferenceValue = up;
            so.FindProperty("downButton").objectReferenceValue = down;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureBlackScreenStoryPanelPrefab()
        {
            var path = $"{UiPrefabRoot}/BlackScreenStoryPanel.prefab";
            var root = new GameObject(
                "BlackScreenStoryPanel",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(BlackScreenStoryPanel),
                typeof(BlackScreenStoryController));

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.012f, 0.014f, 0.022f, 1f);

            var title = NewUiText("ChapterTitle", root.transform, new Vector2(1080f, 70f), new Vector2(0f, 405f), "序章 · 雾中档案馆", 38);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.78f, 0.69f, 0.55f, 1f);

            // Future portrait presenters can assign sprites to these reserved slots.
            MakePortraitSlot("PortraitLeft", root.transform, new Vector2(-590f, 105f));
            MakePortraitSlot("PortraitRight", root.transform, new Vector2(590f, 105f));

            var frame = NewUiObject("StoryFrame", root.transform, new Vector2(1520f, 360f), new Vector2(0f, -315f));
            var frameImage = frame.AddComponent<Image>();
            frameImage.color = new Color(0.045f, 0.052f, 0.072f, 0.96f);

            var frameTop = NewUiText("FrameLabel", frame.transform, new Vector2(1330f, 48f), new Vector2(0f, 128f), "档案馆记录  /  第一夜", 22);
            frameTop.alignment = TextAnchor.MiddleLeft;
            frameTop.color = new Color(0.62f, 0.66f, 0.70f, 1f);

            var storyText = NewUiText("StoryText", frame.transform, new Vector2(1330f, 190f), new Vector2(0f, 5f), string.Empty, 32);
            storyText.alignment = TextAnchor.UpperLeft;
            storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            storyText.verticalOverflow = VerticalWrapMode.Truncate;
            storyText.lineSpacing = 1.18f;
            storyText.color = new Color(0.91f, 0.90f, 0.85f, 1f);

            var hint = NewUiText("ContinueHint", frame.transform, new Vector2(800f, 42f), new Vector2(0f, -138f), "单击鼠标或按任意键继续", 20);
            hint.color = new Color(0.66f, 0.69f, 0.74f, 1f);
            hint.gameObject.SetActive(false);

            var panel = root.GetComponent<BlackScreenStoryPanel>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("panelId").enumValueIndex = (int)PanelId.BlackScreenStory;
            panelSo.FindProperty("pausesGame").boolValue = true;
            panelSo.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            panelSo.FindProperty("startClosed").boolValue = true;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = root.GetComponent<BlackScreenStoryController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("storyText").objectReferenceValue = storyText;
            controllerSo.FindProperty("continueHint").objectReferenceValue = hint;
            controllerSo.FindProperty("charactersPerSecond").floatValue = 36f;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void MakePortraitSlot(string name, Transform parent, Vector2 position)
        {
            var slot = NewUiObject(name, parent, new Vector2(520f, 720f), position);
            var image = slot.AddComponent<Image>();
            image.color = new Color(0.45f, 0.50f, 0.58f, 0f);
            image.preserveAspect = true;
        }

        private static GameObject NewUiObject(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private static Text NewUiText(string name, Transform parent, Vector2 size, Vector2 position, string value, int fontSize)
        {
            var go = NewUiObject(name, parent, size, position);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.93f, 0.88f, 0.80f, 1f);
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static Button NewUiButton(string name, Transform parent, Vector2 size, Vector2 position, string label)
        {
            var go = NewUiObject(name, parent, size, position);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.48f, 0.31f, 0.23f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            NewUiText("Label", go.transform, size, Vector2.zero, label, 26);
            return button;
        }

        private static void BuildOpeningStoryScene()
        {
            var path = $"{SceneRoot}/{Stage3Ids.OpeningStorySceneName}.unity";
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            var cameraGo = new GameObject("StoryCamera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            cameraGo.tag = "MainCamera";
            var storyCamera = cameraGo.GetComponent<Camera>();
            storyCamera.clearFlags = CameraClearFlags.SolidColor;
            storyCamera.backgroundColor = new Color(0.012f, 0.014f, 0.022f, 1f);
            storyCamera.orthographic = true;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiPrefabRoot}/BlackScreenStoryPanel.prefab");
            if (prefab == null)
            {
                Debug.LogError("BlackScreenStoryPanel prefab not found.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
            instance.name = "BlackScreenStoryPanel";
            var panel = instance.GetComponent<BasePanel>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("panelId").enumValueIndex = (int)PanelId.BlackScreenStory;
            panelSo.FindProperty("pausesGame").boolValue = true;
            panelSo.FindProperty("startClosed").boolValue = true;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            var registry = canvasGo.AddComponent<ScenePanelRegistry>();
            var registrySo = new SerializedObject(registry);
            var panels = registrySo.FindProperty("panels");
            panels.arraySize = 1;
            panels.GetArrayElementAtIndex(0).objectReferenceValue = panel;
            registrySo.ApplyModifiedPropertiesWithoutUndo();

            var flow = new GameObject("OpeningStoryFlowController", typeof(OpeningStoryFlowController));
            SceneManager.MoveGameObjectToScene(flow, scene);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void ConfigureBuildScenes()
        {
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var openingPath = $"{SceneRoot}/{Stage3Ids.OpeningStorySceneName}.unity";
            var openingGuid = AssetDatabase.AssetPathToGUID(openingPath);
            if (!string.IsNullOrEmpty(openingGuid) && !existing.Exists(s => s.guid.ToString() == openingGuid))
            {
                existing.Add(new EditorBuildSettingsScene(openingPath, true));
            }

            foreach (var d in Corridors)
            {
                var sp = $"{SceneRoot}/{d.SceneId}.unity";
                var g = AssetDatabase.AssetPathToGUID(sp);
                if (string.IsNullOrEmpty(g)) continue;
                if (!existing.Exists(s => s.guid.ToString() == g))
                    existing.Add(new EditorBuildSettingsScene(sp, true));
            }
            EditorBuildSettings.scenes = existing.ToArray();
        }
    }
}
#endif
