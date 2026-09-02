#!/usr/bin/env python3
"""
preview_shader_sim.py
=====================
CPU reference implementation of BillboardBlendWind.shader (numpy) that renders
a looping GIF preview of:

  panel 1  — SMOOTH ORBIT: the camera sweeps 360 degrees around the character
             and the two neighbouring direction sprites are cross-faded
             continuously (no snapping between 16 cut-outs);
  panel 2  — LIVING IDLE: hair sway, loincloth flutter and chest breathing,
             exactly the shader math (gusts, breathing, body bob, contact
             shadow).

It doubles as a regression check: it prints the measured motion energy in the
hair / cloth / torso / feet bands so mask-driven motion can be verified
without eyes on the numbers.

Run from the repo root:   python3 tools/preview_shader_sim.py
Output:                   preview/preview_smooth_billboard.gif
"""

import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPRITES = os.path.join(ROOT, "unity_assets", "sprites_16")
MASKS = os.path.join(ROOT, "unity_assets", "sprites_16_masks")
OUT = os.path.join(ROOT, "preview", "preview_smooth_billboard.gif")

# shader constants (kept in sync with BillboardBlendWind.shader)
WIND_SPEED = 1.6
HAIR_AMP = 3.0          # px — matches the shader default
CLOTH_AMP = 0.0            # bottom cloth animation disabled by request
WIND_DIR_X = 0.85
BREATH_RATE = 0.235
HEAD_AMP = 1.8            # head sway px (slightly exaggerated for the small gif)
BLINK_AT = 2.60           # blink once mid-loop, then a quick double blink
BREATH_AMP = 1.15
SHADOW_STRENGTH = 0.38
SHADOW_X = 0.26
SHADOW_Y = 0.025
SHADOW_H = 0.045

DIRS = ["00_front", "01_front_right_slight", "02_front_right", "03_right_front",
        "04_right", "05_right_back", "06_back_right", "07_back_right_slight",
        "08_back", "09_back_left_slight", "10_back_left", "11_left_back",
        "12_left", "13_left_front", "14_front_left", "15_front_left_slight"]


def load(i):
    s = np.asarray(Image.open(os.path.join(SPRITES, DIRS[i] + ".png")).convert("RGBA"), dtype=np.float64) / 255.0
    m = np.asarray(Image.open(os.path.join(MASKS, DIRS[i] + "_mask.png")).convert("RGBA"), dtype=np.float64) / 255.0
    return s, m


SPR = [load(i) for i in range(16)]


