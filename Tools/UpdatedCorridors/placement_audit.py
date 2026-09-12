"""Read-only rendered placement audit, excluding scenes awaiting new art."""
import json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CODE=r'''
if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
var original=UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save scene edits before capture.");
var rows=new System.Collections.Generic.List<object>();
System.IO.Directory.CreateDirectory("Logs/PlacementAudit");
try {
foreach(var name in new[]{"FrontHall","Floor_2F","Floor_3F","Floor_4F","Room_Office","Room_Reception","Room_ArchiveA","Room_Director","Room_TreatmentA","Room_TreatmentB","Room_Terrace"}) {
 var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
 var roots=scene.GetRootGameObjects();
 var bounds=roots.SelectMany(g=>g.GetComponentsInChildren<PolygonCollider2D>()).Single(c=>c.name=="CameraConfiner"&&c.enabled).bounds;
 var points=roots.SelectMany(g=>g.GetComponentsInChildren<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>()).Where(p=>p.isActiveAndEnabled).ToArray();
 var go=new GameObject("Placement Audit Camera");var cam=go.AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
 Action<string,float,float,float,int,int> capture=(file,x,y,size,w,h)=>{
 cam.transform.position=new Vector3(x,y,-20);cam.orthographicSize=size;var rt=new RenderTexture(w,h,24);var previous=RenderTexture.active;cam.targetTexture=rt;
 try {cam.Render();RenderTexture.active=rt;var im=new Texture2D(w,h,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,w,h),0,0);im.Apply();System.IO.File.WriteAllBytes("Logs/PlacementAudit/"+file+".png",im.EncodeToPNG());UnityEngine.Object.DestroyImmediate(im);}
 finally {RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);}
 };
 try {
 int parts=Mathf.CeilToInt(bounds.size.x/38.4f);
 for(int part=0;part<parts;part++) {float left=bounds.min.x+part*38.4f;float width=Mathf.Min(38.4f,bounds.max.x-left);capture(name+"_"+part,left+width/2,0,5.4f,Mathf.RoundToInt(width*50),540);}
 foreach(var p in points) {
 var c=p.GetComponent<Collider2D>();var renders=p.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).Select(r=>new {asset=UnityEditor.AssetDatabase.GetAssetPath(r.sprite),x=r.bounds.center.x,y=r.bounds.center.y,minY=r.bounds.min.y,maxY=r.bounds.max.y,w=r.bounds.size.x,h=r.bounds.size.y}).ToArray();
 rows.Add(new {scene=name,id=p.InteractionId,kind=p.InteractionType.ToString(),x=p.transform.position.x,y=p.transform.position.y,left=bounds.min.x,triggerCoversWalk=c!=null&&c.bounds.min.y<=-5.2f&&c.bounds.max.y>=-5.2f,renders});
 if(p.InteractionType==MemorialArchive.Gameplay.Interaction.Data.InteractionType.SavePoint || p.InteractionType==MemorialArchive.Gameplay.Interaction.Data.InteractionType.NotePickup || (p.InteractionType==MemorialArchive.Gameplay.Interaction.Data.InteractionType.LightSource&&renders.Length>0))
 capture(name+"_"+p.InteractionId,p.transform.position.x,-2.4f,3,800,600);
 }
 }finally {UnityEngine.Object.DestroyImmediate(go);}
}
} finally {UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(original);}
System.IO.File.WriteAllText("Logs/PlacementAudit/points.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));return rows.Count;
'''
p=subprocess.run([sys.executable,'-X','utf8','Tools/Narrative/unity_mcp.py','tools/call',json.dumps(dict(name='execute_code',arguments=dict(action='execute',code=CODE)))],cwd=ROOT,capture_output=True,text=True,encoding='utf8',check=True)
r=json.loads(p.stdout)['result']['structuredContent'];assert r['success'],r
print(r)
