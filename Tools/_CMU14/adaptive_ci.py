#!/usr/bin/env python3
"""CMU14: history is a scheduling hint; only exact trusted evidence can skip execution."""

import argparse
from collections import defaultdict, deque
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import re
import signal
import subprocess
import sys
import time
from types import SimpleNamespace

from ci import run_tests, write_summary
from ci_selection import CS_NONCODE, TEST_ROOTS, may_select

SCHEMA = 1
SHA = re.compile(r"^[0-9a-f]{40}$")
FIXTURE = re.compile(r"^[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)+$", re.ASCII)
TOKEN = re.compile(r"[A-Za-z_][A-Za-z_0-9]*")
RESOURCE_TOKEN = re.compile(r"[A-Za-z_][\w.-]*")
PART = re.compile(r"^(.+)_\d+$")
NIGHTLY = ("Content.IntegrationTests.Tests.EntityTest.", ".Localization.", ".GameRules.AntagGhostRoleTest.")
POLICY = "".join(" & FullyQualifiedName!~" + prefix for prefix in NIGHTLY)
# ci.yml runs MAX_LANES lanes at once. The plan opens at most that many lanes up to LANE_BUDGET of
# work each, and a lane takes work past LANE_BUDGET only up to LANE_BUDGET + LANE_WIGGLE, the most
# its cut allowance covers. Work beyond that opens a lane the pool queues for a second wave: that
# idles the pool behind one lane, where stacking the lane would cut its later groups for certain.
# LANE_BUDGET is the planned test time, with the job timeout covering setup and uploads.
LANE_BUDGET = 20 * 60
# A lane may outrun its plan by LANE_WIGGLE or LANE_DRIFT, whichever is later, but never past LANE_LIMIT. The
# runner cuts a group there and keeps its log and artifacts. The job timeout in ci.yml is only the backstop,
# because that kill keeps neither.
LANE_WIGGLE = 5 * 60
LANE_DRIFT = 1.36
LANE_LIMIT = 30 * 60
# Free GitHub-hosted runner slots after this run's jobs, assuming 20 slots and one Unit Tests job.
# A concurrent run's jobs queue behind this one's either way; this only sets how many of them start early.
ACCOUNT_SLOTS = 20
FREE_SLOTS = 5
LANE_CEILING = 14
MAX_LANES = min(LANE_CEILING, ACCOUNT_SLOTS - 1 - FREE_SLOTS)


def load_json(path, default=None):
    try:
        if Path(path).stat().st_size > 20_000_000:
            return default
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return default


def save_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, separators=(",", ":")), encoding="utf-8")


def git(*args):
    return subprocess.check_output(["git", *args], timeout=90).decode("utf-8")


def history_inputs(state):
    provenance = load_json(state / "provenance.json", {})
    result = {}
    for kind in ("baseline", "latest", "trusted"):
        meta = provenance.get(kind, {}) if isinstance(provenance, dict) else {}
        data = load_json(state / "imports" / kind / "history.json", {})
        if not isinstance(data, dict) or not isinstance(meta, dict):
            continue
        if (data.get("schema") != SCHEMA or data.get("run_id") != str(meta.get("id"))
                or data.get("head_sha") != meta.get("head_sha")
                or not SHA.fullmatch(str(data.get("source_sha", "")))
                or not isinstance(data.get("records"), dict)):
            continue
        # Provenance is supplied independently by the GitHub API picker, never
        # taken from a downloaded artifact's claim about its own trust.
        if kind == "trusted" and not (
            meta.get("event") == "push" and meta.get("conclusion") == "success"
            and meta.get("head_branch") == provenance.get("default_branch")
            and meta.get("head_repository") == provenance.get("repository")
            and data["source_sha"] == meta.get("head_sha")
        ):
            continue
        if kind in ("baseline", "trusted") and data.get("complete") is not True:
            continue
        cleaned = {}
        for name, record in data["records"].items():
            if not isinstance(record, dict):
                continue
            record = dict(record)
            record["cases"] = record.get("cases") if isinstance(record.get("cases"), list) else []
            cleaned[name] = record
        data["records"] = cleaned
        data["url"] = meta.get("html_url", "")
        result[kind] = data
    return result


