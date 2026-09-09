"""Apply reviewed layout.json without replacing UI/player prefab instances.

Usage: python -X utf8 Tools/SceneRebuild/apply_layout.py
Requires PyYAML and Pillow. Close/save Unity scenes before applying offline changes.
"""
import copy,json,re,shutil
from pathlib import Path
from PIL import Image
from unity_yaml import ROOT,Document,common,guid,ref

PLAN=json.loads((ROOT/'Tools/SceneRebuild/layout.json').read_text(encoding='utf8'))
for scene in PLAN['scenes']:
    for background in scene['backgrounds']:
        native_width=Image.open(ROOT/background['path']).width
        if abs(background['width']-native_width)>1:
            raise ValueError(f"{scene['scene']}: background must retain native width {native_width}")
IP='Assets/Scripts/Gameplay/Interaction/View/InteractionPointView.cs'
IC='Assets/Scripts/Gameplay/Interaction/Config/InteractionConfig.cs'
LS='Assets/Scripts/Gameplay/Lighting/View/LightSourceView.cs'
LC='Assets/Scripts/Gameplay/Lighting/Config/LightSourceConfig.cs'
SP='Assets/Scripts/Framework/Scene/SceneSpawnPoint.cs'
SEED='Assets/Scripts/Gameplay/Inventory/View/SceneContainerSeedView.cs'
MONSTER='Assets/Scripts/Gameplay/Monster/View/MonsterSpawnPointView.cs'
CONFIG_ROOT='Assets/GameConfigs/SceneLayout'
sprite_template=next(d for _,d in Document('Assets/Scenes/Room_Office.unity').find('SpriteRenderer'))
box_template=next(d for _,d in Document('Assets/Prefabs/Lighting/LightFixture.prefab').find('BoxCollider2D'))
polygon_template=next(d for _,d in Document('Assets/Scenes/Room_Office.unity').find('PolygonCollider2D'))
registered={'interactions':[],'lightSources':[],'items':[],'monsters':[]}
report=[]

def asset(path,script,name,**fields):
    p=ROOT/path;p.parent.mkdir(parents=True,exist_ok=True)
    if not p.exists():p.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour: {}\n',encoding='utf8')
    d=Document(path);d.data[11400000]={'MonoBehaviour':dict(common(),m_GameObject=ref(),m_Enabled=1,m_EditorHideFlags=0,m_Script=ref(script,11500000),m_Name=name,m_EditorClassIdentifier='',**fields)};d.dirty.add(11400000);d.save();guid(path)
    return ref(path,11400000,2)

def texture(path):
    p=ROOT/path
    if not p.exists():raise FileNotFoundError(path)
    w,h=Image.open(p).size
    meta=Path(str(p)+'.meta')
    if not meta.exists():
        src=ROOT/'Assets/Art/Scenes/接待室素材8.4/接待室素材/接待室.png.meta'
        text=src.read_text(encoding='utf-8-sig')
        text=re.sub(r'^guid: \w+',f'guid: {guid(path)}',text,flags=re.M)
        meta.write_text(text,encoding='utf8')
    text=meta.read_text(encoding='utf8')
    for name,value in [('spritePixelsToUnits',100),('spriteMode',1),('textureType',8),('maxTextureSize',16384 if w>8192 else 8192 if w>4096 else 4096),('enableMipMap',0)]:
        text=re.sub(r'(?m)^(\s*'+name+r':) .*$',lambda m:m[1]+' '+str(value),text)
    meta.write_text(text,encoding='utf8')
    return w,h

def sprite(doc,parent,path,x,y,width=None,order=-10,name='Background'):
    w,h=texture(path);sx=width/w if width else 1
    go,t=doc.game_object(name,(x,y,0),parent,scale=(sx,1,1))
    doc.clone_component(go,212,'SpriteRenderer',sprite_template,m_Sprite=ref(path,21300000),m_SortingOrder=order,m_Color={'r':1,'g':1,'b':1,'a':1},m_DrawMode=0,m_Size={'x':w/100,'y':h/100},m_Enabled=1)
    return go,t

def box(doc,go,width,height,trigger=False,offset_y=0):
    return doc.clone_component(go,61,'BoxCollider2D',box_template,m_IsTrigger=int(trigger),m_Size={'x':width,'y':height},m_Offset={'x':0,'y':offset_y},m_Enabled=1)

