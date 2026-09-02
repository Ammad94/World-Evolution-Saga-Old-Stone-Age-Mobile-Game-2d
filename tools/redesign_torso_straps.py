#!/usr/bin/env python3
"""
redesign_torso_straps.py  (cylinder-model edition)
==================================================
Makes the character's EXISTING dark-brown chest/back belt consistent — the
SAME belt, cleanly drawn, on the front views only (back/side views untouched).

MODEL
-----
The belt is two straps wrapped around the torso (a cylinder). For a camera at
view angle v, the strap seen at screen column x sits at

        row(x) = y0 +/- k * sin(v + delta),   delta = asin((x - cx) / r)

which reproduces exactly what the original art shows on the back and sides:
an X crossing when the straps face the camera, two separated bands at the
sides converging at the silhouette edges. Parameters (k, y0, strap width,
leather colours) are MEASURED from the cleanest back view of the same art,
so the redrawn front belt is the original belt - same leather, same tilt.

FRONT VIEWS: old inconsistent bands are erased (region-growing + inpaint),
then the two straps are drawn with the model, and any bright pixel outside
the drawn straps is cleaned. Back/side views are never modified.

    python3 tools/redesign_torso_straps.py            # apply + verify
    python3 tools/redesign_torso_straps.py --check-only
"""

import os
import sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

DIRS16 = ["00_front", "01_front_right_slight", "02_front_right", "03_right_front",
          "04_right", "05_right_back", "06_back_right", "07_back_right_slight",
          "08_back", "09_back_left_slight", "10_back_left", "11_left_back",
          "12_left", "13_left_front", "14_front_left", "15_front_left_slight"]

DIRS8 = ["00_front", "01_front_right", "02_right", "03_back_right",
         "04_back", "05_back_left", "06_left", "07_front_left"]

FRONT16 = [0, 1, 2, 3, 13, 14, 15]
FRONT8 = [0, 1, 7]
CLEAN_DONOR = {16: 8, 8: 4}          # the cleanest back view per set

ZONE_TOP = 0.295
ZONE_BOT = 0.445


def lum(rgb):
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def body_stats(rgba):
    al = rgba[:, :, 3]
    ys, xs = np.where(al > 60)
    return ys.min(), ys.max(), xs.min(), xs.max()


def torso_runs(row_body):
    runs, x, n = [], 0, len(row_body)
    while x < n:
        if row_body[x]:
            s = x
            while x < n and row_body[x]:
                x += 1
            runs.append((s, x - 1))
        else:
            x += 1
    return runs


def torso_profile(rgba, ya, yb):
    al = rgba[:, :, 3] > 60
    centers, widths = [], []
    for y in range(ya, yb):
        runs = torso_runs(al[y])
        if not runs:
            continue
        s, e = max(runs, key=lambda r: r[1] - r[0])
        if (e - s) < 8:
            continue
        centers.append((s + e) * 0.5)
        widths.append(e - s)
    if not centers:
        return None
    return float(np.median(centers)), float(np.median(widths))


def zone_of(rgba):
    yt, yb_, _, _ = body_stats(rgba)
    bh = yb_ - yt
    ya, yb = int(yt + ZONE_TOP * bh), int(yt + ZONE_BOT * bh)
    prof = torso_profile(rgba, ya, yb)
    return (ya, yb), prof, yt, bh


def fur_median(rgba, ya, yb, cx, w_med):
    al = rgba[:, :, 3]
    L = lum(rgba[:, :, :3])
    m = (al > 60) & (np.abs(np.arange(L.shape[1])[None, :] - cx) < w_med * 0.75)
    m &= (np.arange(L.shape[0])[:, None] >= ya) & (np.arange(L.shape[0])[:, None] <= yb)
    return float(np.median(L[m])) if m.sum() > 50 else 120.0


