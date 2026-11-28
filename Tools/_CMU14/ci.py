#!/usr/bin/env python3
"""CMU14: conservative CI planning and observable, bounded test execution."""

import argparse
from collections import deque
import html
import json
import os
from pathlib import Path
import queue
import re
import signal
import subprocess
import sys
import threading
import time
import xml.etree.ElementTree as ET
from ci_selection import documentation


def select_tests(event, paths):
    # An incomplete diff must never reduce coverage. Resource files (including
    # Markdown shipped as resources) and unknown paths always take the full path.
    full = dict(build=True, unit=True, gameplay=True, reason="Full suite: code, data, or unknown changes.")
    if event not in ("pull_request", "push", "merge_group") or not isinstance(paths, list) or not paths:
        return full
    if any(not isinstance(path, str) or not path for path in paths):
        return full

    relevant = [path for path in paths if not documentation(path)]
    if not relevant:
        return dict(build=False, unit=False, gameplay=False, reason="Documentation only: no compiled or runtime inputs changed.")
    if all(path.endswith(".cs") and path.startswith(("Content.IntegrationTests/", "Content.CMU/IntegrationTests/"))
           for path in relevant):
        return dict(build=True, unit=False, gameplay=True,
                    reason="Integration sources only: build and gameplay suite; unit inputs unchanged.")
    # Content.IntegrationTests references Content.Tests, including its helpers.
    # Keep both eligible here; dependency analysis can narrow fixture-only edits.
    return full


def plan(args):
    from adaptive_ci import baseline_changes, git, plan_run
    paths = None
    if args.changes:
        try:
            paths = json.loads(Path(args.changes).read_text(encoding="utf-8"))
        except (OSError, ValueError):
            pass
    if paths is None and args.event in ("pull_request", "push", "merge_group"):
        try:
            if args.event == "pull_request":
                base, _ = git("rev-parse", "HEAD^1", "HEAD^2").splitlines()
            else:
                base = os.environ.get("CI_BASE_SHA", "")
            paths = baseline_changes(base)
        except (OSError, ValueError, subprocess.SubprocessError):
            pass
    selected = select_tests(args.event, paths)
    shards = json.loads(Path(__file__).with_name("ci-shards.json").read_text(encoding="utf-8"))
    selected = plan_run(selected, paths, args.event, shards)
    for key, value in selected.items():
        line = f"{key}={json.dumps(value, separators=(',', ':')) if not isinstance(value, str) else value}"
        if os.environ.get("GITHUB_OUTPUT"):
            with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
                output.write(line + "\n")
    print(selected["reason"], flush=True)
    write_summary("### Test plan\n\n" + selected["reason"] + "\n")


def write_summary(text):
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
            summary.write(text)


def read_results(path):
    root = ET.parse(path).getroot()
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    summary = root.find("t:ResultSummary", ns)
    if summary is None:
        raise ValueError("TRX has no result summary")
    counters = summary.find("t:Counters", ns)
    if counters is None:
        raise ValueError("TRX has no test counters")
    count = {key: int(counters.get(key, "0")) for key in
             ("total", "executed", "passed", "failed", "error", "timeout", "aborted", "notExecuted")}
    count["outcome"] = summary.get("outcome", "").lower()
    valid = (summary.get("outcome", "").lower() in ("completed", "passed")
             and count["executed"] > 0
             and not any(count[key] for key in ("failed", "error", "timeout", "aborted")))
    results = root.findall("t:Results/t:UnitTestResult", ns)
    failed = []
    timings = []
    for result in results:
        name = result.get("testName", "unnamed")
        if result.get("outcome") not in ("Passed", "NotExecuted"):
            info = result.find("t:Output/t:ErrorInfo", ns)
            detail = "" if info is None else "\n".join(info.itertext()).strip()
            failed.append((name, detail))
        duration = result.get("duration", "0:0:0")
        hours, minutes, seconds = duration.split(":")
        timings.append((int(hours) * 3600 + int(minutes) * 60 + float(seconds), name))
    return valid and not failed, count, failed, sorted(timings, reverse=True)[:5]


def case_history(path):
    """Use TRX identities, rather than ambiguous display names, for future priority."""
    root = ET.parse(path).getroot()
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    definitions = {}
    for test in root.findall("t:TestDefinitions/t:UnitTest", ns):
        method = test.find("t:TestMethod", ns)
        if method is not None:
            definitions[test.get("id")] = method.get("className", "")
    cases = []
    for result in root.findall("t:Results/t:UnitTestResult", ns):
        hours, minutes, seconds = result.get("duration", "0:0:0").split(":")
        cases.append(dict(id=result.get("testId", ""), fixture=definitions.get(result.get("testId"), ""),
                          name=result.get("testName", ""), outcome=result.get("outcome", ""),
                          seconds=int(hours) * 3600 + int(minutes) * 60 + float(seconds)))
    return cases


