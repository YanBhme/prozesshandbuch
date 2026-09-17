"""Generate the bundled tenant-change procedure and printable AcroForm PDFs.
Run with Python and reportlab; source content is maintained in checklist-content.json.
"""
import json
from pathlib import Path
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import os
FONT_DIR = Path(os.environ.get("CHECKLIST_FONT_DIR", "/usr/share/fonts/truetype/dejavu"))
pdfmetrics.registerFont(TTFont("Body", str(FONT_DIR/"DejaVuSans.ttf")))
pdfmetrics.registerFont(TTFont("Body-Bold", str(FONT_DIR/"DejaVuSans-Bold.ttf")))
from reportlab.lib.colors import HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.lib.utils import simpleSplit

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Resources/Mieterwechsel'
CONTENT = json.loads((ROOT / 'scripts/checklist-content.json').read_text())
BLACK = HexColor('#24282E'); GRAY = HexColor('#5D636B'); LINE = HexColor('#B8BDC3')
W, H = A4

def wrapped(c, text, x, y, width, font='Body', size=10, leading=14):
    c.setFont(font, size); c.setFillColor(BLACK)
    lines = simpleSplit(text, font, size, width)
    for line in lines:
        c.drawString(x, y, line); y -= leading
    return y

def field(c, name, label, x, y, width, height=19, multiline=False):
    c.setFont('Body', 8); c.setFillColor(GRAY); c.drawString(x, y + height + 5, label)
    c.acroForm.textfield(name=name, tooltip=label, x=x, y=y, width=width, height=height,
        borderStyle='underlined', borderWidth=.5, borderColor=LINE, fillColor=white,
        textColor=BLACK, fontName='Helvetica', fontSize=10, forceBorder=True,
        fieldFlags='multiline' if multiline else '', maxlen=4000 if multiline else 200)

def page(c, phase, number):
    pre = f'p{number:02d}'
    c.setTitle('Checkliste Mieterwechsel – Bruno Grüttner')
    c.setAuthor('Bruno Grüttner Grundstücksverwaltungen Immobilien e.K.')
    c.setFillColor(GRAY); c.setFont('Body', 9)
    c.drawString(44, H-48, 'MIETHÄUSER / ARBEITSHILFE')
    c.drawImage(str(ROOT/'Logo-Bruno-Gruettner.jpg'), W-207, H-76, width=163, height=40,
        preserveAspectRatio=True, anchor='ne', mask='auto')
    c.setFillColor(BLACK); c.setFont('Body-Bold', 22)
    c.drawString(44, H-111, 'Checkliste Mieterwechsel')
    y = wrapped(c, f'{number:02d}  {phase["title"]}', 44, H-141, W-88,
        font='Body-Bold', size=14, leading=18)
    y -= 37
    field(c, pre+'_objekt', 'Objekt / Wohneinheit', 44, y, 295)
    field(c, pre+'_datum', 'Datum', 365, y, W-409)
    y -= 44
    field(c, pre+'_name', 'Bearbeitet von', 44, y, W-88)
    y -= 29
    c.setFont('Body', 8); c.setFillColor(GRAY)
    c.drawString(44, y, 'Erledigte Punkte abhaken. Nicht zutreffende Punkte in den Notizen kennzeichnen.')
    y -= 23
    for i, item in enumerate(phase['items'], 1):
        c.acroForm.checkbox(name=f'{pre}_check_{i:02d}', tooltip=f'{number}.{i}: {item}',
            x=44, y=y-2, size=11, buttonStyle='check', borderWidth=.6,
            borderColor=GRAY, fillColor=white, textColor=BLACK, forceBorder=True)
        y = wrapped(c, item, 64, y, W-108, size=9.5, leading=12.5) - 7
    y -= 3
    c.setStrokeColor(LINE); c.setLineWidth(.5); c.line(44,y,W-44,y); y-=16
    y = wrapped(c, phase['note'], 44, y, W-88, size=8.5, leading=11)
    if phase.get('source'):
        y -= 2; c.setFont('Body',7.5); c.setFillColor(GRAY)
        c.drawString(44,y,'Rechtsgrundlage: '+phase['source_label'])
        c.linkURL(phase['source'], (44,y-2,W-44,y+9), relative=0); y-=13
    y -= 34
    assert y >= 91, (number, y, 'Page overflow')
    field(c, pre+'_notizen', 'Notizen / offene Punkte / nicht zutreffend', 44, 69, W-88,
        height=max(28, y-69), multiline=True)
    c.setStrokeColor(LINE); c.line(44,48,W-44,48)
    c.setFont('Body',7); c.setFillColor(GRAY)
    c.drawString(44,35,'Arbeitsfassung · Stand 17.09.2026 · Persönliche Kopie')
    c.drawRightString(W-44,35,f'Abschnitt {number} / {len(CONTENT)}')
    c.showPage()

def pdf(path, phases):
    c=canvas.Canvas(str(path),pagesize=A4,pageCompression=1)
    for n,p in phases: page(c,p,n)
    c.save()

OUT.mkdir(parents=True,exist_ok=True)
manifest=[{'Name':'Mieterwechsel – Gesamtcheckliste.pdf','File':'mw-v1-gesamt.pdf'}]
procedure=dict(Id='builtin-mieterwechsel-v1',Title='Checkliste Mieterwechsel',Department='Miethäuser',
    Topic='Checkliste Mieterwechsel',Summary='Neun Abschnitte vom Kündigungseingang bis zum Abschluss der Abrechnung. Die zugehörigen PDFs lassen sich als persönliche Kopie speichern, digital abhaken oder ausdrucken.',Updated='17.09.2026',Steps=[])
for n,p in enumerate(CONTENT,1):
    a={'Name':f'Mieterwechsel {n:02d} – {p["title"]}.pdf','File':f'mw-v1-{n:02d}.pdf'}
    manifest.append(a)
    procedure['Steps'].append(dict(Id=f'mw-{n:02d}',Title=p['title'],Owner='',
        Instructions=p['note'],Checklist='\n'.join(p['items']),Next=f'mw-{n+1:02d}' if n<len(CONTENT) else '',
        Otherwise='',Documents=[a]+([manifest[0]] if n==1 else [])))
    pdf(OUT/a['File'],[(n,p)])
pdf(OUT/'mw-v1-gesamt.pdf',list(enumerate(CONTENT,1)))
for name,obj in [('templates.json',manifest),('procedure.json',procedure)]:
    (OUT/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
print(f'Created 10 PDFs; {sum(len(p["items"]) for p in CONTENT)} checkboxes in full checklist.')
