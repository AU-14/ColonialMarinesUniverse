from __future__ import annotations

import math
import json
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
BASELINE_PRICE_REVISION = "f748e1d4f4"
PRICES_PATH = ROOT / "Content.CMU" / "Resources" / "Prototypes" / "CMU14" / "Economy" / "asrs_industry_prices.yml"
PROTOTYPES_PATH = ROOT / "Content.CMU" / "Resources" / "Prototypes" / "CMU14"

# 2026-11-22 economy pass. Raw ores and crystals take the larger reduction;
# every processed or otherwise catalogued product takes the general reduction.
RAW_RESOURCE_MULTIPLIER = 0.50
GENERAL_PRODUCT_MULTIPLIER = 0.75
PROCESSING_MARGIN = 1.10
REFINED_GEMS = {
    "alexandrite": "alexandrite crystal",
    "diamond": "rough diamond crystal",
    "emerald": "emerald crystal",
    "ruby": "ruby crystal",
}

# Names follow the canonical registry, including the historical chrome/chromium split.
# Powder must be worth more per unit than its raw ore or crystal because the guide
# and appraisal tool display unit prices. Complete batches are checked as well.
# Ingots and sheets must then clear the full cost of the preceding stage.
METAL_PROCESSING_CHAINS = {
    "aluminum ore": ("aluminum powder", "aluminum ingot", "aluminum sheet"),
    "chrome ore": ("chromium powder", "chromium ingot", "chrome sheet"),
    "cobalt ore": ("cobalt powder", "cobalt ingot", "cobalt sheet"),
    "copper ore": ("copper powder", "copper ingot", "copper sheet"),
    "gold ore": ("gold powder", "gold ingot", "gold sheet"),
    "iron ore": ("iron powder", "iron ingot", "steel"),
    "lead ore": ("lead powder", "lead ingot", "lead sheet"),
    "platinum ore": ("platinum powder", "platinum ingot", "platinum sheet"),
    "silver ore": ("silver powder", "silver ingot", "silver sheet"),
    "titanium ore": ("titanium powder", "titanium ingot", "titanium sheet"),
    "tungsten ore": ("tungsten powder", "tungsten ingot", "tungsten sheet"),
    "uranium ore": ("uranium powder", None, None),
    "zinc ore": ("zinc powder", "zinc ingot", "zinc sheet"),
}

CRYSTAL_PROCESSING_CHAINS = {
    "alexandrite crystal": ("alexandrite powder", "alexandrite", "alexandrite lens"),
    "asbestos crystal": ("asbestos powder", None, None),
    "beryllium crystal": ("beryllium powder", None, None),
    "cinnabar crystal": ("cinnabar powder", None, None),
    "coltan crystal": ("coltan powder", None, None),
    "emerald crystal": ("emerald powder", "emerald", "emerald lens"),
    "fluorite crystal": ("fluorite powder", None, None),
    "laxanite crystal": ("laxanite powder", None, None),
    "laytoite crystal": ("laytoite powder", None, None),
    "phosphorus crystal": ("phosphorus powder", None, None),
    "quartz crystal": ("quartz powder", None, None),
    "rough diamond crystal": ("diamond dust", "diamond", "diamond lens"),
    "ruby crystal": ("ruby powder", "ruby", "ruby lens"),
    "saltpeter crystal": ("saltpeter powder", None, None),
    "trimorid crystal": ("trimorid powder", None, None),
}

# These are the actual per-fluid-unit values consumed by PricingSystem.  Keep
# the generated registry/PDF in lockstep with the reagent prototypes so the
# guide, appraisal tool, tank examine text, and ASRS payout cannot disagree.
PREVIOUS_SELLABLE_FLUID_PRICES = {
    "reagent-name-au14-crude-oil": 0.10,
    "reagent-name-au14-naphtha": 0.50,
    "reagent-name-au14-light-fuel": 0.50,
    "reagent-name-au14-heavy-fuel": 0.25,
    "reagent-name-au14-diesel": 1.00,
}
SELLABLE_FLUID_DISPLAY_NAMES = {
    "reagent-name-au14-crude-oil": "crude oil",
    "reagent-name-au14-naphtha": "naphtha",
    "reagent-name-au14-light-fuel": "light fuel",
    "reagent-name-au14-heavy-fuel": "heavy fuel",
    "reagent-name-au14-diesel": "diesel",
}

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


@dataclass(frozen=True)
class LiveRecipe:
    """A consumed-input/output batch read from the current prototype YAML."""

    id: str
    machine: str
    inputs: tuple[InputItem, ...]
    outputs: tuple[InputItem, ...]


def clean(value: str | None) -> str:
    return re.sub(r"\s+", " ", value or "").strip()


def capital_machine_names() -> set[str]:
    """Read capital machinery names from the renamed RUMCMachine prototype family."""
    names: set[str] = set()
    for path in PROTOTYPES_PATH.rglob("*.yml"):
        text = path.read_text(encoding="utf-8")
        for block in re.split(r"(?=^- type: )", text, flags=re.MULTILINE):
            id_match = re.search(r"^  id:\s*(RUMCMachine\S+)\s*$", block, flags=re.MULTILINE)
            if not id_match:
                continue
            name_match = re.search(r"^  name:\s*[\"']?([^\n\"']+)[\"']?\s*$", block, flags=re.MULTILINE)
            if name_match:
                names.add(key(name_match.group(1)))
    return names


