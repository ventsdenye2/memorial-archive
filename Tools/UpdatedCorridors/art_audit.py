"""Read-only inventory of actual visible scene art and its texture provenance."""
import json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CODE=r'''
var rows=new System.Collections.Generic.List<object>();
var sceneNames=new[]{"FrontHall","Floor_1F","Floor_2F","Floor_3F","Floor_4F","Room_Office","Room_Reception","Room_ArchiveA","Room_ArchiveB","Room_ArchiveC","Room_Director","Room_TreatmentA","Room_TreatmentB","Room_Toilet","Room_Terrace","MainMenu","OpeningStory"};
foreach(var name in sceneNames) {
 var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);bool loaded=scene.IsValid()&&scene.isLoaded;
 if(!loaded)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
 try {
 var roots=scene.GetRootGameObjects();
 foreach(var r in roots.SelectMany(g=>g.GetComponentsInChildren<SpriteRenderer>(true))) {
 var hierarchy=r.name;for(var t=r.transform.parent;t!=null;t=t.parent)hierarchy=t.name+"/"+hierarchy;
 rows.Add(new {scene=name,kind="SpriteRenderer",hierarchy,active=r.enabled&&r.gameObject.activeInHierarchy,asset=UnityEditor.AssetDatabase.GetAssetPath(r.sprite),texture=r.sprite==null?"":UnityEditor.AssetDatabase.GetAssetPath(r.sprite.texture),x=r.transform.position.x,y=r.transform.position.y,w=r.bounds.size.x,h=r.bounds.size.y,order=r.sortingOrder});
 }
 foreach(var r in roots.SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Image>(true))) {
 rows.Add(new {scene=name,kind="UI",hierarchy=r.name,active=r.enabled&&r.gameObject.activeInHierarchy,asset=UnityEditor.AssetDatabase.GetAssetPath(r.sprite),texture=r.sprite==null?"":UnityEditor.AssetDatabase.GetAssetPath(r.sprite.texture)});
 }
 foreach(var r in roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))foreach(var m in r.sharedMaterials.Where(m=>m!=null)) {
 rows.Add(new {scene=name,kind="MeshRenderer",hierarchy=r.name,active=r.enabled&&r.gameObject.activeInHierarchy,asset=UnityEditor.AssetDatabase.GetAssetPath(m),texture=UnityEditor.AssetDatabase.GetAssetPath(m.mainTexture)});
 }
 } finally {if(!loaded)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
System.IO.Directory.CreateDirectory("Logs/ArtAudit");
System.IO.File.WriteAllText("Logs/ArtAudit/references.json",Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
return rows.Count;
'''
p=subprocess.run([sys.executable,'-X','utf8','Tools/Narrative/unity_mcp.py','tools/call',json.dumps(dict(name='execute_code',arguments=dict(action='execute',code=CODE)))],cwd=ROOT,capture_output=True,text=True,encoding='utf8',check=True)
r=json.loads(p.stdout)['result']['structuredContent'];assert r['success'],r
rows=json.loads((ROOT/'Logs/ArtAudit/references.json').read_text(encoding='utf8'))
violations={}
for row in rows:
    path=row['texture'] or row['asset']
    if not row['active'] or not path or '/Imported' in path or '8.4' in path:continue
    violations.setdefault(path,dict(kinds=set(),scenes=set(),objects=set()))
    v=violations[path];v['kinds'].add(row['kind']);v['scenes'].add(row['scene']);v['objects'].add(row['hierarchy'])
out={k:{f:sorted(vs) for f,vs in v.items()} for k,v in violations.items()}
(ROOT/'Logs/ArtAudit/outside-approved-folders.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(out,ensure_ascii=False,indent=2))
