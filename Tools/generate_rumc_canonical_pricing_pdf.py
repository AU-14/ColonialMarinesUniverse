from __future__ import annotations

import math
import re
import statistics
import subprocess
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import date
from pathlib import Path

import pdfplumber
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter, landscape
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
PDF_PATH = ROOT / "output" / "pdf" / "RUMC_industry_catalog_and_pricing.pdf"
SOURCE_PDF_PATH = ROOT / "tmp" / "pdfs" / "RUMC_industry_catalog_route_source.pdf"
SOURCE_REVISION = "605bbf60ba"

CATEGORY_NAMES = (
    "Components and power",
    "Fuels and oils",
    "Gems and jewelry",
    "Materials and refining",
    "Medical and chemical goods",
    "Other manufactured goods",
    "Textiles and leather",
    "Tools and equipment",
)

CATEGORY_PRIORITY = {name: index for index, name in enumerate(CATEGORY_NAMES)}


@dataclass(frozen=True)
class InputItem:
    quantity: float
    name: str
    key: str


@dataclass(frozen=True)
class Route:
    category: str
    route: str
    machine: str
    inputs: tuple[InputItem, ...]
    output: str
    output_key: str
    seconds: float
    old_input_cost: float
    process_add: float
    old_payout: float


def clean(value: str | None) -> str:
    return re.sub(r"\s+", " ", value or "").strip()


def key(value: str) -> str:
    return clean(value).casefold()


def number(value: str | None) -> float:
    value = clean(value)
    if not value or value == "-":
        return 0.0
    return float(value)


def parse_category_route(value: str | None) -> tuple[str, str]:
    text = clean(value)
    for category in CATEGORY_NAMES:
        if text.casefold().startswith(category.casefold()):
            return category, text[len(category):].strip()
    return "Other manufactured goods", text


def parse_inputs(value: str | None) -> tuple[InputItem, ...]:
    text = clean(value)
    if not text or text.casefold() == "none":
        return ()

    parts = re.split(r",\s*(?=\d+(?:\.\d+)?\s+x\s+)", text)
    result: list[InputItem] = []
    for part in parts:
        match = re.match(r"^(\d+(?:\.\d+)?)\s+x\s+(.+)$", part)
        if not match:
            continue
        name = clean(match.group(2))
        result.append(InputItem(float(match.group(1)), name, key(name)))
    return tuple(result)


def parse_output(value: str | None) -> str:
    return re.sub(r"^1\s+x\s+", "", clean(value), flags=re.IGNORECASE).strip()


def extract_routes(path: Path) -> list[Route]:
    routes: list[Route] = []
    with pdfplumber.open(path) as pdf:
        for page in pdf.pages:
            for table in page.extract_tables():
                for row in table or []:
                    if len(row) != 10 or clean(row[0]) == "Category / route":
                        continue
                    category, route_name = parse_category_route(row[0])
                    output = parse_output(row[3])
                    if not route_name or not output:
                        continue
                    routes.append(
                        Route(
                            category=category,
                            route=route_name,
                            machine=clean(row[1]),
                            inputs=parse_inputs(row[2]),
                            output=output,
                            output_key=key(output),
                            seconds=number(row[4]),
                            old_input_cost=number(row[5]),
                            process_add=max(1.0, number(row[6])),
                            old_payout=number(row[8]),
                        )
                    )
    if len(routes) != 542:
        raise RuntimeError(f"Expected 542 source rows, extracted {len(routes)}")
    return routes


def restore_route_source() -> Path:
    SOURCE_PDF_PATH.parent.mkdir(parents=True, exist_ok=True)
    source_object = f"{SOURCE_REVISION}:output/pdf/RUMC_industry_catalog_and_pricing.pdf"
    with SOURCE_PDF_PATH.open("wb") as stream:
        subprocess.run(
            ["git", "show", source_object],
            cwd=ROOT,
            stdout=stream,
            check=True,
        )
    return SOURCE_PDF_PATH


