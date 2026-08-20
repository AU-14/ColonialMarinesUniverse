#!/usr/bin/env python3
"""Headless mirrors of the port-relevant CMU integration tests.

Replaces a 30-min build+shard CI roundtrip for yaml-side changes:
- CMUAsrsStockMetadataSourceTest (stock pairing + expected-offer manifests)
- CMUAsrsVehicleAmmoCatalogTest  (profile has every vehicle ammo crate, stock 2/300)
- LegacyRoundForceAsrsCatalogParityTest (compiled profiles match force catalogs, ordered)
- duplicate prototype ids (entity, typed protos, inline Tag tags)

Usage: python3 Tools/port_checks.py [--rev REF]   (default: working tree)
Exit 0 = clean. Needs PyYAML only; no engine, no build.
"""
import argparse
import re
import subprocess
import sys
from pathlib import Path

import yaml


class LooseLoader(yaml.SafeLoader):
    pass


LooseLoader.add_multi_constructor("!", lambda loader, suffix, node: None)


def yaml_load(text):
    return yaml.load(text, Loader=LooseLoader)


ROOT = Path(__file__).resolve().parent.parent
PROFILES = "Content.CMU/Resources/Prototypes/_CMU14/RoundSetup/Forces/asrs_profiles.yml"
STOCK_TEST = "Content.IntegrationTests/_CMU14/Requisitions/CMUAsrsStockMetadataSourceTest.cs"
VEHICLE_TEST = "Content.IntegrationTests/_CMU14/Requisitions/CMUAsrsVehicleAmmoCatalogTest.cs"
PARITY_TEST = "Content.IntegrationTests/_CMU14/Round/LegacyRoundForceAsrsCatalogParityTest.cs"
SHARED_CATALOG = "Content.CMU/Resources/Prototypes/_CMU14/Entities/Structures/Machines/corporate_asrs.yml"
FORCES = {
    "uscm": "USCM", "cmbciu": "CMBCIU", "lacn": "LACN", "weyu": "WEYU",
    "rmc": "RMC", "vaipo": "VAIPO", "hazops": "HAZOPS", "prodigy": "ProdigySF", "upp": "UPP",
}

errors = []


def err(msg):
    errors.append(msg)
    print(f"FAIL {msg}")


def load(rev, path):
    if rev:
        txt = subprocess.run(["git", "show", f"{rev}:{path}"], cwd=ROOT,
                             capture_output=True, text=True, check=True).stdout
    else:
        txt = (ROOT / path).read_text(encoding="utf-8")
    return yaml_load(txt)


def catalog_path(f):
    return f"Content.CMU/Resources/Prototypes/_CMU14/Economy/Catalog/Cargo/{f}_requisitions_catalog.yml"


def find_computer(docs, entity_id):
    for d in docs or []:
        if isinstance(d, dict) and d.get("id") == entity_id:
            for c in d.get("components") or []:
                if c.get("type") == "RequisitionsComputer":
                    return c
    raise SystemExit(f"no RequisitionsComputer on {entity_id}")


def stock_offers(comp):
    out = []
    for cat in comp["categories"]:
        for e in cat.get("entries", []):
            if "maxStock" in e or "stockReplenishDelay" in e:
                out.append((cat["name"], e["crate"], e["cost"], e.get("maxStock"), e.get("stockReplenishDelay")))
    return out


def check_pairing(rev):
    sources = [catalog_path(f) for f in FORCES] + [SHARED_CATALOG]
    for src in sources:
        for doc in load(rev, src) or []:
            for c in doc.get("components") or [] if isinstance(doc, dict) else []:
                if c.get("type") != "RequisitionsComputer":
                    continue
                for cat in c.get("categories", []):
                    for e in cat.get("entries", []):
                        if ("maxStock" in e) != ("stockReplenishDelay" in e):
                            err(f"{src} {doc['id']} {cat['name']}/{e['crate']}: maxStock/stockReplenishDelay must be paired")


