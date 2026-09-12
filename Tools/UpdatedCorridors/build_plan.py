"""Reviewed source-layer assembly and semantic gameplay anchors, in native pixels."""
import json
from pathlib import Path
from PIL import Image
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Art/Imported/UpdatedCorridors'
OUT=ROOT/'Logs/CorridorQA'
matches=json.loads((OUT/'matches.json').read_text(encoding='utf-8'))
lamps=json.loads((OUT/'lamps.json').read_text(encoding='utf-8'))
plans=[]
for floor in ['1F','2F','3F']:
    layers=[]
    def layer(name,x=0,y=0,order=None,tint=None):
        im=Image.open(ART/floor/'Source'/name)
        item=dict(path=f'Assets/Art/Imported/UpdatedCorridors/{floor}/Source/{name}',x=x,y=y,width=im.width,height=im.height,order=order if order is not None else -30+len(layers),tint=tint or [1,1,1,1])
        layers.append(item)
    layer('纸张.png',tint=[1,1,1,1] if floor=='1F' else [142/154,75/96,54/77,1])
    wall={'1F':'图层 1 副本 12.png','2F':'图层 1.png','3F':'图层 1 副本 11.png'}[floor]
    layer(wall)
    if floor=='1F':layer('壁纸 副本.png')
    groups={'1F':[('组 1 副本.png',200,0),('组 5.png',1963,0),('组 6 副本.png',1896,4)],'2F':[('组 5.png',24,0),('组 6 副本.png',1896,4)],'3F':[('组 4.png',63,0)]}[floor]
    for name,x,y in groups:layer(name,x,y,order=40 if name.startswith('组 6') else None)
    # Columns are foreground occluders even where the flattened reference omits them.
    if floor=='3F':layer('组 6.png',1896,19,order=40)
    door={'1F':[(14784,514)],'2F':[(89,535),(3451,526),(7884,523),(13655,521)],'3F':[(584,531),(3155,525),(8150,532),(11132,534)]}[floor]
    doorname='门 副本 2.png'
    for x,y in door:layer(doorname,x,y)
    stairs={'1F':[(7850,466)],'2F':[(1004,474),(8687,480)],'3F':[(1006,468)]}[floor]
    for x,y in stairs:layer('楼梯 副本.png',x,y)
    if floor=='2F':
        layer('图层 8 副本 3.png',3944,540)
        layer('桌子 副本 8.png',6883,821)
        layer('门 副本.png',15213,452)
    if floor=='3F':
        layer('组 5.png',10857,543)
        layer('电话本体 副本.png',4737,741)
        layer('电话通 副本.png',4746,737)
    layer({'1F':'地面 副本 3.png','2F':'地面 副本 2.png','3F':'地面 副本.png'}[floor],order=-5)
    if floor=='1F':layer('漫画专用渐变 副本.png',order=-4)
    anchors={}
    def anchor(id,x,y=1060):anchors[id]=dict(id=id,x=x,y=y)
    def entry(id,spawn,x):anchor(id,x);anchor(spawn,x)
    if floor=='1F':
        entry('Floor1_to_FrontHall','Floor1_spawn_from_FrontHall',86)
        entry('Floor1_to_Toilet','Floor1_spawn_from_Toilet',14916)
        entry('Corridor_1F_3_stairs','Corridor_1F_3_spawn_stairs',8129.5)
    elif floor=='2F':
        for room,(x,y) in zip(['Office','ArchiveA','ArchiveB','Reception'],door):entry('Room_'+room+'_enter','Floor2_spawn_from_Room_'+room,x+154)
        for section,(x,y) in zip([1,3],stairs):entry(f'Corridor_2F_{section}_stairs',f'Corridor_2F_{section}_spawn_stairs',x+279.5)
        anchor('light_floor_2f_special',4006,683.5)
        anchor('Floor_2F_container_1',7034,940)
        anchor('Floor_2F_hide',8665,940)
    else:
        for room,(x,y) in zip(['Director','ArchiveC','TreatmentA','TreatmentB'],door):entry('Room_'+room+'_enter','Floor3_spawn_from_Room_'+room,x+154)
        entry('Corridor_3F_1_stairs','Corridor_3F_1_spawn_stairs',1285.5)
        anchor('Floor_3F_save',4792,783.5)
        anchor('Room_Terrace_enter',150,910)
        anchor('Floor3_spawn_from_Terrace',310)
    # Segment spawn identities are retained, but their positions use the new 3840px spans.
    for i in range(1,5):
        for suffix,offset in [('left',220),('right',3620),('initial',220)]:anchor(f'Corridor_{floor}_{i}_spawn_{suffix}',(i-1)*3840+offset)
    ordinary=[]
    for p in sorted(lamps[floor],key=lambda p:p['x']):
        if not any(abs(p['x']+21-q['x'])<90 for q in ordinary):ordinary.append(dict(x=p['x']+21,y=p['y']+60))
    plan=dict(floor=floor,scene='Floor_'+floor,width=15360,height=1080,left=-9.6,layers=layers,anchors=list(anchors.values()),lights=ordinary)
    plans.append(plan)
    canvas=Image.new('RGBA',(15360,1080))
    for l in sorted(layers,key=lambda l:l['order']):
        im=Image.open(ROOT/l['path']).convert('RGBA')
        if l['tint']!=[1,1,1,1]:im=Image.fromarray((np.array(im,dtype=float)*l['tint']).clip(0,255).astype('uint8'))
        canvas.alpha_composite(im,(l['x'],l['y']))
    canvas.resize((3840,270)).save(OUT/(floor+'-assembled.png'))
    ref=np.array(Image.open(OUT/(floor+'-reference.png')).convert('RGB').resize((3840,270)),dtype=float)
    print(floor,'layers',len(layers),'lights',len(ordinary),'MAE',round(np.abs(np.array(canvas.convert('RGB').resize((3840,270)),dtype=float)-ref).mean(),2))
(ROOT/'Tools/UpdatedCorridors/layout.json').write_text(json.dumps(dict(floors=plans),ensure_ascii=False,indent=2),encoding='utf-8')
