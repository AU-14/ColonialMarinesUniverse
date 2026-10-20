"""Create the reviewed Bosenmori exact-art to functional-prototype manifest."""

from __future__ import annotations

import json

from bosenmori_prototype_audit import ROOT, load


# Exact pixels alone are insufficient: these prototypes use shared icons but
# represent a different object (or a spawn bundle rather than a placeable item).
EXCLUDED = {
    "CMUBosenmoriExactaaa024ab7156ba4f",  # bone-chips projectile, not a placed shard
    "CMUBosenmoriExact66ef404347ad2c7a",  # humanoid, not a helmet
    "CMUBosenmoriExact005b6b79cf3ad472",  # vendor, not a briefcase
    "CMUBosenmoriExact0c914abdbc94b5b3",  # crayon box, not a crayon
    "CMUBosenmoriExact3b44a3e546a750c6",  # butchered torso, not skeleton clothing
    "CMUBosenmoriExactb28e4e9c8ca8dc48",  # glasses, not an autoinjector
    "CMUBosenmoriExact112ef00a7ad15e73",  # cap, not a helmet
}

# Reviewed below-threshold matches with an exact bitmap and a source label that
# identifies the same working item. Also includes useful structures with matching
# interaction behavior, omitted by the generic item-only selection.
ADDITIONS = {
    "CMUBosenmoriExact370d07041a01cff6": "RMCLightReplacer",
    "CMUBosenmoriExactebcdfd6e029168ca": "RMCLightReplacer",
    "CMUBosenmoriExact706f6001994fc005": "RMCBeltUtilityGeneralRCM",
    "CMUBosenmoriExactfbec1ba58d163dec": "CMIV",
    "CMUBosenmoriExact6ca8a3afe42c9aad": "CMCrowbarRed",
    "CMUBosenmoriExactfa97ec63456263d9": "RMCMotionDetectorCompact",
    "CMUBosenmoriExactc668c690036f91b7": "RMCCigarettePackLadyFingers",
    "CMUBosenmoriExact1a87b421dd34293c": "CMWindowReinforcedWhite",
    "CMUBosenmoriExactcf92ac8c95a5af4d": "ArmorHelmetRiotOld",
    "CMUBosenmoriExact68a88a28f987ef7a": "RMCHeadCapCorrespondent",
    "CMUBosenmoriExact2099bd5259c88ec6": "RMCPonchoDesert",
    "CMUBosenmoriExactfcba2c65e3f6b0c0": "RMCPonchoDesert",
    "CMUBosenmoriExact3e70cfd102cce574": "CMUInflatableWall",
    "CMUBosenmoriExact1415a3199a4b9651": "CMRollerBed",
    "CMUBosenmoriExact6aa6da6cad9bbb09": "RMCOrbitalCannonCableConnector",
    "CMUBosenmoriExactef794bae74f7265c": "RMCOrbitalCannonCable",
    # Cash values have exact matching RMC sprite states, but the source labels
    # abbreviate them as c50/c100/etc., lowering their text-match score.
    "CMUBosenmoriExact12333d750cb66a9c": "RMCSpaceCash200",
    "CMUBosenmoriExact3938306fc97a9db2": "RMCSpaceCash50",
    "CMUBosenmoriExact50ab7ebb36bb1f10": "RMCSpaceCash500",
    "CMUBosenmoriExact645c57a6dec81118": "RMCSpaceCash1000",
    "CMUBosenmoriExact898c4af5bcecd98e": "RMCSpaceCash100",
    "CMUBosenmoriExactaf8908b1d760bcb2": "RMCSpaceCash1000",
    "CMUBosenmoriExactf252aa3abdfa3472": "RMCSpaceCash100",
    "CMUBosenmoriExactce4334e8974a3967": "RMCMedalGoldExceptionalHeroism",
    "CMUBosenmoriExacteade50c5886fb007": "RMCMedalGoldExceptionalHeroism",
}

