"""Compare observed engine PVS stage windows without treating missing timers as zero."""
from __future__ import annotations
import argparse
from collections import Counter
import json
import math
from pathlib import Path
import re

FIELDS = re.compile(r"([A-Za-z][A-Za-z0-9]*)=(.*?)(?=\s+[A-Za-z][A-Za-z0-9]*=|$)")

def summarize(lines):
    stages = {}
    malformed = 0
    for line in lines:
        if "[CMU-PERF] pvs-stage-window " not in line:
            continue
        fields = {key: value.strip() for key, value in FIELDS.findall(line.rstrip())}
        area, mode = fields.get("area"), fields.get("pvsAsync", "unknown")
        if not area:
            malformed += 1
            continue
        key = (area, mode)
        row = stages.setdefault(key, {"area": area, "pvs_async": mode,
            "windows": 0, "statuses": Counter(), "calls": 0, "total_ms": 0.0, "observed_seconds": 0.0})
        status = fields.get("status", "unknown")
        row["windows"] += 1
        if status == "observed":
            try:
                calls = int(fields["calls"])
                total, seconds = float(fields["totalMs"]), float(fields["windowSeconds"])
                if calls <= 0 or total < 0 or seconds <= 0 or not math.isfinite(total + seconds):
                    raise ValueError()
            except (KeyError, ValueError):
                status = "malformed"
                malformed += 1
            else:
                row["calls"] += calls
                row["total_ms"] += total
                row["observed_seconds"] += seconds
        row["statuses"][status] += 1
    output = []
    for _, row in sorted(stages.items()):
        row["statuses"] = dict(row["statuses"])
        if not row["calls"]:
            row["total_ms"] = row["avg_ms"] = row["observed_ms_per_second"] = None
        else:
            row["avg_ms"] = row["total_ms"] / row["calls"]
            row["observed_ms_per_second"] = row["total_ms"] / row["observed_seconds"]
        output.append(row)
    return {"status": "has-stage-windows" if output else "missing-stage-windows",
            "malformed_windows": malformed, "stages": output}

def compare(before, after):
    prior = {(r["area"], r["pvs_async"]): r for r in before["stages"]}
    result = []
    for row in after["stages"]:
        old = prior.get((row["area"], row["pvs_async"]))
        delta = None
        if old and old["avg_ms"] is not None and row["avg_ms"] is not None and old["avg_ms"] > 0:
            delta = 100 * (row["avg_ms"] / old["avg_ms"] - 1)
        result.append({"area": row["area"], "pvs_async": row["pvs_async"], "average_change_percent": delta})
    return result

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path, nargs="?")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    def load(path):
        with path.open(encoding="utf-8-sig", errors="replace") as stream:
            return {"file": str(path.resolve()), **summarize(stream)}
    result = {"before": load(args.before),
              "interpretation": "Stage observations are process-wide. Match maps, players, entity counts, settings and workload before attributing a change. Untimed async work is unavailable, not zero."}
    if args.after:
        result["after"] = load(args.after)
        result["comparison"] = compare(result["before"], result["after"])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({key: value["status"] for key, value in result.items() if key in ("before", "after")}))

if __name__ == "__main__":
    main()
