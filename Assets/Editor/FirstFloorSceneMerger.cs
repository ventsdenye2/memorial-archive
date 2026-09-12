#if UNITY_EDITOR
using System;
using System.Linq;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Interaction.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Editor
{
    public static class FirstFloorSceneMerger
    {
        public const string SectionName = "FirstFloorCorridor";
        [MenuItem("Tools/Memorial Archive/Merge Front Hall and First Floor")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before merging scenes.");
            var hall=SceneManager.GetSceneByPath("Assets/Scenes/FrontHall.unity");
            if(!hall.IsValid()||!hall.isLoaded)hall=EditorSceneManager.OpenScene("Assets/Scenes/FrontHall.unity",OpenSceneMode.Additive);
            var corridor=SceneManager.GetSceneByPath("Assets/Scenes/Floor_1F.unity");
            bool corridorLoaded=corridor.IsValid()&&corridor.isLoaded;
            if(!corridorLoaded)corridor=EditorSceneManager.OpenScene("Assets/Scenes/Floor_1F.unity",OpenSceneMode.Additive);
            try
            {
                var layout=hall.GetRootGameObjects().Single(g=>g.name=="SceneLayout0909").transform;
                var old=layout.Find(SectionName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                var section=new GameObject(SectionName);SceneManager.MoveGameObjectToScene(section,hall);section.transform.SetParent(layout,false);
                // Copy scene content, retaining all authored identities, but use the hall's one player/UI/camera.
                foreach(var original in corridor.GetRootGameObjects().Where(g=>g.name.StartsWith("Corridor_1F_") || g.name=="SceneLayout0909" || g.name=="NarrativeContent0912"))
                {
                    var copy=UnityEngine.Object.Instantiate(original);copy.name=original.name;SceneManager.MoveGameObjectToScene(copy,hall);
                    copy.transform.SetParent(section.transform,true);
                    foreach(var t in copy.GetComponentsInChildren<Transform>(true).Where(t=>t.CompareTag("Player")).ToArray())
                    {
                        var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                        if(prefab!=null)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                    }
                    foreach(var t in copy.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Geometry" || t.name=="RoomGeometry"))t.gameObject.SetActive(false);
                }
                section.transform.localPosition=new Vector3(FirstFloorSceneLayout.CorridorOffsetX,0,0);
                // Reuse a single column via a sprite rect to cover the different wall trims at the seam.
                const string joinAsset="Assets/Art/FirstFloorJoinColumn.asset";
                var joinSprite=AssetDatabase.LoadAssetAtPath<Sprite>(joinAsset);
                if(joinSprite==null)
                {
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Imported/UpdatedCorridors/1F/Source/组 6 副本.png");
                    joinSprite=Sprite.Create(texture,new Rect(0,0,165,1071),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                    joinSprite.name="FirstFloorJoinColumn";AssetDatabase.CreateAsset(joinSprite,joinAsset);
                }
                var join=new GameObject("JoinColumn");join.transform.SetParent(section.transform,false);join.transform.position=new Vector3(57.6f,.005f,0);
                var renderer=join.AddComponent<SpriteRenderer>();renderer.sprite=joinSprite;renderer.sortingOrder=40;
                foreach(var point in All<InteractionPointView>(hall).Where(p=>p.InteractionId=="FrontHall_to_Floor1" || p.InteractionId=="Floor1_to_FrontHall"))point.gameObject.SetActive(false);
                var geometry=layout.Find("Geometry");const float left=-57.6f,right=211.2f;float width=right-left,center=(left+right)/2;
                var poly=geometry.Find("CameraConfiner").GetComponent<PolygonCollider2D>();poly.transform.position=new Vector3(center,0,0);poly.offset=Vector2.zero;
                poly.SetPath(0,new[]{new Vector2(-width/2,-5.4f),new Vector2(width/2,-5.4f),new Vector2(width/2,5.4f),new Vector2(-width/2,5.4f)});
                Wall(geometry,"Wall_Left",left-.15f,0,.3f,10.8f);Wall(geometry,"Wall_Right",right+.15f,0,.3f,10.8f);
                Wall(geometry,"Wall_Top",center,5.55f,width,.3f);Wall(geometry,"Wall_Bottom",center,-5.55f,width,.3f);
                foreach(var component in All<MonoBehaviour>(hall))
                {
                    if(component==null)continue;var so=new SerializedObject(component);
                    foreach(var field in new[]{"cameraBounds","m_BoundingShape2D"})
                    {var p=so.FindProperty(field);if(p!=null&&p.propertyType==SerializedPropertyType.ObjectReference)p.objectReferenceValue=poly;}
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach(var guid in AssetDatabase.FindAssets("t:InteractionConfig"))
                {
                    var cfg=AssetDatabase.LoadAssetAtPath<InteractionConfig>(AssetDatabase.GUIDToAssetPath(guid));var so=new SerializedObject(cfg);
                    foreach(var field in new[]{"sceneId","transitionSceneId","stairUpSceneId","stairDownSceneId"})
                    {var p=so.FindProperty(field);if(p!=null&&p.stringValue==FirstFloorSceneLayout.LegacySceneName)p.stringValue=FirstFloorSceneLayout.SceneName;}
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                // Retain Floor_1F as an authoring source only. Runtime routes and old saves resolve to FrontHall.
                var buildScenes=EditorBuildSettings.scenes;
                foreach(var entry in buildScenes)if(entry.path=="Assets/Scenes/Floor_1F.unity")entry.enabled=false;
                EditorBuildSettings.scenes=buildScenes;
                EditorSceneManager.MarkSceneDirty(hall);EditorSceneManager.SaveScene(hall);AssetDatabase.SaveAssets();
                SceneManager.SetActiveScene(hall);
                Debug.Log("Merged FrontHall + first-floor corridor: -57.6..211.2, continuous seam at 57.6.");
            }
            finally {if(!corridorLoaded)EditorSceneManager.CloseScene(corridor,true);}
        }
        static T[] All<T>(Scene s) where T:Component => s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        static void Wall(Transform parent,string name,float x,float y,float width,float height)
        {var t=parent.Find(name);t.position=new Vector3(x,y,0);var c=t.GetComponent<BoxCollider2D>();c.size=new Vector2(width,height);c.offset=Vector2.zero;}
    }
}
#endif
