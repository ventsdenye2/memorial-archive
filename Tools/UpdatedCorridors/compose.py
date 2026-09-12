import json
from pathlib import Path
from PIL import Image
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Art/Imported/UpdatedCorridors'
OUT=ROOT/'Logs/CorridorQA'
matches=json.loads((OUT/'matches.json').read_text(encoding='utf-8'))

def compose(floor,layers):
    canvas=Image.new('RGBA',(15360,1080))
    for name,x,y in layers:
        canvas.alpha_composite(Image.open(ART/floor/'Source'/name).convert('RGBA'),(x,y))
    return canvas

for floor in ['1F','2F']:
    m=matches[floor]
    base=[('纸张.png',0,0)]
    groups=['组 1 副本.png','组 5.png','组 6 副本.png'] if floor=='1F' else ['组 5.png','组 6 副本.png']
    props=[]
    for name in groups:
        p=m[name][0];props.append((name,p['x'],p['y']))
    if floor=='1F':
        props += [('楼梯 副本.png',7850,466),('门 副本 2.png',14784,514),('地面 副本 3.png',0,0)]
        variants=[['图层 1 副本 12.png'],['壁纸 副本.png'],['图层 1 副本 12.png','壁纸 副本.png']]
    else:
        props += [('楼梯 副本.png',1004,474),('楼梯 副本 2.png',8687,480),('门 副本.png',15213,452),('图层 8 副本 3.png',3944,540),('地面 副本 2.png',0,0)]
        props += [('门 副本 2.png',p['x'],p['y']) for p in m['门 副本 2.png'] if p['score']>.9]
        variants=[['图层 1.png']]
    reference=np.asarray(Image.open(OUT/(floor+'-reference.png')).convert('RGB').resize((3840,270)),dtype=float)
    for k,wallpapers in enumerate(variants):
        for gradient in ([False,True] if floor=='1F' else [False]):
            layers=base+[(w,0,0) for w in wallpapers]+props
            if gradient:layers.append(('漫画专用渐变 副本.png',0,0))
            canvas=compose(floor,layers)
            error=np.abs(np.asarray(canvas.convert('RGB').resize((3840,270)),dtype=float)-reference)
            print(floor,k,gradient,'MAE',round(error.mean(),3),flush=True)
            canvas.resize((3840,270)).save(OUT/f'{floor}-composed-{k}-{gradient}.png')
