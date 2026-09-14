#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Camera.View;

namespace MemorialArchive.Editor
{
    /// <summary>Targeted room upgrade; preserves interaction IDs, contents, player and UI.</summary>
    public static class UpdatedRoomAssetApplier
    {
        const string Manifest = "Tools/UpdatedRooms/rooms.json";
        static Vector3 Position(JToken p) => new Vector3(-19.2f+(float)p["x"]/100f,5.4f-(float)p["y"]/100f,0);

        [MenuItem("Tools/Memorial Archive/Apply Updated Archive And Toilet Art")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles("Assets/Art/Imported/UpdatedRooms0914","*.png",SearchOption.AllDirectories))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=100;
                importer.maxTextureSize=4096;
                importer.mipmapEnabled=false;
                importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spriteMeshType=SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);importer.SaveAndReimport();
            }
            var interaction=JObject.Parse(File.ReadAllText("Tools/SceneRebuild/interaction_layout.json"));
            var narrative=JArray.Parse(File.ReadAllText("Tools/Narrative/placements.json"));
            foreach(var room in JArray.Parse(File.ReadAllText(Manifest)))
            {
                string name=(string)room["scene"];
                var scene=SceneManager.GetSceneByName(name);bool loaded=scene.IsValid()&&scene.isLoaded;
                if(!loaded) scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive);
                try
                {
                    var roots=scene.GetRootGameObjects();
                    var layout=roots.Single(r=>r.name=="SceneLayout0909").transform;
                    var art=layout.Find("Art");
                    foreach(Transform child in art.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    foreach(var b in room["backgrounds"]) Sprite(art,"Background",(string)b["path"],new Vector3((float)b["x"],0,0),(int)b["order"]);
                    foreach(var l in room["layers"])
                    {
                        var pos=new Vector3(-19.2f+((float)l["x"]+(float)l["width"]/2)/100,5.4f-((float)l["y"]+(float)l["height"]/2)/100,0);
                        Sprite(art,Path.GetFileNameWithoutExtension((string)l["path"]),(string)l["path"],pos,(int)l["order"]);
                    }
                    foreach(var d in room["decor"]) Sprite(art,"DoorArt",(string)d["path"],Position(d),-5);
                    var points=roots.SelectMany(r=>r.GetComponentsInChildren<InteractionPointView>(false)).Where(p=>p.gameObject.activeInHierarchy).ToDictionary(p=>p.InteractionId);
                    foreach(var p in room["points"])
                    {
                        var node=points[(string)p["id"]];node.transform.position=Position(p);
                        if(p["sprite"]!=null)
                        {
                            var renderer=node.GetComponent<SpriteRenderer>();
                            if(renderer==null) renderer=node.gameObject.AddComponent<SpriteRenderer>();
                            renderer.sprite=LoadSprite((string)p["sprite"]);renderer.color=Color.white;renderer.sortingOrder=-5;
                            renderer.drawMode=SpriteDrawMode.Simple;node.transform.localScale=Vector3.one;
                        }
                    }
                    int index=0;
                    foreach(var l in room["lights"]) points["light_"+name.ToLowerInvariant()+"_ordinary_"+(++index).ToString("00")].transform.position=Position(l);
                    foreach(var p in narrative.Where(p=>(string)p["scene"]==name))
                        if(points.TryGetValue((string)p["id"],out var node)) node.transform.position=Position(p);
                    var entries=interaction["scenes"].Single(s=>(string)s["scene"]==name)["points"];
                    foreach(var p in entries)
                    {
                        var node=points[(string)p["id"]];var t=node.transform;
                        t.position=new Vector3((float)p["x"],t.position.y,0);
                        var collider=node.GetComponent<Collider2D>();
                        if(collider is BoxCollider2D box) { box.size=new Vector2((float)p["width"],2.4f);box.offset=new Vector2(0,-4.6f-t.position.y); }
                        else if(collider is CircleCollider2D circle) { circle.radius=(float)p["width"]/2;circle.offset=new Vector2(0,-5.2f-t.position.y); }
                        EditorUtility.SetDirty(node);EditorUtility.SetDirty(t);EditorUtility.SetDirty(collider);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    }
                    float door=-19.2f+(float)room["door_x"]/100;
                    float spawnX=room["spawn_x"]!=null ? -19.2f+(float)room["spawn_x"]/100 : door-1.7f;
                    foreach(var spawn in roots.SelectMany(r=>r.GetComponentsInChildren<SceneSpawnPoint>(false)))
                    { spawn.transform.position=new Vector3(spawnX,-5.2f,0);PrefabUtility.RecordPrefabInstancePropertyModifications(spawn.transform); }
                    var player=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(false)).Single(t=>t.CompareTag("Player"));
                    player.position=new Vector3(spawnX,-5.2f,0);PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                    if(name=="Room_ArchiveC") layout.Find("archive_c_guard").position=new Vector3(8.3f,-5.2f,0);
                    var geometry=layout.Find("Geometry");
                    var bounds=geometry.Find("CameraConfiner").GetComponent<PolygonCollider2D>();
                    bounds.transform.position=Vector3.zero;bounds.SetPath(0,new[]{new Vector2(-19.2f,-5.4f),new Vector2(19.2f,-5.4f),new Vector2(19.2f,5.4f),new Vector2(-19.2f,5.4f)});
                    foreach(var side in new[]{"Left","Right","Top","Bottom"})
                    {
                        var t=geometry.Find("Wall_"+side);bool vertical=side=="Left"||side=="Right";
                        t.position=vertical?new Vector3(side=="Left"?-19.35f:19.35f,0,0):new Vector3(0,side=="Top"?5.55f:-5.55f,0);
                        t.GetComponent<BoxCollider2D>().size=vertical?new Vector2(.3f,10.8f):new Vector2(38.4f,.3f);
                    }
                    foreach(var camera in roots.SelectMany(r=>r.GetComponentsInChildren<CameraFollowView>(false)))
                    { var so=new SerializedObject(camera);so.FindProperty("cameraBounds").objectReferenceValue=bounds;so.ApplyModifiedPropertiesWithoutUndo();camera.transform.position=new Vector3(Mathf.Clamp(spawnX,-9.6f,9.6f),0,-10); }
                    foreach(var confiner in roots.SelectMany(r=>r.GetComponentsInChildren<Cinemachine.CinemachineConfiner2D>(false)))
                    { confiner.m_BoundingShape2D=bounds;confiner.InvalidateCache(); }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally {if(!loaded) EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Updated Archive B, Archive C and Toilet art/layout at native 3840x1080.");
        }
        static Sprite LoadSprite(string path)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null) throw new InvalidOperationException("Missing sprite: "+path);
            return sprite;
        }
        static void Sprite(Transform parent,string name,string path,Vector3 pos,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=pos;
            var r=go.AddComponent<SpriteRenderer>();r.sprite=LoadSprite(path);r.sortingOrder=order;
        }
    }
}
#endif