def duration(text):
    units = {"ms": 0.001, "s": 1, "m": 60, "h": 3600}
    return sum(float(value) * units[unit] for value, unit in re.findall(r"(\d+(?:\.\d+)?)\s*(ms|[smh])", text))


def logged_cases(results):
    """Cases from console result lines, for a phase killed before VSTest wrote its TRX."""
    cases = []
    for outcome, text in results:
        match = re.match(r"(.+?) \[([^\]]*)\]$", text)
        name, clock = (match.group(1), duration(match.group(2))) if match else (text, 0.0)
        cases.append(dict(id="", fixture="", name=name, seconds=clock,
                          outcome="NotExecuted" if outcome == "Skipped" else outcome))
    return cases


# After the kill, how long the runner keeps waiting on the test process tree and its log pipe.
KILL_GRACE = 30


def stop_process(process):
    # CI runs on Linux: terminate the whole test-host group, including descendants
    # which could otherwise keep the pipe open after dotnet itself has exited.
    if os.name == "posix":
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            return
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            pass
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    elif process.poll() is None:
        process.kill()
    # A process in uninterruptible sleep takes SIGKILL only once it leaves the kernel. The lane
    # must not wait on it.
    try:
        process.wait(timeout=KILL_GRACE)
    except subprocess.TimeoutExpired:
        print("::warning::The test process outlived SIGKILL; the lane continues without reaping it.", flush=True)