def interaction_config(scene,p):
    id=p['id'];path=f'{CONFIG_ROOT}/Interactions/{id}.asset'
    # Keep existing GUIDs for established interactions and saves.
    old=ROOT/f'Assets/GameConfigs/Interaction/{id}.asset'
    if old.exists():path=old.relative_to(ROOT).as_posix()
    fields=dict(interactionId=id,interactionType=p['kind'],displayName=p.get('display',id),sceneId=scene,containerId=id if p['kind']==1 else '',puzzleId='',storyId='',noteId=p.get('noteId',''),transitionSceneId=p.get('target',''),transitionSpawnPointId=p.get('spawn',''),requiresConfirmation=int(p['kind']==6),confirmationMessage='确认前往？',stairPrompt='请选择目的楼层',stairUpSceneId='',stairUpSpawnPointId='',stairDownSceneId='',stairDownSpawnPointId='')
    registered['interactions'].append(asset(path,IC,id,**fields))

def lighting_config(s,id,special=False):
    registered['lightSources'].append(asset(f'{CONFIG_ROOT}/Lights/{id}.asset',LC,id,lightId=id,regionId=s['region'],isSpecial=int(special),radius=8,exemptFromDarkness=0))

def add_point(doc,parent,s,p):
    x=s['left']+p['x']/100;y=5.4-p['y']/100
    if p.get('sprite'):
        go,t=sprite(doc,parent,p['sprite'],x,y,order=-5,name=p['id'])
    else:go,t=doc.game_object(p['id'],(x,y,0),parent)
    for part in p.get('visualParts',[]):
        sprite(doc,t,part['path'],s['left']+part['x']/100-x,5.4-part['y']/100-y,order=-4,name='Visual')
    box(doc,go,1.4,4.8,True,offset_y=-3.4-y)
    doc.mono(go,IP,interactionId=p['id'],interactionType=p['kind'],playerTag='Player')
    interaction_config(s['scene'],p)
    if p['kind']==1:
        seeds=[];column=0;row=0
        for item in p.get('items',[]):
            cfg=next((ROOT/'Assets/GameConfigs/Items').glob(str(item)+'_*.asset'))
            data=Document(cfg.relative_to(ROOT)).get(11400000);width=2 if data.get('backpackFootprint')==1 else 1
            if column+width>2:column=0;row+=1
            seeds.append(dict(itemId=item,quantity=1,x=column,y=row));column+=width
        doc.mono(go,SEED,containerId=p['id'],initialItems=seeds,randomItemPool=[1012,1013,1020] if p.get('random') else [],randomItemCount=1)
    if p['kind']==11:
        doc.mono(go,LS,lightId=p['id'],litColor={'r':1,'g':1,'b':1,'a':1},offColor={'r':0.5,'g':0.5,'b':0.5,'a':1})
        lighting_config(s,p['id'],True)
    return go

def disable_prefab(doc,i,d):
    source=d.get('m_SourcePrefab',{}).get('guid')
    p=prefab_paths.get(source)
    if not p:return
    prefab=Document(p);root_go=next((x['m_GameObject']['fileID'] for _,x in prefab.find('Transform') if x.get('m_Father',{}).get('fileID')==0),None)
    if root_go:
        mod=doc.edit(i)['m_Modification']['m_Modifications']
        existing=next((x for x in mod if x['target']['fileID']==root_go and x['propertyPath']=='m_IsActive'),None)
        if existing:existing['value']='0'
        else:mod.append(dict(target={'fileID':root_go,'guid':source,'type':3},propertyPath='m_IsActive',value='0',objectReference=ref()))

