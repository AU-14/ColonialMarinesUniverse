"""Static parity checks for Yautja-related localization."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import re
from typing import Iterable

import yaml


_FLUENT_MESSAGE_RE = re.compile(r"^([A-Za-z][A-Za-z0-9_.-]*)\s*=")
_FLUENT_ATTRIBUTE_RE = re.compile(r"^\s+\.([A-Za-z][A-Za-z0-9_-]*)\s*=")
_PLACEHOLDER_RE = re.compile(r"\{\s*\$\s*([A-Za-z_][A-Za-z0-9_-]*)")
_LOCALIZATION_CALL_RE = re.compile(
    r"Loc\.(?:GetString|GetStringP|TryGetString)\s*\(\s*\"([^\"]+)\""
)
_LOC_ID_RE = re.compile(
    r"\b(?:LocId|LocalizedName)\b[^\"\n]*\"((?:cmu-(?:yautja|predalien|hellhound)|ent-CMU)[A-Za-z0-9_.-]*)\""
)
_LOCALIZATION_REFERENCE_RE = re.compile(r"^(?:cmu|ent)-[A-Za-z0-9_.-]+$")

_STATIC_LOCALIZATION_KEYS = {
    "cmu-yautja-hunt-console-blooding-cancelled",
    "cmu-yautja-hunt-console-hunt-ground-cancelled",
    "cmu-yautja-hunt-console-selection-cancelled",
    "cmu-yautja-lobby-translator-help-combo",
    "cmu-yautja-lobby-translator-help-modern",
    "cmu-yautja-lobby-translator-help-retro",
    "cmu-yautja-mark-already-dishonored",
    "cmu-yautja-mark-already-gear-carrier",
    "cmu-yautja-mark-already-honored",
    "cmu-yautja-mark-already-marked",
    "cmu-yautja-mark-dishonored-broadcast",
    "cmu-yautja-mark-honored-broadcast",
    "cmu-yautja-unmark-dishonored-broadcast",
    "cmu-yautja-unmark-honored-broadcast",
}

_YAML_GLOBS = (
    "Resources/Prototypes/_CMU14/Threats/Yautja/**/*.yml",
    "Resources/Prototypes/_CMU14/Yautja/**/*.yml",
    "Resources/Prototypes/_CMU14/Roles/Shared/Skills/Yautja.yml",
)

_HARD_CODED_UI_PATTERNS = {
    "Content.Client/_CMU14/Yautja/YautjaBadBloodWeaponChoiceWindow.xaml": (
        "Title=\"Choose Your Weapon\"",
        "Text=\"This action is irreversible, are you sure?\"",
    ),
    "Content.Client/_CMU14/Yautja/Lobby/YautjaProfileEditor.cs": (
        'ToolTip = "Filter"',
        'selector.AddItem("ALL"',
        'Text = "Filter"',
        '"RETRO / EBONY / SILVER"',
        '"BRONZE / CRIMSON / BONE"',
    ),
    "Content.Shared/_CMU14/Yautja/YautjaCharacterProfile.cs": (
        "legacy armor",
        "unique armor",
        "legacy bio-mask",
        "unique bio-mask",
        "legacy greaves",
        "unique greaves",
        "legacy bracers",
        "clan armor",
        "clan mask",
        "clan greaves",
        "clan bracers",
        "shoulder plasma caster",
        '"None"',
        "mask accessory",
        ", pattern ",
    ),
}


@dataclass
class FluentMessage:
    key: str
    placeholders: set[str] = field(default_factory=set)
    path: Path | None = None
    line: int | None = None


@dataclass
class AuditResult:
    errors: list[str]
    expected_keys: set[str]


def extract_placeholders(value: str) -> set[str]:
    """Return Fluent variable names used in a value or attribute."""

    return set(_PLACEHOLDER_RE.findall(value))


def parse_fluent(path: Path) -> dict[str, FluentMessage]:
    """Parse message and attribute keys sufficiently for parity checks."""

    messages: dict[str, FluentMessage] = {}
    current_key: str | None = None

    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        message_match = _FLUENT_MESSAGE_RE.match(line)
        if message_match:
            current_key = message_match.group(1)
            value = line.split("=", 1)[1]
            messages[current_key] = FluentMessage(
                key=current_key,
                placeholders=extract_placeholders(value),
                path=path,
                line=line_number,
            )
            continue

        attribute_match = _FLUENT_ATTRIBUTE_RE.match(line)
        if attribute_match and current_key is not None:
            attribute_key = f"{current_key}.{attribute_match.group(1)}"
            value = line.split("=", 1)[1]
            messages[attribute_key] = FluentMessage(
                key=attribute_key,
                placeholders=extract_placeholders(value),
                path=path,
                line=line_number,
            )
            current_key = attribute_key
            continue

        if current_key is not None and line.strip() and line[:1].isspace():
            messages[current_key].placeholders.update(extract_placeholders(line))

    return messages


def derive_entity_keys(entity: dict) -> set[str]:
    """Derive generated Fluent keys for explicit entity name/description fields."""

    if str(entity.get("type", "")).lower() != "entity" or not entity.get("id"):
        return set()

    prototype_id = str(entity["id"])
    keys: set[str] = set()
    if entity.get("name") not in (None, ""):
        keys.add(f"ent-{prototype_id}")
    if entity.get("description") not in (None, ""):
        keys.add(f"ent-{prototype_id}.desc")
    return keys


def _iter_yaml_paths(root: Path) -> Iterable[Path]:
    seen: set[Path] = set()
    for pattern in _YAML_GLOBS:
        for path in sorted(root.glob(pattern)):
            if path not in seen:
                seen.add(path)
                yield path


def _collect_yaml_entity_keys(root: Path) -> tuple[set[str], list[str]]:
    keys: set[str] = set()
    errors: list[str] = []

    for path in _iter_yaml_paths(root):
        try:
            documents = yaml.load_all(path.read_text(encoding="utf-8-sig"), Loader=yaml.BaseLoader)
            for document in documents:
                entities = document if isinstance(document, list) else [document]
                for entity in entities:
                    if isinstance(entity, dict):
                        keys.update(derive_entity_keys(entity))
                        for field_name in ("name", "description"):
                            value = entity.get(field_name)
                            if isinstance(value, str) and _LOCALIZATION_REFERENCE_RE.fullmatch(value):
                                keys.add(value)
        except Exception as error:  # pragma: no cover - exercised by repository failures
            errors.append(f"YAML parse error: {path}: {error}")

    return keys, errors


def _collect_locale_messages(root: Path, locale: str) -> tuple[dict[str, FluentMessage], list[str]]:
    messages: dict[str, FluentMessage] = {}
    errors: list[str] = []
    locale_root = root / "Resources" / "Locale" / locale

    for path in sorted(locale_root.rglob("*.ftl")):
        try:
            parsed = parse_fluent(path)
            messages.update(parsed)
        except Exception as error:  # pragma: no cover - exercised by repository failures
            errors.append(f"FTL parse error: {path}: {error}")

    return messages, errors


def _collect_runtime_keys(root: Path) -> tuple[set[str], dict[str, str]]:
    keys: set[str] = set()
    sources: dict[str, str] = {}

    for source_root_name in ("Content.Client", "Content.Server", "Content.Shared"):
        source_root = root / source_root_name
        for path in source_root.rglob("*.cs"):
            relative = path.relative_to(root).as_posix()
            lower_path = relative.lower()
            if not any(token in lower_path for token in ("yautja", "predalien", "hellhound")):
                continue

            for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
                matches = set(_LOCALIZATION_CALL_RE.findall(line))
                matches.update(_LOC_ID_RE.findall(line))
                matches.update(_STATIC_LOCALIZATION_KEYS.intersection(set(_LOCALIZATION_CALL_RE.findall(line))))
                for key in matches:
                    keys.add(key)
                    sources.setdefault(key, f"{relative}:{line_number}")

    for key in _STATIC_LOCALIZATION_KEYS:
        keys.add(key)
        sources.setdefault(key, "Yautja static localization selector")

    return keys, sources


def _collect_hardcoded_ui(root: Path) -> list[str]:
    errors: list[str] = []
    for relative, patterns in _HARD_CODED_UI_PATTERNS.items():
        path = root / Path(relative)
        if not path.exists():
            continue
        for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
            for pattern in patterns:
                if pattern in line:
                    errors.append(f"Hardcoded Yautja UI text: {relative}:{line_number}: {pattern}")
    return errors


def audit_repository(root: Path) -> AuditResult:
    """Audit the complete repository surface covered by the Yautja localization design."""

    en_messages, en_errors = _collect_locale_messages(root, "en-US")
    ru_messages, ru_errors = _collect_locale_messages(root, "ru-RU")
    yaml_keys, yaml_errors = _collect_yaml_entity_keys(root)
    runtime_keys, runtime_sources = _collect_runtime_keys(root)

    yautja_locale_keys: set[str] = set()
    for locale in ("en-US", "ru-RU"):
        locale_root = root / "Resources" / "Locale" / locale / "_CMU14" / "yautja"
        for path in locale_root.glob("*.ftl"):
            yautja_locale_keys.update(key for key in parse_fluent(path) if not key.startswith("ent-"))

    expected_keys = yaml_keys | runtime_keys | yautja_locale_keys
    errors = [*en_errors, *ru_errors, *yaml_errors]

    for key in sorted(expected_keys):
        if key not in en_messages:
            source = runtime_sources.get(key, "Yautja source")
            errors.append(f"Missing en-US key: {key} ({source})")
        if key not in ru_messages:
            source = runtime_sources.get(key, "Yautja source")
            errors.append(f"Missing ru-RU key: {key} ({source})")

    for key in sorted(expected_keys & en_messages.keys() & ru_messages.keys()):
        en_placeholders = en_messages[key].placeholders
        ru_placeholders = ru_messages[key].placeholders
        if en_placeholders != ru_placeholders:
            errors.append(
                f"Placeholder mismatch: {key} (en-US={sorted(en_placeholders)}, ru-RU={sorted(ru_placeholders)})"
            )

    errors.extend(_collect_hardcoded_ui(root))
    return AuditResult(errors=errors, expected_keys=expected_keys)