def parse_expected_stock(rev):
    txt = load_text(rev, STOCK_TEST)
    if txt is None:
        return [], []
    block = re.search(r"ExpectedForceStockOffers =\s*\[(.*?)\]", txt, re.S).group(1)
    force = re.findall(r'new\("([^"]+)", "([^"]+)", (\d+), (\d+), (\d+)\)', block)
    block = re.search(r"ExpectedSharedStockOffers =\s*\[(.*?)\]", txt, re.S).group(1)
    shared = re.findall(r'new\("([^"]+)", "([^"]+)", (\d+), (\d+), (\d+)\)', block)
    return sorted((c, k, int(o), int(m), int(d)) for c, k, o, m, d in force), \
           sorted((c, k, int(o), int(m), int(d)) for c, k, o, m, d in shared)


def load_text(rev, path):
    if rev:
        r = subprocess.run(["git", "show", f"{rev}:{path}"], cwd=ROOT,
                           capture_output=True, text=True)
        if r.returncode != 0:
            return None
        return r.stdout
    p = ROOT / path
    return p.read_text(encoding="utf-8") if p.exists() else None


def parse_catalog_ids(rev):
    txt = load_text(rev, STOCK_TEST)
    if txt is None:
        return []
    return re.findall(r'\("(\w+CargoCatalog)", new ResPath\("([^"]+)"\)\)', txt)


def resolve_repo_path(res_path):
    if res_path.startswith("/Prototypes/_CMU14/"):
        return "Content.CMU/Resources" + res_path
    if res_path.startswith("/Prototypes/"):
        return "Resources" + res_path
    raise SystemExit(f"unmapped resource path {res_path}")


def check_manifest(rev):
    exp_force, exp_shared = parse_expected_stock(rev)
    actual_errs = 0
    for cat_id, res_path in parse_catalog_ids(rev):
        comp = find_computer(load(rev, resolve_repo_path(res_path)), cat_id)
        if sorted(stock_offers(comp)) != exp_force:
            err(f"{cat_id}: stock offers != ExpectedForceStockOffers "
                f"(yaml={sorted(stock_offers(comp))} test={exp_force})")
            actual_errs += 1
    shared = stock_offers(find_computer(load(rev, SHARED_CATALOG), "CMUASRSResearchGoodies"))
    if sorted(shared) != exp_shared:
        err(f"ExpectedSharedStockOffers out of sync: yaml={sorted(shared)} test={exp_shared}")


def _layers(pid, docs):
    """Inheritance-merged profile data, own layer first then parents (mirrors
    how the engine pushes list fields before RoundAsrsCatalogResolver runs)."""
    d = docs[pid]
    own = next(c for c in d["components"] if c["type"] == "RoundForceAsrsProfile")
    out = [own]
    parents = d.get("parent", [])
    for p in ([parents] if isinstance(parents, str) else parents):
        out += _layers(p, docs)
    return out


def compile_profiles(rev):
    docs_all = load(rev, PROFILES)
    docs = {d["id"]: d for d in docs_all if isinstance(d, dict) and "id" in d}
    force_prof, name_to_cid = {}, {}
    for d in docs.values():
        for c in d.get("components") or []:
            if c.get("type") == "RoundForceAsrsProfile" and c.get("forceId"):
                force_prof[c["forceId"]] = d["id"]

    per_force = {}
    for force, pid in force_prof.items():
        layers = _layers(pid, docs)
        cats, offers, original = {}, {}, {}
        for layer in reversed(layers):  # parents define categories; first seen wins
            for cat in layer.get("categories") or []:
                cats.setdefault(cat["id"], {"name": cat["name"], "offers": []})
        for cid, cat in cats.items():
            name_to_cid.setdefault(cat["name"], cid)
        for layer in reversed(layers):
            for cat in layer.get("categories") or []:
                bucket = cats[cat["id"]]["offers"]
                for off in cat["offers"]:
                    if any(o["id"] == off["id"] for o in bucket):
                        err(f"dupe offer {off['id']} in {pid}")
                    original[off["id"]] = (cat["id"], len(bucket))
                    bucket.append(off)
        excluded = set()
        for layer in layers:
            for excl in layer.get("exclusions") or []:
                cid = excl.split("_", 1)[0]
                bucket = cats.get(cid, {}).get("offers", [])
                cats[cid]["offers"] = [o for o in bucket if o["id"] != excl]
                excluded.add(excl)
        for layer in layers:
            for add in layer.get("additions") or []:
                cat = cats.get(add["category"])
                if cat is None:
                    err(f"{pid}: addition category '{add['category']}' missing")
                    continue
                bucket = cat["offers"]
                off = add["offer"]
                if any(o["id"] == off["id"] for o in bucket):
                    err(f"{pid}: addition '{off['id']}' reuses an existing id")
                    continue
                insert_at = len(bucket)
                before = add.get("insertBefore")
                if before:
                    insert_at = next((i for i, o in enumerate(bucket) if o["id"] == before), insert_at)
                elif off["id"] in excluded and original.get(off["id"], (None,))[0] == add["category"]:
                    orig_idx = original[off["id"]][1]
                    insert_at = len(bucket)
                    for i, o in enumerate(bucket):
                        pos = original.get(o["id"])
                        if pos and pos[0] == add["category"] and pos[1] > orig_idx:
                            insert_at = i
                            break
                bucket.insert(insert_at, off)
                excluded.discard(off["id"])
        per_force[force] = cats
    return per_force, name_to_cid