def round_price(value: float) -> float:
    value = max(0.5, value)
    if value < 10:
        return math.ceil(value * 2 - 1e-9) / 2
    if value < 100:
        return math.ceil(value / 5 - 1e-9) * 5
    return math.ceil(value / 10 - 1e-9) * 10


def estimate_external_bases(routes: list[Route]) -> tuple[dict[str, float], dict[str, str]]:
    output_keys = {route.output_key for route in routes}
    display_names: dict[str, str] = {}
    for route in routes:
        for item in route.inputs:
            display_names.setdefault(item.key, item.name)

    old_products: dict[str, float] = {}
    for output_key in output_keys:
        candidates = [
            route.old_payout
            for route in routes
            if route.output_key == output_key
            and route.old_payout > 0
            and route.machine.casefold() != "recycler"
        ]
        if not candidates:
            candidates = [route.old_payout for route in routes if route.output_key == output_key and route.old_payout > 0]
        if candidates:
            old_products[output_key] = min(candidates)

    estimates: dict[str, list[float]] = defaultdict(list)
    equations: list[tuple[Route, list[InputItem], float]] = []
    for route in routes:
        if not route.inputs or route.old_input_cost <= 0:
            continue
        known = sum(item.quantity * old_products[item.key] for item in route.inputs if item.key in old_products)
        unknown = [item for item in route.inputs if item.key not in output_keys]
        residual = route.old_input_cost - known
        if unknown and residual > 0:
            equations.append((route, unknown, residual))
            distinct = {item.key for item in unknown}
            if len(distinct) == 1:
                quantity = sum(item.quantity for item in unknown)
                estimates[unknown[0].key].append(residual / quantity)

    for _, unknown, residual in equations:
        quantity = sum(item.quantity for item in unknown)
        if quantity <= 0:
            continue
        shared = residual / quantity
        for item in unknown:
            if not estimates[item.key]:
                estimates[item.key].append(shared)

    bases: dict[str, float] = {}
    for input_key, display in display_names.items():
        if input_key in output_keys:
            continue
        values = [value for value in estimates[input_key] if 0 < value < 5000]
        if values:
            value = statistics.median(values)
        else:
            lowered = display.casefold()
            if "water" in lowered:
                value = 0.5
            elif "oxygen" in lowered or "hydrogen" in lowered:
                value = 1
            elif "ore" in lowered:
                value = 20
            elif "crystal" in lowered:
                value = 25
            elif lowered in {"branch", "plant fiber"}:
                value = 2
            elif lowered in {"wood", "coal", "clay", "cotton"}:
                value = 5
            else:
                value = 10
        bases[input_key] = round_price(value)

    # Stable, explicit bases for the fundamental resources players gather directly.
    overrides = {
        "water": 0.5,
        "oxygen": 1,
        "branch": 2,
        "plant fiber": 2,
        "wood": 5,
        "coal": 8,
        "clay": 5,
        "cotton": 5,
        "stone fragment": 5,
        "black stone fragment": 8,
        "aluminum ore": 25,
        "chrome ore": 30,
        "cobalt ore": 45,
        "copper ore": 20,
        "gold ore": 50,
        "iron ore": 15,
        "lead ore": 25,
        "platinum ore": 40,
        "silver ore": 45,
        "titanium ore": 35,
        "tungsten ore": 50,
        "uranium ore": 60,
        "zinc ore": 25,
        "alexandrite crystal": 20,
        "asbestos crystal": 10,
        "beryllium crystal": 20,
        "cinnabar crystal": 20,
        "coltan crystal": 25,
        "emerald crystal": 120,
        "fluorite crystal": 15,
        "laxanite crystal": 20,
        "laytoite crystal": 20,
        "phosphorus crystal": 15,
        "quartz crystal": 10,
        "rough diamond crystal": 120,
        "ruby crystal": 120,
        "saltpeter crystal": 25,
        "trimorid crystal": 25,
        "reagent-name-au14-crude-oil": 8,
    }
    for raw_key, value in overrides.items():
        if raw_key in display_names:
            bases[raw_key] = value

    return bases, display_names