def key(value: str) -> str:
    return clean(value).casefold()


def product_display_name(value: str) -> str:
    return SELLABLE_FLUID_DISPLAY_NAMES.get(key(value), value)


def yaml_scalar(block: str, field: str) -> str | None:
    prefix = r"- " if field == "type" else r"  "
    match = re.search(rf"^{prefix}{re.escape(field)}:\s*(.*?)\s*$", block, flags=re.MULTILINE)
    if not match:
        return None
    value = re.sub(r"\s+#.*$", "", match.group(1)).strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
        value = value[1:-1]
    return value or None


def yaml_mapping(block: str, field: str) -> list[tuple[str, float]]:
    """Read the simple scalar dictionaries used by RUMC recipe prototypes."""
    inline = re.search(rf"^  {re.escape(field)}:\s*\{{(.*?)\}}\s*$", block, flags=re.MULTILINE)
    if inline:
        result = []
        for entry in inline.group(1).split(","):
            if ":" not in entry:
                continue
            item, amount = entry.split(":", 1)
            result.append((item.strip(), float(amount.strip())))
        return result

    header = re.search(rf"^  {re.escape(field)}:\s*$", block, flags=re.MULTILINE)
    if not header:
        return []
    result = []
    for line in block[header.end():].splitlines():
        if line and not line.startswith("    "):
            break
        match = re.match(r"^    ([^:#][^:]*):\s*([-+]?[0-9]+(?:\.[0-9]+)?)\s*(?:#.*)?$", line)
        if match:
            result.append((match.group(1).strip(), float(match.group(2))))
    return result


