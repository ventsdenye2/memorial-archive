"""Render actors behind columns with and without the occluder; no scene changes saved."""
import json,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
source=r'''
var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
var result=new System.Collections.Generic.List<object>();
try {
foreach(var floor in new[]{"1F","2F","3F"}) {
 var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Floor_"+floor+".unity");
 var column=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SpriteRenderer>()).Single(r=>r.enabled&&r.name.StartsWith("组 6"));
 var player=GameObject.FindGameObjectWithTag("Player");
 var x=column.bounds.min.x+.8f;
 player.transform.position=new Vector3(x,-5.2f,0);
 var go=new GameObject("Occlusion QA Camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.4f;camera.transform.position=new Vector3(x,0,-20);
 var rt=new RenderTexture(960,540,24);camera.targetTexture=rt;
 var previous=RenderTexture.active;
 try {
 foreach(var enabled in new[]{false,true}) {
 column.enabled=enabled;camera.Render();RenderTexture.active=rt;
 var image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
 System.IO.File.WriteAllBytes("Logs/CorridorQA/"+floor+(enabled?"-occluded.png":"-unoccluded.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
 }
 result.Add(new {floor,columnOrder=column.sortingOrder,actorOrders=player.GetComponentsInChildren<Renderer>().Select(r=>r.sortingOrder).ToArray()});
 } finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);}
}
} finally {UnityEditor.SceneManagement.EditorSceneManager.OpenScene(original);}
return result;
'''
p=subprocess.run([sys.executable,'-X','utf8','Tools/Narrative/unity_mcp.py','tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':source}})],cwd=root,capture_output=True,text=True,encoding='utf8',check=True)
r=json.loads(p.stdout)['result']['structuredContent']
assert r['success'],r
(root/'Logs/CorridorQA/occlusion.json').write_text(json.dumps(r,indent=2),encoding='utf8')
print(json.dumps(r,indent=2))