def calculate_prices(
    routes: list[Route],
    external_bases: dict[str, float],
) -> tuple[dict[str, float], dict[str, Route], dict[str, tuple[float, float]]]:
    prices: dict[str, float] = dict(external_bases)
    best_route: dict[str, Route] = {}
    components: dict[str, tuple[float, float]] = {}

    usable = [
        route
        for route in routes
        if route.inputs
        and route.machine.casefold() != "recycler"
        and "recycle" not in route.route.casefold()
    ]

    for _ in range(80):
        changed = False
        for route in usable:
            if any(item.key not in prices for item in route.inputs):
                continue
            input_value = sum(item.quantity * prices[item.key] for item in route.inputs)
            candidate = input_value + route.process_add
            if candidate <= 0:
                continue
            previous = prices.get(route.output_key, math.inf)
            if candidate + 1e-7 < previous:
                prices[route.output_key] = candidate
                best_route[route.output_key] = route
                components[route.output_key] = (input_value, route.process_add)
                changed = True
        if not changed:
            break

    for output_key in {route.output_key for route in routes}:
        if output_key in prices:
            if output_key not in best_route:
                representative = min(
                    (route for route in routes if route.output_key == output_key),
                    key=lambda item: item.old_input_cost + item.process_add,
                )
                best_route[output_key] = representative
                components[output_key] = (prices[output_key], 0.0)
            continue
        candidates = [
            route
            for route in routes
            if route.output_key == output_key
            and route.machine.casefold() != "recycler"
        ]
        if not candidates:
            candidates = [route for route in routes if route.output_key == output_key]
        route = min(candidates, key=lambda item: item.old_input_cost + item.process_add)
        fallback = route.old_input_cost + route.process_add
        if fallback <= 0:
            fallback = max(1.0, route.old_payout)
        prices[output_key] = fallback
        best_route[output_key] = route
        components[output_key] = (route.old_input_cost, route.process_add)

    rounded = {product_key: round_price(value) for product_key, value in prices.items()}
    for output_key, route in best_route.items():
        if route.inputs and all(item.key in rounded for item in route.inputs):
            input_value = sum(item.quantity * rounded[item.key] for item in route.inputs)
            components[output_key] = (input_value, route.process_add)
            rounded[output_key] = round_price(input_value + route.process_add)

    return rounded, best_route, components


def fmt(value: float) -> str:
    if abs(value - round(value)) < 1e-9:
        return f"{int(round(value)):,}"
    return f"{value:,.2f}".rstrip("0").rstrip(".")


def primary_raw_key(name: str) -> bool:
    lowered = name.casefold()
    return (
        " ore" in lowered
        or lowered.endswith("ore")
        or " crystal" in lowered
        or lowered in {
            "water",
            "oxygen",
            "branch",
            "plant fiber",
            "wood",
            "coal",
            "clay",
            "cotton",
            "stone fragment",
            "black stone fragment",
            "reagent-name-au14-crude-oil",
        }
    )


