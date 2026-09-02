#!/usr/bin/env python3
"""
apply_harness.py
================
Re-applies the AI-painted chest harness to the sprite sets from the
(harness-edited) green-screen raw sheets in raw_sheets/.

Pipeline (run from the repo root):
  1. split each edited sheet into 16 keyed cells
     (tools/rebuild_sprites_from_sheet.py: split_sheet + place_on_baseline)
  2. zone-confine: only the chest/shoulder band is taken from the edited
     art; every pixel outside the feathered zone stays exactly the
     previous art (tools/confine_harness_zone.py)
  3. the 8-direction set (different art scale) receives the harness by
     transplanting the scaled harness region from the matching 16-dir
     views (00/02/04/06/08/10/12/14), plus a tight local inpaint of the
     old chest bands near the new strap rows

The raw sheets in raw_sheets/ ALREADY contain the harness (painted with an
image model directly on the green-screen sheets, then verified). The
pre-harness originals are archived in tools/raw_sheets_original.zip, and the
pre-harness sprites in tools/originals_backup.zip / tools/idle_pre_belt_backup.zip.

Verification performed when the harness was applied (2026-09-02):
  * 16-dir statics: strap rows fit y0 +/- k*sin(view) with median 2.7 px,
    max 7.8 px residual (bodyH ~363 px); front X pattern matches the
    original back view's X cluster-for-cluster
  * 48 idle frames: median 0.4-2.8 px residual; strap rows agree across
    frames within 4 px
  * 8-dir: median 4.0 px residual; side views show the two separate bands
  * all sets pass the coverage checks (no truncated tops/feet/holes)
"""
import os
import sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(__file__)))
import rebuild_sprites_from_sheet as rb
import redesign_torso_straps as rt
from confine_harness_zone import confine

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S16 = os.path.join(ROOT, "unity_assets", "sprites_16")
IDLE = os.path.join(ROOT, "unity_assets", "sprites_16_idle")
D8 = os.path.join(ROOT, "unity_assets", "sprites")
RAWS = os.path.join(ROOT, "raw_sheets")

DON8 = ["00_front", "02_front_right", "04_right", "06_back_right",
        "08_back", "10_back_left", "12_left", "14_front_left"]


def safe_crop(rgba, r0, r1, x0, x1):
    h, w = rgba.shape[:2]
    out = np.zeros((r1 - r0, x1 - x0, 4))
    ry0, ry1 = max(0, r0), min(h, r1)
    rx0, rx1 = max(0, x0), min(w, x1)
    if ry1 > ry0 and rx1 > rx0:
        out[ry0 - r0:ry1 - r0, rx0 - x0:rx1 - x0] = rgba[ry0:ry1, rx0:rx1]
    return out


def apply_16dir():
    cells = rb.split_sheet(os.path.join(RAWS, "stone_age_sheet_16_raw.png"))
    for i, d in enumerate(rb.DIRS16):
        gen = rb.place_on_baseline(cells[i]) * 255
        cur = np.asarray(Image.open(os.path.join(S16, d + ".png")).convert("RGBA")).astype(float)
        out = confine(gen, cur, rt.zone_of(gen))
        Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(S16, d + ".png"))
    print("16-dir statics: harness applied")


def apply_idle():
    for fr in range(3):
        cells = rb.split_sheet(os.path.join(RAWS, f"stone_age_idle_16_f{fr}_raw.png"))
        for i, d in enumerate(rb.DIRS16):
            gen = rb.place_on_baseline(cells[i]) * 255
            p = os.path.join(IDLE, f"{d}_f{fr}.png")
            cur = np.asarray(Image.open(p).convert("RGBA")).astype(float)
            out = confine(gen, cur, rt.zone_of(gen))
            Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(p)
    print("idle frames: harness applied")


