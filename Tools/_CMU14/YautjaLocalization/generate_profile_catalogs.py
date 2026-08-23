"""Generate locale entries for Yautja profile-editor display keys."""

from __future__ import annotations

from pathlib import Path
import re

from .audit import parse_fluent


ROOT = Path(__file__).resolve().parents[3]
EN_OUTPUT = ROOT / "Resources/Locale/en-US/_CMU14/yautja/profile_generated.ftl"
RU_OUTPUT = ROOT / "Resources/Locale/ru-RU/_CMU14/yautja/profile_generated.ftl"

MATERIALS = {
    "ebony": ("ebony", "эбеновая"),
    "silver": ("silver", "серебряная"),
    "bronze": ("bronze", "бронзовая"),
    "crimson": ("crimson", "алая"),
    "bone": ("bone", "костяная"),
}
BRACERS = {
    "retro": ("retro", "ретро"),
    "ebony": ("ebony", "эбеновый"),
    "silver": ("silver", "серебряный"),
    "bronze": ("bronze", "бронзовый"),
    "crimson": ("crimson", "алый"),
    "bone": ("bone", "костяной"),
    "dragon": ("dragon", "драконий"),
    "swamp": ("swamp", "болотный"),
    "enforcer": ("enforcer", "энфорсерский"),
    "collector": ("collector", "коллекционерский"),
}
CAPES = {
    "full": ("battle-worn cape", "боевой плащ"),
    "ceremonial": ("ceremonial cape", "церемониальный плащ"),
    "third": ("third cape", "третий плащ"),
    "half": ("half cape", "полуплащ"),
    "quarter": ("quarter cape", "четвертной плащ"),
    "poncho": ("councilor poncho", "пончо советника"),
    "damaged": ("damaged cape", "повреждённый плащ"),
}
SETS = {
    "none": ("None", "Нет"),
    "dragon": ("Dragon", "Драконий"),
    "swamp": ("Swamp", "Болотный"),
    "enforcer": ("Enforcer", "Энфорсер"),
    "collector": ("Collector", "Коллекционер"),
}
UNIQUES = {
    "none": ("None", "Нет"),
    "anubys": ("Anubys", "Анубис"),
    "cleopatra": ("Cleopatra", "Клеопатра"),
    "plated": ("Plated", "Пластинчатый"),
    "ronin": ("Ronin", "Ронин"),
}


def _existing(locale: str) -> set[str]:
    result: set[str] = set()
    for path in (ROOT / "Resources/Locale" / locale).rglob("*.ftl"):
        if path in (EN_OUTPUT, RU_OUTPUT):
            continue
        result.update(parse_fluent(path))
    return result


