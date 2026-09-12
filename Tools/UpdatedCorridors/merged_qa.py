"""Validate continuous traversal and pre-merge save/return compatibility in Unity."""
import json,subprocess,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def call(name,**args):
    p=subprocess.run([sys.executable,'-X','utf8','Tools/Narrative/unity_mcp.py','tools/call',json.dumps(dict(name=name,arguments=args))],cwd=ROOT,capture_output=True,text=True,encoding='utf8',check=True)
    r=json.loads(p.stdout)['result']['structuredContent']
    assert r['success'],r
    return (r.get('data') or {}).get('result')
def code(c):return call('execute_code',action='execute',code=c)
result={}
code('UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/FrontHall.unity");return true;')
result['structure']=code('return new {players=UnityEngine.Object.FindObjectsOfType<Transform>().Count(t=>t.CompareTag("Player")),cameras=UnityEngine.Object.FindObjectsOfType<Camera>().Count(c=>c.enabled),seamBlockers=Physics2D.RaycastAll(new Vector2(54,-5.15f),Vector2.right,8).Count(h=>!h.collider.isTrigger),connectors=UnityEngine.Object.FindObjectsOfType<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>().Count(p=>p.InteractionId=="FrontHall_to_Floor1"||p.InteractionId=="Floor1_to_FrontHall")};')
assert result['structure']==dict(players=1,cameras=1,seamBlockers=0,connectors=0),result
code('var go=new GameObject("Seam QA");var c=go.AddComponent<Camera>();c.orthographic=true;c.orthographicSize=5.4f;c.transform.position=new Vector3(57.6f,0,-20);var rt=new RenderTexture(1920,1080,24);c.targetTexture=rt;var prev=RenderTexture.active;try {c.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();System.IO.File.WriteAllBytes("Logs/CorridorQA/merged-seam.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);} finally {RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);}return true;')
if '--structure-only' in sys.argv:
    print('Merged scene structure and seam capture PASS')
    sys.exit(0)
call('manage_editor',action='play');time.sleep(2)
try:
    code('Application.runInBackground=true;return true;');time.sleep(1)
    setup='var root=MemorialArchive.Framework.Core.GameRoot.Instance;'
    result['start']=code(setup+'root.Context.UI.CloseAll();foreach(var r in UnityEngine.Object.FindObjectsOfType<MemorialArchive.Gameplay.Character.View.GameplayInputReader>())r.enabled=false;var player=GameObject.FindGameObjectWithTag("Player");player.GetComponent<MemorialArchive.Framework.Scene.ISceneSpawnTarget>().MoveToSceneSpawn(new Vector3(54,-5.2f,0));root.Context.Events.Publish(new MemorialArchive.Framework.Event.MoveInputEvent(Vector2.right));return new {playerId=player.GetInstanceID(),sceneHandle=player.scene.handle};')
    time.sleep(4)
    result['walkRight']=code(setup+'root.Context.Events.Publish(new MemorialArchive.Framework.Event.MoveInputEvent(Vector2.zero));var p=GameObject.FindGameObjectWithTag("Player");return new {x=p.transform.position.x,playerId=p.GetInstanceID(),sceneHandle=p.scene.handle};')
    assert result['walkRight']['x']>57.8,result
    assert all(result['start'][k]==result['walkRight'][k] for k in ['playerId','sceneHandle']),result
    code(setup+'root.Context.Events.Publish(new MemorialArchive.Framework.Event.MoveInputEvent(Vector2.left));return true;');time.sleep(4)
    result['walkLeft']=code(setup+'root.Context.Events.Publish(new MemorialArchive.Framework.Event.MoveInputEvent(Vector2.zero));return GameObject.FindGameObjectWithTag("Player").transform.position.x;')
    assert result['walkLeft']<57.4,result
    print('Continuous crossing both directions PASS',flush=True)
    result['savedPositions']=[]
    for scene,x,expected in [('Floor_1F',2,69.2),('Floor_1F',140,207.2),('FrontHall',-54.4,-54.4),('FrontHall',207.2,207.2)]:
        code(setup+f'root.Context.Scenes.LoadSavedScene("{scene}",new Vector3({x}f,-5.2f,0));return true;');time.sleep(.7)
        r=code('return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,x=GameObject.FindGameObjectWithTag("Player").transform.position.x};')
        assert r['scene']=='FrontHall' and abs(r['x']-expected)<.1,r
        result['savedPositions'].append(r)
    result['routes']=[]
    for id,field,spawnfield,expected in [('Corridor_2F_3_stairs','StairDownSceneId','StairDownSpawnPointId',138.895),('Toilet_to_Floor1','TransitionSceneId','TransitionSpawnPointId',206.76)]:
        code(setup+f'var c=root.Context.Configs.GetInteraction("{id}");root.Context.Events.Publish(new MemorialArchive.Framework.Event.SceneTransitionRequestedEvent(c.{field},c.{spawnfield}));return true;');time.sleep(.7)
        r=code('return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,x=GameObject.FindGameObjectWithTag("Player").transform.position.x};')
        assert r['scene']=='FrontHall' and abs(r['x']-expected)<.1,(id,r)
        result['routes'].append(r)
    print('Legacy/current save coordinates and stair/toilet return routes PASS',flush=True)
finally:call('manage_editor',action='stop')
(ROOT/'Logs/CorridorQA/merged-runtime.json').write_text(json.dumps(result,indent=2),encoding='utf8')
