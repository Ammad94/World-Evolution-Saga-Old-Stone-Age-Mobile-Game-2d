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


def _sanitize_after_scale(rgba):
    """LANCZOS re-mixes interior colour with transparency and can resurrect
    a green fringe. Re-clamp G and inpaint after the resize."""
    r, g, b = rgba[:, :, 0], rgba[:, :, 1], rgba[:, :, 2]
    max_rb = np.maximum(r, b)
    spill = np.clip(g - max_rb, 0.0, None)
    out = rgba.copy()
    out[:, :, 1] = g - spill
    out[:, :, 0] = np.clip(r + spill * 0.20, 0.0, 1.0)
    out[:, :, 2] = np.clip(b + spill * 0.08, 0.0, 1.0)
    g_dom = g - max_rb
    out[:, :, 3] *= np.clip(1.0 - np.clip(g_dom - 0.02, 0.0, None) / 0.16, 0.0, 1.0)
    return rb.inpaint_rgb(out)


def rebuild_8dir():
    cells = rb.split_sheet(os.path.join(RAWS, "stone_age_sheet_16_raw.png"))
    for i, d8 in enumerate(rb.DIRS8):
        cell = rb.place_on_baseline(cells[2 * i])
        img = Image.fromarray(np.clip(cell * 255, 0, 255).astype(np.uint8))
        img = img.resize((int(round(176 * S8)), int(round(392 * S8))), Image.LANCZOS)
        a = _sanitize_after_scale(np.asarray(img).astype(float) / 255.0)
        bottom = np.where((a[:, :, 3] > 0.5).any(axis=1))[0].max()
        canvas = np.zeros((CH8, CW8, 4))
        dy = int(round(FEET8 - bottom))
        y0 = max(0, dy)
        src = a[max(0, -dy): CH8 - y0]
        x0 = max(0, (CW8 - a.shape[1]) // 2)
        canvas[y0:y0 + src.shape[0], x0:x0 + a.shape[1]] = src
        canvas = _sanitize_after_scale(canvas)
        _save(canvas, os.path.join(D8, d8 + ".png"))
    print("8-dir rebuilt from the same raw sheet (scaled)")


def write_contact_sheet():
    """Full-body 8x2 grid of the current 16-dir sprites on a dark ground,
    so leftover green fringe would be obvious if it came back."""
    cols, rows = 8, 2
    imgs = [Image.open(os.path.join(S16, d + ".png")).convert("RGBA") for d in rb.DIRS16]
    w, h = imgs[0].size
    pad, label = 8, 18
    bg = (32, 36, 40, 255)
    sheet = Image.new("RGBA", (cols * (w + pad) + pad, rows * (h + pad + label) + pad), bg)
    from PIL import ImageDraw, ImageFont
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.load_default()
    except Exception:
        font = None
    for i, (d, im) in enumerate(zip(rb.DIRS16, imgs)):
        r, c = divmod(i, cols)
        x = pad + c * (w + pad)
        y = pad + r * (h + pad + label)
        sheet.alpha_composite(im, (x, y))
        draw.text((x + 4, y + h + 2), f"{i:02d}", fill=(220, 220, 220), font=font)
    out = os.path.join(ROOT, "preview", "sprites_current_all16.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)
    print("contact sheet ->", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    rebuild_16dir()
    rebuild_idle()
    rebuild_8dir()
    ok = rb.verify_set(S16, rb.DIRS16, "16-dir")
    ok &= rb.verify_set(IDLE, [f"{d}_f{fr}" for fr in range(3) for d in rb.DIRS16],
                        "idle", blend_neighbours=False)
    ok &= rb.verify_set(D8, rb.DIRS8, "8-dir")
    write_contact_sheet()
    print("ALL SETS:", "PASS" if ok else "FAIL")
    raise SystemExit(0 if ok else 1)
