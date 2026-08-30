#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Imports the updated wide corridor exports and swaps the four Background
    /// renderers in each floor scene. The source exports remain under Source for
    /// later layered-art work; the generated 3840x1080 slices are the runtime refs.
    /// </summary>
    public static class UpdatedCorridorAssetApplier
    {
        private const string Root = "Assets/Art/Imported/UpdatedCorridors";
        private const float PixelsPerUnit = 100f;
        private const float SegmentWidth = 38.4f;
        private const float SegmentHalfWidth = SegmentWidth * 0.5f;
        private const float SegmentHeight = 10.8f;
        private const string SceneRoot = "Assets/Scenes";

        private static readonly (string floor, string scene)[] Floors =
        {
            ("1F", "Floor_1F"),
            ("2F", "Floor_2F"),
            ("3F", "Floor_3F"),
        };

        [MenuItem("Tools/Memorial Archive/Import Updated Corridor Assets")]
        public static void Apply()
        {
            AssetDatabase.Refresh();

            foreach (var floor in Floors)
            {
                for (var i = 1; i <= 4; i++)
                {
                    ConfigureSlice($"{Root}/{floor.floor}/Background/Background_{i}.png");
                }
            }

            foreach (var floor in Floors)
            {
                ApplyFloor(floor.floor, floor.scene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Imported updated corridor assets and replaced Floor_1F/2F/3F Background references.");
        }

        private static void ConfigureSlice(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer not found: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static void ApplyFloor(string floor, string sceneName)
        {
            var scenePath = $"{SceneRoot}/{sceneName}.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                for (var i = 1; i <= 4; i++)
                {
                    var root = FindInScene(scene, $"Corridor_{floor}_{i}Root");
                    if (root == null)
                    {
                        throw new InvalidOperationException($"{sceneName} is missing Corridor_{floor}_{i}Root.");
                    }

                    var background = FindChild(root.transform, "Background");
                    var renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
                    if (renderer == null)
                    {
                        throw new InvalidOperationException($"{sceneName}/Corridor_{floor}_{i}Root/Background has no SpriteRenderer.");
                    }

                    var path = $"{Root}/{floor}/Background/Background_{i}.png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        throw new InvalidOperationException($"Updated corridor slice could not be loaded: {path}");
                    }

                    renderer.sprite = sprite;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.size = sprite.bounds.size;
                    EditorUtility.SetDirty(renderer);
                }

                ReplacePropReferences(scene, floor);
                ExpandCorridorLayout(scene, floor);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ReplacePropReferences(Scene scene, string floor)
        {
            var sourceFolder = $"{Root}/{floor}/Source";
            ConfigureSourceSprites(sourceFolder);

            // The supplied packs contain updated cut-outs for the props that are
            // already present in these scenes. Match by the old rendered size so
            // duplicated Chinese export names cannot select the wrong object.
            if (floor == "1F")
            {
                ReplaceByDimensions(scene, 559, 593, FindSourceSprite(sourceFolder, 559, 593));
                ReplaceByDimensions(scene, 264, 556, FindSourceSprite(sourceFolder, 264, 556));
            }
            else if (floor == "2F")
            {
                ReplaceByDimensions(scene, 559, 593, FindSourceSprite(sourceFolder, 559, 593));
                ReplaceByDimensions(scene, 308, 528, FindSourceSprite(sourceFolder, 308, 528));
                ReplaceByDimensions(scene, 103, 584, FindSourceSprite(sourceFolder, 120, 613));
            }
            else if (floor == "3F")
            {
                ReplaceByDimensions(scene, 559, 593, FindSourceSprite(sourceFolder, 559, 593));
                ReplaceByDimensions(scene, 308, 528, FindSourceSprite(sourceFolder, 308, 528));
            }
        }

        private static void ConfigureSourceSprites(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 16384;
                importer.SaveAndReimport();
            }
        }

        private static Sprite FindSourceSprite(string folder, int width, int height)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null && texture.width == width && texture.height == height)
                {
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }

            return null;
        }

        private static void ReplaceByDimensions(Scene scene, int oldWidth, int oldHeight, Sprite replacement)
        {
            if (replacement == null) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var current = renderer.sprite;
                    if (current == null || current.texture.width != oldWidth || current.texture.height != oldHeight) continue;
                    renderer.sprite = replacement;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.size = replacement.bounds.size;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private static void ExpandCorridorLayout(Scene scene, string floor)
        {
            var roots = new GameObject[4];
            for (var i = 1; i <= 4; i++)
            {
                roots[i - 1] = FindInScene(scene, $"Corridor_{floor}_{i}Root");
                if (roots[i - 1] == null) throw new InvalidOperationException($"Missing Corridor_{floor}_{i}Root.");
            }

            // The previous layout used 19.2-wide segments. Scale child placements
            // once when upgrading so props, triggers and light regions follow the
            // doubled-width art. The spacing check makes this operation idempotent.
            var needsChildScale = Mathf.Abs(roots[1].transform.localPosition.x - 19.2f) < 0.1f;
            if (needsChildScale)
            {
                foreach (var root in roots)
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == root.transform) continue;
                        var p = child.localPosition;
                        child.localPosition = new Vector3(p.x * 2f, p.y, p.z);
                    }
                }
            }

            for (var i = 0; i < roots.Length; i++)
            {
                roots[i].transform.localPosition = new Vector3(i * SegmentWidth, 0f, 0f);
                ConfigureGeometry(roots[i].transform);

                var interactionRoot = FindChild(roots[i].transform, "InteractionPoints");
                if (interactionRoot != null)
                {
                    foreach (Transform child in interactionRoot.transform)
                    {
                        if (child.name.Contains("exit_left")) child.localPosition = new Vector3(-SegmentHalfWidth + 0.4f, child.localPosition.y, child.localPosition.z);
                        else if (child.name.Contains("exit_right")) child.localPosition = new Vector3(SegmentHalfWidth - 0.4f, child.localPosition.y, child.localPosition.z);
                    }
                }

                var spawnRoot = FindChild(roots[i].transform, "SpawnPoints");
                if (spawnRoot != null)
                {
                    foreach (Transform child in spawnRoot.transform)
                    {
                        var p = child.localPosition;
                        if (child.name.EndsWith("_spawn_left") || child.name.EndsWith("_spawn_initial")) p.x = -16.8f;
                        else if (child.name.EndsWith("_spawn_right")) p.x = 16.8f;
                        child.localPosition = p;
                    }
                }
            }
        }

        private static void ConfigureGeometry(Transform root)
        {
            var geometry = FindChild(root, "RoomGeometry");
            if (geometry == null) return;

            var confiner = FindChild(geometry.transform, "CameraConfiner");
            var polygon = confiner == null ? null : confiner.GetComponent<PolygonCollider2D>();
            if (polygon != null)
            {
                polygon.isTrigger = true;
                polygon.pathCount = 1;
                polygon.SetPath(0, new[]
                {
                    new Vector2(-SegmentHalfWidth, -SegmentHeight * 0.5f),
                    new Vector2( SegmentHalfWidth, -SegmentHeight * 0.5f),
                    new Vector2( SegmentHalfWidth,  SegmentHeight * 0.5f),
                    new Vector2(-SegmentHalfWidth,  SegmentHeight * 0.5f)
                });
                EditorUtility.SetDirty(polygon);
            }

            SetWall(geometry.transform, "Wall_Left", new Vector2(-SegmentHalfWidth - 0.15f, 0f), new Vector2(0.3f, SegmentHeight));
            SetWall(geometry.transform, "Wall_Right", new Vector2(SegmentHalfWidth + 0.15f, 0f), new Vector2(0.3f, SegmentHeight));
            SetWall(geometry.transform, "Wall_Top", new Vector2(0f, SegmentHeight * 0.5f + 0.15f), new Vector2(SegmentWidth, 0.3f));
            SetWall(geometry.transform, "Wall_Bottom", new Vector2(0f, -SegmentHeight * 0.5f - 0.15f), new Vector2(SegmentWidth, 0.3f));
        }

        private static void SetWall(Transform geometry, string name, Vector2 position, Vector2 size)
        {
            var wall = FindChild(geometry, name);
            if (wall == null) return;
            wall.transform.localPosition = new Vector3(position.x, position.y, wall.transform.localPosition.z);
            var collider = wall.GetComponent<BoxCollider2D>();
            if (collider == null) collider = wall.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            EditorUtility.SetDirty(collider);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var nested = FindChild(root.transform, name);
                if (nested != null) return nested;
            }

            return null;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child.gameObject;
                var nested = FindChild(child, name);
                if (nested != null) return nested;
            }

            return null;
        }
    }
}
#endif
