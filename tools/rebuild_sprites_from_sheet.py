#!/usr/bin/env python3
"""
rebuild_sprites_from_sheet.py
=============================
REPAIRS the "vanishing body parts" problem at the source.

The individually-split direction sprites were damaged (missing heads, missing
feet, interior torso holes). The RAW green-screen sheets in the repo root are
complete and consistent turntables, so this tool rebuilds every sprite set
from them:

  * unity_assets/sprites_16/       <- raw_sheets/stone_age_sheet_16_raw.png     (16 dirs)
  * unity_assets/sprites_16_idle/  <- raw_sheets/stone_age_idle_16_f{0,1,2}_raw.png (48 frames)
  * unity_assets/sprites/          <- tops repaired by affine band-borrow from
                                      the rebuilt 16-dir set (same angles)

Pipeline per cell: green-screen key -> despill -> speck cleanup -> baseline
alignment (feet on one common row) -> uniform canvas. Originals are backed up
to tools/originals_backup.zip first.

It then VERIFIES the result: every sprite must have full vertical coverage
(no truncated tops/feet) and no interior holes, and every neighbouring
direction pair must stay fully covered while cross-fading at 25/50/75%
(exactly what BillboardBlendWind.shader does while the camera orbits).

Run from the repo root:   python3 tools/rebuild_sprites_from_sheet.py
"""

import os
import glob
import zipfile
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

DIRS16 = ["00_front", "01_front_right_slight", "02_front_right", "03_right_front",
          "04_right", "05_right_back", "06_back_right", "07_back_right_slight",
          "08_back", "09_back_left_slight", "10_back_left", "11_left_back",
          "12_left", "13_left_front", "14_front_left", "15_front_left_slight"]

DIRS8 = ["00_front", "01_front_right", "02_right", "03_back_right",
         "04_back", "05_back_left", "06_left", "07_front_left"]

CANVAS_W, CANVAS_H = 176, 392      # uniform output canvas for the 16-dir sets
FEET_Y = 385                       # common baseline row for feet

# ------------------------------------------------------------------ keying


def key_cell(cell_rgb: np.ndarray) -> np.ndarray:
    """Green-screen key -> RGBA float (0..1), despilled, speck-cleaned."""
    rgb = cell_rgb.astype(np.float64) / 255.0
    g_dom = rgb[:, :, 1] - np.maximum(rgb[:, :, 0], rgb[:, :, 2])

    alpha = np.clip((0.20 - g_dom) / 0.20, 0.0, 1.0)     # bg green-dom ~0.82
    alpha = np.clip((alpha - 0.10) / 0.80, 0.0, 1.0)     # soften haze, keep AA edge

    # despill: remove leftover green dominance on semi-transparent edge pixels
    spill = np.clip(g_dom, 0.0, None) * alpha
    rgb[:, :, 1] = np.clip(rgb[:, :, 1] - spill, 0.0, 1.0)
    rgb[:, :, 1] = np.minimum(rgb[:, :, 1], np.maximum(rgb[:, :, 0], rgb[:, :, 2]) + 0.04)

    # speck cleanup: binary opening on the solid mask
    mask = alpha > 0.25
    mask = binary_open(mask)
    alpha *= mask

    out = np.dstack([rgb, alpha[:, :, None]])
    return out


def _shift_or(m, dx, dy):
    return np.roll(np.roll(m, dy, axis=0), dx, axis=1)


def binary_open(mask, it=1):
    """Erode+dilate with a 3x3 structuring element (numpy only)."""
    m = mask.copy()
    for _ in range(it):
        e = m.copy()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                e &= _shift_or(m, dx, dy)
        m = e
    for _ in range(it):
        d = m.copy()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                d |= _shift_or(m, dx, dy)
        m = d
    return m


