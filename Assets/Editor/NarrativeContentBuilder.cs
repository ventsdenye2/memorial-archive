using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Interaction.Config;
using MemorialArchive.Gameplay.Interaction.Data;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Story.Data;
using MemorialArchive.Gameplay.Story.View;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MemorialArchive.Editor
{
    public static class NarrativeContentBuilder
    {
        private const string Ui = "Assets/Art/UI/Imported_UI2.0/UI2.0/自白页/";
        private const string TerraceArt = "Assets/Art/Imported/SceneRebuild0909/露台素材/";
        private const string NarrativePrefab = "Assets/Prefabs/UI/NarrativePanel.prefab";
        private static readonly string[] Scenes = { "FrontHall", "Floor_1F", "Floor_2F", "Floor_3F", "Floor_4F", "Room_Office", "Room_Reception", "Room_ArchiveA", "Room_ArchiveB", "Room_ArchiveC", "Room_TreatmentA", "Room_TreatmentB", "Room_Director", "Room_Toilet", "Room_Terrace" };
        [Serializable] private sealed class Placement { public string id, scene, title; public float x, y; }
        [Serializable] private sealed class PlacementList { public Placement[] items; }
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("c506da8782f548b28383ac9de840f310"));

        [MenuItem("Tools/Memorial Archive/Apply Narrative Content")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying narrative content.");
            var content = NarrativeContent.Load();
            if (content.notes.Length != 16) throw new InvalidOperationException("Import the reviewed story document first.");
            var placements = JsonUtility.FromJson<PlacementList>("{\"items\":" + File.ReadAllText("Tools/Narrative/placements.json") + "}").items;
            BuildDiary();
            BuildNarrative();
            var configs = LoadConfigs();
            var usedConfigs = new List<InteractionConfig>();
            foreach (var sceneName in Scenes)
            {
                var path = "Assets/Scenes/" + sceneName + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                var alreadyLoaded = scene.IsValid() && scene.isLoaded;
                if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var root = FindOrCreateRoot(scene, "NarrativeContent0912");
                    var activePoints = Components<InteractionPointView>(scene).Where(p => p.gameObject.activeInHierarchy).ToArray();
                    var usedPoints = new HashSet<InteractionPointView>();
                    foreach (var placement in placements.Where(p => p.scene == sceneName))
                    {
                        var note = content.FindNote(placement.id);
                        var point = activePoints.FirstOrDefault(p => p.InteractionType == InteractionType.NotePickup && configs.TryGetValue(p.InteractionId, out var c) && c.NoteId == note.id);
                        if (point == null) point = activePoints.FirstOrDefault(p => p.InteractionId == note.id);
                        if (point == null)
                        {
                            var left = sceneName == "FrontHall" ? -57.6f : sceneName == "Room_ArchiveB" || sceneName == "Room_ArchiveC" ? -9.6f : -19.2f;
                            point = Point(root.transform, note.id, InteractionType.NotePickup, new Vector2(left + placement.x / 100f, 5.4f - placement.y / 100f));
                            var renderer = Ensure<SpriteRenderer>(point.gameObject);
                            renderer.sprite = Sprite(TerraceArt + "线索 副本.png");
                            renderer.sortingOrder = -5;
                        }
                        ConfigurePoint(point, point.InteractionId, InteractionType.NotePickup);
                        var config = Config(configs, point.InteractionId, sceneName, note.title, InteractionType.NotePickup);
                        Set(config, "noteId", note.id);
                        usedConfigs.Add(config);
                        usedPoints.Add(point);
                    }
                    // Authored document has no text for the old corridor/reception placeholders.
                    foreach (var point in activePoints)
                        if (point.InteractionType == InteractionType.NotePickup && !usedPoints.Contains(point)) point.gameObject.SetActive(false);

                    if (sceneName == "Room_Reception") CreateCecil(root.transform, configs, usedConfigs);
                    if (sceneName == "Room_Director")
                        foreach (var point in activePoints.Where(p => p.InteractionId == "Room_Terrace_enter")) point.gameObject.SetActive(false);
                    if (sceneName == "Floor_3F")
                    {
                        var point = Point(root.transform, "Room_Terrace_enter", InteractionType.SceneExit, new Vector2(-8.1f, -3.7f));
                        var c = Config(configs, point.InteractionId, sceneName, "前往露台", InteractionType.SceneExit);
                        Set(c, "transitionSceneId", "Room_Terrace"); Set(c, "transitionSpawnPointId", "Room_Terrace_spawn_entry");
                        Set(c, "requiresConfirmation", true); Set(c, "confirmationMessage", "前往露台？"); usedConfigs.Add(c);
                        Spawn(root.transform, "Floor3_spawn_from_Terrace", new Vector2(-6.5f, -5.2f));
                        var sign = Ensure<SpriteRenderer>(point.gameObject);
                        sign.sprite = Sprite("Assets/Art/Imported/UpdatedCorridors/2F/Source/门 副本.png"); sign.sortingOrder = -6;
                    }
                    if (sceneName == "Room_Terrace")
                    {
                        ApplyTerraceLayers(scene);
                        var point = activePoints.FirstOrDefault(p => p.InteractionId == "Room_Terrace_return") ?? Point(root.transform, "Room_Terrace_return", InteractionType.SceneExit, new Vector2(17.3f, -3.7f));
                        var c = Config(configs, point.InteractionId, sceneName, "返回三层走廊", InteractionType.SceneExit);
                        Set(c, "transitionSceneId", "Floor_3F"); Set(c, "transitionSpawnPointId", "Floor3_spawn_from_Terrace");
                        Set(c, "requiresConfirmation", true); Set(c, "confirmationMessage", "返回三层走廊？"); usedConfigs.Add(c);
                    }
                    AddNarrativePanel(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            RegisterConfigs(usedConfigs);
            AssetDatabase.SaveAssets();
            Debug.Log("NARRATIVE_AUTHORING_PASS: 16 notes, scene dialogue, Cecil, third-floor terrace.");
        }
        private static Dictionary<string, InteractionConfig> LoadConfigs() => AssetDatabase.FindAssets("t:InteractionConfig").Select(g => AssetDatabase.LoadAssetAtPath<InteractionConfig>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).GroupBy(c => c.InteractionId).ToDictionary(g => g.Key, g => g.First());
        private static InteractionConfig Config(Dictionary<string, InteractionConfig> configs, string id, string scene, string title, InteractionType type)
        {
            if (!configs.TryGetValue(id, out var config))
            {
                Directory.CreateDirectory("Assets/GameConfigs/Narrative");
                config = ScriptableObject.CreateInstance<InteractionConfig>();
                AssetDatabase.CreateAsset(config, "Assets/GameConfigs/Narrative/" + id + ".asset"); configs[id] = config;
            }
            Set(config, "interactionId", id); Set(config, "interactionType", (int)type); Set(config, "sceneId", scene); Set(config, "displayName", title);
            return config;
        }
        private static void RegisterConfigs(List<InteractionConfig> additions)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            var values = db.Interactions.Where(c => c != null).ToDictionary(c => c.InteractionId, c => c);
            foreach (var c in additions) values[c.InteractionId] = c;
            var so = new SerializedObject(db); var array = so.FindProperty("interactions"); array.arraySize = values.Count;
            var i = 0; foreach (var c in values.Values) array.GetArrayElementAtIndex(i++).objectReferenceValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void BuildDiary()
        {
            const string path = "Assets/Prefabs/UI/DiaryPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var content = root.transform.Find("ReadingContent"); if (content != null) Object.DestroyImmediate(content.gameObject);
                var rect = Rect(root.transform, "ReadingContent", new Vector2(-5, 0), new Vector2(600, 870));
                var ink = new Color32(0x4f, 0x3e, 0x37, 255);
                var title = Text(rect, "Title", new Vector2(0, 310), new Vector2(410, 52), 24, ink); title.alignment = TextAnchor.MiddleLeft;
                var body = Text(rect, "Body", new Vector2(0, -30), new Vector2(410, 610), 20, new Color32(0x59, 0x50, 0x4c, 255));
                var readingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/FZCHSJW.TTF");
                if (readingFont == null) throw new InvalidOperationException("Missing supplied FZCHSJW.TTF font.");
                title.font = body.font = readingFont;
                body.lineSpacing = 28f / (readingFont.lineHeight * 20f / readingFont.fontSize);
                title.gameObject.AddComponent<ReadingTitleTracking>();
                var page = Text(rect, "Page", new Vector2(0, -355), new Vector2(410, 32), 18, ink); page.font = readingFont; page.alignment = TextAnchor.MiddleCenter;
                var panel = root.GetComponent<DiaryPanel>(); Set(panel, "titleText", title); Set(panel, "bodyText", body); Set(panel, "pageText", page);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void BuildNarrative()
        {
            var go = new GameObject("NarrativePanel", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Stretch((RectTransform)go.transform);
                var canvas = go.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 1000;
                go.AddComponent<GraphicRaycaster>();
                var panel = go.AddComponent<NarrativePanel>(); Set(panel, "panelId", (int)PanelId.Narrative); Set(panel, "startClosed", true); Set(panel, "canvasGroup", go.GetComponent<CanvasGroup>());
                var frame = Rect(go.transform, "Frame", new Vector2(10, 123), new Vector2(1545, 242)); frame.anchorMin = frame.anchorMax = new Vector2(.5f, 0f);
                var image = frame.gameObject.AddComponent<Image>(); image.sprite = Sprite(Ui + "话框.png"); image.raycastTarget = true;
                var text = Text(frame, "Text", new Vector2(40, -8), new Vector2(1360, 110), 20, new Color32(0xe3, 0xd7, 0xb2, 255));
                text.lineSpacing = 40f / (text.font.lineHeight * 20f / text.font.fontSize);
                text.gameObject.AddComponent<ReadingTitleTracking>();
                var hint = Rect(frame, "Continue", new Vector2(605, -80), new Vector2(221, 76)); hint.gameObject.AddComponent<Image>().sprite = Sprite(Ui + "按任意键继续.png");
                var choices = new Button[2];
                for (var i = 0; i < 2; i++)
                {
                    var r = Rect(go.transform, "Choice" + i, new Vector2(100, 370 - i * 80), new Vector2(1150, 68)); r.anchorMin = r.anchorMax = new Vector2(.5f, 0f);
                    var bg = r.gameObject.AddComponent<Image>(); bg.color = new Color(.2f, .13f, .09f, .97f);
                    var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
                    var label = Text(r, "Label", Vector2.zero, new Vector2(1080, 58), 25, new Color(.94f,.86f,.68f)); label.alignment = TextAnchor.MiddleLeft;
                    choices[i] = button;
                }
                Set(panel, "dialogueText", text); Set(panel, "continueHint", hint.gameObject);
                var so = new SerializedObject(panel); var array = so.FindProperty("choiceButtons"); array.arraySize = 2;
                for (var i = 0; i < 2; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = choices[i]; so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, NarrativePrefab);
            }
            finally { Object.DestroyImmediate(go); }
        }
        private static void AddNarrativePanel(Scene scene)
        {
            var diary = Components<DiaryPanel>(scene).FirstOrDefault();
            if (diary == null) throw new InvalidOperationException(scene.name + " has no diary panel");
            var old = Components<NarrativePanel>(scene).FirstOrDefault();
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NarrativePrefab), diary.transform.parent);
            var registry = Components<ScenePanelRegistry>(scene).First();
            var so = new SerializedObject(registry); var array = so.FindProperty("panels");
            var values = registry.Panels.Where(p => p != null && p.PanelId != PanelId.Narrative).ToList(); values.Add(go.GetComponent<NarrativePanel>());
            array.arraySize = values.Count; for (var i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void CreateCecil(Transform parent, Dictionary<string, InteractionConfig> configs, List<InteractionConfig> used)
        {
            var point = Point(parent, "Cecil", InteractionType.Npc, new Vector2(-5f, -4.5f));
            var existing = point.transform.Find("CecilArt"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var data = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { "Assets/Art/Characters/Cecil" }).Select(g => AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (data == null) throw new InvalidOperationException("Cecil Spine import has not finished. Refresh and retry.");
            var skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
            skeleton.name = "CecilArt"; skeleton.transform.SetParent(point.transform, false); skeleton.transform.localScale = Vector3.one * .45f;
            skeleton.AnimationName = "Cecil_idle"; skeleton.loop = true; skeleton.GetComponent<MeshRenderer>().sortingOrder = 12;
            var view = Ensure<CecilView>(point.gameObject); Set(view, "skeleton", skeleton);
            used.Add(Config(configs, "Cecil", "Room_Reception", "塞西尔", InteractionType.Npc));
        }
        private static void ApplyTerraceLayers(Scene scene)
        {
            var layout = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).First(t => t.name == "SceneLayout0909");
            foreach (var renderer in layout.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                var path = AssetDatabase.GetAssetPath(renderer.sprite);
                if (renderer.name.Contains("light_room_terrace_special")) renderer.sprite = Sprite(TerraceArt + "图层 8 副本.png");
                if (path == TerraceArt + "露台.png") renderer.gameObject.SetActive(false);
                if (path == TerraceArt + "图层 1 副本.png") renderer.transform.localScale = Vector3.one;
            }
            var expected = new[] { "纸张.png", "组 2.png", "图层 1 副本.png" };
            for (var i = 0; i < expected.Length; i++)
            {
                var path = TerraceArt + expected[i];
                var renderer = layout.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => AssetDatabase.GetAssetPath(r.sprite) == path);
                if (renderer == null)
                {
                    var obj = new GameObject("TerraceLayer" + i); obj.transform.SetParent(layout, false); renderer = obj.AddComponent<SpriteRenderer>();
                }
                renderer.gameObject.SetActive(true); renderer.sprite = Sprite(path); renderer.sortingOrder = -12 + i * 2;
                renderer.transform.position = Vector3.zero; renderer.transform.localScale = Vector3.one;
            }
        }
        private static InteractionPointView Point(Transform parent, string id, InteractionType type, Vector2 position)
        {
            var child = parent.Find(id); var go = child != null ? child.gameObject : new GameObject(id);
            go.transform.SetParent(parent, false); go.transform.position = position; go.SetActive(true);
            var collider = Ensure<BoxCollider2D>(go); collider.isTrigger = true;
            var point = Ensure<InteractionPointView>(go); ConfigurePoint(point, id, type);
            collider.size = new Vector2(1.6f, 2.8f); collider.offset = new Vector2(0, -4.5f - position.y);
            return point;
        }
        private static void ConfigurePoint(InteractionPointView point, string id, InteractionType type) { Set(point, "interactionId", id); Set(point, "interactionType", (int)type); }
        private static void Spawn(Transform parent, string id, Vector2 position)
        {
            var t = parent.Find(id); var go = t != null ? t.gameObject : new GameObject(id); go.transform.SetParent(parent, false); go.transform.position = position;
            var spawn = Ensure<SceneSpawnPoint>(go); Set(spawn, "pointId", id);
        }
        private static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (root == null) { root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); } return root;
        }
        private static IEnumerable<T> Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
        private static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Text Text(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var t = Rect(parent, name, position, size).gameObject.AddComponent<Text>(); t.font = Font; t.fontSize = fontSize; t.color = color; t.alignment = TextAnchor.UpperLeft; t.supportRichText = false; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; return t;
        }
        private static Sprite Sprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Missing sprite " + path);
        }
        private static void Set(Object target, string name, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(name) ?? throw new InvalidOperationException(target.name + " missing " + name);
            if (value is string s) p.stringValue = s; else if (value is bool b) p.boolValue = b; else if (value is int i) p.intValue = i; else p.objectReferenceValue = value as Object;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
    }
}