def apply_8dir():
    """8-dir set: keep NATIVE art where it is already correct (the back views
    03/04/05 carry the artist's own crossed X straps -- an earlier attempt
    overpainted them with an upscaled donor and came out blurry, which the
    user flagged on 04_back). The other views receive the harness from the
    matching 16-dir view, LANCZOS-scaled:
      * front-ish views (00/01/07): full chest-zone paste (old bands erased)
      * side views (02/06): pasted ONLY inside the two chest-strap row
        windows (detected from the donor, filtered to the chest zone), so
        the native silhouette and between-strap art stay untouched"""
    import zipfile, io
    z = zipfile.ZipFile(os.path.join(ROOT, "tools", "originals_backup.zip"))
    don_map = {"00_front": "00_front", "01_front_right": "02_front_right",
               "02_right": "04_right", "06_left": "12_left",
               "07_front_left": "14_front_left"}
    for i, d8 in enumerate(rb.DIRS8):
        native = np.asarray(Image.open(io.BytesIO(z.read(
            f"unity_assets/sprites/{d8}.png"))).convert("RGBA")).astype(float)
        if d8 not in don_map:
            Image.fromarray(native.astype(np.uint8)).save(os.path.join(D8, d8 + ".png"))
            continue
        don = np.asarray(Image.open(os.path.join(S16, don_map[d8] + ".png"))
                         .convert("RGBA")).astype(float)
        (ya, yb), (cx, w), yt, bh = rt.zone_of(native)
        (_, _), (cdx, cwd), ytd, bhd = rt.zone_of(don)
        s = bh / bhd
        r0, r1 = int(ytd + 0.20 * bhd), int(ytd + 0.54 * bhd)
        x0, x1 = int(cdx - 1.05 * cwd), int(cdx + 1.05 * cwd)
        crop = np.asarray(Image.fromarray(safe_crop(don, r0, r1, x0, x1).astype(np.uint8)).resize(
            (max(2, int((x1 - x0) * s)), max(2, int((r1 - r0) * s))), Image.LANCZOS)).astype(float)
        py = int(yt + 0.37 * bh) - crop.shape[0] // 2
        px = int(cx - crop.shape[1] / 2)
        h, wd = native.shape[:2]
        yy, xx = np.mgrid[0:h, 0:wd]
        canvas = np.zeros_like(native)
        yA, yB = max(0, py), min(h, py + crop.shape[0])
        xA, xB = max(0, px), min(wd, px + crop.shape[1])
        canvas[yA:yB, xA:xB] = crop[yA - py:yB - py, xA - px:xB - px]
        ca = canvas[:, :, 3:4] / 255.0
        if d8 in ("02_right", "06_left"):
            al_c, L_c = crop[:, :, 3], rt.lum(crop[:, :, :3])
            furc = np.median(L_c[al_c > 128]) if (al_c > 128).any() else 100
            prof = [((L_c[y, al_c[y] > 60] < furc - 13).mean()
                     if (al_c[y] > 60).sum() > 10 else 0) for y in range(crop.shape[0])]
            rows = [y for y, pv in enumerate(prof) if pv > 0.30]
            groups = []
            for y in rows:
                if groups and y - groups[-1][-1] <= 3:
                    groups[-1].append(y)
                else:
                    groups.append([y])
            bands = [int(np.mean(g)) for g in groups if len(g) >= 2]
            bands = [b for b in bands if ya - 4 <= py + b <= yb + 4]  # chest zone only
            roww = np.zeros((h, 1))
            for b in bands:
                c = py + b
                roww = np.maximum(roww, np.clip(1 - (np.abs(np.arange(h) - c) - 9) / 4, 0, 1)[:, None])
            W = ca * np.broadcast_to(roww[:, None, :], (h, wd, 1))
            out = native * (1 - W) + canvas * W
            out[:, :, 3] = np.maximum(native[:, :, 3], canvas[:, :, 3] * (W[:, :, 0] > 0.4))
        else:
            zone_w = (np.clip((yy - (ya - 8)) / 5, 0, 1) * np.clip(((yb + 8) - yy) / 5, 0, 1) *
                      np.clip((1.0 * w + 5 - np.abs(xx - cx)) / 5, 0, 1))[..., None]
            out = native.copy()
            al = native[:, :, 3]
            L = rt.lum(native[:, :, :3])
            fur = rt.fur_median(native, ya, yb, cx, w)
            rows = [y for y in range(ya, min(yb, int(ya + 0.55 * (yb - ya))) + 1)
                    if ((al[y] > 60) & (np.abs(np.arange(wd) - cx) < w * 0.8)).sum() >= 18
                    and (L[y, (al[y] > 60) & (np.abs(np.arange(wd) - cx) < w * 0.8)] < fur - 13).mean() > 0.33]
            near = np.zeros(h, bool)
            for b in rows:
                near[max(0, b - 10):b + 11] = True
            oldband = near[:, None] & (np.abs(xx - cx) < 0.85 * w) & (al > 60) & (L < fur - 13)
            for y in np.where(oldband.any(1))[0]:
                for x in np.where(oldband[y])[0]:
                    for dyy in (3, -3, 5, -5, 7, -7):
                        y2 = y + dyy
                        if 0 <= y2 < h and al[y2, x] > 60 and not oldband[y2, x]:
                            out[y, x, :3] = np.clip(native[y2, x, :3] + np.random.normal(0, 3, 3), 0, 255)
                            break
            out = out * (1 - ca * zone_w) + canvas * ca * zone_w
            out[:, :, 3] = np.maximum(out[:, :, 3],
                                      canvas[:, :, 3] * (ca[:, :, 0] > 0.4) * zone_w[:, :, 0])
        Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(D8, d8 + ".png"))
    print("8-dir set: native backs kept; LANCZOS harness on the other views "
          "(row-band confined on the sides)")


if __name__ == "__main__":
    apply_16dir()
    apply_idle()
    apply_8dir()
    ok = rb.verify_set(S16, rb.DIRS16, "16-dir")
    ok &= rb.verify_set(IDLE, [f"{d}_f{fr}" for fr in range(3) for d in rb.DIRS16], "idle",
                        blend_neighbours=False)
    ok &= rb.verify_set(D8, rb.DIRS8, "8-dir")
    print("ALL SETS:", "PASS" if ok else "FAIL")
