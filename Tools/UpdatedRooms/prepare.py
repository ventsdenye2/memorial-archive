"""Synchronize the three 2026-09-14 room art/layout manifests (no scene rewrite)."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = 'Assets/Art/Imported/UpdatedRooms0914/'
LEFT = -19.2
def read(p): return json.loads((ROOT/p).read_text(encoding='utf-8'))
def write(p,d): (ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
plan=read('Tools/SceneRebuild/layout.json')
interaction=read('Tools/SceneRebuild/interaction_layout.json')
narrative=read('Tools/Narrative/placements.json')
definitions={
 'Room_ArchiveB':dict(folder='档案室b素材',door=2070,lights=[(612,510),(1180,513),(2258,536),(2980,540)],
   points={'Room_ArchiveB_note_1':(2818,788.5),'light_room_archiveb_special':(1450,430)},
   parts=[('组 2.png',1182,654,-9),('漫画专用渐变 副本.png',0,0,-8)]),
 'Room_ArchiveC':dict(folder='档案室c素材',door=3730,lights=[(640,500),(2450,516)],
   points={'Room_ArchiveC_container_1':(3495.5,751.5),'light_room_archivec_special':(3495,260),
           'Room_ArchiveC_records':(1300,815),'Room_ArchiveC_williams':(2250,810)},
   parts=[]),
 'Room_Toilet':dict(folder='厕所素材',door=3730,lights=[(420,580),(805,580)],
   points={'Room_Toilet_container_1':(1780,910),'Room_Toilet_hide':(2755,940),'light_room_toilet_special':(1200,430)},
   parts=[('组 2.png',2251,411,-9),('漫画专用渐变 副本 2.png',0,0,-8)])
}
for s in plan['scenes']:
 if s['scene'] not in definitions: continue
 n=s['scene'];d=definitions[n];base=ART+d['folder']+'/'
 s.update(width=3840,left=LEFT,door_x=d['door'])
 s['spawn_x']=3320 if n=='Room_ArchiveC' else d['door']-170
 s['backgrounds']=[dict(path=base+'纸张.png',x=0,width=3840,order=-12),dict(path=base+'组 1.png',x=0,width=3840,order=-10)]
 s['layers']=[]
 from PIL import Image
 for name,x,y,order in d['parts']:
  w,h=Image.open(ROOT/base/name).size
  s['layers'].append(dict(path=base+name,x=x,y=y,width=w,height=h,order=order,tint=[1,1,1,1]))
 s['decor']=[] if n=='Room_ArchiveB' else [dict(path='Assets/Art/Imported/UpdatedCorridors/2F/Source/门 副本.png',x=d['door'],y=753.5)]
 for p in s['points']:
  p['x'],p['y']=d['points'][p['id']]
  if n=='Room_ArchiveB' and p['kind']==9:p['sprite']=base+'线索 副本.png'
  if n=='Room_ArchiveC' and p['kind']==1:p['sprite']=base+'衣柜 副本.png'
 s['lights']=[dict(x=x,y=y) for x,y in d['lights']]
 if n=='Room_ArchiveC':s['monsters'][0]['x']=2750
 if n!='Room_ArchiveC':s['referencePlate']=base+('档案室b.png' if n=='Room_ArchiveB' else '插画.png')
 positions=dict(d['points'])
 positions.update({'light_'+n.lower()+'_ordinary_'+str(i).zfill(2):(x,y) for i,(x,y) in enumerate(d['lights'],1)})
 positions['Toilet_to_Floor1' if n=='Room_Toilet' else n+'_return']=(d['door'],1060)
 entries=next(v for v in interaction['scenes'] if v['scene']==n)
 for p in entries['points']:
  p['x']=round(LEFT+positions[p['id']][0]/100,4)
 # Keep the lamp's visual and interaction away from the wardrobe and exit.
 if n=='Room_ArchiveC':
  d['points']['light_room_archivec_special']=(3210,330)
  next(p for p in s['points'] if p['id']=='light_room_archivec_special').update(x=3210,y=330)
  next(p for p in entries['points'] if p['id']=='light_room_archivec_special')['x']=12.9
 entries['points'].sort(key=lambda p:p['x'])
 for p in narrative:
  if p['scene']==n and p['id'] in d['points']:p['x'],p['y']=d['points'][p['id']]
write('Tools/SceneRebuild/layout.json',plan)
write('Tools/SceneRebuild/interaction_layout.json',interaction)
write('Tools/Narrative/placements.json',narrative)
write('Tools/UpdatedRooms/rooms.json',[s for s in plan['scenes'] if s['scene'] in definitions])
