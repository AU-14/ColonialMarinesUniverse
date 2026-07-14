#!/usr/bin/env python3
import shutil
from pathlib import Path

# Run this from the ColonialMarinesUniverse repo root
# It looks for ColonialMarinesAudio next to it (by moving one directory up)
# Overwrites placeholder/silent .ogg/.mp3 file(s) at the matching path on the public repo

PRIVATE_ROOT = Path("../ColonialMarinesAudio/Resources/Audio/_CMU14/Private")
PUBLIC_ROOT = Path("Resources/Audio/_CMU14/Private")
PLACEHOLDERS = {
    ".ogg": Path(__file__).resolve().parent / "sync_audio_placeholder.ogg",
    ".mp3": Path(__file__).resolve().parent / "sync_audio_placeholder.mp3",
}
created = updated = removed = 0

if not PRIVATE_ROOT.exists():
    raise SystemExit("CMAudio repository not found, should be next to CMU repo.")
for ext, placeholder in PLACEHOLDERS.items():
    if not placeholder.exists():
        raise SystemExit(f"Missing the placeholder {ext} file.")

private_assets = sorted(
    p.relative_to(PRIVATE_ROOT)
    for ext in PLACEHOLDERS
    for p in PRIVATE_ROOT.rglob(f"*{ext}")
)

for rel in private_assets:
    dst = PUBLIC_ROOT / rel
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists():
        print(f"Refreshing placeholder: {dst}")
        updated += 1
    else:
        created += 1
    shutil.copy2(PLACEHOLDERS[dst.suffix], dst)

private_asset_set = set(private_assets)
for ext in PLACEHOLDERS:
    for placeholder in PUBLIC_ROOT.rglob(f"*{ext}"):
        rel = placeholder.relative_to(PUBLIC_ROOT)
        if rel not in private_asset_set:
            placeholder.unlink()
            removed += 1
            print(f"Removed orphaned placeholder: {rel}")

print()
print("Audio placeholder sync complete:")
print(f"    Created: {created}")
print(f"    Updated: {updated}")
print(f"    Removed: {removed}")
