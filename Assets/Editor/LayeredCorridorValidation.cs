#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemorialArchive.Framework.Scene;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Camera.View;

namespace MemorialArchive.Editor
{
    public static class LayeredCorridorValidation
    {
        [MenuItem("Tools/Memorial Archive/Validate Layered Corridors")]
        public static void Validate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode first.");
            var original=SceneManager.GetActiveScene().path;
            var report=new List<string>();
            var plan=JsonUtility.FromJson<LayeredCorridorBuilder.Plan>(File.ReadAllText("Tools/UpdatedCorridors/layout.json"));
            try
            {
                foreach(var f in plan.floors)
                {
                    var s=EditorSceneManager.OpenScene($"Assets/Scenes/{f.scene}.unity",OpenSceneMode.Single);
                    var roots=s.GetRootGameObjects();
                    var renderers=roots.SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>()).Where(r=>r.enabled).ToArray();
                    var art=renderers.Where(r=>AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Art/")).ToArray();
                    foreach(var r in art)
                    {
                        var path=AssetDatabase.GetAssetPath(r.sprite);
                        if(!path.Contains("/UpdatedCorridors/"+f.floor+"/Source/") && !r.GetComponentsInParent<Transform>().Any(t=>t.name=="Room_Terrace_enter"))throw new Exception(f.scene+" old art remains: "+path);
                        if(Mathf.Abs(r.transform.lossyScale.x-1)>.001f)throw new Exception(f.scene+" stretched art: "+r.name);
                        if(path.Contains("/Source/") && r.sprite.rect.width>8192 && r.sprite.texture.width<r.sprite.rect.width)throw new Exception("Downscaled wide layer: "+path);
                        if(r.name.StartsWith("组 6") && r.sortingOrder!=40)throw new Exception(f.scene+" columns must occlude actors below darkness overlay");
                    }
                    var bounds=roots.SelectMany(r=>r.GetComponentsInChildren<PolygonCollider2D>()).Single(p=>p.name=="CameraConfiner"&&p.enabled);
                    Physics2D.SyncTransforms();
                    if(Mathf.Abs(bounds.bounds.min.x+9.6f)>.01f || Mathf.Abs(bounds.bounds.max.x-144)>.01f)throw new Exception(f.scene+" wrong bounds "+bounds.bounds);
                    var points=roots.SelectMany(r=>r.GetComponentsInChildren<InteractionPointView>()).ToArray();
                    var spawns=roots.SelectMany(r=>r.GetComponentsInChildren<SceneSpawnPoint>()).ToArray();
                    foreach(var a in f.anchors)
                    {
                        var matches=points.Where(p=>p.InteractionId==a.id).Select(p=>p.transform).Concat(spawns.Where(p=>p.PointId==a.id).Select(p=>p.transform)).ToArray();
                        foreach(var t in matches)if(Mathf.Abs(t.position.x-(f.left+a.x/100))>.01f)throw new Exception(f.scene+" wrong anchor "+a.id);
                    }
                    // Sweep the entire walking lane, including the former right wall at x=67.2.
                    foreach(var hit in Physics2D.RaycastAll(new Vector2(-8,-5.15f),Vector2.right,151))
                        if(!hit.collider.isTrigger && hit.collider.name.StartsWith("Wall") && hit.point.x<143.9f)throw new Exception(f.scene+" blocks walking at "+hit.point);
                    foreach(var cam in roots.SelectMany(r=>r.GetComponentsInChildren<CameraFollowView>()))
                        if(new SerializedObject(cam).FindProperty("cameraBounds").objectReferenceValue!=bounds)throw new Exception(f.scene+" camera bound reference is stale");
                    Capture(f.scene);
                    report.Add(f.scene+": PASS; bounds -9.6..144; Source renderers="+art.Length+"; spawns="+spawns.Length+"; walking lane clear");
                }
                File.WriteAllLines("Logs/CorridorQA/validation.txt",report);
            }
            finally { if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single); }
        }
        static void Capture(string name)
        {
            Directory.CreateDirectory("Logs/CorridorQA");
            var go=new GameObject("Corridor QA Camera");var camera=go.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=5.4f;camera.transform.position=new Vector3(67.2f,0,-20);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var rt=new RenderTexture(3840,270,24);camera.targetTexture=rt;
            var previous=RenderTexture.active;
            try { camera.Render();RenderTexture.active=rt;var image=new Texture2D(3840,270,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,270),0,0);image.Apply();File.WriteAllBytes("Logs/CorridorQA/"+name+"-unity.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image); }
            finally {RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
