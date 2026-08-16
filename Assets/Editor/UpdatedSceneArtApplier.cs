#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// Applies the current wide room composites and keeps scene bounds in sync with
    /// the imported sprite size. Re-run this after replacing any listed composite.
    /// </summary>
    public static class UpdatedSceneArtApplier
    {
        private const float PixelsPerUnit = 100f;
        private const float RoomHalfHeight = 5.4f;
        private const float WallThickness = 0.3f;

        private sealed class RoomDefinition
        {
            public string ScenePath;
            public string SpritePath;
        }

        private static readonly RoomDefinition[] Rooms =
        {
            new RoomDefinition
            {
                ScenePath = "Assets/Scenes/Room_Office.unity",
                SpritePath = "Assets/Art/Scenes/办公室素材/办公室素材/办公室.png"
            },
            new RoomDefinition
            {
                ScenePath = "Assets/Scenes/Room_ArchiveA.unity",
                SpritePath = "Assets/Art/Scenes/档案室a素材/档案室a素材/档案室a.png"
            },
            new RoomDefinition
            {
                ScenePath = "Assets/Scenes/Room_Director.unity",
                SpritePath = "Assets/Art/Scenes/馆长办公室素材/馆长办公室素材/馆长办公室.png"
            },
            new RoomDefinition
            {
                ScenePath = "Assets/Scenes/Room_Reception.unity",
                SpritePath = "Assets/Art/Scenes/接待室/接待室/接待室.png"
            }
        };

        [MenuItem("Tools/Memorial Archive/Apply Updated Scene Art")]
        public static void Apply()
        {
            AssetDatabase.Refresh();

            foreach (var room in Rooms)
            {
                ConfigureRoomTexture(room.SpritePath);
            }

            foreach (var room in Rooms)
            {
                ApplyRoom(room);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Applied updated art and sprite-derived bounds to {Rooms.Length} room scenes.");
        }

        private static void ConfigureRoomTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Room texture importer was not found: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void ApplyRoom(RoomDefinition room)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(room.SpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Room sprite could not be loaded: {room.SpritePath}");
            }

            var scene = SceneManager.GetSceneByPath(room.ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(room.ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var background = FindInScene(scene, "Background");
                var renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
                if (renderer == null)
                {
                    throw new InvalidOperationException($"{room.ScenePath} does not contain Background/SpriteRenderer.");
                }

                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.size = sprite.bounds.size;
                EditorUtility.SetDirty(renderer);

                var halfWidth = sprite.rect.width / (PixelsPerUnit * 2f);
                var geometry = FindInScene(scene, "RoomGeometry");
                if (geometry == null)
                {
                    throw new InvalidOperationException($"{room.ScenePath} does not contain RoomGeometry.");
                }

                ConfigureConfiner(geometry.transform, halfWidth);
                ConfigureWalls(geometry.transform, halfWidth);
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

        private static void ConfigureConfiner(Transform geometry, float halfWidth)
        {
            var confiner = FindChildRecursive(geometry, "CameraConfiner");
            var polygon = confiner != null ? confiner.GetComponent<PolygonCollider2D>() : null;
            if (polygon == null)
            {
                throw new InvalidOperationException("RoomGeometry does not contain CameraConfiner/PolygonCollider2D.");
            }

            polygon.isTrigger = true;
            polygon.pathCount = 1;
            polygon.SetPath(0, new[]
            {
                new Vector2(-halfWidth, -RoomHalfHeight),
                new Vector2( halfWidth, -RoomHalfHeight),
                new Vector2( halfWidth,  RoomHalfHeight),
                new Vector2(-halfWidth,  RoomHalfHeight)
            });
            EditorUtility.SetDirty(polygon);
        }

        private static void ConfigureWalls(Transform geometry, float halfWidth)
        {
            var horizontalSize = new Vector2(halfWidth * 2f, WallThickness);
            var verticalSize = new Vector2(WallThickness, RoomHalfHeight * 2f);
            var edgeOffset = WallThickness * 0.5f;

            ConfigureWall(geometry, "Wall_Left", new Vector2(-halfWidth - edgeOffset, 0f), verticalSize);
            ConfigureWall(geometry, "Wall_Right", new Vector2(halfWidth + edgeOffset, 0f), verticalSize);
            ConfigureWall(geometry, "Wall_Top", new Vector2(0f, RoomHalfHeight + edgeOffset), horizontalSize);
            ConfigureWall(geometry, "Wall_Bottom", new Vector2(0f, -RoomHalfHeight - edgeOffset), horizontalSize);
        }

        private static void ConfigureWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = FindChildRecursive(parent, name);
            if (wall == null)
            {
                wall = new GameObject(name);
                wall.transform.SetParent(parent, false);
            }

            wall.transform.localPosition = new Vector3(position.x, position.y, 0f);
            var collider = wall.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = wall.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = false;
            collider.size = size;
            collider.offset = Vector2.zero;
            EditorUtility.SetDirty(wall);
            EditorUtility.SetDirty(collider);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }

                var child = FindChildRecursive(root.transform, name);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }

                var nested = FindChildRecursive(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
#endif
