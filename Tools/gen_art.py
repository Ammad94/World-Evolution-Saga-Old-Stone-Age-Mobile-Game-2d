#!/usr/bin/env python3
"""
DEPRECATED — the old procedural pixel-art generator lived here.

The project now uses the photorealistic art pipeline instead:

    Tools/realart.py     slices AI-generated master sheets (Tools/sheets/*.png)
                         into the game sprites and writes/patches Unity .meta
                         files. Run `python3 Tools/realart.py list` to see which
                         sheets are present and `all` to process everything.

The old low-resolution pixel art was replaced wholesale; running this script
does nothing except point you at the new pipeline (so nobody accidentally
regenerates the old art over the new assets).
"""
import sys

if __name__ == '__main__':
    print(__doc__)
    sys.exit(0)