def strap_lum(rgba, ya, yb, cx, w_med):
    al = rgba[:, :, 3]
    L = lum(rgba[:, :, :3])
    fur_med = fur_median(rgba, ya, yb, cx, w_med)
    m = (L > fur_med + 15.0) & (al > 60)
    m &= np.abs(np.arange(L.shape[1])[None, :] - cx) < w_med * 0.8
    m &= (np.arange(L.shape[0])[:, None] >= ya) & (np.arange(L.shape[0])[:, None] <= yb)
    return float(np.median(L[m])) if m.sum() > 80 else None


def _dilate(m):
    d = m.copy()
    d[1:, :] |= m[:-1, :]
    d[:-1, :] |= m[1:, :]
    d[:, 1:] |= m[:, :-1]
    d[:, :-1] |= m[:, 1:]
    return d


def grown_belt_mask(rgba, ya, yb, cx, w_med, strict=20.0, loose=6.0):
    """Every belt pixel: strict seeds region-grown with a looser tolerance."""
    al = rgba[:, :, 3]
    L = lum(rgba[:, :, :3])
    H, W = L.shape
    fur = fur_median(rgba, ya, yb, cx, w_med)
    zone = np.zeros((H, W), bool)
    zone[ya + 2:yb - 3, :] = True
    centre = np.abs(np.arange(W)[None, :] - cx) < w_med * 1.05
    body = al > 60
    mask = zone & body & centre & (L > fur + strict)
    while True:
        cand = _dilate(mask) & zone & body & centre & (L > fur + loose)
        new = cand & ~mask
        if not new.any():
            break
        mask |= new
    return mask, fur


def inpaint(rgba, mask, rng):
    rgb = rgba[:, :, :3]
    body = rgba[:, :, 3] > 60
    H, W = rgb.shape[:2]
    fur_ref = np.median(rgb[body & ~mask], axis=0) if (body & ~mask).sum() > 20 \
        else np.array([95.0, 70.0, 50.0])
    ys, xs = np.where(mask)
    for y, x in zip(ys, xs):
        xl = x - 1
        while xl >= 0 and mask[y, xl]:
            xl -= 1
        xr = x + 1
        while xr < W and mask[y, xr]:
            xr += 1
        cl = rgb[y, xl] if xl >= 0 and not mask[y, xl] else None
        cr = rgb[y, xr] if xr < W and not mask[y, xr] else None
        if cl is not None and cr is not None:
            t = (x - xl) / max(xr - xl, 1)
            base = cl * (1 - t) + cr * t
        elif cl is not None:
            base = cl
        elif cr is not None:
            base = cr
        else:
            base = None
            for dy in (1, -1, 2, -2, 3, -3):
                yy = y + dy
                if 0 <= yy < H and not mask[yy, x]:
                    base = rgb[yy, x]
                    break
            if base is None:
                base = fur_ref
        rgba[y, x, :3] = np.clip(base + rng.normal(0, 4.0, 3), 0, 255)


def donor_soft_mask(rgba, ya, yb, cx, w_med):
    al = rgba[:, :, 3]
    L = lum(rgba[:, :, :3])
    fur = fur_median(rgba, ya, yb, cx, w_med)
    m = (al > 60) & (np.abs(np.arange(L.shape[1])[None, :] - cx) < w_med * 0.85)
    m &= (np.arange(L.shape[0])[:, None] >= ya) & (np.arange(L.shape[0])[:, None] <= yb)
    soft = np.clip((L - (fur + 10.0)) / 12.0, 0.0, 1.0)
    return soft * m, fur