def _pairs() -> list[tuple[str, str, str]]:
    pairs: list[tuple[str, str, str]] = []

    for material, (en_material, ru_material) in MATERIALS.items():
        for category, en_item, ru_item, maximum in (
            ("armor", "clan armor", "клановая броня", 8),
            ("mask", "clan mask", "клановая маска", 20),
            ("greaves", "clan greaves", "клановые поножи", 4),
        ):
            for style in range(1, maximum + 1):
                key = f"cmu-yautja-profile-{category}-{material}-{style}"
                pairs.append((key, f"{en_material} {en_item}, pattern {style}", f"{ru_material} {ru_item}, образец {style}"))

    for material, (en_material, ru_material) in BRACERS.items():
        legacy = material in {"dragon", "swamp", "enforcer", "collector"}
        key = f"cmu-yautja-profile-bracer-{material}-{'legacy' if legacy else 'clan'}"
        en_suffix = "legacy bracers" if legacy else "clan bracers"
        ru_suffix = "наследные наручи" if legacy else "клановые наручи"
        pairs.append((key, f"{en_material} {en_suffix}", f"{ru_material} {ru_suffix}"))

    for material, (en_material, ru_material) in list(BRACERS.items())[:6]:
        pairs.append((f"cmu-yautja-profile-caster-{material}", f"{en_material} shoulder plasma caster", f"{ru_material} плечевой плазменный кастер"))

    for style, (en_value, ru_value) in CAPES.items():
        pairs.append((f"cmu-yautja-profile-cape-{style}", en_value, ru_value))

    pairs.append(("cmu-yautja-profile-mask-accessory-none", "None", "Нет"))
    for material, (en_material, ru_material) in MATERIALS.items():
        for style in range(1, 4):
            pairs.append((
                f"cmu-yautja-profile-mask-accessory-{material}-{style}",
                f"{en_material} mask accessory {style}",
                f"{ru_material} украшение маски {style}",
            ))

    for material, (en_value, ru_value) in MATERIALS.items():
        pairs.append((f"cmu-yautja-profile-material-{material}", en_value, ru_value))
    for material, (en_value, ru_value) in BRACERS.items():
        pairs.append((f"cmu-yautja-profile-bracer-material-{material}", en_value, ru_value))

    for category, values in (
        ("translator", {"modern": ("Modern", "Современный"), "retro": ("Retro", "Ретро"), "combo": ("Combo", "Комбо")}),
        ("invisibility-sound", {"modern": ("Modern", "Современный"), "retro": ("Retro", "Ретро")}),
        ("status", {"normal": ("Normal", "Обычный"), "council": ("Council", "Совет"), "leader": ("Leader", "Лидер")}),
    ):
        for suffix, (en_value, ru_value) in values.items():
            pairs.append((f"cmu-yautja-profile-{category}-{suffix}", en_value, ru_value))

    for category, values in (
        ("legacy", SETS),
        ("unique", UNIQUES),
    ):
        for suffix, (en_value, ru_value) in values.items():
            pairs.append((f"cmu-yautja-profile-{category}-{suffix}", en_value, ru_value))

    for category, values in (
        ("skin-color", {"green": ("green", "зелёный"), "tan": ("tan", "смуглый"), "purple": ("purple", "фиолетовый"), "blue": ("blue", "синий"), "red": ("red", "красный"), "black": ("black", "чёрный")}),
        ("eye-color", {"black": ("black", "чёрный"), "gold": ("gold", "золотой"), "amber": ("amber", "янтарный"), "copper": ("copper", "медный"), "red": ("red", "красный"), "jade": ("jade", "нефритовый"), "slate": ("slate", "сланцевый")}),
        ("dread-color", {"match-skin": ("match skin", "как кожа"), "black": ("black", "чёрный"), "dark-brown": ("dark brown", "тёмно-коричневый"), "brown": ("brown", "коричневый"), "auburn": ("auburn", "каштановый"), "ash": ("ash", "пепельный"), "bone": ("bone", "костяной")}),
        ("quill", {"standard": ("Standard", "Стандартный"), "short-thick": ("Short Thick", "Короткие толстые"), "straight-thin": ("Straight Thin", "Прямые тонкие"), "long-tied": ("Long Tied", "Длинные связанные"), "short-thin": ("Short Thin", "Короткие тонкие"), "long-curved": ("Long Curved", "Длинные изогнутые"), "long-straight": ("Long Straight", "Длинные прямые"), "long-wide": ("Long Wide", "Длинные широкие"), "short-wide": ("Short Wide", "Короткие широкие")}),
    ):
        for suffix, (en_value, ru_value) in values.items():
            pairs.append((f"cmu-yautja-profile-{category}-{suffix}", en_value, ru_value))

    pairs.extend([
        ("cmu-yautja-profile-material-group-core", "RETRO / EBONY / SILVER", "РЕТРО / ЭБЕН / СЕРЕБРО"),
        ("cmu-yautja-profile-material-group-colored", "BRONZE / CRIMSON / BONE", "БРОНЗА / АЛЫЙ / КОСТЬ"),
        ("cmu-yautja-profile-material-group-legacy", "LEGACY", "НАСЛЕДНЫЕ"),
        ("cmu-yautja-lobby-filter-tooltip", "Filter", "Фильтр"),
        ("cmu-yautja-lobby-filter-label", "Filter", "Фильтр"),
        ("cmu-yautja-lobby-filter-all", "ALL", "ВСЕ"),
    ])

    for prefix, values, item_names in (
        ("legacy", SETS, ("armor", "mask", "greaves", "bracer")),
        ("unique", UNIQUES, ("armor", "mask", "greaves")),
    ):
        for suffix, (en_set, ru_set) in values.items():
            if suffix == "none":
                continue
            for item in item_names:
                pairs.append((f"cmu-yautja-profile-{prefix}-{suffix}-{item}", f"{en_set} {prefix} {item}", f"{ru_set} {item}"))
    return pairs


def main() -> None:
    en_existing = _existing("en-US")
    ru_existing = _existing("ru-RU")
    en_rows: list[str] = []
    ru_rows: list[str] = []
    for key, en_value, ru_value in _pairs():
        if key not in en_existing:
            en_rows.append(f"{key} = {en_value}")
        if key not in ru_existing:
            ru_rows.append(f"{key} = {ru_value}")

    EN_OUTPUT.write_text("# Generated profile-editor localization.\n\n" + "\n\n".join(en_rows) + "\n", encoding="utf-8")
    RU_OUTPUT.write_text("# Generated profile-editor localization.\n\n" + "\n\n".join(ru_rows) + "\n", encoding="utf-8")
    print(f"generated en-US: {len(en_rows)} keys -> {EN_OUTPUT}")
    print(f"generated ru-RU: {len(ru_rows)} keys -> {RU_OUTPUT}")


if __name__ == "__main__":
    main()