def baseline_changes(source, root=Path(".")):
    """Compare complete trees, including base changes and earlier failed commits."""
    if not SHA.fullmatch(source):
        raise ValueError("Invalid baseline commit")
    try:
        subprocess.run(["git", "cat-file", "-e", source + "^{commit}"], cwd=root,
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except subprocess.CalledProcessError:
        subprocess.run(["git", "fetch", "--no-tags", "--depth=1", "origin", source], cwd=root,
                       check=True, timeout=90, stdout=subprocess.DEVNULL)
    changed = subprocess.check_output(["git", "diff", "--name-only", "-z", "--no-renames", source, "HEAD"],
                                      cwd=root, timeout=30).decode("utf-8")
    return [path for path in changed.split("\0") if path]


def dependency_priority(files, changed):
    """Conservative lexical reverse graph: types, event types, and prototype IDs.

    A system's subscribed components also reach tests which exercise it through
    events or spawned prototypes without naming the system itself. This closure
    broadens the minimum feature selection; unknown inputs run all.
    """
    symbols = defaultdict(set)
    definitions = defaultdict(set)
    fixtures = defaultdict(set)
    handlers = {}
    tokens = {}
    type_names = defaultdict(set)
    imports = {}
    global_imports = defaultdict(set)
    qualified = {}
    def project(path):
        parts = path.split("/")
        return "Content." + parts[1] if parts[0] == "Content.CMU" else parts[0]
    # A component change affects prototypes that use it, not unrelated entities
    # that happen to be declared in the same YAML file.
    nodes = {}
    changed = set(changed)
    for path, text in files.items():
        if path.endswith(".cs"):
            nodes[path] = text
            continue
        documents = re.split(r"(?m)(?=^-[ ]+(?:type|id):)", text)
        for index, document in enumerate(documents):
            node = f"{path}#{index}"
            nodes[node] = document
            if path in changed:
                changed.add(node)
    for path, text in nodes.items():
        if path.endswith(".cs"):
            literals = []
            def strip_noncode(match):
                value = match[0]
                if not value.startswith(("//", "/*")):
                    literals.append(value)
                return " "
            code = CS_NONCODE.sub(strip_noncode, text)
            names = set(re.findall(r"\b(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(\w+)", code))
            ns = re.search(r"\bnamespace\s+([\w.]+)", code)
            namespace = ns[1] if ns else ""
            if namespace and path.startswith(("Content.Tests/", "Content.IntegrationTests/",
                                               "Content.CMU/Tests/", "Content.CMU/IntegrationTests/")):
                fixtures[path] = {namespace + "." + name for name in names}
            usings = set(re.findall(r"\busing\s+(?:static\s+)?([\w.]+)\s*;", code))
            for i in range(len(namespace.split("."))):
                usings.add(".".join(namespace.split(".")[:i+1]))
            imports[path] = usings | {""}
            global_imports[project(path)].update(re.findall(r"\bglobal\s+using\s+([\w.]+)\s*;", code))
            for name in names:
                type_names[name].add((namespace, "type:" + (namespace + "." if namespace else "") + name))
            # Extension calls name the method, not its declaring class. Resolve
            # them through namespace imports (or a using-static class import).
            extensions = set()
            for method in re.findall(r"\b(\w+)\s*(?:<[^();{}]+>)?\s*\(\s*(?:\[[^\]]+\]\s*)*this\s+", code):
                symbol = "extension:" + (namespace + "." if namespace else "") + method
                extensions.add(symbol)
                type_names[method].add((namespace, symbol))
                for owner in names:
                    type_names[method].add(((namespace + "." if namespace else "") + owner, symbol))
            owned = set(re.findall(r"\bSubscribeLocalEvent<\s*(\w+Component)\s*,", code))
            handlers[path] = owned
            # YAML registers components without the Component suffix.
            aliases = {"component:" + name[:-9] for name in names if name.endswith("Component")}
            aliases.update("component:" + name for name in re.findall(r'\[Prototype\("([\w]+)"', text))
            names = {"type:" + (namespace + "." if namespace else "") + name for name in names} | aliases | extensions
            tokens[path] = set(TOKEN.findall(code))
            resources = set()
            for literal in literals:
                resources.update(RESOURCE_TOKEN.findall(literal))
                tokens[path].update(TOKEN.findall(literal))
            qualified[path] = set(RESOURCE_TOKEN.findall(code)) | resources
            tokens[path].update(kind + name for name in resources for kind in ("prototype:", "component:"))
        else:
            # Nested id fields are references (e.g. crate contents), not declarations.
            names = {"prototype:" + name for name in re.findall(
                r"^(?:  |-[ ]+)id:[ ]*[\"']?([A-Za-z_][\w.-]*)", text, re.M)}
            values = set(RESOURCE_TOKEN.findall(text))
            tokens[path] = values | {kind + name for name in values for kind in ("component:", "prototype:")}
        definitions[path] = names
        for name in names:
            symbols[name].add(path)
    readers = defaultdict(set)
    known_symbols = set(symbols)
    for path, names in tokens.items():
        for name in names & known_symbols:
            readers[name].add(path)
        visible = imports.get(path, set()) | global_imports[project(path)]
        for name in names & type_names.keys():
            for namespace, symbol in type_names[name]:
                if not path.endswith(".cs") or namespace in visible:
                    readers[symbol].add(path)
        # Fully qualified names and using aliases do not require a namespace import.
        for token in qualified.get(path, ()):
            while "." in token:
                if "type:" + token in known_symbols:
                    readers["type:" + token].add(path)
                token = token.rsplit(".", 1)[0]
    affected = set(changed)
    pending = deque(changed)
    # Components handled by a system connect its behavior to dynamically spawned
    # fixtures, including when a shared dependency reaches that system indirectly.
    # Attach these edges to the declared type so partial files share the fan-out.
    handled = defaultdict(set)
    for path, owned in handlers.items():
        visible = imports[path] | global_imports[project(path)]
        owned_symbols = {symbol for name in owned for namespace, symbol in type_names[name] if namespace in visible}
        owned_symbols.update("component:" + name[:-9] for name in owned)
        for declared in definitions[path]:
            handled[declared].update(owned_symbols)
    while pending:
        path = pending.popleft()
        # A consuming test is an endpoint, not a new production change. Its local
        # fixtures/prototypes must not pull unrelated consumers into the closure.
        if path not in changed and fixtures[path] and re.search(r"\[(?:Test|TestCase|TestCaseSource)\b", nodes[path]):
            continue
        propagated = definitions[path] | {owned for declared in definitions[path] for owned in handled[declared]}
        for symbol in propagated:
            for dependent in readers[symbol] - affected:
                affected.add(dependent)
                pending.append(dependent)
    return sorted({fixture for path in affected for fixture in fixtures[path] if FIXTURE.fullmatch(fixture)})


def source_graph_inputs(changed):
    # Read bounded blobs from the tested Git tree in one process. This avoids
    # thousands of working-tree opens and cannot follow resource junctions.
    entries = []
    for entry in git("ls-tree", "-rlz", "HEAD").split("\0"):
        if not entry:
            continue
        meta, path = entry.split("\t", 1)
        mode, kind, oid, size = meta.split()
        source = path.startswith("Content.") and path.endswith(".cs")
        prototype = path.startswith(("Resources/Prototypes/", "Content.CMU/Resources/Prototypes/")) and path.endswith((".yml", ".yaml"))
        if kind != "blob" or not (source or prototype):
            continue
        entries.append((path, oid, int(size)))
    def priority(entry):
        path = entry[0]
        return (0 if path in changed else 1 if "/Tests/" in path or "/IntegrationTests/" in path
                or path.startswith(("Content.Tests/", "Content.IntegrationTests/")) else 2, path)
    chosen, size = [], 0
    for entry in sorted(entries, key=priority):
        if size + entry[2] > 256_000_000:
            break
        chosen.append(entry)
        size += entry[2]
    request = "".join(oid + "\n" for _, oid, _ in chosen).encode()
    output = subprocess.run(["git", "cat-file", "--batch"], input=request, capture_output=True,
                            check=True, timeout=60).stdout
    files, offset = {}, 0
    for path, oid, size in chosen:
        end = output.index(b"\n", offset)
        header = output[offset:end].decode().split()
        if header != [oid, "blob", str(size)]:
            raise ValueError("Git blob response does not match the requested source")
        offset = end + 1
        files[path] = output[offset:offset + size].decode("utf-8", errors="replace")
        offset += size + 1
    return files, len(chosen) < len(entries)


def runtime_settings(name):
    # Four cold pairs start eight worlds and can exhaust hosted-runner memory
    # before a test finishes. Two workers keep setup below the inactivity limit.
    workers, hang = 2, "5m"
    if name.startswith(("cmu_yautja_smoke", "nightly_affected")) or name in ("cmu_threed", "map_loading"):
        workers = 1
    if name.startswith("nightly_affected") or name in ("cmu_movement", "map_loading"):
        hang = "20m"
    return workers, hang


def balance(shards, count=6):
    """Balance estimated runtime into at least `count` lanes sized by LANE_BUDGET, then run each lane's highest-priority groups first."""
    if not shards:
        return []
    total = sum(shard["seconds"] for shard in shards)
    count = min(len(shards), MAX_LANES, max(count, math.ceil(total / LANE_BUDGET)))
    lanes = [dict(name=f"{i + 1:02}", shards=[], seconds=0) for i in range(count)]
    for shard in sorted(shards, key=lambda s: (-s["seconds"], s["name"])):
        lane = min(lanes, key=lambda lane: lane["seconds"])
        # A lane absorbs work past LANE_BUDGET only up to what its cut allowance still covers group by
        # group. Past that it opens a lane even if the pool must queue it: a wave costs the pool
        # some idle time, where a stacked lane costs the second group a certain cut.
        if lane["shards"] and lane["seconds"] + shard["seconds"] > LANE_BUDGET + LANE_WIGGLE:
            lane = dict(name=f"{len(lanes) + 1:02}", shards=[], seconds=0)
            lanes.append(lane)
        lane["shards"].append(shard)
        lane["seconds"] += shard["seconds"]
    for lane in lanes:
        lane["shards"] = [shard["name"] for shard in sorted(
            lane["shards"], key=lambda s: (s["rank"], -s["seconds"], s["name"]))]
    return lanes


def priority_fixtures(record, affected, failures=()):
    failed, durations = list(failures), defaultdict(float)
    for case in record.get("cases", []):
        if not isinstance(case, dict) or not isinstance(case.get("fixture"), str):
            continue
        fixture = case["fixture"]
        if not FIXTURE.fullmatch(fixture):
            continue
        if case.get("outcome") not in ("Passed", "NotExecuted"):
            failed.append(fixture)
        seconds = case.get("seconds")
        if isinstance(seconds, (int, float)) and math.isfinite(seconds) and seconds >= 0:
            durations[fixture] += seconds
    result = list(dict.fromkeys(failed))[:8]
    elapsed = record.get("seconds", 0)
    budget = elapsed * 0.25 if isinstance(elapsed, (int, float)) and math.isfinite(elapsed) else 0
    for fixture in sorted(set(affected) & durations.keys(), key=lambda f: durations[f]):
        if fixture not in result and len(result) < 8 and durations[fixture] <= budget:
            result.append(fixture)
            budget -= durations[fixture]
    return result


def integration_fixtures(files):
    """Read fixture names from declarations in both compiled source trees."""
    fixtures = set()
    for path, text in files.items():
        if not path.startswith(TEST_ROOTS) or not path.endswith(".cs"):
            continue
        code = CS_NONCODE.sub(" ", text)
        namespace = re.search(r"\bnamespace\s+([\w.]+)", code)
        if not namespace:
            continue
        scopes = []
        pending_class = None
        tokens = re.finditer(r"\bclass\s+(\w+)|([{};])|\[\s*(?:Test|TestCase|TestCaseSource)\b", code)
        for token in tokens:
            if token[1]:
                pending_class = token[1]
            elif token[2] == "{":
                scopes.append(pending_class)
                pending_class = None
            elif token[2] == "}":
                if scopes:
                    scopes.pop()
            elif token[2] == ";":
                pending_class = None
            else:
                classes = [scope for scope in scopes if scope]
                if classes:
                    fixtures.add(namespace[1] + "." + ".".join(classes))
    return fixtures


def unlisted_fixtures(shards, fixtures):
    normal_filters = [shard["filter"] for shard in shards
                      if shard["name"] != "tests_unlisted" and not shard["name"].startswith("nightly_affected")]
    return sorted(fixture for fixture in fixtures
                  if not any(prefix in fixture + "." for prefix in NIGHTLY)
                  and not any(may_select(expression, fixture) for expression in normal_filters if expression))


def parent_group(name):
    match = PART.fullmatch(name)
    return match[1] if match else name


def group_seconds(records, name, partial_records=None):
    """Recorded wall time of a group plus the numbered parts an earlier plan split it into."""
    pattern = re.compile(re.escape(name) + r"(?:_\d+)?$")
    times = {key: record["seconds"] for key, record in records.items() if isinstance(record, dict)
             and pattern.fullmatch(key) and isinstance(record.get("seconds"), (int, float))
             and math.isfinite(record["seconds"])}
    for key, record in (partial_records or {}).items():
        if pattern.fullmatch(key):
            floor = timeout_floor(record)
            if floor:
                times[key] = max(times.get(key, 0), floor)
    return sum(times.values())


def timeout_floor(record):
    if not isinstance(record, dict) or record.get("status") != 124:
        return 0
    elapsed = record.get("seconds")
    if not isinstance(elapsed, (int, float)) or not math.isfinite(elapsed) or elapsed <= 0:
        return 0
    return max(LANE_BUDGET, elapsed)


def recorded_seconds(records, name, selection, partial_records=None):
    """Wall time a split piece takes, from what its last runs say.

    Per-test durations leave out the pool work between tests, so they hand a group's wall time
    to the wrong piece. A record counts while the piece still runs at least half the fixtures
    that run timed; below that it describes another membership. A piece the latest run had to
    cut was misplanned, so its time is a floor, not a measure. Give it at least the lane budget
    or the elapsed time of the cut run, whichever is longer.
    """
    floor = timeout_floor((partial_records or {}).get(name))
    record = records.get(name)
    seconds = record.get("seconds") if isinstance(record, dict) else None
    if not isinstance(seconds, (int, float)) or not math.isfinite(seconds) or seconds <= 0:
        return floor
    timed = {case.get("fixture") for case in record.get("cases", []) if isinstance(case, dict)}
    timed = {fixture for fixture in timed if isinstance(fixture, str) and FIXTURE.fullmatch(fixture)}
    kept = sum(may_select(selection, fixture) for fixture in timed)
    return max(floor, seconds if kept * 2 >= len(timed) else 0)


def split_heavy(shards, records, current_fixtures=None, partial_records=None):
    """Cut a group no lane can absorb into numbered parts by recorded test time.

    Selection is already done, so every part of a selected group runs. Parts name
    their fixtures in contiguous name ranges, so the part grows at its end as timings
    drift; the parent keeps everything else, so tests without recorded timing still run.
    """
    result = []
    for shard in shards:
        name = shard["name"]
        total = max(group_seconds(records, name, partial_records), shard["seconds"])
        fixtures = defaultdict(float)
        for source in (records, partial_records or {}):
            observed = defaultdict(float)
            for key, record in source.items():
                if not isinstance(record, dict) or parent_group(key) != name:
                    continue
                for case in record.get("cases", []):
                    fixture = case.get("fixture") if isinstance(case, dict) else None
                    if not isinstance(fixture, str) or not FIXTURE.fullmatch(fixture):
                        continue
                    if current_fixtures is not None and (fixture not in current_fixtures
                                                         or not may_select(shard["filter"], fixture)):
                        continue
                    seconds = case.get("seconds")
                    if isinstance(seconds, (int, float)) and math.isfinite(seconds) and seconds > 0:
                        observed[fixture] += seconds
            for fixture, seconds in observed.items():
                fixtures.setdefault(fixture, seconds)
        fixture_seconds = sum(fixtures.values())
        # A group that fits a lane's budget with its margin is never cut into parts.
        if not shard["filter"] or total <= LANE_BUDGET + LANE_WIGGLE or len(fixtures) < 2 or not fixture_seconds:
            result.append(shard)
            continue
        scale = total / fixture_seconds
        ranges, part, seconds = [], [], 0.0
        for fixture in sorted(fixtures):
            estimate = fixtures[fixture] * scale
            if part and seconds + estimate > LANE_BUDGET:
                ranges.append((part, seconds))
                part, seconds = [], 0.0
            part.append(fixture)
            seconds += estimate
        priority, claimed = shard["priority"], set()
        for number, (fixtures_in_part, estimate) in enumerate(ranges, 1):
            claimed.update(fixtures_in_part)
            part = f"{name}_{number}"
            selection = f"({shard['filter']}) & (" + " | ".join("FullyQualifiedName~" + f + "." for f in fixtures_in_part) + ")"
            result.append(dict(shard, name=part, seconds=max(30, recorded_seconds(records, part, selection, partial_records) or estimate),
                               cold=False, filter=selection,
                               priority=[f for f in priority if f in fixtures_in_part]))
        selection = f"({shard['filter']}) & " + " & ".join("FullyQualifiedName!~" + f + "." for f in sorted(claimed))
        # Until the parts have records of their own, the parent's record timed the whole group.
        known_parts = all(f"{name}_{number}" in records for number in range(1, len(ranges) + 1))
        parent_time = recorded_seconds(records, name, selection, partial_records) if known_parts else 0
        result.append(dict(shard, seconds=max(LANE_BUDGET if shard.get("cold") else 30, parent_time or seconds),
                           filter=selection,
                           priority=[f for f in priority if f not in claimed]))
    return result


def plan_run(selected, paths, event, shards):
    names = [shard.get("name", "") for shard in shards]
    if not names or len(names) != len(set(names)) or any(
        not re.fullmatch(r"[a-z][a-z0-9_]*", name) or PART.fullmatch(name)
        or name == "unit" or name.startswith("nightly_affected")
        for name in names
    ):
        raise ValueError("Gameplay groups need unique safe names; unit, nightly_affected and _N suffixes are reserved")
    state = Path(os.environ.get("CMU_CI_STATE", ".ci"))
    histories = history_inputs(state)
    baseline = histories.get("baseline") or histories.get("trusted")
    notes = []
    valid_paths = isinstance(paths, list) and bool(paths) and all(isinstance(p, str) and p for p in paths)
    narrow_selection = event in ("pull_request", "push", "merge_group") and valid_paths
    changed = set(paths) if valid_paths else set()
    if baseline:
        try:
            changed.update(baseline_changes(baseline["source_sha"]))
            # History can only broaden the API-derived selection, never narrow it.
            from ci import select_tests
            previous = select_tests(event, sorted(changed))
            for key in ("build", "unit", "gameplay"):
                selected[key] |= previous[key]
            notes.append(f"Compared all changes since green baseline {baseline['source_sha'][:12]}.")
        except (OSError, ValueError, subprocess.SubprocessError):
            selected.update(build=True, unit=True, gameplay=True)
            narrow_selection = False
            notes.append("Baseline tree unavailable: full suite selected.")
    else:
        notes.append("No usable green baseline; conservative selection and default timing estimates.")
    try:
        source_sha = git("rev-parse", "HEAD").strip()
        tree = git("rev-parse", "HEAD^{tree}").strip()
    except (OSError, subprocess.SubprocessError):
        source_sha, tree = "", ""
    files = {}
    complete_graph = False
    if changed and selected["build"]:
        try:
            files, partial = source_graph_inputs(changed)
            complete_graph = not partial
            if partial:
                notes.append("Dependency scan budget reached; partial hints only, full selected suite retained.")
        except (OSError, ValueError, subprocess.SubprocessError):
            notes.append("Dependency graph unavailable; full selected suite retained.")
    impacted = dependency_priority(files, changed)
    notes.append(f"Recursive dependencies reach {len(impacted)} fixtures.")
    nightly = [fixture for fixture in impacted if fixture.startswith("Content.IntegrationTests.")
               and any(prefix in fixture + "." for prefix in NIGHTLY)]
    if selected["gameplay"] and nightly:
        # Scheduled sweeps stay out of unrelated runs, but a changed fixture or
        # one reached through production dependencies must not be excluded by POLICY.
        selection = " | ".join("FullyQualifiedName~" + fixture + "." for fixture in nightly)
        entity_fixture = "Content.IntegrationTests.Tests.EntityTest"
        if entity_fixture in nightly:
            # Each bulk entity sweep needs its own budget; together with other
            # nightly fixtures they exceed the 30-minute group timeout.
            selection = "(" + selection + ")"
            for suffix, method in (("maps", "SpawnAndDeleteAllEntitiesOnDifferentMaps"),
                                   ("dirty", "SpawnAndDirtyAllEntities"),
                                   ("count", "SpawnAndDeleteEntityCountTest")):
                shards.append(dict(name="nightly_affected_entity_" + suffix,
                                   filter=f"FullyQualifiedName~{entity_fixture}. & Name={method}"))
                selection += f" & FullyQualifiedName!={entity_fixture}.{method}"
        shards.append(dict(name="nightly_affected", filter=selection))
        notes.append(f"Included {len(nightly)} affected nightly fixtures.")
    current_fixtures = None
    if selected["gameplay"]:
        if complete_graph:
            current_fixtures = integration_fixtures(files)
        else:
            test_sources = {path.as_posix(): path.read_text(encoding="utf-8", errors="replace")
                            for root in TEST_ROOTS for path in Path(root).rglob("*.cs")
                            if not {"bin", "obj"}.intersection(path.parts)}
            current_fixtures = integration_fixtures(test_sources) if test_sources else None
    records = {}
    partial_records = {}
    latest_failures = defaultdict(list)
    latest_failed_groups = set()
    for kind in ("trusted", "baseline", "latest"):
        history = histories.get(kind, {})
        incoming = history.get("records", {})
        if kind == "latest":
            for name, observation in incoming.items():
                group = parent_group(name)
                latest_failures[group].extend(priority_fixtures(observation, ()))
                if observation.get("status", 0):
                    latest_failed_groups.add(group)
        if history.get("complete") is not True:
            partial_records.update(incoming)
            continue
        parents = {parent_group(name) for name in incoming}
        records = {name: record for name, record in records.items() if parent_group(name) not in parents}
        records.update(incoming)
    for shard in shards:
        name = shard["name"]
        workers, hang = runtime_settings(name)
        shard.update(workers=workers, hang=hang)
        if name == "root":
            root_files = Path("Content.IntegrationTests/Tests").glob("*.cs")
            selectors = ["FullyQualifiedName~Content.IntegrationTests.Tests." + p.stem + "."
                         for p in sorted(root_files) if p.stem not in ("EntityTest", "PostMapInitTest")]
            shard["filter"] = " | ".join(selectors)
        if not shard["filter"]:
            # Planning outside a checkout is allowed for planner regression tests;
            # execution explicitly rejects an empty filter.
            shard["filter"] = ""
        else:
            shard["filter"] = "(" + shard["filter"] + ")" + ("" if name.startswith("nightly_affected") else POLICY)
        record = records.get(name, {})
        if not isinstance(record, dict):
            record = {}
        failed = [case.get("fixture", "") for case in record.get("cases", []) if isinstance(case, dict)
                  and case.get("outcome") not in ("Passed", "NotExecuted")]
        # Namespace membership is a priority hint; actual execution intersects the
        # complete original shard expression, including all of its exclusions.
        prefixes = re.findall(r"FullyQualifiedName~([\w.]+)", shard["filter"])
        relevant = [fixture for fixture in impacted if any(prefix in fixture + "." for prefix in prefixes)]
        shard["priority"] = priority_fixtures(record, relevant, latest_failures[name])
        shard["rank"] = 0 if name in latest_failed_groups or latest_failures[name] or record.get("status", 0) or failed else 1 if relevant else 2
        seconds = group_seconds(records, name)
        shard["cold"] = seconds <= 0
        if seconds > 0:
            shard["seconds"] = max(30, group_seconds(records, name, partial_records))
        else:
            shard["seconds"] = max(LANE_BUDGET, group_seconds(partial_records, name))
    fallback = next((shard for shard in shards if shard["name"] == "tests_unlisted"), None)
    if fallback is not None and current_fixtures:
        remainder = unlisted_fixtures(shards, current_fixtures)
        if remainder:
            selectors = " | ".join("FullyQualifiedName~" + fixture + "." for fixture in remainder)
            fallback["filter"] = f"({selectors})" + POLICY
    shards = [shard for shard in shards if shard["name"] != "tests_unlisted" or shard["filter"]]
    # Selection happens after filters (including the root group) are expanded.
    # A failed baseline comparison must never permit a narrowed PR plan.
    if narrow_selection and selected["gameplay"]:
        from ci_selection import select_shards
        wanted = select_shards(sorted(changed), shards, files, impacted, complete_graph)
        shards = [shard for shard in shards if shard["name"] in wanted]
        selected["gameplay"] = bool(shards)
        notes.append(f"Selected {len(shards)} gameplay groups for the changed features and their consumers.")
    if not selected["gameplay"]:
        shards = []
    shards = split_heavy(shards, records, current_fixtures, partial_records)
    parents = sorted({parent_group(shard["name"]) for shard in shards if PART.fullmatch(shard["name"])})
    if parents:
        notes.append("Split long groups by recorded test time: " + ", ".join(parents) + ".")
    for shard in shards:
        if shard["seconds"] > LANE_LIMIT:
            notes.append(f"{shard['name']} still exceeds the gameplay execution cap with the recorded fixture timings.")
    if narrow_selection and selected["unit"]:
        from ci_selection import needs_unit
        selected["unit"] = needs_unit(sorted(changed), files, impacted, complete_graph)
        if not selected["unit"]:
            notes.append("No unit-test dependencies or matching unit feature: unit shard skipped.")
    lanes = balance(shards)
    unit_record = records.get("unit", {})
    unit_priority = priority_fixtures(unit_record if isinstance(unit_record, dict) else {},
                                     [f for f in impacted if f.startswith(("Content.Tests.", "Content.CMU.Tests."))],
                                     latest_failures["unit"])
    from ci_selection import select_assets
    selected["assets"] = select_assets(sorted(changed) if narrow_selection else None)
    data = dict(schema=SCHEMA, source_sha=source_sha, source_tree=tree,
                head_sha=os.environ.get("CI_HEAD_SHA", source_sha), run_id=os.environ.get("GITHUB_RUN_ID", "local"),
                shards=shards, lanes=lanes, unit_priority=unit_priority, selected=dict(selected),
                trusted=histories.get("trusted", {}), notes=notes)
    save_json(state / "plan.json", data)
    selected["matrix"] = {"lane": [dict(name=lane["name"], label=lane["shards"][0]) for lane in lanes]}
    selected["reason"] += " " + " ".join(notes)
    write_summary("\n| Lane | Estimated minutes | Groups in execution order |\n| --- | ---: | --- |\n" +
                  "".join(f"| {lane['name']} | {lane['seconds'] / 60:.1f} | {', '.join(lane['shards'])} |\n" for lane in lanes))
    return selected


def fingerprint(plan, settings, environment):
    if not SHA.fullmatch(plan.get("source_tree", "")):
        return ""
    value = dict(schema=SCHEMA, tree=plan["source_tree"], settings=settings, environment=environment)
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()


def artifact_fingerprint(roots):
    """Bind proof to the actual restored/compiled runtime, including native libraries."""
    digest = hashlib.sha256()
    count = 0
    for root in roots:
        root = Path(root)
        for path in sorted(root.rglob("*")):
            if not path.is_file() or "TestResults" in path.parts:
                continue
            if path.suffix not in (".dll", ".exe", ".so", ".dylib") and not path.name.endswith((".deps.json", ".runtimeconfig.json")):
                continue
            digest.update((root.name + "/" + path.relative_to(root).as_posix()).encode() + b"\0")
            with path.open("rb") as stream:
                while chunk := stream.read(1024 * 1024):
                    digest.update(chunk)
            digest.update(b"\0")
            count += 1
    if not count:
        raise ValueError("No runtime artifacts found; cannot verify execution inputs")
    return digest.hexdigest()


def reusable(record, key):
    return (isinstance(record, dict) and bool(key) and record.get("fingerprint") == key
            and record.get("status") == 0 and record.get("valid") is True
            and isinstance(record.get("counters"), dict)
            and isinstance(record["counters"].get("executed"), int) and record["counters"]["executed"] > 0
            and not any(record["counters"].get(k, 0) for k in ("failed", "error", "aborted", "timeout")))


def execute_group(settings, priority, previous, key, output, runner=run_tests, budget=None):
    """At most two invocations: priority then remainder; failed priority stays red."""
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    name = settings["name"]
    if reusable(previous, key):
        observation = dict(previous, reused=True)
        save_json(output / f"{name}.json", observation)
        evidence = previous.get("evidence_url", "")
        print(f"{name}: reused exact-input successful evidence {evidence}", flush=True)
        return 0
    priority = [f for f in priority if isinstance(f, str) and FIXTURE.fullmatch(f)][:8]
    positive = " | ".join("FullyQualifiedName~" + f + "." for f in priority)
    negative = " & ".join("FullyQualifiedName!~" + f + "." for f in priority)
    base = settings.get("filter", "")
    selectors = [("priority", f"({base}) & ({positive})" if base else positive)] if priority else []
    selectors.append(("remaining" if priority else "full", base))
    results = []
    priority_executed = False
    # A lane hands down what is left of its own budget. On its own, a group keeps its configured timeout.
    remaining_budget = settings["timeout"] if budget is None else budget
    for phase, selection in selectors:
        if remaining_budget <= 0:
            print(f"::error::{name} / {phase}: execution budget exhausted before launch.", flush=True)
            results.append(dict(status=124))
            break
        if phase == "remaining" and priority_executed:
            selection = f"({base}) & {negative}" if base else negative
        phase_dir = output / f"{name}-{phase}"
        trx, log = phase_dir / "results.trx", phase_dir / "test-output.log"
        command = ["dotnet", "test", settings["assembly"], "--verbosity", "normal",
                   "--logger", "trx;LogFileName=results.trx", "--results-directory", str(phase_dir.resolve())]
        if selection:
            command += ["--filter", selection]
        if settings.get("hang"):
            # DLL-mode dotnet test drops --blame-hang-dump-type and collects a
            # full dump. Pass VSTest's complete option to retain the no-dump limit.
            command += [f"--Blame:CollectHangDump;TestTimeout={settings['hang']};HangDumpType=none"]
        command += ["--", "NUnit.ExplicitMode=None", "NUnit.AssemblySelectLimit=10000"]
        if name != "unit":
            command += ["NUnit.MapWarningTo=Failed", f"NUnit.NumberOfTestWorkers={settings['workers']}"]
        allow_empty = phase == "priority" or (phase == "remaining" and priority_executed)
        args = SimpleNamespace(command=command, name=f"{name} / {phase}", log=str(log), trx=str(trx),
                               timeout=remaining_budget, heartbeat=30, allow_empty=allow_empty)
        try:
            status = runner(args)
            record = load_json(log.with_suffix(".json"), {})
        except (OSError, ValueError) as error:
            print(f"{name}: test process failed to start: {error}", flush=True)
            status, record = 1, {}
        record["status"] = status
        elapsed = record.get("seconds", 0)
        print(f"{name} / {phase}: {elapsed:.1f}s", flush=True)
        results.append(record)
        remaining_budget -= elapsed
        if status:
            break
        priority_executed = phase == "priority" and record.get("counters", {}).get("executed", 0) > 0
    counters = {k: sum(r.get("counters", {}).get(k, 0) for r in results)
                for k in ("total", "executed", "passed", "failed", "notExecuted")}
    status = next((r["status"] for r in results if r["status"]), 0)
    seen = set()
    for record in results:
        identities = {case["id"] for case in record.get("cases", [])
                      if case.get("id") and case.get("outcome") != "NotExecuted"}
        if seen & identities:
            print("::error::Priority and remainder overlapped; the test filter may have been ignored.", flush=True)
            status = status or 1
        seen.update(identities)
    status = status or (0 if counters["executed"] > 0 else 1)
    observation = dict(fingerprint=key, status=status, valid=status == 0, reused=False, counters=counters,
                       seconds=sum(r.get("seconds", 0) for r in results),
                       cases=[case for r in results for case in r.get("cases", [])])
    save_json(output / f"{name}.json", observation)
    return status


def lane_cut(budget, seconds):
    """What one group may run: its recorded time plus drift or LANE_WIGGLE, whichever is later, inside the lane."""
    return max(0, min(budget, max(seconds * LANE_DRIFT, seconds + LANE_WIGGLE)))


def execute(args):
    state = Path(args.state).resolve()
    plan = load_json(state / "plan.json")
    environment = dict(dotnet=subprocess.check_output(["dotnet", "--info"], text=True),
                       os=platform.platform(), arch=platform.machine(),
                       image=os.environ.get("ImageOS", ""), image_version=os.environ.get("ImageVersion", ""),
                       variables={k: v for k, v in os.environ.items() if k.startswith("DOTNET_") or k in ("LANG", "LC_ALL", "TZ")})
    environment["artifacts"] = artifact_fingerprint([
        "bin/Content.Tests" if args.unit else "bin/Content.IntegrationTests",
        "bin/Content.Client", "bin/Content.Server",
    ])
    budget = None
    if args.unit:
        groups = [dict(name="unit", assembly="bin/Content.Tests/Content.Tests.dll", filter="", timeout=720)]
    else:
        lane = next(lane for lane in plan["lanes"] if lane["name"] == args.lane)
        groups = [dict(next(s for s in plan["shards"] if s["name"] == name),
                       assembly="Content.IntegrationTests.dll", timeout=1800) for name in lane["shards"]]
        budget = LANE_LIMIT if any(group.get("cold") for group in groups) else lane_cut(LANE_LIMIT, lane["seconds"])
        if "CI_LANE_DEADLINE" in os.environ:
            budget = min(budget, float(os.environ["CI_LANE_DEADLINE"]) - time.time())
        if any(not group["filter"] for group in groups):
            raise ValueError("An empty gameplay filter would run the entire assembly")
        os.chdir("bin/Content.IntegrationTests")
    code = 0
    for group in groups:
        # Priority, ordering and estimates do not affect the complete test selection.
        settings = {k: v for k, v in group.items() if k not in ("priority", "rank", "seconds", "cold")}
        key = fingerprint(plan, settings, environment)
        previous = plan.get("trusted", {}).get("records", {}).get(group["name"], {})
        previous = dict(previous, evidence_url=plan.get("trusted", {}).get("url", ""))
        priority = plan["unit_priority"] if args.unit else group["priority"]
        # Each group's cut comes out of what is left of the lane, so an overrun is paid by the groups after it.
        cut = budget if budget is None or group.get("cold") else lane_cut(budget, group["seconds"])
        started = time.monotonic()
        result = execute_group(settings, priority, previous, key, state / "observations", budget=cut)
        if budget is not None:
            budget -= time.monotonic() - started
        code |= result
        if result in (130, 143):
            return result
    return code


def collect(args):
    state = Path(args.state)
    plan = load_json(state / "plan.json", {})
    selected = plan.get("selected") if isinstance(plan, dict) else None
    if (not isinstance(selected, dict) or plan.get("schema") != SCHEMA
            or any(not isinstance(selected.get(key), bool) for key in ("build", "unit", "gameplay"))
            or not isinstance(plan.get("shards"), list)):
        print("::error::The test plan is missing or invalid; completeness cannot be verified.", flush=True)
        return 1
    records = {p.stem: load_json(p, {}) for p in (state / "observations").glob("*.json")}
    expected = {s["name"] for s in plan["shards"]} if selected["gameplay"] else set()
    if selected["unit"]:
        expected.add("unit")
    issues, rows = [], []
    for name in sorted(expected):
        record = records.get(name)
        counters = record.get("counters", {}) if isinstance(record, dict) else {}
        executed = counters.get("executed") if isinstance(counters, dict) else None
        if not isinstance(record, dict) or not record:
            result = "Missing evidence"
        elif (record.get("valid") is not True or record.get("status") != 0
              or not isinstance(executed, int) or executed <= 0):
            result = "Failed or incomplete evidence"
        else:
            result = "Reused exact-input success" if record.get("reused") else "Passed"
        if result in ("Missing evidence", "Failed or incomplete evidence"):
            issues.append(f"{name}: {result.lower()}")
        rows.append(f"| {name} | {result} | {executed if isinstance(executed, int) else '-'} |\n")
    if selected["build"] and not expected:
        issues.append("The build plan selected no test groups")
    complete = bool(expected) and not issues
    if issues:
        write_summary("### Test coverage evidence\n\n| Group | Result | Executed |\n| --- | --- | ---: |\n" + "".join(rows))
    for issue in issues:
        print(f"::error::{issue}", flush=True)
    data = {key: plan.get(key) for key in ("schema", "source_sha", "source_tree", "head_sha", "run_id")}
    data.update(records=records, complete=complete)
    # Persist failed observations too: they are the first work on the next revision.
    save_json(state / "history" / "history.json", data)
    return 1 if issues else 0


def main():
    def interrupted(_signal, _frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, interrupted)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("execute", "collect"))
    parser.add_argument("--state", default=".ci")
    parser.add_argument("--unit", action="store_true")
    parser.add_argument("--lane")
    args = parser.parse_args()
    return execute(args) if args.mode == "execute" else collect(args)


if __name__ == "__main__":
    sys.exit(main())