def extract_live_recipes(
    prices: dict[str, float],
) -> tuple[list[LiveRecipe], dict[str, int], dict[str, dict[str, set[str]]]]:
    """Read current assembly/fabricator recipes with their real batch quantities.

    These two prototype families account for nearly every modern RUMC production
    recipe. Reading them directly prevents a stale catalog baseline from pricing
    a wrench (or any other manufactured item) below its current material cost.
    """
    blocks: dict[tuple[str, str], str] = {}
    roots = (ROOT / "Resources" / "Prototypes", ROOT / "Content.CMU" / "Resources" / "Prototypes")
    for root in roots:
        for path in root.rglob("*.yml"):
            text = path.read_text(encoding="utf-8")
            for block in re.split(r"(?=^- type: )", text, flags=re.MULTILINE):
                prototype_type = yaml_scalar(block, "type")
                prototype_id = yaml_scalar(block, "id")
                if prototype_type and prototype_id:
                    blocks[(prototype_type, prototype_id)] = block

    entity_blocks = {prototype_id: block for (prototype_type, prototype_id), block in blocks.items()
        if prototype_type == "entity"}
    stack_blocks = {prototype_id: block for (prototype_type, prototype_id), block in blocks.items()
        if prototype_type == "stack"}
    reagent_blocks = {prototype_id: block for (prototype_type, prototype_id), block in blocks.items()
        if prototype_type == "reagent"}

    locale: dict[str, str] = {}
    for locale_root in (ROOT / "Resources" / "Locale" / "en-US",
                        ROOT / "Content.CMU" / "Resources" / "Locale" / "en-US"):
        for path in locale_root.rglob("*.ftl"):
            for match in re.finditer(r"^([A-Za-z0-9_.-]+)\s*=\s*([^\n{]+?)\s*$",
                                     path.read_text(encoding="utf-8"), flags=re.MULTILINE):
                locale[match.group(1)] = match.group(2).strip()

    name_overrides = {
        "CMSteel": "Steel",
        "CMGlass": "Glass",
        "CMGlassReinforced": "Glass Reinforced",
        "CMRodMetal": "Rod Metal",
        "RMCWood": "Wood",
        "RUMCChromeIngot": "chromium ingot",
        "CMUThinCableLV": "Uthin Cable Lv",
        "CableMV": "Cable Mv",
        "CableHV": "Cable Hv",
        "RUMCElectricScrewdriver": "Electric Screwdriver",
        "RUMCElectricWrench": "Electric Wrench",
        "RUMCElectricWirecutters": "Electric Wirecutters",
        "RUMCElectricCrowbar": "Electric Crowbar",
        "RUMCElectricHammer": "Electric Hammer",
        "RUMCPowerDrill": "Power Drill",
        "RUMCElectricShovel": "Electric Shovel",
        "RUMCElectricPickaxe": "Electric Pickaxe",
        "RUMCProspector": "Prospector",
        "RUMCJackhammer": "Jackhammer",
        "RMCPouchGeneralLarge": "large general pouch",
    }

    resolved_entities: dict[str, str | None] = {}

    def entity_name(prototype_id: str, seen: set[str] | None = None) -> str | None:
        if prototype_id in resolved_entities:
            return resolved_entities[prototype_id]
        if seen is None:
            seen = set()
        if prototype_id in seen:
            return None
        seen.add(prototype_id)
        block = entity_blocks.get(prototype_id)
        if block is None:
            resolved_entities[prototype_id] = None
            return None
        name = yaml_scalar(block, "name")
        if name is None:
            parent = yaml_scalar(block, "parent")
            parents: list[str] = []
            if parent:
                if parent.startswith("[") and parent.endswith("]"):
                    parents.extend(item.strip() for item in parent[1:-1].split(",") if item.strip())
                else:
                    parents.append(parent)
            else:
                parent_block = re.search(r"^  parent:\s*$", block, flags=re.MULTILINE)
                if parent_block:
                    for line in block[parent_block.end():].splitlines():
                        match = re.match(r"^  -\s*([^#\s]+)", line)
                        if not match:
                            break
                        parents.append(match.group(1))
            for parent_id in parents:
                name = entity_name(parent_id, seen)
                if name:
                    break
        resolved_entities[prototype_id] = name
        return name

    def stack_name(prototype_id: str) -> str | None:
        block = stack_blocks.get(prototype_id)
        if block is None:
            return None
        return yaml_scalar(block, "name") or entity_name(yaml_scalar(block, "spawn") or "")

    def reagent_name(prototype_id: str) -> str | None:
        block = reagent_blocks.get(prototype_id)
        return yaml_scalar(block, "name") if block else None

    def material(prototype_id: str, amount: float, kind: str, output: bool = False) -> InputItem | None:
        name = name_overrides.get(prototype_id) or (stack_name(prototype_id) if kind == "stack" else
            entity_name(prototype_id) if kind == "entity" else reagent_name(prototype_id))
        if not name:
            return None
        candidates = [name]
        if name in locale:
            candidates.append(locale[name])
        resolved = next((candidate for candidate in candidates if key(candidate) in prices), candidates[-1])
        if kind == "fluid" and output and key(resolved) not in prices:
            return None
        return InputItem(amount, resolved, key(resolved))

    recipes: list[LiveRecipe] = []
    skipped = Counter()
    bindings: dict[str, dict[str, set[str]]] = defaultdict(lambda: {"stack": set(), "entity": set()})
    for (prototype_type, prototype_id), block in blocks.items():
        if prototype_type == "cmuMachineRecipe":
            machine = yaml_scalar(block, "machine") or ""
            if machine.casefold() == "recycler":
                skipped["recycler"] += 1
                continue
            raw_inputs = [
                *((item, amount, "stack") for item, amount in yaml_mapping(block, "inputs")),
                *((item, amount, "entity") for item, amount in yaml_mapping(block, "itemInputs")),
                *((item, amount, "fluid") for item, amount in yaml_mapping(block, "fluidInputs")),
            ]
            raw_outputs = [
                *((item, amount, "stack") for item, amount in yaml_mapping(block, "outputs")),
                *((item, amount, "entity") for item, amount in yaml_mapping(block, "itemOutputs")),
                *((item, amount, "fluid") for item, amount in yaml_mapping(block, "fluidOutputs")),
            ]
        elif prototype_type == "cmuAssemblyRecipe":
            machine = "assembly"
            raw_inputs = []
            layout = re.search(r"^  layout:\s*$", block, flags=re.MULTILINE)
            if layout:
                for line in block[layout.end():].splitlines():
                    if line and not line.startswith("    "):
                        break
                    match = re.match(
                        r"^    \d+:\s*\{\s*material:\s*([^, }]+),\s*amount:\s*([0-9.]+)\s*\}\s*$",
                        line,
                    )
                    if match:
                        raw_inputs.append((match.group(1), float(match.group(2)), "stack"))
            result = yaml_scalar(block, "result")
            raw_outputs = [(result, float(yaml_scalar(block, "resultAmount") or 1), "entity")] if result else []
        else:
            continue

        inputs = tuple(material(item, amount, kind) for item, amount, kind in raw_inputs)
        resolved_outputs = [material(item, amount, kind, output=True) for item, amount, kind in raw_outputs]
        if not raw_inputs or any(item is None for item in inputs):
            skipped["unresolved input"] += 1
            continue
        outputs = tuple(item for item in resolved_outputs if item is not None)
        if not outputs:
            skipped[f"unresolved output ({prototype_id})"] += 1
            continue
        for (prototype_id, _, kind), output in zip(raw_outputs, resolved_outputs):
            if output is not None and kind in {"stack", "entity"}:
                bindings[output.key][kind].add(prototype_id)
        recipes.append(LiveRecipe(prototype_id, machine, inputs, outputs))  # type: ignore[arg-type]

    return recipes, dict(skipped), dict(bindings)


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


