#!/usr/bin/env python3
"""
generate_sway_masks.py
======================
Generates per-direction SWAY MASK textures for the caveman billboard character.

Each mask is an RGBA PNG that matches its source sprite 1:1 (same size, same
alignment). Channels:

    R  = HAIR   weight
    G  = CLOTH  weight  (how much this pixel flutters  - loincloth / fur hem)
    B  = TORSO  weight  (how much this pixel participates in breathing)
    A  = HEAD   weight  (whole head: hair + face - used for head sway + blinks)

The Unity shader (BillboardBlendWind.shader) samples the two masks of the two
direction sprites it is cross-fading and displaces pixels accordingly, so the
character visibly breathes and his hair / loincloth sway in the wind.

Segmentation is driven by simple, robust rules that hold for this art set:
  * hair   = very dark pixels in the head region (top ~30% of the body)
  * cloth  = dark / mid "fur" pixels in the hip band (~34%..63% of body height)
  * torso  = light "skin" pixels in the chest band (~12%..42% of body height)

Run from the repository root:

    python3 tools/generate_sway_masks.py

Outputs:
    unity_assets/sprites_16_masks/<name>_mask.png        (16-dir set, recommended)
    unity_assets/sprites_masks/<name>_mask.png           (8-dir legacy set)
    unity_assets/sprites_16_masks/_preview_masks.png     (contact sheet)
"""

import os
import glob
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# ---------------------------------------------------------------------------
# Tunables (0..1 fractions of body height, measured from the top of the body)
# ---------------------------------------------------------------------------
HAIR_BAND      = (0.00, 0.30)   # head region
HAIR_MAX_LUM   = 88.0           # very dark = hair
HAIR_ROOT_W    = 0.35           # scalp moves less ...
HAIR_TIP_W     = 1.00           # ... tips move more

CLOTH_BAND     = (0.34, 0.63)   # hip / thigh region
CLOTH_MAX_LUM  = 128.0          # dark-to-mid = fur / hide
CLOTH_TOP_W    = 0.15           # waistband barely moves ...
CLOTH_HEM_W    = 1.00           # ... the hem flutters

HEAD_BAND      = (0.00, 0.175)  # whole head (hair + face), feathered at the neck
TORSO_BAND     = (0.12, 0.42)   # chest region
TORSO_MIN_LUM  = 95.0           # light = bare skin (backs are shadowed, keep low)
TORSO_CORE_W   = 1.00           # centre of the chest
TORSO_EDGE_W   = 0.40           # outer arms still rise a little with breath

BLUR_RADIUS    = 2.5            # soft mask edges -> no displacement seams


def lum(rgb: np.ndarray) -> np.ndarray:
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def band_weight(y: np.ndarray, top: float, bottom: float,
                w_top: float, w_bottom: float) -> np.ndarray:
    """Linear ramp from w_top at band start to w_bottom at band end (0 outside)."""
    t = np.clip((y - top) / max(bottom - top, 1e-6), 0.0, 1.0)
    return w_top + (w_bottom - w_top) * t


def make_mask(path: str) -> np.ndarray:
    a = np.asarray(Image.open(path).convert("RGBA"), dtype=np.float64)
    rgb, alpha = a[..., :3], a[..., 3]
    H, W = alpha.shape

    body = alpha > 60.0
    ys, xs = np.where(body)
    if len(ys) == 0:
        return np.zeros((H, W, 4), dtype=np.float64)
    y_top, y_bot = ys.min(), ys.max()
    body_h = max(y_bot - y_top, 1)

    yy, xx = np.mgrid[0:H, 0:W]
    rel_y = (yy - y_top) / body_h                      # 0 at head, 1 at feet
    L = lum(rgb)

    # ---- hair ----------------------------------------------------------
    hair = body & (L < HAIR_MAX_LUM) & (rel_y >= HAIR_BAND[0]) & (rel_y < HAIR_BAND[1])
    hair = hair.astype(np.float64) * band_weight(rel_y, HAIR_BAND[0], HAIR_BAND[1],
                                                 HAIR_ROOT_W, HAIR_TIP_W)
    # side strands (further from the head's centre-x) sway a little more
    hy, hx = np.where(hair > 0.05)
    if len(hy):
        cx = hx.mean()
        span = max(np.abs(hx - cx).max(), 1.0)
        hair = np.clip(hair * (0.75 + 0.4 * np.clip(np.abs(xx - cx) / span, 0.0, 1.0)),
                       0.0, 1.0)

    # ---- cloth (loincloth / fur hem) ------------------------------------
    cloth = body & (L < CLOTH_MAX_LUM) & (rel_y >= CLOTH_BAND[0]) & (rel_y <= CLOTH_BAND[1])
    cloth = cloth.astype(np.float64) * band_weight(rel_y, CLOTH_BAND[0], CLOTH_BAND[1],
                                                   CLOTH_TOP_W, CLOTH_HEM_W)

    # ---- head (sway + blink region) ---------------------------------------
    head = body & (rel_y >= HEAD_BAND[0]) & (rel_y < HEAD_BAND[1])
    head = head.astype(np.float64)
    # feather the bottom 25% of the band so the chin/neck seam stays soft
    feather = np.clip((HEAD_BAND[1] - rel_y) / (0.25 * (HEAD_BAND[1] - HEAD_BAND[0])), 0.0, 1.0)
    head *= feather

    # ---- torso (breathing) ----------------------------------------------
    torso = body & (L >= TORSO_MIN_LUM) & (rel_y >= TORSO_BAND[0]) & (rel_y <= TORSO_BAND[1])
    torso = torso.astype(np.float64)
    # emphasise the centre columns (chest) over the outer arms
    if torso.sum() > 0:
        ty, tx = np.where(torso > 0)
        x15, x85 = np.percentile(tx, 15), np.percentile(tx, 85)
        centre = 1.0 - np.clip(np.abs(xx - (x15 + x85) * 0.5) /
                               max((x85 - x15) * 0.5 + 1, 1.0), 0.0, 1.0)
        torso *= TORSO_EDGE_W + (TORSO_CORE_W - TORSO_EDGE_W) * centre

    # ---- soften & normalise ---------------------------------------------
    def soften(m: np.ndarray) -> np.ndarray:
        img = Image.fromarray(np.clip(m * 255.0, 0, 255).astype(np.uint8))
        img = img.filter(ImageFilter.GaussianBlur(BLUR_RADIUS))
        out = np.asarray(img, dtype=np.float64) / 255.0
        return np.clip(out, 0.0, 1.0)

    hair, cloth, torso = soften(hair), soften(cloth), soften(torso)

    out = np.zeros((H, W, 4), dtype=np.float64)
    out[..., 0] = hair
    out[..., 1] = cloth
    out[..., 2] = torso
    out[..., 3] = soften(head)
    return out