# Repeated source labels whose prototype names diverge from CMSS13 wording.
# Apply these only where the same candidate also has an exact sprite match.
SOURCE_OVERRIDES = {
    "/obj/item/storage/backpack/marine/satchel/intel": "CMSatchelMarineIntel",
    "/obj/item/storage/fancy/cigar/tarbacktube": "RMCTarbackTube",
    "/obj/item/reagent_container/food/condiment/saltshaker": "RMCCondimentSmallSalt",
    "/obj/item/reagent_container/food/condiment/peppermill": "RMCCondimentSmallPepper",
    "/obj/item/reagent_container/food/snacks/wrapped/barcardine": "RMCFoodSnackBarcaridine",
    "/obj/item/prop/helmetgarb/prescription_bottle": "RMCHelmetGarbPrescription",
    "/obj/item/clothing/suit/armor/riot/marine/vintage_riot": "CMArmorRiotOld",
    "/obj/item/tool/portadialysis": "RMCPortableDialysis",
    "/obj/item/trash/barcardine": "RMCFoodPacketBarcaridineTrash",
    "/obj/item/storage/pouch/flare/full": "RMCPouchFlareFilled",
    "/obj/item/storage/backpack/rmc/light": "RMCBackpackRCMSmall",
    "/obj/item/storage/backpack/rmc/heavy": "RMCBackpackRCMLarge",
    "/obj/item/storage/pouch/vials": "RMCPouchVial",
    "/obj/item/stack/rods": "CMRodMetal",
    "/obj/item/tool/shovel/etool": "CMEntrenchingTool",
    "/obj/item/storage/fancy/vials/empty": "RMCVialBox",
    "/obj/item/tool/weldingtool": "CMWelder",
    "/obj/item/stack/sheet/mineral/plastic/small_stack": "RMCSheetPlastic1",
    "/obj/item/storage/toolkit/empty": "RMCEngineerKit",
    "/obj/item/clothing/suit/armor/vest/ballistic": "RMCArmorVest",
    "/obj/item/clothing/head/helmet/marine/veteran/royal_marine": "AU14HeadHelmetRMC",
    "/obj/item/clothing/suit/storage/jacket/marine/reporter": "RMCCoatCorrespondent",
    "/obj/item/device/motiondetector/m717/hacked": "RMCMotionDetectorCompact",
    "/obj/item/device/flashlight/tnr": "RMCFlashlight",
    "/obj/item/prop/helmetgarb/gunoil": "RMCHelmetGarbGunOil",
    "/obj/item/clothing/suit/storage/jacket/marine/chef": "CMCoatMessTech",
}


def run() -> None:
    placeholders, counts = load()
    ranked = json.loads((ROOT / ".codex-build/bosenmori_ranked_candidates.json").read_text(encoding="utf-8"))
    candidates = json.loads((ROOT / ".codex-build/bosenmori_candidate_report.json").read_text(encoding="utf-8"))
    out = ROOT / "Tools/bosenmori_functional_matches.json"
    existing = json.loads(out.read_text(encoding="utf-8")) if out.is_file() else []
    selected = {item["source_id"]: item["target"] for item in existing}
    selected.update({
        item["id"]: item["candidates"][0]["id"]
        for item in ranked
        if item["source"].startswith("/obj/item/")
        and item["candidates"][0]["score"] >= 0.95
        and item["id"] not in EXCLUDED
    })
    for item in ranked:
        target = SOURCE_OVERRIDES.get(item["source"])
        if target and any(candidate["id"] == target for candidate in candidates[item["id"]]):
            selected[item["id"]] = target
    selected.update(ADDITIONS)
    selected = {identifier: ("CMPaperBin5" if target == "CMPaperBin" else target)
                for identifier, target in selected.items()}
    for identifier, target in selected.items():
        assert identifier in placeholders
        if identifier in candidates:
            assert any(candidate["id"] == target for candidate in candidates[identifier]), (identifier, target)
    manifest = []
    for identifier, target in sorted(selected.items()):
        source = placeholders[identifier]["source"]
        entry = {"source_id": identifier, "source": source,
                 "name": placeholders[identifier]["name"], "target": target,
                 "offset": placeholders[identifier]["offset"]}
        if source in {"/obj/item/tool/pen/clicky", "/obj/item/tool/pen/fountain"}:
            entry["visual_state"] = "pen"
        manifest.append(entry)
    out.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Saved {len(manifest)} reviewed matches ({sum(item['source_id'] in counts for item in manifest)} still to apply) to {out}")


if __name__ == "__main__":
    run()