def build_pdf(
    path: Path,
    routes: list[Route],
    prices: dict[str, float],
    external_bases: dict[str, float],
    display_names: dict[str, str],
    best_route: dict[str, Route],
    components: dict[str, tuple[float, float]],
) -> None:
    page_size = landscape(letter)
    width, height = page_size
    margin = 0.42 * inch
    frame = Frame(margin, margin + 0.20 * inch, width - 2 * margin, height - 2 * margin - 0.20 * inch, id="body")

    doc = BaseDocTemplate(
        str(path),
        pagesize=page_size,
        leftMargin=margin,
        rightMargin=margin,
        topMargin=margin,
        bottomMargin=margin,
        title="RUMC Canonical Industry Pricing",
        author="Colonial Marines Universe",
        subject="One canonical additive price per RUMC product prototype",
    )

    def footer(canvas, document):
        canvas.saveState()
        canvas.setFillColor(colors.HexColor("#53616A"))
        canvas.setFont("Helvetica", 6.5)
        canvas.drawString(margin, 0.23 * inch, "RUMC canonical pricing - raw base plus processing value - 2026-11-15")
        canvas.drawRightString(width - margin, 0.23 * inch, f"Page {document.page}")
        canvas.restoreState()

    doc.addPageTemplates([PageTemplate(id="catalog", frames=[frame], onPage=footer)])

    styles = getSampleStyleSheet()
    title = ParagraphStyle(
        "TitleCustom",
        parent=styles["Title"],
        fontName="Helvetica-Bold",
        fontSize=24,
        leading=29,
        textColor=colors.HexColor("#174E60"),
        alignment=TA_CENTER,
        spaceAfter=12,
    )
    subtitle = ParagraphStyle(
        "Subtitle",
        parent=styles["BodyText"],
        fontName="Helvetica",
        fontSize=10,
        leading=14,
        textColor=colors.HexColor("#33434C"),
        alignment=TA_CENTER,
    )
    heading = ParagraphStyle(
        "HeadingCustom",
        parent=styles["Heading1"],
        fontName="Helvetica-Bold",
        fontSize=15,
        leading=18,
        textColor=colors.HexColor("#174E60"),
        spaceAfter=8,
    )
    body = ParagraphStyle(
        "BodyCustom",
        parent=styles["BodyText"],
        fontName="Helvetica",
        fontSize=8.5,
        leading=12,
        textColor=colors.HexColor("#26343B"),
        spaceAfter=6,
    )
    small = ParagraphStyle(
        "Small",
        parent=body,
        fontSize=7.2,
        leading=9,
        spaceAfter=0,
    )
    cell = ParagraphStyle(
        "Cell",
        parent=body,
        fontSize=6.4,
        leading=7.5,
        spaceAfter=0,
    )
    cell_right = ParagraphStyle("CellRight", parent=cell, alignment=2)
    header = ParagraphStyle(
        "Header",
        parent=cell,
        fontName="Helvetica-Bold",
        textColor=colors.white,
        alignment=TA_LEFT,
    )

    story = [
        Spacer(1, 0.72 * inch),
        Paragraph("RUMC Canonical Industry Pricing", title),
        Paragraph("One product prototype, one price", subtitle),
        Spacer(1, 0.18 * inch),
        Paragraph(
            "Every directly gathered raw resource has a fixed base value. Washing, grinding, sifting, smelting, pressing, assembly, and every other transformation add processing value to the materials consumed. A product never receives a different sale value merely because it was made by a different route.",
            ParagraphStyle("Lead", parent=subtitle, fontSize=11, leading=16),
        ),
        Spacer(1, 0.28 * inch),
    ]

    summary_data = [
        [Paragraph("Pricing scope", header), Paragraph("Count", header), Paragraph("Result", header)],
        [Paragraph("Source production rows", cell), Paragraph("542", cell_right), Paragraph("Used as route data only", cell)],
        [Paragraph("Unique priced products", cell), Paragraph(str(len(best_route)), cell_right), Paragraph("Exactly one canonical sale price per product", cell)],
        [Paragraph("Duplicate product listings", cell), Paragraph("0", cell_right), Paragraph("Alternative routes are consolidated", cell)],
        [Paragraph("Permitted fluids", cell), Paragraph("Fuels and oils", cell_right), Paragraph("Other reagents, water, acids, and gases remain inputs only", cell)],
    ]
    summary = Table(summary_data, colWidths=[2.2 * inch, 1.2 * inch, 4.1 * inch], repeatRows=1)
    summary.setStyle(table_style())
    story.extend([summary, PageBreak()])

    story.extend(
        [
            Paragraph("Canonical additive pricing rules", heading),
            Paragraph(
                "1. Raw base: each directly gathered material has one fixed base price B. Raw resources do not inherit a category margin.",
                body,
            ),
            Paragraph(
                "2. Processing increase: each recipe adds its normalized time and handling allowance F. F is always positive, so transforming material always increases its value.",
                body,
            ),
            Paragraph(
                "3. Route value: C = sum(input quantity x canonical input price) + F. Inputs are valued using their own canonical prices, which carries all earlier processing value forward.",
                body,
            ),
            Paragraph(
                "4. One price per product: when several recipes make the same prototype, the canonical price uses the least-cost valid production route. This prevents a cheap route from creating an ASRS resale exploit while still valuing every transformation.",
                body,
            ),
            Paragraph(
                "5. Co-products: recipe inputs and processing are normalized per output unit before valuation. Co-products do not duplicate the final catalog entry.",
                body,
            ),
            Paragraph(
                "6. Rounding: values under 10 round upward to 0.5; values from 10 to 99.99 round upward to 5; values of 100 or more round upward to 10. Import value is 1.5 times the canonical price using the same rounding.",
                body,
            ),
            Spacer(1, 0.08 * inch),
            Paragraph("Formula", heading),
        ]
    )
    formula_data = [
        [Paragraph("Stage", header), Paragraph("Rule", header)],
        [Paragraph("Raw resource", cell), Paragraph("P(raw) = B(raw)", cell)],
        [Paragraph("Processed product by route r", cell), Paragraph("C(r, product) = sum(qi x P(input i)) + F(r)", cell)],
        [Paragraph("Canonical product price", cell), Paragraph("P(product) = round up(minimum valid C(r, product))", cell)],
        [Paragraph("Import price", cell), Paragraph("I(product) = round up(1.5 x P(product))", cell)],
    ]
    formula_table = Table(formula_data, colWidths=[2.25 * inch, 5.25 * inch], repeatRows=1)
    formula_table.setStyle(table_style())
    story.extend([formula_table, PageBreak()])

    story.append(Paragraph("Raw-material base prices", heading))
    story.append(
        Paragraph(
            "These values anchor the entire production graph. They are sale values per raw unit before any industrial processing. Names are the prototype display names used by the production catalog.",
            body,
        )
    )
    raw_rows = [
        (display_names[raw_key], external_bases[raw_key])
        for raw_key in external_bases
        if raw_key in display_names and primary_raw_key(display_names[raw_key])
    ]
    raw_rows.sort(key=lambda item: item[0].casefold())
    split = math.ceil(len(raw_rows) / 2)
    left_rows = raw_rows[:split]
    right_rows = raw_rows[split:]
    raw_data = [[
        Paragraph("Raw material", header),
        Paragraph("Base price/unit", header),
        Paragraph("Raw material", header),
        Paragraph("Base price/unit", header),
    ]]
    for index, (left_name, left_value) in enumerate(left_rows):
        if index < len(right_rows):
            right_name, right_value = right_rows[index]
            right_cells = [Paragraph(right_name, cell), Paragraph(fmt(right_value), cell_right)]
        else:
            right_cells = [Paragraph("", cell), Paragraph("", cell_right)]
        raw_data.append([
            Paragraph(left_name, cell),
            Paragraph(fmt(left_value), cell_right),
            *right_cells,
        ])
    raw_table = Table(raw_data, colWidths=[2.85 * inch, 0.9 * inch, 2.85 * inch, 0.9 * inch], repeatRows=1)
    raw_table.setStyle(table_style())
    story.extend([raw_table, PageBreak()])

    story.append(Paragraph("Canonical product catalog", heading))
    story.append(
        Paragraph(
            "Each product appears once. The reference route is the least-cost route used to establish its canonical price; other valid recipes still produce the same prototype at this same ASRS value.",
            body,
        )
    )

    routes_by_output: dict[str, list[Route]] = defaultdict(list)
    for route in routes:
        routes_by_output[route.output_key].append(route)

    catalog_rows = []
    for output_key, route in best_route.items():
        output_name = min((item.output for item in routes_by_output[output_key]), key=lambda value: (len(value), value.casefold()))
        input_value, process_add = components[output_key]
        category_counts = Counter(item.category for item in routes_by_output[output_key])
        category = min(
            category_counts,
            key=lambda value: (-category_counts[value], CATEGORY_PRIORITY.get(value, 999), value),
        )
        sale = prices[output_key]
        import_price = round_price(sale * 1.5)
        reference = "Raw resource base" if output_key in external_bases else f"{route.machine}: {route.route}"
        catalog_rows.append((category, output_name, input_value, process_add, sale, import_price, reference))

    catalog_rows.sort(key=lambda item: (CATEGORY_PRIORITY.get(item[0], 999), item[1].casefold()))
    catalog_data = [[
        Paragraph("Category", header),
        Paragraph("Product prototype / unit", header),
        Paragraph("Carried input value", header),
        Paragraph("Processing increase", header),
        Paragraph("Canonical ASRS price", header),
        Paragraph("Import price", header),
        Paragraph("Reference route", header),
    ]]
    for category, output, input_value, process_add, sale, import_price, reference in catalog_rows:
        catalog_data.append([
            Paragraph(category, cell),
            Paragraph(output, cell),
            Paragraph(fmt(input_value), cell_right),
            Paragraph("+" + fmt(process_add), cell_right),
            Paragraph(fmt(sale), cell_right),
            Paragraph(fmt(import_price), cell_right),
            Paragraph(reference, cell),
        ])

    catalog = Table(
        catalog_data,
        colWidths=[1.05 * inch, 1.60 * inch, 0.82 * inch, 0.82 * inch, 0.92 * inch, 0.74 * inch, 3.65 * inch],
        repeatRows=1,
        splitByRow=1,
    )
    catalog.setStyle(table_style())
    story.append(catalog)

    story.extend(
        [
            PageBreak(),
            Paragraph("Implementation notes", heading),
            Paragraph(
                "The price is attached conceptually to the output prototype, not to the recipe. ASRS appraisal and sale logic should therefore query one canonical prototype price. Recipe systems may continue to produce the same product through any valid machine without changing its sale value.",
                body,
            ),
            Paragraph(
                "Tangible products remain eligible. Fuel and oil products remain the only fluid exceptions. Water, process reagents, acids, gases, and other non-fuel fluids remain non-sellable even when they carry an input value for manufacturing calculations. Capital machinery remains outside the product pricing catalog.",
                body,
            ),
            Paragraph(
                "This document replaces route-specific producer margins with additive material and processing value. It is intended to be the canonical price schedule used when static prices are applied to RUMC output prototypes.",
                body,
            ),
        ]
    )

    doc.build(story)


def table_style() -> TableStyle:
    return TableStyle(
        [
            ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#205A6E")),
            ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#9DB2BC")),
            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#EDF3F5")]),
            ("LEFTPADDING", (0, 0), (-1, -1), 3),
            ("RIGHTPADDING", (0, 0), (-1, -1), 3),
            ("TOPPADDING", (0, 0), (-1, -1), 2.5),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 2.5),
        ]
    )


def main() -> None:
    routes = extract_routes(restore_route_source())
    external_bases, display_names = estimate_external_bases(routes)
    prices, best_route, components = calculate_prices(routes, external_bases)
    if len(best_route) != 315:
        raise RuntimeError(f"Expected 315 unique products, calculated {len(best_route)}")
    build_pdf(PDF_PATH, routes, prices, external_bases, display_names, best_route, components)
    print(f"Wrote {PDF_PATH}")
    print(f"Raw bases: {sum(primary_raw_key(display_names[k]) for k in external_bases if k in display_names)}")
    print(f"Canonical products: {len(best_route)}")


if __name__ == "__main__":
    main()
