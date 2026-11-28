#!/usr/bin/env python3
"""Reject Destructive test pairs that state no reason.

Destructive = true kills the pair after the test, so the next test boots a fresh
server and client (about 48 s on CI). The flag is right when a test changes
prototypes or system-level state a recycled pair would keep; it must say so in a
comment on its own line, on one of the two lines above, or in a
"// destructive-pairs:" line anywhere in the same file.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOTS = ("Content.CMU/IntegrationTests", "Content.IntegrationTests/_CMU14")
FLAG = re.compile(r"\bDestructive\s*=\s*true\b")
DIRECTIVE = "// destructive-pairs:"


def violations(path: Path) -> list[tuple[int, str]]:
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    if any(DIRECTIVE in line for line in lines):
        return []
    found = []
    for index, line in enumerate(lines):
        match = FLAG.search(line)
        if not match or "//" in line:
            continue
        if any("//" in previous for previous in lines[max(0, index - 2):index]):
            continue
        found.append((index + 1, match.group(0)))
    return found


def main(argv: list[str]) -> int:
    failed = False
    for root in argv[1:] or ROOTS:
        for path in sorted(Path(root).rglob("*.cs")):
            for line, flag in violations(path):
                print(f"::error file={path},line={line},title=Destructive test pair without a reason::"
                      f"`{flag}` boots a fresh server and client for the next test. Say why it cannot recycle a pair in a comment, or remove the flag.")
                failed = True
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
