"""Compatibility entry point for the recording-based fighter/VTOL mix.

See AUDIO.md for source preparation and dependencies.
"""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name("generate-fighter-audio.py")), run_name="__main__")