def measure_belt(donor_rgba):
    """Measure (k, y0rel, halfA, halfB, leatherA, leatherB, tone) from the
    clean back view. k and y0 are stored relative to body metrics."""
    (dya, dyb), prof, dyt, dbh = zone_of(donor_rgba)
    dcx, dw = prof
    soft, fur = donor_soft_mask(donor_rgba, dya, dyb, dcx, dw)

    # per-column strap runs
    x0, x1 = int(dcx - dw * 0.72), int(dcx + dw * 0.72)
    cols = {}
    for x in range(x0, x1 + 1):
        col = np.where(soft[dya:dyb + 1, x] > 0.35)[0]
        if len(col) == 0:
            continue
        col = col + dya
        runs = np.split(col, np.where(np.diff(col) > 2)[0] + 1)
        runs = [r for r in runs if len(r) >= 4]
        if runs:
            cols[x] = [(float((r[0] + r[-1]) / 2), float(len(r) / 2)) for r in runs]

    # strap A = upper-left + lower-right, strap B = lower-left + upper-right
    pts = {0: ([], [], []), 1: ([], [], [])}
    for x, runs in sorted(cols.items()):
        if len(runs) < 2:
            continue
        rs = sorted(runs, key=lambda r: r[0])
        k = 0 if x < dcx else 1
        pts[k][0].append(x);        pts[k][1].append(rs[0][0]); pts[k][2].append(rs[0][1])
        pts[1 - k][0].append(x);    pts[1 - k][1].append(rs[1][0]); pts[1 - k][2].append(rs[1][1])

    fits = []
    for k in (0, 1):
        xs, ys, hs = pts[k]
        m, b = np.polyfit(xs, ys, 1)
        fits.append((float(m), float(b), float(np.median(hs))))
    (mA, bA, hA), (mB, bB, hB) = fits
    if mA * mB >= 0:
        raise ValueError("clean donor straps do not cross (fit failed)")

    r_d = dw / 2.0
    k_px = 0.5 * (abs(mA) + abs(mB)) * r_d          # vertical spread of the X
    crossX = (bB - bA) / (mA - mB)
    ycross = bA + mA * crossX

    # leather colours along each strap
    def leather(m, b, half):
        px = []
        for x, runs in cols.items():
            y = int(round(m * x + b))
            for dy in range(-int(half), int(half) + 1):
                yy = y + dy
                if 0 <= yy < donor_rgba.shape[0] and soft[yy, x] > 0.4:
                    px.append(donor_rgba[yy, x, :3])
        return np.median(np.array(px), axis=0) if px else np.array([185.0, 145.0, 112.0])

    # full strap thickness incl. shading (soft core underestimates it):
    # at each column, grow the run around the fitted line with a lower threshold
    def full_half(m, b):
        hs = []
        for x, runs in cols.items():
            y = int(round(m * x + b))
            col = np.where(soft[dya:dyb + 1, x] > 0.18)[0] + dya
            if len(col) == 0:
                continue
            grp = col[np.abs(col - y) <= 14]
            if len(grp) >= 4:
                hs.append(len(grp) / 2)
        return float(np.median(hs)) if hs else 4.0

    hA = max(full_half(mA, bA), hA)
    hB = max(full_half(mB, bB), hB)

    leatherA = leather(mA, bA, hA)
    leatherB = leather(mB, bB, hB)
    tone = strap_lum(donor_rgba, dya, dyb, dcx, dw) or 130.0
    return dict(k_rel=k_px / dbh, y0_rel=(ycross - dyt) / dbh, r_rel=dw / dbh / 2.0,
                halfA=hA, halfB=hB, leatherA=leatherA, leatherB=leatherB, tone=tone)


def strap_rows(model, tcx, r_t, y0, k_t, view_deg, xt):
    """Rows of the two straps at target column xt: y0 +/- k*sin(v+delta)."""
    delta = np.arcsin(np.clip((xt - tcx) / max(r_t, 1.0), -1.0, 1.0))
    s = np.sin(np.deg2rad(view_deg) + delta)
    return y0 + k_t * s, y0 - k_t * s, s


