from __future__ import annotations

"""Build the ASRS price sheet from the live prototypes, retaining the recipe appendix."""

import argparse
import json
import re
from io import BytesIO
from pathlib import Path
from xml.sax.saxutils import escape

from pypdf import PdfReader, PdfWriter
from reportlab.lib import colors
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / 'Content.CMU/Resources/Prototypes/CMU14/Economy/asrs_industry_prices.yml'
OUTPUT = ROOT / 'output/pdf/RUMC_industry_catalog_and_pricing.pdf'


def rows_from_registry() -> list[tuple[str, str, str]]:
    text = REGISTRY.read_text(encoding='utf-8')
    blocks = re.split(r'(?=^- type: cmuIndustryPrice\s*$)', text, flags=re.M)
    rows = []
    for block in blocks:
        fields = dict(re.findall(r'^  (product|price|stack|entity|reagent|sellable):\s*(.*?)\s*$', block, flags=re.M))
        if not fields or fields.get('sellable') == 'false':
            continue
        if 'product' not in fields or 'price' not in fields:
            raise ValueError('Incomplete price entry')
        name = fields['product']
        if name.startswith('"'):
            name = json.loads(name)
        identifier = fields.get('stack') or fields.get('entity') or fields.get('reagent')
        if not identifier:
            raise ValueError(f'Unmapped product: {name}')
        rows.append((name, identifier, fields['price']))
    if not rows:
        raise ValueError('No sellable prices found')
    return sorted(rows, key=lambda row: (row[0].casefold(), row[1]))


def build_price_pages(rows: list[tuple[str, str, str]]) -> bytes:
    buf = BytesIO()
    styles = getSampleStyleSheet()
    normal = styles['BodyText']
    normal.fontSize = 8
    normal.leading = 10
    story = [Paragraph('RUMC industry: ASRS opening prices', styles['Title']), Spacer(1, 8),
        Paragraph('Credits per unit. These prices come from the same registry as the in-game price sheet and ASRS. '
                  'The actual offer can fall with shared round demand and faction market discounts. '
                  'Industry goods need the sellable component; only listed fuels and oils can be exported as fluids. '
                  'Returning a crate removes all of its contents, including items that earn no credits.', normal), Spacer(1, 10)]
    header = ParagraphStyle('PriceHeader', parent=normal, textColor=colors.white)
    data = [[Paragraph('<b>Product</b>', header), Paragraph('<b>Prototype</b>', header), Paragraph('<b>Credits / unit</b>', header)]]
    data += [[Paragraph(escape(name), normal), Paragraph(escape(proto), normal), value] for name, proto, value in rows]
    table = Table(data, colWidths=[2.35*inch, 2.85*inch, 1.05*inch], repeatRows=1, hAlign='LEFT')
    table.setStyle(TableStyle([('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#205a6e')),
        ('TEXTCOLOR', (0, 0), (-1, 0), colors.white), ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, colors.HexColor('#edf3f5')]),
        ('GRID', (0, 0), (-1, -1), .25, colors.HexColor('#b3c5cd')), ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('LEFTPADDING', (0, 0), (-1, -1), 5), ('RIGHTPADDING', (0, 0), (-1, -1), 5),
        ('TOPPADDING', (0, 0), (-1, -1), 3), ('BOTTOMPADDING', (0, 0), (-1, -1), 3)]))
    story.append(table)
    SimpleDocTemplate(buf, pagesize=letter, leftMargin=.6*inch, rightMargin=.6*inch,
                      topMargin=.55*inch, bottomMargin=.55*inch).build(story)
    return buf.getvalue()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--routes-source', type=Path, default=OUTPUT,
                        help='Existing catalog PDF containing the manufacturing routes appendix')
    args = parser.parse_args()
    route_reader = PdfReader(args.routes_source)
    route_start = next((i for i, page in enumerate(route_reader.pages)
                        if 'Manufacturing routes' in (page.extract_text() or '')), None)
    if route_start is None:
        raise SystemExit('The source PDF has no Manufacturing routes appendix; no file was changed.')
    rows = rows_from_registry()
    writer = PdfWriter()
    for page in PdfReader(BytesIO(build_price_pages(rows))).pages:
        writer.add_page(page)
    for page in route_reader.pages[route_start:]:
        writer.add_page(page)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT.open('wb') as output:
        writer.write(output)
    print(f'Wrote {OUTPUT}: {len(rows)} registry prices, {len(route_reader.pages) - route_start} route pages')


if __name__ == '__main__':
    main()