def check_parity(rev):
    per_force, name_to_cid = compile_profiles(rev)
    for f, force in FORCES.items():
        comp = None
        for doc in load(rev, catalog_path(f)) or []:
            if isinstance(doc, dict):
                for c in doc.get("components") or []:
                    if c.get("type") == "RequisitionsComputer":
                        comp = c
        state = per_force.get(force)
        if comp is None or state is None:
            err(f"{force}: missing catalog or profile")
            continue
        for cat in comp["categories"]:
            cat_id = name_to_cid.get(cat["name"])
            if cat_id is None:
                err(f"{force}: no profile category named '{cat['name']}'")
                continue
            want = [e["crate"] for e in cat["entries"]]
            got = [o["crate"] for o in state[cat_id]["offers"]]
            if want != got:
                miss = [c for c in want if c not in got]
                extra = [c for c in got if c not in want]
                err(f"{force}/{cat['name']}: order/content mismatch "
                    f"(catalog-only={miss} profile-only={extra} want={want} got={got})")


def check_vehicle_ammo(rev):
    txt = load_text(rev, VEHICLE_TEST)
    if txt is None:
        return
    crates = re.findall(r'"(CMUCrate\w+|RMCCrate\w+)"', txt.split("BaseAsrsConsoles")[0])
    profile = load_text(rev, VEHICLE_TEST)
    m = re.search(r'AsrsProfile = "(\w+)"', profile)
    prof_id = m.group(1)
    docs = {d["id"]: d for d in load(rev, PROFILES) if isinstance(d, dict) and "id" in d}

    def compile(pid, state):
        d = docs[pid]
        parents = d.get("parent", [])
        for p in ([parents] if isinstance(parents, str) else parents):
            compile(p, state)
        comp = next(c for c in d["components"] if c["type"] == "RoundForceAsrsProfile")
        for cat in comp.get("categories", []):
            if cat["name"] == "Vehicle Ammo":
                for off in cat["offers"]:
                    state[off["crate"]] = off.get("stock")
        return state

    stock = compile(prof_id, {})
    for c in sorted(set(crates)):
        s = stock.get(c)
        if s is None:
            err(f"{prof_id}: vehicle ammo offer {c} missing or has no stock")
        elif s.get("maximum") != 2 or s.get("replenishDelay") != 300:
            err(f"{prof_id}: {c} stock={s}, expected maximum 2 / replenishDelay 300")


def check_dupes():
    seen = {}
    roots = ["Resources/Prototypes", "Content.CMU/Resources/Prototypes"]
    for root in roots:
        for p in (ROOT / root).rglob("*.yml"):
            rel = str(p.relative_to(ROOT))
            try:
                docs = yaml_load(p.read_text(encoding="utf-8"))
            except Exception as e:
                err(f"{rel}: yaml parse {e}")
                continue
            for d in docs or []:
                if not isinstance(d, dict):
                    continue
                kind = d.get("type")
                if kind and "id" in d:
                    key = (kind, d["id"])
                    if key in seen and seen[key] != rel:
                        err(f"dupe prototype {kind} {d['id']}: {seen[key]} and {rel}")
                    seen[key] = rel


