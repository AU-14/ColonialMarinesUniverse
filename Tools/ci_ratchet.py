#!/usr/bin/env python3
"""Baseline ratchet for CI jobs carrying known base debt.

Usage:
  ci_ratchet.py lint <current-errors-file> <baseline-file>
      Entries are full '::error in ...' lines (sorted unique).
  ci_ratchet.py trx <trx-dir-or-file>... <baseline-file>
      Extracts failed test names from NUnit TRX files, compares to baseline.

Fails (exit 1) only on entries present now but missing from the baseline.
Baseline entries that no longer reproduce are reported so the baseline
can be shrunk.
"""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def load_set(path):
    return {
        line.strip()
        for line in Path(path).read_text().splitlines()
        if line.strip() and not line.strip().startswith("#")
    }


def failed_tests_from_trx(paths):
    names = set()
    for p in paths:
        for trx in Path(p).glob("*.trx") if Path(p).is_dir() else [Path(p)]:
            root = ET.parse(trx).getroot()
            for result in root.iter():
                if not result.tag.endswith("}UnitTestResult") and result.tag != "UnitTestResult":
                    continue
                if result.get("outcome") in ("Failed", "Error"):
                    names.add(result.get("testName", "?"))
    return names


def ratchet(current, baseline, current_label):
    new = current - baseline
    fixed = baseline - current
    if fixed:
        print(f"{len(fixed)} baseline entries no longer reproduce (shrink the baseline):")
        for e in sorted(fixed):
            print(f"  - {e}")
    if new:
        print(f"NEW failures ({len(new)}):")
        for e in sorted(new):
            print(f"  + {e}")
        return 1
    print(f"OK: no new entries ({len(current)} {current_label}, {len(baseline)} baseline)")
    return 0


def main(argv):
    if len(argv) >= 4 and argv[1] == "lint":
        sys.exit(ratchet(load_set(argv[2]), load_set(argv[3]), "lint errors"))
    if len(argv) >= 3 and argv[1] == "trx":
        current = failed_tests_from_trx(argv[2:-1])
        if not Path(argv[-1]).exists():
            print(f"BOOTSTRAP: {argv[-1]} absent; first run defines the baseline.")
            print(f"Save these {len(current)} entries to {argv[-1]}:")
            for e in sorted(current):
                print(e)
            sys.exit(0)
        sys.exit(ratchet(current, load_set(argv[-1]), "failed tests"))
    print(__doc__)
    sys.exit(2)


if __name__ == "__main__":
    main(sys.argv)