prefab_paths={guid(p.relative_to(ROOT).as_posix()):p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/Prefabs').rglob('*.prefab')}
# Missing mission items reuse the authored key icon until dedicated art exists.
for item,name,effect in [(1027,'四楼钥匙','floor4_key'),(1028,'馆长办公室钥匙','director_key')]:
    source=Document('Assets/GameConfigs/Items/1024_办公室钥匙.asset').get(11400000)
    fields={k:v for k,v in source.items() if not k.startswith('m_')}
    fields.update(itemId=item,itemName=name,description='用于打开'+('通往四楼的门锁。' if item==1027 else '馆长办公室门锁。'),effectId=effect,effectDescription='持有时允许进入对应区域。',isConsumable=0)
    registered['items'].append(asset(f'Assets/GameConfigs/Items/{item}_{name}.asset','Assets/Scripts/Gameplay/Item/Config/ItemConfig.cs',f'{item}_{name}',**fields))
source=Document('Assets/GameConfigs/Monsters/Stage1MeleeMonster.asset').get(11400000)
fields={k:v for k,v in source.items() if not k.startswith('m_')};fields.update(monsterId=2003,monsterName='二楼走廊强化守卫',maxHealth=2000,attackDamage=2)
registered['monsters'].append(asset(f'{CONFIG_ROOT}/Monsters/CorridorEnhanced.asset','Assets/Scripts/Gameplay/Monster/Config/MonsterConfig.cs','CorridorEnhanced',**fields))

terrace_path=ROOT/'Assets/Scenes/Room_Terrace.unity'
if not terrace_path.exists():
    shutil.copyfile(ROOT/'Assets/Scenes/Room_Office.unity',terrace_path);guid('Assets/Scenes/Room_Terrace.unity')
for s in PLAN['scenes']:
    scene=s['scene'];path=f'Assets/Scenes/{scene}.unity'
    doc=Document(path)
    for i,d in list(doc.find('GameObject')):
        if d.get('m_Name')=='SceneLayout0909':doc.remove_hierarchy(i)
    # Remove stale visible art and generated fixtures from the active layout.
    for _,d in doc.find('SpriteRenderer'):
        go=d['m_GameObject']['fileID']
        if go in doc.data:doc.edit(go)['m_IsActive']=0
    for i,d in doc.find('PrefabInstance'):
        source=prefab_paths.get(d.get('m_SourcePrefab',{}).get('guid'),'')
        if '/Lighting/' in source or '/Interaction/' in source or '/Monster/' in source:disable_prefab(doc,i,d)
    for _,d in doc.find('MonoBehaviour'):
        script=d.get('m_Script',{}).get('guid')
        if script in [guid(SEED),guid(MONSTER),guid(LS)]:doc.edit(d['m_GameObject']['fileID'])['m_IsActive']=0
        if script==guid(IP) and d.get('interactionType')!=6:doc.edit(d['m_GameObject']['fileID'])['m_IsActive']=0
    for i,d in doc.find('GameObject'):
        if d.get('m_Name') in ['RoomGeometry','MonsterSpawnPoints']:doc.edit(i)['m_IsActive']=0
    _,root=doc.game_object('SceneLayout0909')
    _,artroot=doc.game_object('Art',(0,0,0),root)
    for b in s['backgrounds']:sprite(doc,artroot,b['path'],b['x'],0,b['width'],order=b.get('order',-10))
    for b in s.get('decor',[]):sprite(doc,artroot,b['path'],s['left']+b['x']/100,5.4-b['y']/100,order=-5,name='DoorArt')
    _,geometry=doc.game_object('Geometry',(0,0,0),root)
    width=s['width']/100;left=s['left'];right=left+width;cx=(left+right)/2
    go,_=doc.game_object('CameraConfiner',(cx,0,0),geometry)
    poly=copy.deepcopy(polygon_template);poly['m_Points']={'m_Paths':[[{'x':-width/2,'y':-5.4},{'x':width/2,'y':-5.4},{'x':width/2,'y':5.4},{'x':-width/2,'y':5.4}]]}
    confiner=doc.clone_component(go,60,'PolygonCollider2D',poly,m_IsTrigger=1,m_Offset={'x':0,'y':0},m_Enabled=1)
    for name,x,y,w,h in [('Left',left-.15,0,.3,10.8),('Right',right+.15,0,.3,10.8),('Top',cx,5.55,width,.3),('Bottom',cx,-5.55,width,.3)]:
        go,_=doc.game_object('Wall_'+name,(x,y,0),geometry);box(doc,go,w,h)
    _,points=doc.game_object('InteractionPoints',(0,0,0),root)
    for p in s['points']:add_point(doc,points,s,p)
    _,lights=doc.game_object('NormalLights',(0,0,0),root)
    for n,l in enumerate(s['lights'],1):
        id='light_'+scene.lower()+f'_ordinary_{n:02}'
        go,_=doc.game_object(id,(left+l['x']/100,5.4-l['y']/100,0),lights)
        doc.mono(go,LS,lightId=id);lighting_config(s,id)
        box(doc,go,1.2,4.8,True,offset_y=-3.4-(5.4-l['y']/100))
        doc.mono(go,IP,interactionId=id,interactionType=11,playerTag='Player')
    for m in s.get('monsters',[]):
        go,_=doc.game_object(m['id'],(left+m['x']/100,-5.2,0),root)
        prefab='Assets/Prefabs/Monster/Stage1MeleeMonster.prefab';pd=Document(prefab)
        prefab_go=next(x['m_GameObject']['fileID'] for _,x in pd.find('Transform') if x['m_Father']['fileID']==0)
        doc.mono(go,MONSTER,spawnPointId=m['id'],roomId=scene,monsterId=m['monster'],initialHealth=2000 if m['monster']==2003 else 1500,monsterPrefab=ref(prefab,prefab_go),spawnParent=ref(file_id=root))
    # Keep existing identities; update their world positions against the selected door art.
    for i,d in list(doc.find('MonoBehaviour')):
        script=d.get('m_Script',{}).get('guid');go=d.get('m_GameObject',{}).get('fileID')
        if go not in doc.data:continue
        id=d.get('interactionId') if script==guid(IP) else d.get('pointId') if script==guid(SP) else None
        if not id:continue
        if scene=='Room_Terrace':
            if id.startswith('Room_Office'):
                id=id.replace('Room_Office','Room_Terrace');doc.edit(i)['interactionId' if script==guid(IP) else 'pointId']=id
        if scene.startswith('Room_') and (script==guid(SP) or d.get('interactionType')==6):
            inset=(1.7 if s['door_x']<s['width']/2 else -1.7) if script==guid(SP) else 0
            doc.move(go,left+s['door_x']/100+inset,-5.2)
        elif id in s['moves']:doc.move(go,left+s['moves'][id]/100,-5.2)
    # Bind both possible camera implementations to the rebuilt shape.
    for i,d in doc.find('MonoBehaviour'):
        if 'cameraBounds' in d:doc.edit(i)['cameraBounds']=ref(file_id=confiner)
        if 'm_BoundingShape2D' in d:doc.edit(i)['m_BoundingShape2D']=ref(file_id=confiner)
    for i,d in doc.find('PrefabInstance'):
        for m in d['m_Modification']['m_Modifications']:
            if m['propertyPath'] in ['cameraBounds','m_BoundingShape2D']:
                doc.dirty.add(i);m['objectReference']=ref(file_id=confiner)
        if prefab_paths.get(d.get('m_SourcePrefab',{}).get('guid'))=='Assets/Prefabs/Character/Player.prefab':
            spawn_x=left+(s['door_x']+(170 if s['door_x']<s['width']/2 else -170))/100 if scene.startswith('Room_') else left+(320 if scene=='FrontHall' else 220)/100
            for m in d['m_Modification']['m_Modifications']:
                if m['propertyPath']=='m_LocalPosition.x' and m['target']['fileID']==6489321317651925394:
                    doc.dirty.add(i);m['value']=str(spawn_x)
    if scene=='Room_Terrace':
        interaction_config(scene,dict(id='Room_Terrace_return',kind=6,target='Room_Director',spawn='Room_Director_spawn_from_Terrace',display='返回馆长办公室'))
    if scene=='Room_Director':
        p=dict(id='Room_Terrace_enter',kind=6,x=130,y=800,target='Room_Terrace',spawn='Room_Terrace_spawn_entry',display='前往露台')
        add_point(doc,points,s,p)
        go,_=doc.game_object('Room_Director_spawn_from_Terrace',(left+3,-5.2,0),root);doc.mono(go,SP,pointId='Room_Director_spawn_from_Terrace')
    doc.save();report.append(dict(scene=scene,width=s['width'],points=len(s['points']),normalLights=len(s['lights']),monsters=len(s.get('monsters',[]))))
    print(scene,'applied')

database=Document('Assets/GameConfigs/GameConfigDatabase.asset');d=database.edit(11400000)
for field,refs in registered.items():
    existing=d[field];seen={x.get('guid') for x in existing}
    existing.extend(x for x in refs if x['guid'] not in seen)
database.save()
settings=Document('ProjectSettings/EditorBuildSettings.asset');d=settings.edit(1)
if not any(x['path']=='Assets/Scenes/Room_Terrace.unity' for x in d['m_Scenes']):
    d['m_Scenes'].append(dict(enabled=1,path='Assets/Scenes/Room_Terrace.unity',guid=guid('Assets/Scenes/Room_Terrace.unity')))
settings.save()
(ROOT/'Temp/SceneRebuildAudit/applied.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
