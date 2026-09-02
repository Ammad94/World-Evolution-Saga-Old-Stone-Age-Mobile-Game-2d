#!/usr/bin/env python3
"""
rebuild_all_sets.py
===================
Rebuilds ALL sprite sets directly from the green-screen raw sheets in
raw_sheets/ (which contain the hand-painted chest harness), so the sprites
are EXACTLY the keyed-out raw images:

  * unity_assets/sprites_16/       <- stone_age_sheet_16_raw.png, cells 0..15,
                                     feet baseline-aligned (176x392 canvas)
  * unity_assets/sprites_16_idle/  <- the three idle raw sheets, same way
  * unity_assets/sprites/ (8-dir)  <- the EVEN cells (0,2,..,14) of the same
                                     16-dir sheet, LANCZOS-scaled onto the
                                     232x578 canvas, feet at row 563 — the
                                     8-dir set has no raw sheet of its own

Keying/baseline code is shared with rebuild_sprites_from_sheet.py
(split_sheet + place_on_baseline). Pre-harness archives:
tools/raw_sheets_original.zip (raw sheets), tools/originals_backup.zip +
tools/idle_pre_belt_backup.zip (sprites).

Run from the repo root:  python3 tools/rebuild_all_sets.py
"""
import os
import sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebuild_sprites_from_sheet as rb

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S16 = os.path.join(ROOT, "unity_assets", "sprites_16")
IDLE = os.path.join(ROOT, "unity_assets", "sprites_16_idle")
D8 = os.path.join(ROOT, "unity_assets", "sprites")
RAWS = os.path.join(ROOT, "raw_sheets")

S8 = 232 / 176.0            # 8-dir canvas scale
CW8, CH8, FEET8 = 232, 578, 563


def _save(arr, path):
    Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8)).save(path)


def rebuild_16dir():
    cells = rb.split_sheet(os.path.join(RAWS, "stone_age_sheet_16_raw.png"))
    for i, d in enumerate(rb.DIRS16):
        _save(rb.place_on_baseline(cells[i]), os.path.join(S16, d + ".png"))
    print("16-dir statics rebuilt from the raw sheet")


def rebuild_idle():
    for fr in range(3):
        cells = rb.split_sheet(os.path.join(RAWS, f"stone_age_idle_16_f{fr}_raw.png"))
        for i, d in enumerate(rb.DIRS16):
            _save(rb.place_on_baseline(cells[i]), os.path.join(IDLE, f"{d}_f{fr}.png"))
    print("idle frames rebuilt from the raw sheets")


def rebuild_8dir():
    cells = rb.split_sheet(os.path.join(RAWS, "stone_age_sheet_16_raw.png"))
    for i, d8 in enumerate(rb.DIRS8):
        cell = rb.place_on_baseline(cells[2 * i])
        img = Image.fromarray(np.clip(cell * 255, 0, 255).astype(np.uint8))
        img = img.resize((int(round(176 * S8)), int(round(392 * S8))), Image.LANCZOS)
        a = np.asarray(img).astype(float) / 255.0
        bottom = np.where((a[:, :, 3] > 0.5).any(axis=1))[0].max()
        canvas = np.zeros((CH8, CW8, 4))
        dy = int(round(FEET8 - bottom))
        y0 = max(0, dy)
        src = a[max(0, -dy): CH8 - y0]
        x0 = max(0, (CW8 - a.shape[1]) // 2)
        canvas[y0:y0 + src.shape[0], x0:x0 + a.shape[1]] = src
        _save(canvas, os.path.join(D8, d8 + ".png"))
    print("8-dir rebuilt from the same raw sheet (scaled)")


if __name__ == "__main__":
    rebuild_16dir()
    rebuild_idle()
    rebuild_8dir()
    ok = rb.verify_set(S16, rb.DIRS16, "16-dir")
    ok &= rb.verify_set(IDLE, [f"{d}_f{fr}" for fr in range(3) for d in rb.DIRS16],
                        "idle", blend_neighbours=False)
    ok &= rb.verify_set(D8, rb.DIRS8, "8-dir")
    print("ALL SETS:", "PASS" if ok else "FAIL")
    raise SystemExit(0 if ok else 1)
