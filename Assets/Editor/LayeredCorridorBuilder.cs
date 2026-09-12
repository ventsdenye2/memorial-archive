#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Lighting.View;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Monster.View;

namespace MemorialArchive.Editor
{
    /// <summary>Native Source layers and reviewed semantic anchors; safe to reapply.</summary>
    public static class LayeredCorridorBuilder
    {
        [Serializable] public class Plan { public Floor[] floors; }
        [Serializable] public class Floor { public string floor, scene; public float width,height,left; public Layer[] layers; public Anchor[] anchors; public Anchor[] lights; }
        [Serializable] public class Layer { public string path; public float x,y,width,height; public int order; public float[] tint; }
        [Serializable] public class Anchor { public string id; public float x,y; }
        const string Generated = "UpdatedCorridorLayers0912";
        static T[] All<T>(Scene s) where T:Component => s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        static void StringField(UnityEngine.Object o,string key,string value) { var so=new SerializedObject(o); so.FindProperty(key).stringValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static Vector3 Position(Floor f,float x,float y) => new Vector3(f.left+x/100f,5.4f-y/100f,0);
        static GameObject Child(string name,Transform parent) { var go=new GameObject(name); go.transform.SetParent(parent,false); return go; }
        static void Trigger(InteractionPointView point)
        {
            var box=point.GetComponent<BoxCollider2D>();
            if(box==null) box=point.gameObject.AddComponent<BoxCollider2D>();
            box.enabled=true; box.isTrigger=true; box.size=new Vector2(1.2f,4.8f);
            box.offset=new Vector2(0,-3.4f-point.transform.position.y);
        }
        [MenuItem("Tools/Memorial Archive/Rebuild Layered Corridors")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("Tools/UpdatedCorridors/layout.json"));
            foreach(var path in plan.floors.SelectMany(f=>f.layers).Select(l=>l.path).Distinct())
            {
                var ti=AssetImporter.GetAtPath(path) as TextureImporter;
                if(ti==null) throw new FileNotFoundException(path);
                ti.textureType=TextureImporterType.Sprite; ti.spriteImportMode=SpriteImportMode.Single;
                ti.spritePixelsPerUnit=100; ti.maxTextureSize=16384; ti.mipmapEnabled=false;
                ti.alphaIsTransparency=true;
                var settings=new TextureImporterSettings(); ti.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Center; settings.spriteMeshType=SpriteMeshType.FullRect; ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
            foreach(var floor in plan.floors) ApplyFloor(floor);
            FirstFloorSceneMerger.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("Layered corridors rebuilt: 1F-3F, 15360 pixels, Source PNGs, geometry and semantic anchors.");
        }
        static void ApplyFloor(Floor f)
        {
            var path=$"Assets/Scenes/{f.scene}.unity";
            var s=SceneManager.GetSceneByPath(path); bool loaded=s.IsValid()&&s.isLoaded;
            if(!loaded) s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var layout=s.GetRootGameObjects().Single(r=>r.name=="SceneLayout0909").transform;
                var previous=layout.Find(Generated); if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var oldArt=layout.Find("Art"); if(oldArt!=null) oldArt.gameObject.SetActive(false);
                foreach(var r in All<SpriteRenderer>(s))
                {
                    if(r.sprite==null) continue;
                    string asset=AssetDatabase.GetAssetPath(r.sprite);
                    if(asset.StartsWith("Assets/Art/") && !r.GetComponentsInParent<Transform>(true).Any(t=>t.name=="Room_Terrace_enter")) r.enabled=false;
                }
                var art=Child(Generated,layout).transform;
                foreach(var l in f.layers)
                {
                    var go=Child(Path.GetFileNameWithoutExtension(l.path),art);
                    go.transform.position=Position(f,l.x+l.width/2,l.y+l.height/2);
                    var r=go.AddComponent<SpriteRenderer>(); r.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(l.path);
                    if(r.sprite==null) throw new InvalidOperationException("Missing sprite "+l.path);
                    r.sortingOrder=l.order; r.color=new Color(l.tint[0],l.tint[1],l.tint[2],l.tint[3]);
                    if(f.floor=="2F" && l.path.EndsWith("图层 8 副本 3.png"))
                    {
                        var point=All<InteractionPointView>(s).First(p=>p.InteractionId=="light_floor_2f_special"&&p.gameObject.activeInHierarchy);
                        var live=point.GetComponent<SpriteRenderer>(); if(live==null)live=point.gameObject.AddComponent<SpriteRenderer>();
                        live.sprite=r.sprite; live.enabled=true; live.sortingOrder=l.order; live.color=Color.white;
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
                foreach(var a in f.anchors)
                {
                    foreach(var p in All<InteractionPointView>(s).Where(p=>p.InteractionId==a.id)) {p.transform.position=Position(f,a.x,a.y); if(p.gameObject.activeInHierarchy)Trigger(p);}
                    foreach(var p in All<SceneSpawnPoint>(s).Where(p=>p.PointId==a.id)) p.transform.position=Position(f,a.x,a.y);
                }
                var geometry=layout.Find("Geometry"); float w=f.width/100,right=f.left+w,cx=f.left+w/2;
                var poly=geometry.Find("CameraConfiner").GetComponent<PolygonCollider2D>();
                poly.transform.position=new Vector3(cx,0,0); poly.offset=Vector2.zero;
                poly.SetPath(0,new[]{new Vector2(-w/2,-5.4f),new Vector2(w/2,-5.4f),new Vector2(w/2,5.4f),new Vector2(-w/2,5.4f)});
                foreach(var component in All<MonoBehaviour>(s))
                {
                    if(component==null)continue;
                    var so=new SerializedObject(component);
                    foreach(var field in new[]{"cameraBounds","m_BoundingShape2D"})
                    {
                        var property=so.FindProperty(field);
                        if(property!=null && property.propertyType==SerializedPropertyType.ObjectReference)property.objectReferenceValue=poly;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Wall(geometry,"Wall_Left",f.left-.15f,0,.3f,10.8f); Wall(geometry,"Wall_Right",right+.15f,0,.3f,10.8f);
                Wall(geometry,"Wall_Top",cx,5.55f,w,.3f); Wall(geometry,"Wall_Bottom",cx,-5.55f,w,.3f);
                foreach(var monster in All<MonsterSpawnPointView>(s).Where(m=>m.gameObject.activeInHierarchy)) monster.transform.position=Position(f,12000,1060);
                Lights(s,layout,f);
                EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s);
            }
            finally { if(!loaded)EditorSceneManager.CloseScene(s,true); }
        }
        static void Wall(Transform geometry,string name,float x,float y,float w,float h)
        {
            var t=geometry.Find(name); t.position=new Vector3(x,y,0);
            var box=t.GetComponent<BoxCollider2D>(); box.size=new Vector2(w,h); box.offset=Vector2.zero;
        }
        static void Lights(Scene s,Transform layout,Floor f)
        {
            foreach(var light in All<LightSourceView>(s))
            {
                var id=new SerializedObject(light).FindProperty("lightId").stringValue;
                if(id=="light_floor_2f_special" && light.transform.IsChildOf(layout))continue;
                light.gameObject.SetActive(false);
            }
            var old=layout.Find("NormalLights");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var parent=Child("NormalLights",layout).transform;
            var db=AssetDatabase.LoadAssetAtPath<GameConfigDatabase>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameConfigDatabase")[0]));
            var dbso=new SerializedObject(db); var list=dbso.FindProperty("lightSources");
            for(int i=0;i<f.lights.Length;i++)
            {
                var id=$"light_floor_{f.floor.ToLowerInvariant()}_ordinary_{i+1:00}";
                var go=Child(id,parent);go.transform.position=Position(f,f.lights[i].x,f.lights[i].y);
                StringField(go.AddComponent<LightSourceView>(),"lightId",id);
                var point=go.AddComponent<InteractionPointView>();var so=new SerializedObject(point);
                so.FindProperty("interactionId").stringValue=id;so.FindProperty("interactionType").intValue=11;so.ApplyModifiedPropertiesWithoutUndo();Trigger(point);
                string path=$"Assets/GameConfigs/SceneLayout/Lights/{id}.asset";
                var cfg=AssetDatabase.LoadAssetAtPath<LightSourceConfig>(path);
                if(cfg==null){cfg=ScriptableObject.CreateInstance<LightSourceConfig>();AssetDatabase.CreateAsset(cfg,path);}
                so=new SerializedObject(cfg);so.FindProperty("lightId").stringValue=id;so.FindProperty("regionId").stringValue="corridor_"+f.floor.ToLowerInvariant();
                so.FindProperty("radius").floatValue=8;so.FindProperty("isSpecial").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
                bool found=false;for(int n=0;n<list.arraySize;n++)if(list.GetArrayElementAtIndex(n).objectReferenceValue==cfg)found=true;
                if(!found){int n=list.arraySize;list.InsertArrayElementAtIndex(n);list.GetArrayElementAtIndex(n).objectReferenceValue=cfg;}
            }
            dbso.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
