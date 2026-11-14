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
from types import SimpleNamespace

from ci import run_tests, write_summary
from ci_selection import CS_NONCODE

SCHEMA = 1
SHA = re.compile(r"^[0-9a-f]{40}$")
FIXTURE = re.compile(r"^[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)+$", re.ASCII)
TOKEN = re.compile(r"[A-Za-z_][A-Za-z_0-9]*")
RESOURCE_TOKEN = re.compile(r"[A-Za-z_][\w.-]*")
NIGHTLY = ("Content.IntegrationTests.Tests.EntityTest.", ".Localization.", ".GameRules.AntagGhostRoleTest.")
POLICY = "".join(" & FullyQualifiedName!~" + prefix for prefix in NIGHTLY[1:])


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
    """Greedy longest-processing-time placement within priority tiers."""
    if not shards:
        return []
    lanes = [dict(name=f"{i + 1:02}", shards=[], seconds=0) for i in range(min(count, len(shards)))]
    maximum = math.ceil(len(shards) / len(lanes))
    for shard in sorted(shards, key=lambda s: (s["rank"], -s["seconds"], s["name"])):
        lane = min((lane for lane in lanes if len(lane["shards"]) < maximum), key=lambda lane: lane["seconds"])
        lane["shards"].append(shard["name"])
        lane["seconds"] += shard["seconds"]
    return lanes


def priority_fixtures(record, affected):
    failed, durations = [], defaultdict(float)
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


def plan_run(selected, paths, event, shards):
    names = [shard.get("name", "") for shard in shards]
    if not names or len(names) != len(set(names)) or any(
        not re.fullmatch(r"[a-z][a-z0-9_]*", name) or name == "unit" or name.startswith("nightly_affected")
        for name in names
    ):
        raise ValueError("Gameplay groups need unique safe names; unit and nightly_affected are reserved")
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
    records = {}
    for kind in ("trusted", "baseline", "latest"):
        records.update(histories.get(kind, {}).get("records", {}))
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
        shard["priority"] = priority_fixtures(record, relevant)
        shard["rank"] = 0 if record.get("status", 0) or failed else 1 if relevant else 2
        seconds = record.get("seconds", 600)
        shard["seconds"] = min(1800, max(30, seconds)) if isinstance(seconds, (int, float)) and math.isfinite(seconds) else 600
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
    if narrow_selection and selected["unit"]:
        from ci_selection import needs_unit
        selected["unit"] = needs_unit(sorted(changed), files, impacted, complete_graph)
        if not selected["unit"]:
            notes.append("No unit-test dependencies or matching unit feature: unit shard skipped.")
    lanes = balance(shards)
    unit_record = records.get("unit", {})
    unit_priority = priority_fixtures(unit_record if isinstance(unit_record, dict) else {},
                                     [f for f in impacted if f.startswith(("Content.Tests.", "Content.CMU.Tests."))])
    from ci_selection import select_assets
    selected["assets"] = select_assets(sorted(changed) if narrow_selection else None)
    data = dict(schema=SCHEMA, source_sha=source_sha, source_tree=tree,
                head_sha=os.environ.get("CI_HEAD_SHA", source_sha), run_id=os.environ.get("GITHUB_RUN_ID", "local"),
                shards=shards, lanes=lanes, unit_priority=unit_priority, selected=dict(selected),
                trusted=histories.get("trusted", {}), notes=notes)
    save_json(state / "plan.json", data)
    selected["matrix"] = {"lane": [dict(name=lane["name"], label=f"{lane['name']} / {lane['shards'][0]}") for lane in lanes]}
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


def execute_group(settings, priority, previous, key, output, runner=run_tests):
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
    remaining_budget = settings["timeout"]
    for phase, selection in selectors:
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
                               timeout=max(0.01, remaining_budget), heartbeat=30, allow_empty=allow_empty)
        try:
            status = runner(args)
            record = load_json(log.with_suffix(".json"), {})
        except (OSError, ValueError) as error:
            print(f"{name}: test process failed to start: {error}", flush=True)
            status, record = 1, {}
        record["status"] = status
        results.append(record)
        remaining_budget -= record.get("seconds", 0)
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
    if args.unit:
        groups = [dict(name="unit", assembly="bin/Content.Tests/Content.Tests.dll", filter="", timeout=720)]
    else:
        lane = next(lane for lane in plan["lanes"] if lane["name"] == args.lane)
        groups = [dict(next(s for s in plan["shards"] if s["name"] == name),
                       assembly="Content.IntegrationTests.dll", timeout=1800) for name in lane["shards"]]
        if any(not group["filter"] for group in groups):
            raise ValueError("An empty gameplay filter would run the entire assembly")
        os.chdir("bin/Content.IntegrationTests")
    code = 0
    for group in groups:
        # Priority, ordering and estimates do not affect the complete test selection.
        settings = {k: v for k, v in group.items() if k not in ("priority", "rank", "seconds")}
        key = fingerprint(plan, settings, environment)
        previous = plan.get("trusted", {}).get("records", {}).get(group["name"], {})
        previous = dict(previous, evidence_url=plan.get("trusted", {}).get("url", ""))
        priority = plan["unit_priority"] if args.unit else group["priority"]
        result = execute_group(settings, priority, previous, key, state / "observations")
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
