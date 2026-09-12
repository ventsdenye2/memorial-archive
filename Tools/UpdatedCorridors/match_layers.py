"""Locate supplied transparent source layers against the four reference slices."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Imported/UpdatedCorridors'
OUT = ROOT / 'Logs/CorridorQA'

def pixels(im):
    a = np.asarray(im.convert('RGBA'), dtype=np.float32) / 255
    alpha = a[..., 3]
    rgb = a[..., :3] * alpha[..., None] + np.array([154,96,77], np.float32)/255 * (1-alpha[..., None])
    return rgb.mean(axis=2), alpha

def correlate(a, b, shape):
    return np.fft.irfft2(np.fft.rfft2(a, s=shape) * np.conj(np.fft.rfft2(b, s=shape)), s=shape).real

def locate(reference, source, scale=4, count=6):
    r = reference.resize((reference.width//scale, reference.height//scale))
    t = source.resize((max(1,source.width//scale), max(1,source.height//scale)))
    g, _ = pixels(r); q, alpha = pixels(t)
    mask = (alpha > .8).astype(np.float32)
    if mask.sum() < 30: mask = alpha**2
    n = mask.sum(); tq = (q*mask).sum(); var = (q*q*mask).sum()-tq*tq/n
    shape = g.shape
    cross = correlate(g, q*mask, shape)
    sums = correlate(g, mask, shape)
    sq = correlate(g*g, mask, shape)
    score = (cross-tq*sums/n)/np.sqrt(np.maximum(1e-10,var*(sq-sums*sums/n)))
    score = score[:g.shape[0]-q.shape[0]+1,:g.shape[1]-q.shape[1]+1]
    results=[]
    native, aa = pixels(source)
    yy,xx = np.nonzero(aa>.8)
    if len(xx)<30: yy,xx=np.nonzero(aa>.2)
    step=max(1,len(xx)//1500); yy,xx=yy[::step],xx[::step]
    ref,_=pixels(reference)
    v=native[yy,xx];v=v-v.mean()
    for _ in range(count):
        y,x = np.unravel_index(np.argmax(score),score.shape)
        if not np.isfinite(score[y,x]): break
        # Refine each coarse match at native resolution with a sparse set of visible pixels.
        best=(-2,0,0)
        for dy in range(max(0,y*scale-scale),min(reference.height-source.height,y*scale+scale)+1):
            for dx in range(max(0,x*scale-scale),min(reference.width-source.width,x*scale+scale)+1):
                z=ref[yy+dy,xx+dx];z=z-z.mean()
                s=float(np.dot(v,z)/max(1e-8,np.linalg.norm(v)*np.linalg.norm(z)))
                if s>best[0]:best=(s,dx,dy)
        results.append(dict(score=round(best[0],5), x=best[1], y=best[2]))
        score[max(0,y-20):y+21,max(0,x-max(20,q.shape[1]//2)):x+max(20,q.shape[1]//2)+1]=-np.inf
    return results

if __name__ == '__main__':
    report={}
    for floor in ['1F','2F','3F']:
        reference=Image.open(OUT/(floor+'-reference.png')).convert('RGBA')
        report[floor]={}
        for p in (ART/floor/'Source').glob('*.png'):
            if p.name in ['走廊1楼.png','走廊2楼.png','3楼走廊.png','纸张.png','漫画专用渐变 副本.png']:continue
            im=Image.open(p).convert('RGBA')
            matches=locate(reference,im)
            report[floor][p.name]=matches
            print(floor,p.name,matches[:4],flush=True)
        (OUT/'matches.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
