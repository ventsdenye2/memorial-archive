"""Import the reviewed opening, including Word's exact red run boundaries."""
import copy
import json
import re
import sys
from pathlib import Path
from docx import Document

asset = Path('Assets/GameConfigs/Dialogue/OpeningDialogue.asset')
art = json.loads(Path('Tools/Narrative/opening-art.json').read_text(encoding='utf-8'))
portraits, cg_refs = art['portraits'], art['cg']
# The supplemental art manifest predates George's inactive portrait entry.
portraits['george']['inactivePortrait'] = {
    'fileID': 21300000,
    'guid': '234207a5f968da34c818da80f1ed94c1',
    'type': 3,
}
source = Document(sys.argv[1])
nodes = []
cg = None
pending_cg = False
cast = []
portrait_pending = False
black_count = 0
source_texts = []
RED_WORD_COLORS = {'FF0000', 'C21C13'}

def is_red_run(run):
    # Word stores the reviewed red as C21C13 in this document, not FF0000.
    color = run.font.color
    if color is not None and color.rgb is not None:
        return str(color.rgb).upper() in RED_WORD_COLORS
    return any(value in run._r.xml.upper() for value in RED_WORD_COLORS)

def yaml_scalar(value):
    if isinstance(value, bool):
        return '1' if value else '0'
    if value is None:
        return 'null'
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    def normalize(item):
        if isinstance(item, bool): return 1 if item else 0
        if isinstance(item, list): return [normalize(v) for v in item]
        if isinstance(item, dict): return {k: normalize(v) for k, v in item.items()}
        return item
    return json.dumps(normalize(value), ensure_ascii=False)
for pi, paragraph in enumerate(source.paragraphs):
    text = paragraph.text.strip().removesuffix('▼').strip()
    if not text or pi < 2:
        continue
    if text.startswith('CG'):
        cg = text[:4]
        pending_cg = True
        cast = []
        if cg == 'CG02':
            black_count += 1
        continue
    if text.startswith('※'):
        if '主角' in text:
            cast = ['andre']
            portrait_pending = True
        continue
    show_cast = bool(cast)
    speaker = 'george' if text.startswith('父亲：') else 'emily' if text.startswith('母亲：') else ''
    if speaker:
        text = text.split('：', 1)[1]
        cast = ['andre', speaker]
    runs = []
    for run in paragraph.runs:
        part = run.text.replace('▼', '')
        if part:
            red = is_red_run(run)
            if runs and runs[-1][1] == red:
                runs[-1] = (runs[-1][0] + part, red)
            else:
                runs.append((part, red))
    colored = any(red and part.strip() for part, red in runs)
    all_red = colored and all(red or not part.strip() for part, red in runs)
    rendered = text
    if colored and not all_red:
        rendered = ''.join('<size=32><i><color=#FFFFFF>' + part + '</color></i></size>' if red else part for part, red in runs).strip()
    # A switch marker applies to the immediately following shot. Ordinary
    # narration thereafter must not inherit a portrait from that marker.
    node_cast = list(cast) if speaker or (cast == ['andre'] and cg == 'CG02') else []
    portrait_pending = False
    # The witness quotation intentionally removes the narrator portrait.
    if pi == 25:
        node_cast = []
    snapshots = []
    for character in node_cast:
        portrait = copy.deepcopy(portraits[character])
        portrait['visible'] = True
        snapshots.append(portrait)
    node = dict(nodeId=f'opening_{len(nodes)+1:03}', speakerId=speaker,
                textPresentation=2 if speaker else 3 if all_red else 1,
                centerText=pi in [15, 16, 17, 18], text=rendered,
                cgCommand=1 if pending_cg else 0, portraits=snapshots,
                effectIds=art['effect'] if text.startswith('一道闪电') else [],
                blocksAdvance=text.startswith('一道闪电'))
    if pending_cg:
        node['cgSprite'] = cg_refs[cg]
    pending_cg = False
    nodes.append(node)
    source_texts.append(text)
header = asset.read_text(encoding='utf-8').split('  nodes:')[0]
lines = [header.rstrip(), '  nodes:']
for node in nodes:
    for index, (key, value) in enumerate(node.items()):
        lines.append(('  - ' if index == 0 else '    ') + key + ': ' + yaml_scalar(value))
asset.write_text('\n'.join(lines) + '\n', encoding='utf-8')
Path('Temp/feedback0913_review/source-opening.json').write_text(json.dumps(source_texts, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Imported {len(nodes)} exact paragraphs; {sum(n["cgCommand"] == 1 for n in nodes)} CG transitions.')