def run_tests(args):
    command = args.command
    if command and command[0] == "--":
        command = command[1:]
    if not command:
        raise ValueError("A test command is required after --")
    log = Path(args.log)
    trx = Path(args.trx)
    log.parent.mkdir(parents=True, exist_ok=True)
    trx.parent.mkdir(parents=True, exist_ok=True)
    # A rerun must never consume evidence left by an earlier attempt.
    trx.unlink(missing_ok=True)
    start = time.monotonic()
    lines = queue.Queue(maxsize=1000)
    tail = deque(maxlen=40)
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               text=True, encoding="utf-8", errors="replace",
                               start_new_session=os.name == "posix")

    def collect():
        with log.open("w", encoding="utf-8", newline="\n") as output:
            for line in process.stdout:
                output.write(line)
                output.flush()
                lines.put(line.rstrip())
        lines.put(None)

    reader = threading.Thread(target=collect, daemon=True)
    reader.start()
    next_heartbeat = start + args.heartbeat
    completed = 0
    last = "waiting for the first result"
    detail_lines = 0
    logged = []
    no_match_seen = False
    status = None
    print(f"{args.name}: starting; limit {args.timeout:g}s; full log: {log}", flush=True)
    try:
        while True:
            now = time.monotonic()
            if now - start >= args.timeout:
                print(f"{args.name}: wall-clock timeout after {args.timeout:g}s", flush=True)
                status = 124
                break
            if now >= next_heartbeat:
                print(f"{args.name}: {int(now - start)}s elapsed; {completed} results; last: {last}", flush=True)
                next_heartbeat = now + args.heartbeat
            try:
                line = lines.get(timeout=min(0.2, args.heartbeat))
            except queue.Empty:
                continue
            if line is None:
                # A child may close stdout while continuing to run.
                remaining = max(0.01, args.timeout - (time.monotonic() - start))
                try:
                    status = process.wait(timeout=remaining)
                except subprocess.TimeoutExpired:
                    status = 124
                break
            tail.append(line)
            no_match_seen |= "No test matches" in line
            match = re.match(r"\s+(Passed|Failed|Skipped) (.+)", line)
            if match:
                completed += 1
                logged.append((match.group(1), match.group(2)))
                last = match.group(2)[:160]
                detail_lines = 80 if match.group(1) == "Failed" else 0
            if re.search(r"Unhandled exception|test run was aborted|Test Run Failed|No test matches", line, re.I):
                detail_lines = 80
            if detail_lines:
                # Prefix untrusted output so it cannot become an Actions command.
                print("  | " + line, flush=True)
                detail_lines -= 1
    except KeyboardInterrupt:
        status = 130
    finally:
        stop_process(process)
        # Drain after termination so a full queue cannot strand the log writer. A process that
        # outlives the kill, stuck in uninterruptible sleep or outside the process group, keeps the
        # pipe open, and the lane still has to end before the job cap so its evidence uploads.
        drain_until = time.monotonic() + KILL_GRACE
        while reader.is_alive() and time.monotonic() < drain_until or not lines.empty():
            try:
                line = lines.get(timeout=0.1)
                if line is not None:
                    tail.append(line)
                    no_match_seen |= "No test matches" in line
                    match = re.match(r"\s+(Passed|Failed|Skipped) (.+)", line)
                    if match:
                        logged.append((match.group(1), match.group(2)))
            except queue.Empty:
                continue
        reader.join(timeout=max(0.0, drain_until - time.monotonic()))
        # Closing the pipe under a blocked reader would wait on its lock.
        if not reader.is_alive():
            process.stdout.close()

    elapsed = time.monotonic() - start
    observation = dict(valid=False, empty=False, seconds=elapsed, cases=[])
    report = [f"### {args.name}\n", f"Elapsed: {elapsed:.1f}s; process exit: {status}.\n"]
    try:
        observation["cases"] = case_history(trx)
        valid, count, failures, slowest = read_results(trx)
        observation["counters"] = count
        empty_outcome = count["outcome"] in ("completed", "passed") or (no_match_seen and count["outcome"] == "notexecuted")
        observation["empty"] = (status == 0 and count["executed"] == 0 and empty_outcome and not failures and
                                not any(count[k] for k in ("failed", "error", "timeout", "aborted")))
        counts = f"Passed: {count['passed']}, Failed: {count['failed']}, Skipped: {count['notExecuted']}"
        print(counts, flush=True)
        report.append(counts + "\n")
        for name, detail in failures[:10]:
            # HTML escapes prevent test names/messages from becoming summary markup.
            report.append(f"<details><summary>{html.escape(name)}</summary><pre>{html.escape(detail[:6000])}</pre></details>\n")
        if slowest:
            report.append("\nSlowest cases:\n")
            report.extend(f"- {seconds:.1f}s — {html.escape(name)}\n" for seconds, name in slowest)
        if not valid and not (getattr(args, "allow_empty", False) and observation["empty"]):
            report.append("\nTest evidence is failed, aborted, or empty.\n")
            status = status or 1
    except (OSError, ET.ParseError, ValueError) as error:
        if logged:
            # Each finished test was reported on the console, so the count survives the kill.
            observation["cases"] = logged_cases(logged)
            count = dict.fromkeys(("error", "timeout", "aborted"), 0)
            count.update(total=len(logged), executed=sum(outcome != "Skipped" for outcome, _ in logged),
                         passed=sum(outcome == "Passed" for outcome, _ in logged),
                         failed=sum(outcome == "Failed" for outcome, _ in logged),
                         notExecuted=sum(outcome == "Skipped" for outcome, _ in logged), outcome="incomplete")
            observation["counters"] = count
            note = f"{count['executed']} finished tests read from the console log, {count['failed']} failed."
            print(note, flush=True)
            report.append("\n" + note + "\n")
        # VSTest can omit TRX entirely for a stale optional priority filter.
        # Only that explicit no-match result may fall back to the full selection.
        no_match = status == 0 and no_match_seen
        observation["empty"] = no_match
        if getattr(args, "allow_empty", False) and no_match:
            report.append("\nOptional priority selection matched no tests; continuing with full coverage.\n")
        else:
            report.append(f"\nNo complete test evidence: {error}.\n")
            status = status or 1
    if status:
        print("Last output (full log is retained):", flush=True)
        for line in tail:
            print("  | " + line, flush=True)
        print(
            "::error::Test execution failed, did not produce valid results, or timed out.",
            flush=True,
        )
    report.append(f"\nResult: {'failed' if status else 'passed'}.\n")
    observation.update(status=status, valid=status == 0 and not observation["empty"])
    log.with_suffix(".json").write_text(json.dumps(observation), encoding="utf-8")
    if status:
        write_summary("\n".join(report))
    return status


def main():
    # GitHub may cancel with SIGTERM after its initial interrupt.
    def interrupted(_signal, _frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, interrupted)
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="mode", required=True)
    planning = sub.add_parser("plan")
    planning.add_argument("--event", required=True)
    planning.add_argument("--changes")
    running = sub.add_parser("run")
    running.add_argument("--name", required=True)
    running.add_argument("--log", required=True)
    running.add_argument("--trx", required=True)
    running.add_argument("--timeout", type=float, required=True)
    running.add_argument("--heartbeat", type=float, default=30)
    running.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.mode == "plan":
        plan(args)
        return 0
    if args.timeout <= 0 or args.heartbeat <= 0:
        parser.error("Timeout and heartbeat must be positive")
    return run_tests(args)


if __name__ == "__main__":
    sys.exit(main())
