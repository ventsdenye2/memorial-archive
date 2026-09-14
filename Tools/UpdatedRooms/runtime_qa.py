"""Play Mode placement/route checks; uses real SceneFlowManager, never writes saves."""
import json, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def code(cs):
 p=subprocess.run([sys.executable,'-X','utf8','Tools/Narrative/unity_mcp.py','tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':cs}})],cwd=ROOT,capture_output=True,text=True,encoding='utf8',check=True)
 r=json.loads(p.stdout)['result']['structuredContent']
 if not r['success']:raise RuntimeError(r)
 return r['data']['result']
def enter(scene,spawn):
 code('MemorialArchive.Framework.Core.GameRoot.Instance.Context.Events.Publish(new MemorialArchive.Framework.Event.SceneTransitionRequestedEvent('+json.dumps(scene)+','+json.dumps(spawn)+'));return true;')
 time.sleep(.35)
rooms=json.loads((ROOT/'Tools/UpdatedRooms/rooms.json').read_text(encoding='utf8'))
rows=[]
# A player returning from upstairs has cleared the initial FrontHall gate.
# Simulate that milestone in memory; no SaveManager write is performed.
code('Application.runInBackground=true;var progress=(MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem.Progress)MemorialArchive.Framework.Core.GameRoot.Instance.GetSystem<MemorialArchive.Gameplay.Guide.Logic.GuideFlowSystem>().CaptureSaveData();progress.obstacleCleared=true;progress.combatPhase=5;return true;')
for room in rooms:
 n=room['scene'];enter(n,n+'_spawn_entry')
 r=code('var p=GameObject.FindGameObjectWithTag("Player");return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,x=p.transform.position.x,y=p.transform.position.y};')
 assert r['scene']==n,r
 assert abs(r['x']-(-19.2+room['spawn_x']/100))<.05,r
 overlaps=code('var p=GameObject.FindGameObjectWithTag("Player").GetComponent<Collider2D>();Physics2D.SyncTransforms();return UnityEngine.Object.FindObjectsOfType<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>().Where(v=>v.GetComponent<Collider2D>().bounds.Intersects(p.bounds)).Select(v=>v.InteractionId).ToArray();')
 assert not overlaps,(n,'spawn overlaps interaction',overlaps)
 rows.append(dict(check='entry',**r));print(n,'entry PASS',flush=True)
 for x in [-18.5,18.5]:
  code('var p=GameObject.FindGameObjectWithTag("Player");var c=Camera.main.GetComponent<MemorialArchive.Gameplay.Camera.View.CameraFollowView>();c.SetTarget(null);p.transform.position=new Vector3('+str(x)+'f,-5.2f,0);c.SetTarget(p.transform);Physics2D.SyncTransforms();return true;')
  time.sleep(.35)
  r=code('var p=GameObject.FindGameObjectWithTag("Player");var c=Camera.main;return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,playerX=p.transform.position.x,cameraX=c.transform.position.x,left=c.transform.position.x-c.orthographicSize*c.aspect,right=c.transform.position.x+c.orthographicSize*c.aspect};')
  assert abs(r['playerX']-x)<.08 and r['left']>=-19.25 and r['right']<=19.25,r
  assert abs(r['cameraX']-(-9.6 if x<0 else 9.6))<.1,r
  rows.append(dict(check='camera-edge',**r))
 r=code('return UnityEngine.Object.FindObjectsOfType<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>().Select(p=>new {id=p.InteractionId,reachable=p.GetComponent<Collider2D>().OverlapPoint(new Vector2(p.transform.position.x,-5.2f))}).ToArray();')
 assert all(p['reachable'] for p in r),r
 rows.append(dict(check='floor-interaction-overlap',scene=n,points=r));print(n,'camera and floor interaction PASS',flush=True)
 back={'Room_ArchiveB':('Floor_2F','Floor2_spawn_from_Room_ArchiveB',70.78),'Room_ArchiveC':('Floor_3F','Floor3_spawn_from_Room_ArchiveC',23.49),'Room_Toilet':('FrontHall','Floor1_spawn_from_Toilet',206.76)}[n]
 enter(back[0],back[1])
 r=code('var p=GameObject.FindGameObjectWithTag("Player");return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,x=p.transform.position.x};')
 assert r['scene']==back[0] and abs(r['x']-back[2])<.1,r
 rows.append(dict(check='return',**r));print(n,'return PASS',flush=True)
(ROOT/'Logs/UpdatedRooms0914/runtime.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
