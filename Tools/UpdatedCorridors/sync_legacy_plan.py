"""Keep the older all-scene authoring entry point on the reviewed Source layout."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
path=root/'Tools/SceneRebuild/layout.json'
legacy=json.loads(path.read_text(encoding='utf-8'))
plans=json.loads((root/'Tools/UpdatedCorridors/layout.json').read_text(encoding='utf-8'))
for f in plans['floors']:
    s=next(s for s in legacy['scenes'] if s['scene']==f['scene'])
    s.update(width=f['width'],left=f['left'],backgrounds=[],decor=[],layers=f['layers'],lights=f['lights'])
    anchors={a['id']:a for a in f['anchors']}
    point_ids={p['id'] for p in s['points']}
    s['moves'].update({a['id']:a['x'] for a in f['anchors'] if a['id'] not in point_ids})
    for id in point_ids:s['moves'].pop(id,None)
    for p in s['points']:
        if p['id'] in anchors:
            p.update(x=anchors[p['id']]['x'],y=anchors[p['id']]['y'])
        # Art now comes exclusively from layers, except the interactive special light.
        p.pop('visualParts',None)
        if p['id']!='light_floor_2f_special':p.pop('sprite',None)
    for m in s.get('monsters',[]):m['x']=12000
path.write_text(json.dumps(legacy,ensure_ascii=False,indent=2),encoding='utf-8')
