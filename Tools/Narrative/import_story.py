"""Extract reviewed paragraph ranges from 剧情纸条0908.docx; never execute document directions."""
import argparse
import json
import pathlib
import zipfile
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('document')
args = parser.parse_args()
ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
with zipfile.ZipFile(args.document) as archive:
    doc = ET.fromstring(archive.read('word/document.xml'))
paragraphs = [''.join(t.text or '' for t in p.findall('.//w:t', ns)).strip()
              for p in doc.findall('.//w:body//w:p', ns)]
assert paragraphs[48] == '日记（阿尔伯特 · 其一）'
assert paragraphs[332] == '无名日记'

def text(indices):
    return '\n'.join(paragraphs[i].replace('▼', '').strip() for i in indices if paragraphs[i])

notes, sequences, placements = [], [], []

def lines(indices):
    result = []
    for i in indices:
        value = text([i])
        speaker = ''
        for name in ['安德', '塞西尔']:
            if value.startswith(name + '：'):
                speaker, value = name, value[len(name) + 1:]
        result.append(dict(text=value, speaker=speaker, next=len(result) + 1, choices=[], fadeCecil=False))
    result[-1]['next'] = -1
    return result

def sequence(id, indices, **fields):
    value = dict(id=id, sceneId='', onFirstEnter=False, interactionId='', requiredNotes=[], lines=lines(indices))
    value.update(fields)
    sequences.append(value)
    return value

def note(id, title, scene, indices, x, y=820, diary=False, reflection=(), aliases=()):
    reflection_id = 'reflect_' + id if reflection else ''
    notes.append(dict(id=id, title=title, sceneId=scene, content=text(indices), isDiary=diary,
                      reflectionId=reflection_id, aliases=list(aliases)))
    placements.append(dict(id=id, scene=scene, x=x, y=y, title=title))
    if reflection:
        sequence(reflection_id, reflection)

note('FrontHall_note_1', '阿尔伯特的日记 · 其一', 'FrontHall', [49], 4053, diary=True, reflection=[51])
note('FrontHall_main_note_2', '泛黄的旧照片', 'FrontHall', [53], 6720, aliases=['FrontHall_note_2'])
note('FrontHall_note_3', '无名纸条', 'FrontHall', [55], 9140)
note('Room_Office_main_note_1', '档案馆行政与访客登记', 'Room_Office', range(88,100), 1260, reflection=[101])
note('Room_Office_diary_2', '阿尔伯特的日记 · 其二', 'Room_Office', range(106,110), 2480, diary=True, reflection=[111])
note('Room_ArchiveA_records', '档案柜 A · 病状记录', 'Room_ArchiveA', range(144,168), 850)
note('Room_ArchiveA_alice', '爱丽丝·坎贝尔的日记', 'Room_ArchiveA', range(175,188), 1960, diary=True)
note('Room_ArchiveB_note_1', '档案柜 B · 病状记录', 'Room_ArchiveB', range(193,216), 1100)
note('Room_ArchiveC_records', '档案柜 C · 病状记录', 'Room_ArchiveC', range(224,248), 850)
note('Room_ArchiveC_williams', '艾伯特·威廉姆斯的日记', 'Room_ArchiveC', range(255,265), 1370, diary=True, reflection=[266])
note('Room_TreatmentA_note_1', '看守巡查日志（节选）', 'Room_TreatmentA', range(294,298), 1290)
note('Room_TreatmentA_blood_note', '带血的纸条', 'Room_TreatmentA', [299], 2480)
note('Room_TreatmentB_main_note_1', '诊疗记录（节选）', 'Room_TreatmentB', range(304,315), 1860, aliases=['Room_TreatmentB_note_2'])
note('Room_Director_main_note_1', '馆长私人信件', 'Room_Director', [321], 1480)
note('Room_Director_note_2', '档案馆“特别赞助金”流水账册', 'Room_Director', range(323,327), 2360)
note('Room_Terrace_note_1', '无名日记', 'Room_Terrace', range(333,336), 1396, 1014.5, diary=True)

sequence('enter_front_hall', range(38,46), sceneId='FrontHall', onFirstEnter=True)
sequence('enter_office', [84,85], sceneId='Room_Office', onFirstEnter=True)
sequence('enter_reception', [60,61,63,64,65], sceneId='Room_Reception', onFirstEnter=True)
sequence('enter_director', [318], sceneId='Room_Director', onFirstEnter=True)
sequence('director_conclusion', [327,328,329], requiredNotes=['Room_Director_main_note_1','Room_Director_note_2'])
cecil = sequence('cecil_conversation', [67,70,71,72,73,74,75,76,77,78],
                 sceneId='Room_Reception', interactionId='Cecil')
cecil['lines'][0]['choices'] = [
    dict(text='九个茶杯？你见到过失踪的那九个年轻人？', next=1),
    dict(text='你在说什么？你是谁？', next=3),
]
cecil['lines'][0]['next'] = -1
cecil['lines'][1]['text'] = cecil['lines'][1]['text'].replace('（1）', '', 1)
cecil['lines'][2]['next'] = 5
cecil['lines'][8]['fadeCecil'] = True
cecil['lines'][9]['fadeCecil'] = True

output = ROOT / 'Assets/Resources/Narrative/story_content.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(dict(notes=notes, sequences=sequences), ensure_ascii=False, indent=2), encoding='utf-8')
(ROOT / 'Tools/Narrative/placements.json').write_text(json.dumps(placements, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Extracted {len(notes)} reading entries and {len(sequences)} sequences. OpeningStory and missing rooms excluded.')