def process_front(dir_path, names, front_idx, angles, label, draw=True):
    print(f"--- {label}: cylinder-model belt redraw on front views")
    n = len(names)
    donor = np.asarray(Image.open(os.path.join(dir_path, names[CLEAN_DONOR[n]] + ".png"))
                       .convert("RGBA")).astype(float)
    model = measure_belt(donor)
    print(f"    donor {names[CLEAN_DONOR[n]]}: k={model['k_rel']:.4f} bodyH, "
          f"y0={model['y0_rel']:.3f}, halves {model['halfA']:.1f}/{model['halfB']:.1f}px")

    for i in front_idx:
        name = names[i]
        view = angles(i)
        target = np.asarray(Image.open(os.path.join(dir_path, name + ".png")).convert("RGBA")).astype(float)
        (tya, tyb), tprof, tyt, tbh = zone_of(target)
        if tprof is None:
            print(f"    !! {name}: no torso, skipped")
            continue
        tcx, tw = tprof
        rng = np.random.default_rng(abs(hash(name)) % (2 ** 32))

        t_tone = strap_lum(target, tya, tyb, tcx, tw) or model["tone"]
        belt_mask, _ = grown_belt_mask(target, tya, tyb, tcx, tw)
        erased = int(belt_mask.sum())
        inpaint(target, belt_mask, rng)

        # model params in target space
        k_t = model["k_rel"] * tbh
        y0 = tyt + model["y0_rel"] * tbh
        r_t = tw * 0.5
        scale = np.clip(t_tone / max(model["tone"], 1.0), 0.80, 1.40)
        al = target[:, :, 3]
        H, W = al.shape
        painted = 0

        body_cols = [x for x in range(W)
                     if abs((x - tcx) / max(r_t, 1.0)) <= 0.985 and (al[tya:tyb, x] > 60).sum() > 8]
        allowed = np.zeros((H, W), bool)
        for xt in body_cols:
            rowA, rowB, s = strap_rows(model, tcx, r_t, y0, k_t, view, xt)
            for (row, half, leather) in ((rowA, model["halfA"], model["leatherA"]),
                                         (rowB, model["halfB"], model["leatherB"])):
                lo, hi = int(round(row - half - 4)), int(round(row + half + 4))
                if 0 <= lo < H and 0 <= hi < H:
                    allowed[lo:hi + 1, xt] = True
                for dy in np.arange(-half - 0.5, half + 0.51, 1.0):
                    yti = int(round(row + dy))
                    if not (0 <= yti < H) or al[yti, xt] <= 60:
                        continue
                    a = np.clip((half + 0.5 - abs(dy)) / 1.6, 0.0, 1.0)
                    # donor colour at the same 3D phase
                    xd = int(round(tcx - np.sin(np.deg2rad(180.0) + np.deg2rad(view) +
                                                np.arcsin(np.clip((xt - tcx) / max(r_t, 1.0), -1, 1)))
                                   * (model["r_rel"] * 2 * tbh) / 2 * 2 / 2))  # phase-mirrored column
                    xd = int(round(tcx - (xt - tcx)))          # simple mirror is accurate enough
                    yd = int(round(y0 + (row - y0)))           # same row height on the donor art
                    if 0 <= yd < donor.shape[0] and 0 <= xd < donor.shape[1]:
                        src = donor[yd, xd, :3]
                        if lum(src[None, :])[0] < model["tone"] - 20:
                            src = leather
                    else:
                        src = leather
                    col = np.clip(src * scale, 0, 255) + rng.normal(0, 2.5, 3)
                    a3 = a * 0.96
                    target[yti, xt, :3] = np.clip(target[yti, xt, :3] * (1 - a3) + col * a3, 0, 255)
                    painted += 1
            # dark leather seam where the straps overlap
            if abs(rowA - rowB) < (model["halfA"] + model["halfB"]) * 1.4:
                ym = int(round((rowA + rowB) / 2))
                for dy in range(-1, 2):
                    yy = ym + dy
                    if 0 <= yy < H and al[yy, xt] > 60:
                        target[yy, xt, :3] *= 0.90

        # clean every bright pixel that is not one of the drawn straps
        L = lum(target[:, :, :3])
        fur = fur_median(target, tya, tyb, tcx, tw)
        centre = np.abs(np.arange(W)[None, :] - tcx) < tw * 0.95
        zone = np.zeros((H, W), bool)
        zone[tya + 1:tyb, :] = True
        stray = (L > fur + 10.0) & (al > 60) & centre & zone & ~allowed
        strays = int(stray.sum())
        if strays:
            inpaint(target, stray, rng)

        if draw:
            Image.fromarray(target.astype(np.uint8)).save(os.path.join(dir_path, name + ".png"))
        print(f"    {name:26s} erased {erased:5d}px remnants, drew {painted:5d}px straps, "
              f"cleaned {strays:4d}px strays")