def place_on_baseline(rgba: np.ndarray, feet_y: int = FEET_Y,
                      canvas: tuple = (CANVAS_W, CANVAS_H)) -> np.ndarray:
    """Paste into the uniform canvas so the feet sit exactly on `feet_y`."""
    cw, ch = canvas                      # canvas tuple is (width, height)
    out = np.zeros((ch, cw, 4), dtype=np.float64)
    ys, _ = np.where(rgba[:, :, 3] > 0.5)
    if len(ys) == 0:
        return out
    bottom = ys.max()
    dy = feet_y - bottom
    h, w = rgba.shape[:2]
    y0 = max(0, dy)
    x0 = max(0, (cw - w) // 2)
    sh, sw = min(h, ch - y0), min(w, cw)
    out[y0:y0 + sh, x0:x0 + sw] = rgba[:sh, :sw]
    return out


def split_sheet(path: str):
    """Split an 8x2 green-screen sheet into 16 keyed RGBA cells (order 0..15)."""
    a = np.asarray(Image.open(path).convert("RGB"))
    H, W = a.shape[:2]
    cw, ch = W // 8, H // 2
    cells = []
    for r in range(2):
        for c in range(8):
            cells.append(key_cell(a[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]))
    return cells


# ------------------------------------------------------------------ checks


def row_profile(rgba, thr=0.5):
    al = rgba[:, :, 3]
    return (al > thr).mean(axis=1)


def content_span(rgba, thr=0.5):
    p = row_profile(rgba, thr)
    ys = np.where(p > 0.01)[0]
    return (ys.min(), ys.max()) if len(ys) else (None, None)


def verify_set(dir_path, names, label, blend_neighbours=True):
    print(f"--- verifying {label}")
    imgs = [np.asarray(Image.open(os.path.join(dir_path, n + ".png")).convert("RGBA")).astype(float) / 255.0
            for n in names]
    ok = True
    tops, bottoms = [], []
    for n, im in zip(names, imgs):
        H = im.shape[0]
        y0, y1 = content_span(im)
        tops.append(y0); bottoms.append(y1)
        if y1 < H * 0.8 or y0 > H * 0.2:
            print(f"    !! {n}: body span y[{y0},{y1}] of {H} - truncated!")
            ok = False
        if im[:, :, 3].sum() < H * 2:      # alpha mass sanity (body is ~40% of canvas)
            print(f"    !! {n}: almost no body pixels - broken sprite")
            ok = False
        # interior holes: longest consecutive run of empty rows inside the body
        p = row_profile(im)
        inside = p[y0:y1 + 1]
        longest = cur = 0
        prev = -10
        for j in np.where(inside < 0.005)[0]:
            cur = cur + 1 if j == prev + 1 else 1
            longest = max(longest, cur)
            prev = j
        if longest >= 4:
            print(f"    !! {n}: interior hole {longest} rows")
            ok = False
    tmin, tmax = min(tops), max(tops)
    bmin, bmax = min(bottoms), max(bottoms)
    print(f"    tops {tmin}..{tmax}   bottoms {bmin}..{bmax}")
    if tmax - tmin > 14:
        print(f"    !! top variance too large - someone is missing hair")
        ok = False
    if bmax - bmin > 3:
        print(f"    !! bottoms not aligned - someone is missing feet")
        ok = False
    if blend_neighbours:
        for i in range(len(imgs)):
            A, B = imgs[i], imgs[(i + 1) % len(imgs)]
            for w in (0.25, 0.5, 0.75):
                blend = A * (1 - w) + B * w
                y0, y1 = content_span(blend, thr=0.2)
                if y0 is None:
                    continue
                p = row_profile(blend, thr=0.2)
                inside = p[y0:y1 + 1]
                longest = cur = 0
                prev = -10
                for j in np.where(inside < 0.005)[0]:
                    cur = cur + 1 if j == prev + 1 else 1
                    longest = max(longest, cur)
                    prev = j
                if longest >= 5:
                    print(f"    !! blend {names[i]}->{names[(i+1)%len(imgs)]} @ {w}: {longest}-row hole (body part vanishes while orbiting)")
                    ok = False
    print("    OK" if ok else "    FAILED")
    return ok


# ------------------------------------------------------------------ repairs


def main():
    # ---------------- backups ----------------
    bak = os.path.join(ROOT, "tools", "originals_backup.zip")
    with zipfile.ZipFile(bak, "w", zipfile.ZIP_DEFLATED) as z:
        for d in ["unity_assets/sprites_16", "unity_assets/sprites_16_idle", "unity_assets/sprites"]:
            for f in glob.glob(os.path.join(ROOT, d, "*.png")):
                z.write(f, os.path.relpath(f, ROOT))
    print(f"backup -> {os.path.relpath(bak, ROOT)}")

    # ---------------- rebuild 16-dir static set ----------------
    cells = split_sheet(os.path.join(ROOT, "raw_sheets", "stone_age_sheet_16_raw.png"))
    out16 = os.path.join(ROOT, "unity_assets", "sprites_16")
    for name, cell in zip(DIRS16, cells):
        placed = place_on_baseline(cell)
        Image.fromarray((np.clip(placed, 0, 1) * 255).astype(np.uint8)).save(
            os.path.join(out16, name + ".png"))
    print(f"rebuilt 16 static sprites -> {os.path.relpath(out16, ROOT)}")

    # ---------------- rebuild 16-dir idle frames ----------------
    outidle = os.path.join(ROOT, "unity_assets", "sprites_16_idle")
    for fr in range(3):
        cells = split_sheet(os.path.join(ROOT, "raw_sheets", f"stone_age_idle_16_f{fr}_raw.png"))
        for name, cell in zip(DIRS16, cells):
            placed = place_on_baseline(cell)
            fn = os.path.join(outidle, f"{name}_f{fr}.png")
            Image.fromarray((np.clip(placed, 0, 1) * 255).astype(np.uint8)).save(fn)
    print(f"rebuilt 48 idle frames -> {os.path.relpath(outidle, ROOT)}")

    # ---------------- repair 8-dir tops (borrow from 16-dir, same angle) ----
    dir16 = os.path.join(ROOT, "unity_assets", "sprites_16")
    dir8 = os.path.join(ROOT, "unity_assets", "sprites")
    imgs8 = [np.asarray(Image.open(os.path.join(dir8, n + ".png")).convert("RGBA")).astype(float) / 255.0
             for n in DIRS8]
    tops8 = [content_span(im)[0] for im in imgs8]
    tmin = min(tops8)
    # reference body height from the COMPLETE sprites (a truncated target would
    # otherwise shrink the vertical scale of the borrowed band)
    bodies = [content_span(im)[1] - content_span(im)[0] for im, t in zip(imgs8, tops8)
              if t - tmin <= 3]
    ref_body8 = float(np.median(bodies))
    for i, (name, im8, t0) in enumerate(zip(DIRS8, imgs8, tops8)):
        missing = t0 - tmin
        if missing <= 3:
            print(f"    8-dir {name}: ok (top {t0})")
            continue
        donor = np.asarray(Image.open(os.path.join(dir16, DIRS16[2 * i] + ".png")).convert("RGBA")).astype(float) / 255.0
        im8 = borrow_top_band(im8, donor, missing, ref_body_height=ref_body8)
        Image.fromarray((np.clip(im8, 0, 1) * 255).astype(np.uint8)).save(
            os.path.join(dir8, name + ".png"))
        print(f"    8-dir {name}: repaired {missing}px top from {DIRS16[2*i]}")

    # ---------------- verify ----------------
    ok = verify_set(out16, DIRS16, "16-direction set")
    ok &= verify_set(dir8, DIRS8, "8-direction set")
    idle_names = [f"{d}_f{fr}" for fr in range(3) for d in DIRS16]
    ok &= verify_set(outidle, idle_names, "idle frames (per frame, no blend check)", blend_neighbours=False)

    if not ok:
        raise SystemExit("verification FAILED - see messages above")

    # ---------------- consistent torso harness (crossed straps) ----------------
    # runs redesign_torso_straps.py so a rebuild keeps the redesigned belt
    if os.environ.get("SKIP_BELT", "") != "1":
        import runpy
        print("--- applying belt consistency pass (crossed straps on every view)")
        runpy.run_path(os.path.join(ROOT, "tools", "redesign_torso_straps.py"), run_name="__main__")

    print("ALL CHECKS PASSED - no vanishing body parts.")


def borrow_top_band(target, donor, missing, ref_body_height=None):
    """Affine-map the donor's head-top band into the target's empty top area.

    The vertical mapping is anchored at the FEET (the target's own top is
    truncated and would anchor wrongly):  yt = ty1 + (yd - dy1) * sy.
    `ref_body_height` (the set's median body height) keeps the scale honest —
    the target's own body height is short by exactly the missing part.
    The horizontal mapping uses the two content bounding boxes.
    """
    ty0, ty1 = content_span(target)
    txs = np.where((target[:, :, 3] > 0.5).any(axis=0))[0]
    tx0, tx1 = txs.min(), txs.max()
    dy0, dy1 = content_span(donor)
    dxs = np.where((donor[:, :, 3] > 0.5).any(axis=0))[0]
    dx0, dx1 = dxs.min(), dxs.max()

    sx = (tx1 - tx0) / max(dx1 - dx0, 1)
    target_body = ref_body_height if ref_body_height else (ty1 - ty0)
    sy = target_body / max(dy1 - dy0, 1)

    H, W = target.shape[:2]
    out = target.copy()
    feather = 14                       # rows of fade into the target's own head
    need_t = missing + 6               # target rows to fill above the target top
    for yt in range(max(ty0 - need_t - 4, 0), min(ty0 + feather, H)):
        # feet-anchored inverse map: yd = dy1 + (yt - ty1)/sy
        yd = int(dy1 + (yt - ty1) / sy)
        if not (0 <= yd < donor.shape[0]):
            continue
        row = donor[yd]
        # xd = dx0 + (xt - tx0)/sx
        xs = (np.arange(W) - tx0) / sx + dx0
        xi = np.clip(np.round(xs).astype(int), 0, donor.shape[1] - 1)
        src = row[xi]                      # RGBA row, affine-x mapped
        # vertical feather: full opacity above the seam, fading to 0 below it
        f = np.clip((yt - (ty0 - 2)) / float(feather), 0.0, 1.0)   # 0 above seam -> 1 below
        a_d = src[:, 3] * (1.0 - f)
        a_t = out[yt, :, 3]
        a_n = np.clip(a_d + a_t * (1.0 - a_d), 0.0, 1.0)
        safe = a_n > 0.001
        out[yt, safe, :3] = (src[safe, :3] * a_d[safe, None]
                             + out[yt, safe, :3]
                             * (a_t[safe] * (1.0 - a_d[safe]))[:, None]) / a_n[safe, None]
        out[yt, :, 3] = a_n
    return out


if __name__ == "__main__":
    main()
