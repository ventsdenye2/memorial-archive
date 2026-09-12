"""Drive the real SceneFlowManager and camera in Play Mode; never writes saves."""
import json,subprocess,time
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[2]
def code(value):
    p=subprocess.run([sys.executable,'-X','utf8','Tools/Narrative/unity_mcp.py','tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':value}})],cwd=root,capture_output=True,text=True,encoding='utf-8',check=True)
    r=json.loads(p.stdout)['result']['structuredContent']
    if not r['success']:raise RuntimeError(r)
    return r['data']['result']
prefix='MemorialArchive.Framework.Core.GameRoot.Instance.Context.Events.Publish(new MemorialArchive.Framework.Event.SceneTransitionRequestedEvent('
routes=[('Floor_1F','Floor1_spawn_from_Toilet',139.56),('Floor_2F','Corridor_2F_1_spawn_stairs',3.235),('Floor_2F','Floor2_spawn_from_Room_Office',-7.17),('Floor_2F','Floor2_spawn_from_Room_ArchiveA',26.45),('Floor_2F','Floor2_spawn_from_Room_ArchiveB',70.78),('Floor_2F','Floor2_spawn_from_Room_Reception',128.49),('Floor_3F','Corridor_3F_1_spawn_stairs',3.255),('Floor_3F','Floor3_spawn_from_Room_Director',-2.22),('Floor_3F','Floor3_spawn_from_Room_ArchiveC',23.49),('Floor_3F','Floor3_spawn_from_Room_TreatmentA',73.44),('Floor_3F','Floor3_spawn_from_Room_TreatmentB',103.26),('Room_Terrace','Room_Terrace_spawn_entry',None),('Floor_3F','Floor3_spawn_from_Terrace',-6.5)]
rows=[]
code('Application.runInBackground=true;return true;')
for scene,spawn,x in routes:
    code(prefix+json.dumps(scene)+','+json.dumps(spawn)+')); return true;')
    time.sleep(.5)
    r=code('var p=GameObject.FindGameObjectWithTag("Player"); return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,x=p.transform.position.x};')
    assert r['scene']==scene,(scene,r)
    if x is not None:assert abs(r['x']-x)<.15,(spawn,x,r)
    rows.append(dict(spawn=spawn,**r));print(spawn,'PASS',flush=True)
for scene in ['Floor_1F','Floor_2F','Floor_3F']:
    code('MemorialArchive.Framework.Core.GameRoot.Instance.GetSystem<MemorialArchive.Framework.Scene.SceneFlowManager>().LoadSavedScene('+json.dumps(scene)+',new Vector3(140,-5.2f,0));return true;')
    time.sleep(1)
    r=code('var p=GameObject.FindGameObjectWithTag("Player");var c=Camera.main;return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,playerX=p.transform.position.x,cameraX=c.transform.position.x,right=c.transform.position.x+c.orthographicSize*c.aspect};')
    assert r['playerX']>139 and r['cameraX']>120 and r['right']<=144.05,r
    rows.append(r);print(scene,'far-right camera PASS',flush=True)
(root/'Logs/CorridorQA/runtime.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