def verify_set(dir_path, names, front_idx, angles, label):
    """Front views: straps must appear at the MODEL-predicted rows, cleanly."""
    print(f"--- verify {label}")
    n = len(names)
    donor = np.asarray(Image.open(os.path.join(dir_path, names[CLEAN_DONOR[n]] + ".png"))
                       .convert("RGBA")).astype(float)
    model = measure_belt(donor)
    max_w = 22 if n == 16 else 42
    ok = True
    for i, name in enumerate(names):
        rgba = np.asarray(Image.open(os.path.join(dir_path, name + ".png")).convert("RGBA")).astype(float)
        (ya, yb), prof, yt, bh = zone_of(rgba)
        if prof is None:
            continue
        cx, w_med = prof
        al = rgba[:, :, 3]
        L = lum(rgba[:, :, :3])
        fur = fur_median(rgba, ya, yb, cx, w_med)
        centre = np.abs(np.arange(L.shape[1])[None, :] - cx) < w_med * 0.85
        zone = np.zeros(L.shape, bool)
        zone[ya:yb + 1] = True
        m = (L > fur + 17.0) & (al > 60) & centre & zone
        if i in front_idx:
            view = angles(i)
            k_t = model["k_rel"] * bh
            y0 = yt + model["y0_rel"] * bh
            r_t = w_med * 0.5
            good_cols = 0
            total_cols = 0
            messy = 0
            for x in range(int(cx - r_t * 0.95), int(cx + r_t * 0.95)):
                if (al[ya:yb, x] > 60).sum() < 8:
                    continue
                total_cols += 1
                rowA, rowB, s = strap_rows(model, cx, r_t, y0, k_t, view, x)
                col = np.where(m[:, x])[0]
                runs = np.split(col, np.where(np.diff(col) > 3)[0] + 1) if len(col) else []
                if len(runs) > 3 or (runs and max(len(r) for r in runs) > max_w):
                    messy += 1
                near = any(any(abs(r - row) <= 6 for r in col) for row in (rowA, rowB))
                # both straps must show when they are separated enough
                if abs(rowA - rowB) > 2.5 * max(model["halfA"], 2.0):
                    near = near and all(any(abs(r - row) <= 6 for r in col) for row in (rowA, rowB))
                good_cols += 1 if near else 0
            cover = good_cols / max(total_cols, 1)
            status = "ok"
            if cover < 0.70:
                status = f"STRAPS MISSING AT MODEL ROWS ({good_cols}/{total_cols})"
                ok = False
            if messy > total_cols * 0.15:
                status += f" MESSY ({messy}/{total_cols} cols)"
                ok = False
            print(f"    {name:26s} model-match {good_cols}/{total_cols} cols, messy {messy}  {status}")
        else:
            print(f"    {name:26s} original belt untouched ({int(m.sum())}px)")
    if ok:
        print("    OK - the original belt, cleanly crossed/positioned on every front view")
    return ok


def main():
    draw = "--check-only" not in sys.argv
    dir16 = os.path.join(ROOT, "unity_assets", "sprites_16")
    dir8 = os.path.join(ROOT, "unity_assets", "sprites")

    a16 = lambda i: i * 22.5 if i <= 8 else (i - 16) * 22.5   # signed view angle
    a8 = lambda i: i * 45.0 if i <= 4 else (i - 8) * 45.0

    process_front(dir16, DIRS16, FRONT16, a16, "16-direction set", draw)
    process_front(dir8, DIRS8, FRONT8, a8, "8-direction set", draw)

    ok = verify_set(dir16, DIRS16, FRONT16, a16, "16-direction set")
    ok &= verify_set(dir8, DIRS8, FRONT8, a8, "8-direction set")
    if not ok:
        raise SystemExit("belt verification FAILED")
    print("DONE - the original belt, cleanly consistent on every view.")


if __name__ == "__main__":
    main()