def load_previous_price_registry() -> dict[str, float]:
    """Load the last committed live prices as the immutable balance baseline."""
    source_object = f"{BASELINE_PRICE_REVISION}:{PRICES_PATH.relative_to(ROOT).as_posix()}"
    result = subprocess.run(
        ["git", "show", source_object],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return {
        key(match.group(1)): float(match.group(2))
        for match in re.finditer(
            r'^  product:\s*"([^"]+)"\s*\n  price:\s*([0-9.]+)\s*$',
            result.stdout,
            flags=re.MULTILINE,
        )
    }


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
        "reagent-name-au14-crude-oil": PREVIOUS_SELLABLE_FLUID_PRICES["reagent-name-au14-crude-oil"],
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
    return f"{value:,.4f}".rstrip("0").rstrip(".")


def yaml_number(value: float) -> str:
    """Format a machine-readable number without display thousands separators."""
    if abs(value - round(value)) < 1e-9:
        return str(int(round(value)))
    return f"{value:.4f}".rstrip("0").rstrip(".")


def primary_raw_key(name: str) -> bool:
    lowered = name.casefold()
    return (
        " ore" in lowered
        or lowered.endswith("ore")
        or " crystal" in lowered
        or lowered in {
            "branch",
            "plant fiber",
            "wood",
            "coal",
            "clay",
            "cotton",
            "stone fragment",
            "black stone fragment",
            "reagent-name-au14-crude-oil",
            "crude oil",
        }
    )


def reduced_raw_key(name: str) -> bool:
    """Raw mined ores and crystals receive the 0.50 balance multiplier."""
    lowered = name.casefold()
    return lowered.endswith(" ore") or lowered.endswith(" crystal")


def apply_balance_revision(
    prices: dict[str, float],
    external_bases: dict[str, float],
    display_names: dict[str, str],
) -> tuple[dict[str, float], dict[str, float], dict[str, str]]:
    """Apply the requested balance multipliers without compounding route calculations.

    Refined gems inherit 75% of the former high-value rough crystal price. This moves
    the valuable stage from the mined crystal to the sifter output while keeping the
    requested 0.50 raw / 0.75 refined relationship explicit.
    """
    revised: dict[str, float] = {}
    rules: dict[str, str] = {}
    for product_key, value in prices.items():
        name = display_names.get(product_key, product_key)
        multiplier = RAW_RESOURCE_MULTIPLIER if reduced_raw_key(name) else GENERAL_PRODUCT_MULTIPLIER
        revised[product_key] = round(value * multiplier, 2)
        rules[product_key] = f"{multiplier:g}x {'raw ore/crystal' if multiplier == RAW_RESOURCE_MULTIPLIER else 'general product'}"

    for refined_name, raw_name in REFINED_GEMS.items():
        refined_key = key(refined_name)
        raw_key = key(raw_name)
        if refined_key not in revised or raw_key not in prices:
            continue
        revised[refined_key] = round(prices[raw_key] * GENERAL_PRODUCT_MULTIPLIER, 2)
        rules[refined_key] = "0.75x former rough-crystal tier"

    # Fluid reagent pricing supports four decimal places. Preserve the exact
    # 75% values instead of rounding 0.1875/0.375/0.075 to cents.
    for fluid_key, previous_price in PREVIOUS_SELLABLE_FLUID_PRICES.items():
        if fluid_key not in revised:
            continue
        revised[fluid_key] = round(previous_price * GENERAL_PRODUCT_MULTIPLIER, 4)
        rules[fluid_key] = "0.75x eligible fuel/oil"

    revised_bases = {
        raw_key: revised.get(raw_key, round(value * GENERAL_PRODUCT_MULTIPLIER, 2))
        for raw_key, value in external_bases.items()
    }
    return revised, revised_bases, rules


def apply_processing_floors(
    prices: dict[str, float],
    external_bases: dict[str, float],
    rules: dict[str, str],
) -> tuple[dict[str, float], dict[str, float], dict[str, str]]:
    """Keep every primary raw-to-processed batch value-positive.

    The balance multipliers are applied first. These floors then repair stage
    inversions caused by independently reducing raw and processed products.
    Waste/byproduct values are deliberately small so separators and refiners do
    not destroy export value merely by recovering useful material from stone.
    """
    revised = dict(prices)
    revised_rules = dict(rules)

    def set_exact(product: str, value: float, reason: str) -> None:
        product_key = key(product)
        if product_key not in revised:
            return
        revised[product_key] = value
        revised_rules[product_key] = reason

    def set_floor(product: str, value: float, reason: str) -> None:
        product_key = key(product)
        if product_key not in revised:
            return
        floor = round_price(value)
        if revised[product_key] + 1e-9 >= floor:
            return
        revised[product_key] = floor
        revised_rules[product_key] = reason

    # Low-stage stone products are recoverable waste, not premium feedstock.
    set_exact("stone fragment", 0.25, "raw/waste correction")
    set_exact("black stone fragment", 0.25, "raw/waste correction")
    set_exact("stone powder", 0.5, "separator-compatible waste value")
    set_exact("black stone powder", 0.5, "separator-compatible waste value")
    set_exact("sand", 1.5, "sifted raw-material value")
    set_exact("raw rubber", 50.0, "extracted raw-material correction")

    stone_powder = revised[key("stone powder")]
    coke = revised.get(key("coke"), 0.0)

    for raw, (powder, ingot, sheet) in METAL_PROCESSING_CHAINS.items():
        raw_key = key(raw)
        powder_key = key(powder)
        if raw_key not in revised or powder_key not in revised:
            continue

        # The appraiser is per unit: one powder must exceed one raw ore, even
        # though a grinder also produces several powder units per ore.
        set_floor(powder, revised[raw_key] * PROCESSING_MARGIN, "raw-to-powder unit floor")

        if ingot is None or key(ingot) not in revised:
            continue
        powder_batch = 3 * revised[powder_key]
        if raw == "iron ore":
            powder_batch += coke
        ingot_floor = max(revised[raw_key], powder_batch) * PROCESSING_MARGIN
        set_floor(ingot, ingot_floor, "ore-to-ingot processing floor")

        if sheet is not None and key(sheet) in revised:
            set_floor(sheet, revised[key(ingot)] * PROCESSING_MARGIN, "ingot-to-sheet processing floor")

    for raw, (powder, gem, lens) in CRYSTAL_PROCESSING_CHAINS.items():
        raw_key = key(raw)
        powder_key = key(powder)
        if raw_key not in revised or powder_key not in revised:
            continue

        set_floor(powder, revised[raw_key] * PROCESSING_MARGIN, "crystal-to-powder unit floor")

        if gem is not None and key(gem) in revised:
            # Sifting yields one gem, one powder and one stone powder. The gem
            # remains the high-value stage even when compared per unit.
            gem_floor = max(revised[raw_key], revised[powder_key]) * PROCESSING_MARGIN
            set_floor(gem, gem_floor, "sifted-gem unit floor")
            if lens is not None and key(lens) in revised:
                set_floor(lens, revised[key(gem)] * PROCESSING_MARGIN, "gem-to-lens unit floor")

    set_floor("rubber sheet", revised[key("raw rubber")] * PROCESSING_MARGIN,
        "raw-rubber processing floor")
    # The wiremill yields two fine wires per ingot. Keep both precious-metal
    # routes profitable after their ingot floors move upward.
    set_floor("fine gold wire", revised[key("gold ingot")] * PROCESSING_MARGIN / 2,
        "gold-ingot wire batch floor")
    set_floor("fine silver wire", revised[key("silver ingot")] * PROCESSING_MARGIN / 2,
        "silver-ingot wire batch floor")

    revised_bases = {
        raw_key: revised.get(raw_key, value)
        for raw_key, value in external_bases.items()
    }
    validate_processing_floors(revised)
    return revised, revised_bases, revised_rules


def validate_processing_floors(prices: dict[str, float]) -> None:
    """Fail generation if a primary processing stage loses canonical ASRS value."""
    failures: list[str] = []
    stone = prices[key("stone powder")]

    for raw, (powder, ingot, sheet) in METAL_PROCESSING_CHAINS.items():
        if key(raw) not in prices or key(powder) not in prices:
            continue
        raw_value = prices[key(raw)]
        powder_value = prices[key(powder)]
        if powder_value <= raw_value:
            failures.append(f"{raw} -> {powder} per unit")
        if 3 * powder_value + 2 * stone <= raw_value:
            failures.append(f"{raw} -> {powder}")
        if ingot is not None and key(ingot) in prices:
            ingot_value = prices[key(ingot)]
            blast_inputs = 3 * powder_value + (prices.get(key("coke"), 0.0) if raw == "iron ore" else 0.0)
            if ingot_value <= raw_value or ingot_value <= blast_inputs:
                failures.append(f"{raw}/{powder} -> {ingot}")
            if sheet is not None and key(sheet) in prices and prices[key(sheet)] <= ingot_value:
                failures.append(f"{ingot} -> {sheet}")

    for raw, (powder, gem, lens) in CRYSTAL_PROCESSING_CHAINS.items():
        if key(raw) not in prices or key(powder) not in prices:
            continue
        raw_value = prices[key(raw)]
        powder_value = prices[key(powder)]
        if powder_value <= raw_value:
            failures.append(f"{raw} -> {powder} per unit")
        if 3 * powder_value + 2 * stone <= raw_value:
            failures.append(f"{raw} -> {powder}")
        if gem is not None and key(gem) in prices:
            gem_value = prices[key(gem)]
            if gem_value <= raw_value or gem_value <= powder_value:
                failures.append(f"{raw}/{powder} -> {gem} per unit")
            if gem_value + powder_value + stone <= raw_value:
                failures.append(f"{raw} -> {gem}")
            if lens is not None and key(lens) in prices:
                if prices[key(lens)] <= gem_value:
                    failures.append(f"{gem} -> {lens} per unit")
                if 2 * prices[key(lens)] <= gem_value:
                    failures.append(f"{gem} -> {lens} batch")

    if prices[key("rubber sheet")] <= prices[key("raw rubber")]:
        failures.append("raw rubber -> rubber sheet")
    if stone <= prices[key("stone fragment")]:
        failures.append("stone fragment -> stone powder per unit")
    if prices[key("black stone powder")] <= prices[key("black stone fragment")]:
        failures.append("black stone fragment -> black stone powder per unit")
    if 4 * prices[key("sand")] <= 4 * stone:
        failures.append("stone powder -> sand")
    if 3 * prices[key("glass")] <= 4 * prices[key("sand")]:
        failures.append("sand -> glass")
    if prices[key("silicon ingot")] <= 3 * prices[key("sand")] + prices[key("coke")]:
        failures.append("sand/coke -> silicon ingot")
    if 2 * prices[key("fine gold wire")] <= prices[key("gold ingot")]:
        failures.append("gold ingot -> fine gold wire")
    if 2 * prices[key("fine silver wire")] <= prices[key("silver ingot")]:
        failures.append("silver ingot -> fine silver wire")

    if failures:
        raise RuntimeError("Processing price inversions: " + ", ".join(failures))


def apply_live_recipe_floors(
    prices: dict[str, float],
    rules: dict[str, str],
) -> tuple[
    dict[str, float],
    dict[str, str],
    dict[str, str],
    dict[str, dict[str, set[str]]],
    int,
    int,
    dict[str, int],
]:
    """Make current assembly/fabricator batches worth 10% more than their inputs.

    The first declared output is the recipe's primary product. Co-products retain
    their own prices and offset the amount that must be carried by that primary
    product. When several recipes make the same product, only the cheapest valid
    route establishes its floor.
    """
    revised = dict(prices)
    revised_rules = dict(rules)
    recipes, skipped, bindings = extract_live_recipes(revised)
    live_names = {item.key: item.name for recipe in recipes for item in recipe.outputs}
    adjusted: set[str] = set()

    def split_outputs(recipe: LiveRecipe) -> tuple[InputItem, tuple[InputItem, ...]]:
        # Prototype dictionaries do not consistently put the intended product
        # first. Use the most valuable pre-floor output as the primary product;
        # this keeps recovered stone or slag byproducts from carrying a batch.
        primary_index = max(range(len(recipe.outputs)), key=lambda index: prices.get(recipe.outputs[index].key, 0.0))
        primary = recipe.outputs[primary_index]
        coproducts = tuple(item for index, item in enumerate(recipe.outputs) if index != primary_index)
        return primary, coproducts

    last_changes: list[str] = []
    for _ in range(80):
        candidates: dict[str, list[tuple[float, LiveRecipe]]] = defaultdict(list)
        for recipe in recipes:
            primary, coproducts = split_outputs(recipe)
            # Beneficiation recipes return their primary input unchanged and add
            # a recovered co-product. Pricing the unchanged material at +10%
            # would create a self-referential inflation loop.
            if any(item.key == primary.key for item in recipe.inputs):
                continue
            if any(item.key not in revised for item in recipe.inputs):
                continue
            input_value = sum(item.quantity * revised[item.key] for item in recipe.inputs)
            coproduct_value = sum(item.quantity * revised.get(item.key, 0.0) for item in coproducts)
            required_primary = max(0.5, (input_value * PROCESSING_MARGIN - coproduct_value) / primary.quantity)
            candidates[primary.key].append((required_primary, recipe))

        changed = False
        last_changes = []
        for product_key, product_candidates in candidates.items():
            required, recipe = min(product_candidates, key=lambda item: item[0])
            floor = round_price(required)
            if revised.get(product_key, 0.0) + 1e-9 >= floor:
                continue
            revised[product_key] = floor
            revised_rules[product_key] = f"live recipe floor ({recipe.machine}: {recipe.id})"
            adjusted.add(product_key)
            last_changes.append(f"{product_key}={floor:g} via {recipe.id}")
            changed = True
        if not changed:
            break
    else:
        raise RuntimeError("Live recipe price floors did not converge after 80 passes; last changes: " +
            ", ".join(last_changes))

    failures = []
    candidates: dict[str, list[tuple[float, LiveRecipe]]] = defaultdict(list)
    for recipe in recipes:
        primary, coproducts = split_outputs(recipe)
        if any(item.key == primary.key for item in recipe.inputs):
            continue
        if any(item.key not in revised for item in recipe.inputs):
            continue
        input_value = sum(item.quantity * revised[item.key] for item in recipe.inputs)
        coproduct_value = sum(item.quantity * revised.get(item.key, 0.0) for item in coproducts)
        required_primary = max(0.5, (input_value * PROCESSING_MARGIN - coproduct_value) / primary.quantity)
        candidates[primary.key].append((required_primary, recipe))
    for product_key, product_candidates in candidates.items():
        required, recipe = min(product_candidates, key=lambda item: item[0])
        if revised[product_key] + 1e-9 < required:
            failures.append(f"{recipe.id}: {product_key} {revised[product_key]:g} < {required:g}")
    if failures:
        raise RuntimeError("Live recipe price inversions: " + ", ".join(failures))

    return revised, revised_rules, live_names, bindings, len(recipes), len(adjusted), skipped


def build_pdf(
    path: Path,
    routes: list[Route],
    prices: dict[str, float],
    external_bases: dict[str, float],
    display_names: dict[str, str],
    best_route: dict[str, Route],
    components: dict[str, tuple[float, float]],
    baseline_prices: dict[str, float],
    balance_rules: dict[str, str],
    live_names: dict[str, str],
    excluded_products: set[str],
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
        pageCompression=1,
        title="RUMC Canonical Industry Pricing",
        author="Colonial Marines Universe",
        subject="One canonical balanced price per eligible RUMC product prototype",
    )

    def footer(canvas, document):
        canvas.saveState()
        canvas.setFillColor(colors.HexColor("#53616A"))
        canvas.setFont("Helvetica", 6.5)
        canvas.drawString(margin, 0.23 * inch, "RUMC canonical pricing - raw and processed balance pass - 2026-11-22")
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
                "Every product keeps one prototype price regardless of its production route. This revision halves raw ore and crystal values, reduces processed and other products to 75% of their prior canonical values, moves the high-value gemstone tier from rough crystals to sifted gems, and applies live recipe-cost floors so manufactured batches cannot be worth less than their consumed materials.",
            ParagraphStyle("Lead", parent=subtitle, fontSize=11, leading=16),
        ),
        Spacer(1, 0.28 * inch),
    ]

    summary_data = [
        [Paragraph("Pricing scope", header), Paragraph("Count", header), Paragraph("Result", header)],
        [Paragraph("Source production rows", cell), Paragraph("542", cell_right), Paragraph("Used as route data only", cell)],
        [
            Paragraph("Unique priced products", cell),
            Paragraph(
                str(
                    len(
                        {product_key for product_key in best_route if product_key not in excluded_products}
                        | {
                            raw_key
                            for raw_key in external_bases
                            if raw_key in display_names and primary_raw_key(display_names[raw_key])
                        }
                    )
                ),
                cell_right,
            ),
            Paragraph("Exactly one canonical sale price per eligible product", cell),
        ],
        [Paragraph("Duplicate product listings", cell), Paragraph("0", cell_right), Paragraph("Alternative routes are consolidated", cell)],
        [Paragraph("Permitted fluids", cell), Paragraph("Fuels and oils", cell_right), Paragraph("Other reagents, water, acids, and gases remain inputs only", cell)],
    ]
    summary = Table(summary_data, colWidths=[2.2 * inch, 1.2 * inch, 4.1 * inch], repeatRows=1)
    summary.setStyle(table_style())
    story.extend([summary, PageBreak()])

    story.extend(
        [
            Paragraph("Canonical pricing rules", heading),
            Paragraph(
                "1. Raw ore and crystal reduction: final price = former canonical price x 0.50.",
                body,
            ),
            Paragraph(
                "2. Processed and other product reduction: final price = former canonical price x 0.75.",
                body,
            ),
            Paragraph(
                "3. Refined gems: diamond, ruby, emerald, and alexandrite produced by the sifter inherit 75% of the former high-value rough-crystal tier. Their rough crystals receive the 50% raw reduction, so refinement is the valuable stage.",
                body,
            ),
            Paragraph(
                "4. One price per product: every route making the same prototype resolves to the same final ASRS unit price.",
                body,
            ),
            Paragraph(
                "5. Co-products do not create duplicate catalog entries. Each tangible output is priced per unit; eligible fuel and oil liquids are priced per fluid unit.",
                body,
            ),
            Paragraph(
                "6. Balance multiplication is retained to two decimal places. Import value remains 1.5 times the final canonical price using the catalog's import rounding rule.",
                body,
            ),
            Paragraph(
                "7. Processing floor: every primary processed unit is worth more than its raw unit. Current assembly and fabricator recipes also receive a 10% complete-batch margin using their cheapest valid route; co-products contribute to the same batch instead of being charged the full input cost individually.",
                body,
            ),
            Spacer(1, 0.08 * inch),
            Paragraph("Formula", heading),
        ]
    )
    formula_data = [
        [Paragraph("Stage", header), Paragraph("Rule", header)],
        [Paragraph("Raw ore or crystal", cell), Paragraph("P(new) = 0.50 x P(previous)", cell)],
        [Paragraph("Processed / other product", cell), Paragraph("P(new) = 0.75 x P(previous)", cell)],
        [Paragraph("Refined gemstone", cell), Paragraph("P(new) = 0.75 x P(previous rough crystal)", cell)],
        [Paragraph("Live manufactured batch", cell), Paragraph("P(outputs) >= 1.10 x P(consumed inputs)", cell)],
        [Paragraph("Import price", cell), Paragraph("I(product) = round up(1.5 x P(new))", cell)],
    ]
    formula_table = Table(formula_data, colWidths=[2.25 * inch, 5.25 * inch], repeatRows=1)
    formula_table.setStyle(table_style())
    story.extend([formula_table, PageBreak()])

    story.append(Paragraph("Raw-material base prices", heading))
    story.append(
        Paragraph(
            "These are the final sale values per directly gathered raw unit after the balance reduction. Names are the prototype display names used by the production catalog.",
            body,
        )
    )
    raw_rows = [
        (product_display_name(display_names[raw_key]), external_bases[raw_key])
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
            "Each product appears once. The baseline column records the prior canonical result, the balance rule shows the applied reduction, and every valid recipe for that prototype receives the same final ASRS value.",
            body,
        )
    )

    routes_by_output: dict[str, list[Route]] = defaultdict(list)
    for route in routes:
        routes_by_output[route.output_key].append(route)

    catalog_rows = []
    catalog_keys: set[str] = set()
    for output_key, route in best_route.items():
        if output_key in excluded_products:
            continue
        output_name = product_display_name(
            min((item.output for item in routes_by_output[output_key]), key=lambda value: (len(value), value.casefold()))
        )
        category_counts = Counter(item.category for item in routes_by_output[output_key])
        category = min(
            category_counts,
            key=lambda value: (-category_counts[value], CATEGORY_PRIORITY.get(value, 999), value),
        )
        sale = prices[output_key]
        import_price = round_price(sale * 1.5)
        reference = "Raw resource base" if output_key in external_bases else f"{route.machine}: {route.route}"
        catalog_rows.append((category, output_name, baseline_prices[output_key], balance_rules[output_key], sale, import_price, reference))
        catalog_keys.add(output_key)

    for output_key, output_name in live_names.items():
        if output_key in catalog_keys or output_key in excluded_products or output_key not in prices:
            continue
        sale = prices[output_key]
        catalog_rows.append((
            "Other manufactured goods",
            output_name,
            baseline_prices.get(output_key, 0.0),
            balance_rules.get(output_key, "live recipe floor"),
            sale,
            round_price(sale * 1.5),
            "Current RUMC recipe prototype",
        ))

    catalog_rows.sort(key=lambda item: (CATEGORY_PRIORITY.get(item[0], 999), item[1].casefold()))
    catalog_data = [[
        Paragraph("Category", header),
        Paragraph("Product prototype / unit", header),
        Paragraph("Previous canonical", header),
        Paragraph("Balance rule", header),
        Paragraph("Canonical ASRS price", header),
        Paragraph("Import price", header),
        Paragraph("Reference route", header),
    ]]
    for category, output, baseline, balance_rule, sale, import_price, reference in catalog_rows:
        catalog_data.append([
            Paragraph(category, cell),
            Paragraph(output, cell),
            Paragraph(fmt(baseline), cell_right),
            Paragraph(balance_rule, cell),
            Paragraph(fmt(sale), cell_right),
            Paragraph(fmt(import_price), cell_right),
            Paragraph(reference, cell),
        ])

    catalog = Table(
        catalog_data,
        colWidths=[1.05 * inch, 1.60 * inch, 0.82 * inch, 1.20 * inch, 0.92 * inch, 0.74 * inch, 3.27 * inch],
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
                "This document records the 2026-11-22 raw-versus-processed balance reduction. It is the canonical price schedule used by ASRS appraisal for RUMC output prototypes.",
                body,
            ),
        ]
    )

    doc.build(story)