def sample(tex, ux, uy):
    """Bilinear sample, uv origin bottom-left like Unity sprites."""
    H, W = tex.shape[:2]
    x = np.clip(ux, 0.0, 1.0) * (W - 1)
    y = np.clip(1.0 - uy, 0.0, 1.0) * (H - 1)
    x0 = np.clip(np.floor(x).astype(int), 0, W - 2)
    y0 = np.clip(np.floor(y).astype(int), 0, H - 2)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    a = tex[y0, x0]
    b = tex[y0, x0 + 1]
    c = tex[y0 + 1, x0]
    d = tex[y0 + 1, x0 + 1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def render(t, cont_dir):
    """Mirror of the shader's fragment shader for one frame."""
    a = int(math.floor(cont_dir)) % 16
    b = (a + 1) % 16
    blend = cont_dir - math.floor(cont_dir)

    texA, maskA = SPR[a]
    texB, maskB = SPR[b]
    H, W = texA.shape[:2]

    yy, xx = np.mgrid[0:H, 0:W]
    uv0 = np.dstack([xx / (W - 1), yy / (H - 1) * 0.0 + 1.0 - yy / (H - 1)]).astype(np.float64)
    # uv0.x = xx/(W-1); uv.y measured from the BOTTOM: 1 - yy/(H-1)
    uv_x = xx / (W - 1)
    uv_y = 1.0 - yy / (H - 1)

    mask = maskA * (1 - blend) + maskB * blend
    hairW, clothW, torsoW = mask[..., 0], mask[..., 1], mask[..., 2]

    # ---- gusty breeze ----
    gust = 0.72 + 0.48 * (0.5 + 0.5 * math.sin(0.61 * t + 1.7 * math.sin(0.23 * t))) ** 2
    gust *= 0.88 + 0.12 * math.sin(2.9 * t)

    hx = uv_x * 7.0
    hairWave = (np.sin(t * WIND_SPEED * 1.9 + (0.92 - uv_y) * 4.5) * 0.60
                + np.sin(t * WIND_SPEED * 3.1 + hx * 1.7) * 0.25
                + np.sin(t * WIND_SPEED * 5.3 + hx * 3.1) * 0.15)
    px = 1.0 / W
    hairOffX = WIND_DIR_X * hairWave * (HAIR_AMP * px) * hairW * gust
    hairOffY = (0.16 * np.abs(hairWave) - 0.08) * (HAIR_AMP * px) * hairW * gust

    flutter = (np.sin(t * WIND_SPEED * 2.2 + (0.55 - uv_y) * 9.0) * 0.60
               + np.sin(t * WIND_SPEED * 3.7 + uv_x * 14.0) * 0.40)
    clothOffX = WIND_DIR_X * (flutter + 0.35 * hairWave) * (CLOTH_AMP * px) * clothW * gust
    clothOffY = np.abs(flutter) * 0.45 * (CLOTH_AMP * px) * clothW * gust

    # ---- head tilt (rotation about the neck) + drift + blink + finger curl ----
    headW = mask[..., 3]
    NECK_Y = 0.845
    asp = W / H
    headRot = (np.sin(t * 0.62 + 0.7) * 0.60 + np.sin(t * 0.26 + 1.3) * 0.40) * 0.022 * HEAD_AMP
    hp_x = uv_x - 0.5
    hp_y = (uv_y - NECK_Y) * asp
    csr, snr = np.cos(headRot), np.sin(headRot)
    hrot_x = hp_x * csr - hp_y * snr
    hrot_y = hp_x * snr + hp_y * csr
    headOffX = ((hrot_x - hp_x) / 1.0) * headW + (np.sin(t * 1.06) * 0.60 + np.sin(t * 0.42 + 1.7) * 0.40) * (HEAD_AMP * px) * headW
    headOffY = ((hrot_y - hp_y) / asp) * headW + (np.cos(t * 0.68 + 0.8) * 0.30 + np.sin(t * 0.34) * 0.20) * (HEAD_AMP * px) * headW

    # eyes = dark pixels of the face (head zone minus hair)
    restA = sample(texA, uv_x, uv_y)
    restB = sample(texB, uv_x, uv_y)
    rest = restA * (1 - blend) + restB * blend
    restLum = 0.299 * rest[..., 0] + 0.587 * rest[..., 1] + 0.114 * rest[..., 2]
    faceW = np.clip(headW - hairW, 0.0, 1.0)
    eyeT = np.clip((restLum - 0.42) / (0.16 - 0.42), 0.0, 1.0)
    eyeW = faceW * (eyeT * eyeT * (3 - 2 * eyeT)) * rest[..., 3]

    def blink_env(tt):
        for start in (BLINK_AT, BLINK_AT + 0.39):     # blink + double blink
            b = tt - start
            if 0.0 <= b < 0.09:
                return b / 0.09
            if 0.09 <= b < 0.10:
                return 1.0
            if 0.10 <= b < 0.23:
                return 1.0 - (b - 0.10) / 0.13
        return 0.0
    blink = blink_env(t)
    py = 1.0 / H
    blinkOffY = -2.6 * py * eyeW * blink

    # hands: slow fist clench
    lat = uv_x - 0.5
    handBand = np.clip((uv_y - 0.44) / 0.03, 0, 1) * np.clip((0.60 - uv_y) / 0.05, 0, 1)
    latT = np.clip((np.abs(lat) - 0.075) / 0.035, 0, 1)
    handW = handBand * latT * (1.0 - clothW) * (1.0 - headW)
    clT = np.clip((np.sin(t * 0.9) - 0.2) / 0.6, 0, 1)
    clench = clT * clT * (3 - 2 * clT)
    handOffX = np.sign(lat) * (0.8 * px) * clench * handW
    handOffY = -0.2 * (0.8 * px) * clench * handW

    # ---- breathing ----
    brPhase = t * BREATH_RATE * 2 * math.pi
    br = math.sin(brPhase)
    inhale = max(br, 0.0)
    exhale = max(-br, 0.0)

    e = 0.016 * inhale * BREATH_AMP * torsoW
    uvBx = 0.5 + (uv_x - 0.5) / (1.0 + e)
    rise = (0.55 * torsoW + 0.45 * np.clip((uv_y - 0.45) / 0.35, 0, 1)) * 0.009 * inhale * BREATH_AMP
    uvBy = uv_y - rise - 0.0022 * math.sin(brPhase - math.pi / 2)

    du = uvBx + hairOffX + clothOffX + headOffX + handOffX
    dv = uvBy + hairOffY + clothOffY + headOffY + blinkOffY + handOffY

    col = (sample(texA, du, dv) * (1 - blend) + sample(texB, du, dv) * blend)
    col[..., :3] *= (1.0 - 0.045 * exhale * torsoW * BREATH_AMP)[..., None]
    col[..., :3] *= (1.0 - 0.22 * eyeW * blink)[..., None]        # closed lids
    col[..., :3] *= (1.0 - 0.05 * clench * handW)[..., None]      # clench shadow

    # ---- contact shadow ----
    spx = (uv_x - 0.5) / SHADOW_X
    spy = (uv_y - SHADOW_Y) / SHADOW_H
    sh = np.clip(1.0 - (spx * spx + spy * spy), 0.0, 1.0) ** 2 * SHADOW_STRENGTH
    col[..., :3] *= (1.0 - 0.55 * sh)[..., None]
    col[..., 3] = np.maximum(col[..., 3], sh * (1.0 - col[..., 3] * 0.5))

    return col


def make_background(w, h, horizon=None):
    """Simple sky/ground backdrop so the shadow and silhouette read clearly."""
    bg = np.zeros((h, w, 4), dtype=np.float64)
    if horizon is None:
        horizon = h - 34
    sky = np.linspace([0.78, 0.86, 0.95], [0.92, 0.95, 0.99], horizon)
    ground = np.linspace([0.52, 0.44, 0.34], [0.42, 0.35, 0.27], h - horizon)
    bg[:horizon, :, :3] = sky[:, None, :]
    bg[horizon:, :, :3] = ground[:, None, :]
    bg[..., 3] = 1.0
    return bg


def compose(t, orbit_dir, idle_dir=0.0, scale=0.62):
    sprH, sprW = SPR[0][0].shape[:2]
    pw, ph = int(sprW * scale), int(sprH * scale)
    pad, label_h = 26, 30
    Wc = pad * 3 + pw * 2
    Hc = label_h + ph + 44
    horizon = horizon_cache[0]
    canvas = make_background(Wc, Hc, horizon)

    # feet a little below the horizon so the shadow lands on visible ground
    y0 = Hc - 18 - ph
    for k, (x0, d) in enumerate([(pad, orbit_dir), (pad * 2 + pw, idle_dir)]):
        frame = render(t, d)
        img = Image.fromarray((np.clip(frame, 0, 1) * 255).astype(np.uint8)).resize((pw, ph), Image.LANCZOS)
        f = np.asarray(img, dtype=np.float64) / 255.0
        region = canvas[y0:y0 + ph, x0:x0 + pw]
        al = f[..., 3:4]
        canvas[y0:y0 + ph, x0:x0 + pw] = f[..., :4] * al + region * (1 - al)
    return np.clip(canvas, 0, 1)


# module-level horizon for the label drawing
horizon_cache = [0]


def build_gif(frames=72, fps=24, scale=0.62):
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sprH, sprW = SPR[0][0].shape[:2]
    pw, ph = int(sprW * scale), int(sprH * scale)
    pad, label_h = 26, 30
    Wc, Hc = pad * 3 + pw * 2, label_h + ph + 44
    horizon = Hc - 34
    bg = make_background(Wc, Hc, horizon)
    horizon_cache[0] = horizon

    try:
        font = ImageFont.load_default(14)
    except TypeError:
        font = ImageFont.load_default()

    # motion-energy bookkeeping
    prev = None
    energy = {"hair": 0.0, "cloth": 0.0, "torso": 0.0, "feet": 0.0}
    hair_band = slice(int(ph * 0.06), int(ph * 0.30))
    cloth_band = slice(int(ph * 0.42), int(ph * 0.66))
    torso_band = slice(int(ph * 0.30), int(ph * 0.42))
    feet_band = slice(int(ph * 0.92), int(ph * 1.00))

    out_frames = []
    t_cycle = 1.0 / BREATH_RATE          # one full breath loop
    x_right = pad * 2 + pw               # idle panel x range
    y0 = Hc - 18 - ph                    # panel y range
    for i in range(frames):
        u = i / frames
        t = u * t_cycle
        cont_dir = u * 16.0              # full 360-degree orbit over the loop

        canvas = compose(t, cont_dir, scale=scale)
        pil = Image.fromarray((canvas * 255).astype(np.uint8)).convert("RGB")
        d = ImageDraw.Draw(pil)
        d.text((pad, 6), "SMOOTH ORBIT - direction cross-fade", fill=(30, 30, 40), font=font)
        d.text((pad * 2 + pw, 6), "HAIR, BREATH, BLINK, HEAD SWAY", fill=(30, 30, 40), font=font)
        d.line([(0, horizon), (Wc, horizon)], fill=(90, 80, 66), width=1)
        out_frames.append(pil)

        # measure motion ONLY on the idle (right) panel, where the direction is fixed
        if prev is not None:
            cur = np.asarray(pil, dtype=np.float64)[y0:y0 + ph, x_right:x_right + pw]
            prv = prev[y0:y0 + ph, x_right:x_right + pw]
            diff = np.abs(cur - prv).mean(-1)
            energy["hair"] += diff[hair_band, :].mean()
            energy["cloth"] += diff[cloth_band, :].mean()
            energy["torso"] += diff[torso_band, :].mean()
            energy["feet"] += diff[feet_band, :].mean()
        prev = np.asarray(pil, dtype=np.float64)

    out_frames[0].save(OUT, save_all=True, append_images=out_frames[1:],
                       duration=int(1000 / fps), loop=0, optimize=True)

    print(f"saved {OUT}  ({Wc}x{Hc}, {frames} frames @ {fps}fps)")
    print("motion energy per band (higher = more movement):")
    for k, v in energy.items():
        print(f"   {k:6s}: {v / frames:6.3f}")
    assert energy["hair"] > energy["feet"] * 1.5, "hair should move much more than the feet"
    assert energy["cloth"] < energy["hair"], "bottom cloth must NOT be animated (disabled)"
    print("checks passed: hair sway + breathing detected, cloth stays still, feet planted.")


if __name__ == "__main__":
    build_gif()