def process_set(src_dir: str, dst_dir: str, skip_subdirs: bool = True) -> list:
    os.makedirs(dst_dir, exist_ok=True)
    results = []
    for f in sorted(glob.glob(os.path.join(src_dir, "*.png"))):
        name = os.path.basename(f)
        if name.startswith("_") or "preview" in name or "contact" in name:
            continue
        mask = make_mask(f)
        stem = os.path.splitext(name)[0]
        out_path = os.path.join(dst_dir, stem + "_mask.png")
        Image.fromarray(np.clip(mask * 255.0, 0, 255).astype(np.uint8)).save(out_path)
        results.append((name, out_path, int(mask[..., 0].sum()),
                        int(mask[..., 1].sum()), int(mask[..., 2].sum())))
    return results


def contact_sheet(src_dir: str, mask_dir: str, out_path: str, cols: int = 8):
    """Sprite | mask-RGB side by side so the segmentation can be eyeballed."""
    names = [os.path.basename(p) for p in sorted(glob.glob(os.path.join(src_dir, "*.png")))
             if not os.path.basename(p).startswith("_")
             and "preview" not in os.path.basename(p)
             and "contact" not in os.path.basename(p)]
    if not names:
        return
    spr = [Image.open(os.path.join(src_dir, n)).convert("RGBA") for n in names]
    msk = [Image.open(os.path.join(mask_dir, os.path.splitext(n)[0] + "_mask.png"))
           .convert("RGBA") for n in names]
    w = min(im.width for im in spr)
    h = min(im.height for im in spr)
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * (w * 2 + 6) + 6, rows * (h + 6) + 6), (25, 25, 30, 255))
    for i, (s, m) in enumerate(zip(spr, msk)):
        r, c = divmod(i, cols)
        x = 6 + c * (w * 2 + 6)
        y = 6 + r * (h + 6)
        sheet.alpha_composite(s.resize((w, h)), (x, y))
        sheet.alpha_composite(m.resize((w, h)), (x + w, y))
    sheet.save(out_path)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))

    # 16-direction set (recommended)
    src16 = os.path.join(ROOT, "unity_assets", "sprites_16")
    dst16 = os.path.join(ROOT, "unity_assets", "sprites_16_masks")
    res = process_set(src16, dst16)
    for name, path, nh, nc, nt in res:
        print(f"  {name:28s} -> {os.path.relpath(path, ROOT):52s} "
              f"hair~{nh:6d}px cloth~{nc:6d}px torso~{nt:6d}px")
    contact_sheet(src16, dst16, os.path.join(dst16, "_preview_masks.png"))
    print(f"  contact sheet -> {os.path.relpath(os.path.join(dst16, '_preview_masks.png'), ROOT)}")

    # 8-direction legacy set
    src8 = os.path.join(ROOT, "unity_assets", "sprites")
    dst8 = os.path.join(ROOT, "unity_assets", "sprites_masks")
    res = process_set(src8, dst8)
    for name, path, nh, nc, nt in res:
        print(f"  {name:28s} -> {os.path.relpath(path, ROOT):52s} "
              f"hair~{nh:6d}px cloth~{nc:6d}px torso~{nt:6d}px")

    print("done.")