def write_price_prototypes(
    path: Path,
    routes: list[Route],
    prices: dict[str, float],
    external_bases: dict[str, float],
    display_names: dict[str, str],
    live_names: dict[str, str],
    live_bindings: dict[str, dict[str, set[str]]],
    excluded_products: set[str],
) -> int:
    products: dict[str, tuple[str, float]] = {}
    routes_by_output: dict[str, list[Route]] = defaultdict(list)
    for route in routes:
        routes_by_output[route.output_key].append(route)

    for output_key, output_routes in routes_by_output.items():
        if output_key in excluded_products:
            continue
        output_name = min((route.output for route in output_routes), key=lambda value: (len(value), value.casefold()))
        products[output_key] = (output_name, prices[output_key])

    for raw_key, raw_price in external_bases.items():
        if raw_key not in display_names or not primary_raw_key(display_names[raw_key]):
            continue
        products[raw_key] = (display_names[raw_key], raw_price)

    for product_key, product_name in live_names.items():
        if product_key in prices and product_key not in excluded_products:
            products[product_key] = (product_name, prices[product_key])

    lines = [
        "# Generated by Tools/generate_rumc_canonical_pricing_pdf.py.",
        "# One entry per canonical product display name; raw bases and processed goods share this registry.",
        "",
    ]
    for index, (product_key, (product_name, price)) in enumerate(sorted(products.items()), 1):
        lines.extend([
            "- type: cmuIndustryPrice",
            f"  id: RUMCIndustryPrice{index:03d}",
            f"  product: {json.dumps(product_name, ensure_ascii=True)}",
            f"  price: {yaml_number(price)}",
        ])
        binding = live_bindings.get(product_key, {})
        stacks = sorted(binding.get("stack", set()))
        entities = sorted(binding.get("entity", set()))
        if stacks:
            lines.append(f"  stacks: {json.dumps(stacks)}")
        if entities:
            lines.append(f"  entities: {json.dumps(entities)}")
        lines.append("")

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")
    return len(products)


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
    baseline_prices, best_route, components = calculate_prices(routes, external_bases)
    if len(best_route) != 315:
        raise RuntimeError(f"Expected 315 unique products, calculated {len(best_route)}")
    for product_key, previous_price in load_previous_price_registry().items():
        if product_key in baseline_prices:
            baseline_prices[product_key] = previous_price
        if product_key in external_bases:
            external_bases[product_key] = previous_price
    for fluid_key, previous_price in PREVIOUS_SELLABLE_FLUID_PRICES.items():
        if fluid_key in baseline_prices:
            baseline_prices[fluid_key] = previous_price
        if fluid_key in external_bases:
            external_bases[fluid_key] = previous_price
    prices, external_bases, balance_rules = apply_balance_revision(baseline_prices, external_bases, display_names)
    prices, external_bases, balance_rules = apply_processing_floors(prices, external_bases, balance_rules)
    prices, balance_rules, live_names, live_bindings, live_recipe_count, adjusted_count, skipped_live = (
        apply_live_recipe_floors(prices, balance_rules)
    )
    external_bases = {raw_key: prices.get(raw_key, value) for raw_key, value in external_bases.items()}
    excluded_products = capital_machine_names()
    build_pdf(PDF_PATH, routes, prices, external_bases, display_names, best_route, components,
        baseline_prices, balance_rules, live_names, excluded_products)
    price_count = write_price_prototypes(
        PRICES_PATH, routes, prices, external_bases, display_names, live_names, live_bindings, excluded_products)
    print(f"Wrote {PDF_PATH}")
    print(f"Raw bases: {sum(primary_raw_key(display_names[k]) for k in external_bases if k in display_names)}")
    print(f"Canonical products: {len(best_route)}")
    print(f"Capital machinery excluded: {sum(name in best_route for name in excluded_products)}")
    print(f"Runtime prices: {price_count}")
    print(f"Live priced recipes: {live_recipe_count}; adjusted products: {adjusted_count}; skipped: {skipped_live}")


if __name__ == "__main__":
    main()