def _digest(cats):
    """Mirrors ComputeAuditDigest: SHA256 over 'idx|name|oidx|crate|cost|max|delay' lines."""
    import hashlib
    lines = []
    for ci, (cid, cat) in enumerate(cats):
        for oi, off in enumerate(cat["offers"]):
            s = off.get("stock")
            mx = str(s["maximum"]) if s else "-"
            dl = str(s["replenishDelay"]) if s else "-"
            lines.append(f"{ci}|{cat['name']}|{oi}|{off['crate']}|{off['cost']}|{mx}|{dl}")
    return hashlib.sha256("\n".join(lines).encode()).hexdigest().upper()


def check_digest(rev):
    txt = load_text(rev, PARITY_TEST)
    if txt is None:
        return
    exp = {m[0]: m[1:] for m in re.findall(
        r'new\("(USCM|LACN|UPP|WEYU|CMBCIU|HAZOPS|ProdigySF|VAIPO|RMC)", "\w+CargoCatalog", "[^"]+", (\d+), (\d+), "([0-9A-F]+)"\)',
        txt)}
    per_force, _ = compile_profiles(rev)
    shared_docs = load(rev, SHARED_CATALOG)
    for f, force in FORCES.items():
        own = None
        parents = []
        for doc in load(rev, catalog_path(f)) or []:
            if isinstance(doc, dict) and doc.get("id", "").endswith("CargoCatalog") and any(
                    c.get("type") == "RequisitionsComputer" for c in doc.get("components") or []):
                own = next(c for c in doc["components"] if c["type"] == "RequisitionsComputer")
                parents = doc.get("parent", [])
                parents = [parents] if isinstance(parents, str) else parents
        legacy = [(c["name"], [
            {"crate": e["crate"], "cost": e["cost"],
             "stock": ({"maximum": e["maxStock"], "replenishDelay": e.get("stockReplenishDelay")}
                        if "maxStock" in e else None)}
            for e in c.get("entries", [])]) for c in own["categories"]]
        for pid in parents:
            for doc in shared_docs or []:
                if isinstance(doc, dict) and doc.get("id") == pid:
                    for c in next(d for d in shared_docs if d.get("id") == pid)["components"]:
                        if c.get("type") == "RequisitionsComputer":
                            legacy += [(cc["name"], [
                                {"crate": e["crate"], "cost": e["cost"],
                                 "stock": ({"maximum": e["maxStock"], "replenishDelay": e.get("stockReplenishDelay")}
                                            if "maxStock" in e else None)}
                                for e in cc.get("entries", [])]) for cc in c["categories"]]
        legacy_norm = [(name, {"name": name, "offers": offs}) for name, offs in legacy]
        d_leg = _digest(legacy_norm)
        want = exp.get(force)
        n_leg = sum(len(o) for _, o in legacy)
        n_com = sum(len(v["offers"]) for v in per_force[force].values())
        if want and (int(want[0]), int(want[1]), want[2]) != (len(legacy), n_leg, d_leg):
            err(f"{force}: ExpectedCatalog stale: test={want[0]}/{want[1]}/{want[2]} "
                f"legacy={len(legacy)}/{n_leg}/{d_leg}")
        # digest-equality of compiled vs legacy is intentionally not checked here: the sim
        # does not apply overrides: cost edits, so digests differ by construction; the real
        # parity test's line-compare in CI is authoritative for that.
        if n_leg != n_com:
            err(f"{force}: compiled offer count {n_com} != legacy {n_leg}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rev", default=None)
    args = ap.parse_args()
    check_pairing(args.rev)
    check_manifest(args.rev)
    check_parity(args.rev)
    check_digest(args.rev)
    check_vehicle_ammo(args.rev)
    if not args.rev:
        check_dupes()
    print(f"\n{len(errors)} error(s)" + (f" on {args.rev}" if args.rev else ""))
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
