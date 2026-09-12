from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Art/Imported/UpdatedCorridors'
OUT = ROOT / 'Logs/CorridorQA'
OUT.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype(str(ROOT / 'Assets/Font/simhei.ttf'), 20)
overview = Image.new('RGB', (1920, 550), '#bbb5ab')
for k, floor in enumerate(['1F', '2F', '3F']):
    ImageDraw.Draw(overview).text((8, k * 180), floor + ' reference', font=font, fill='black')
    reference = Image.new('RGBA', (15360, 1080))
    for j in range(4):
        im = Image.open(ART / floor / 'Background' / f'Background_{j+1}.png')
        reference.paste(im, (j * 3840, 0))
        overview.paste(im.resize((480, 135)), (j * 480, k * 180 + 25))
    reference.save(OUT / (floor + '-reference.png'))
    files = list((ART / floor / 'Source').glob('*.png'))
    sheet = Image.new('RGB', (1800, len(files) * 160), '#bbb5ab')
    for i, p in enumerate(files):
        ImageDraw.Draw(sheet).text((8, i * 160 + 3), p.name, font=font, fill='black')
        im = Image.open(p).convert('RGBA')
        im.thumbnail((1700, 120))
        sheet.paste(im, (65, i * 160 + 30), im)
    sheet.save(OUT / (floor + '-sources.jpg'))
overview.save(OUT / 'references.jpg')
