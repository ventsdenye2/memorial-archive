"""Idempotent configuration registration and finishing edits for the 0909 layout."""
import json
from pathlib import Path
from unity_yaml import ROOT,Document,guid,ref

db=Document('Assets/GameConfigs/GameConfigDatabase.asset');data=db.edit(11400000)
data['sceneAccessRules']=[
    dict(fromSceneId='Floor_2F',destinationSceneId='Room_Office',requiredItemId=1024),
    dict(fromSceneId='Floor_3F',destinationSceneId='Room_Director',requiredItemId=1028),
    dict(fromSceneId='Floor_3F',destinationSceneId='Floor_4F',requiredItemId=1027)]
# Replace superseded registrations by domain ID, retaining unrelated legacy configs.
asset_paths={guid(p.relative_to(ROOT).as_posix()):p for p in (ROOT/'Assets/GameConfigs').rglob('*.asset')}
for field,key in [('lightSources','lightId'),('interactions','interactionId')]:
    chosen={}
    for entry in data[field]:
        path=asset_paths.get(entry.get('guid'))
        if not path:continue
        value=Document(path).get(11400000).get(key)
        if value not in chosen or 'SceneLayout' in path.parts:chosen[value]=entry
    data[field]=list(chosen.values())
db.save()

mapdoc=Document('Assets/Prefabs/UI/MapPanel.prefab')
for i,d in mapdoc.find('MonoBehaviour'):
    if 'markerPlacements' in d and not any(x['sceneId']=='Room_Terrace' for x in d['markerPlacements']):
        mapdoc.edit(i)['markerPlacements'].append(dict(sceneId='Room_Terrace',roomId='',normalizedPosition={'x':.30,'y':.45}))
mapdoc.save()

# Do not substitute a weapon part for an unspecified puzzle item.
office=Document('Assets/Scenes/Room_Office.unity')
for i,d in office.find('MonoBehaviour'):
    if d.get('containerId')=='Room_Office_container_1':office.edit(i)['initialItems']=[]
office.save()
plan_path=ROOT/'Tools/SceneRebuild/layout.json';plan=json.loads(plan_path.read_text(encoding='utf8'))
for s in plan['scenes']:
    if s['scene']=='Room_Office':
        for p in s['points']:
            if p['id']=='Room_Office_container_1':p['items']=[];p['pending']='Office puzzle item deferred by author; container intentionally empty.'
plan_path.write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf8')

for folder in ['Assets/Art/Imported/SceneRebuild0909','Assets/GameConfigs/SceneLayout']:
    root=ROOT/folder
    for p in [root,*root.rglob('*')]:
        if not p.is_dir():continue
        meta=Path(str(p)+'.meta')
        if not meta.exists():
            g=guid(p.relative_to(ROOT).as_posix())
            meta.write_text(f'fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf8')
for name in ['Assets/Scripts/Framework/Scene/SceneAccessRule.cs','Assets/Scripts/Framework/Event/SceneAccessDeniedEvent.cs']:
    meta=Path(str(ROOT/name)+'.meta')
    if not meta.exists():
        g=guid(name);meta.write_text(f'fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf8')
print('Access rules, terrace map marker, source metadata synchronized.')
