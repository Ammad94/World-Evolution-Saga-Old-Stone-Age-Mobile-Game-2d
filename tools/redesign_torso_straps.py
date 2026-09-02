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

import io
import os
import sys
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


def grown_belt_mask(rgba, ya, yb, cx, w_med, strict=16.0, loose=13.0, cap=6.0):
    """Belt pixels: strict seeds region-grown with a looser tolerance.

    ANTI-FLOOD: growth is capped at `cap` x the seed area - on bright front
    views the general fur can sit close to the strap brightness, and an
    uncapped grow would swallow the whole torso (which flattens the chest)."""
    al = rgba[:, :, 3]
    L = lum(rgba[:, :, :3])
    H, W = L.shape
    fur = fur_median(rgba, ya, yb, cx, w_med)
    zone = np.zeros((H, W), bool)
    zone[ya + 2:yb - 3, :] = True
    centre = np.abs(np.arange(W)[None, :] - cx) < w_med * 1.05
    body = al > 60
    mask = zone & body & centre & (L > fur + strict)
    seeds = int(mask.sum())
    if seeds == 0:
        return mask, fur
    limit = int(seeds * cap)
    while True:
        cand = _dilate(mask) & zone & body & centre & (L > fur + loose)
        new = cand & ~mask
        if not new.any() or int(mask.sum()) >= limit:
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

    # ---- strap TEXTURE BANKS: unroll each donor strap into sections of REAL
    # pixels (keeps the hand-painted look: fur tufts, irregular edges, shading)
    def bank(m, b, half):
        xs_span = sorted(x for x in cols if abs(m * x + b - min([r[0] for r in cols[x]], key=lambda c: abs(c - m * x + b))) < 999)
        xs_span = sorted(cols.keys())
        pad = int(half) + 2
        sections = []
        for x in xs_span:
            y = m * x + b
            rows = np.arange(int(round(y)) - pad, int(round(y)) + pad + 1)
            if rows[0] < 0 or rows[-1] >= donor_rgba.shape[0]:
                sections.append(None)
                continue
            rgb = donor_rgba[rows, x, :3].copy()
            sof = soft[rows, x].copy()
            core = sof > 0.5
            med = float(np.median(lum(rgb)[core])) if core.sum() >= 3 else None
            sections.append((rgb, sof, med))
        good = [sec for sec in sections if sec is not None and sec[2] is not None]
        core_med = float(np.median([sec[2] for sec in good])) if good else 120.0
        return sections, core_med

    def leather(m, b, half):
        px = []
        for x, runs in cols.items():
            y = int(round(m * x + b))
            for dy in range(-int(half), int(half) + 1):
                yy = y + dy
                if 0 <= yy < donor_rgba.shape[0] and soft[yy, x] > 0.4:
                    px.append(donor_rgba[yy, x, :3])
        return np.median(np.array(px), axis=0) if px else np.array([185.0, 145.0, 112.0])

    leatherA = leather(mA, bA, hA)
    leatherB = leather(mB, bB, hB)
    bankA, coremedA = bank(mA, bA, hA)
    bankB, coremedB = bank(mB, bB, hB)
    tone = strap_lum(donor_rgba, dya, dyb, dcx, dw) or 130.0
    return dict(k_rel=k_px / dbh, y0_rel=(ycross - dyt) / dbh, r_rel=dw / dbh / 2.0,
                halfA=hA, halfB=hB, leatherA=leatherA, leatherB=leatherB, tone=tone,
                bankA=bankA, bankB=bankB, coremedA=coremedA, coremedB=coremedB)


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

        zone0 = np.zeros(target.shape[:2], bool)
        zone0[tya:tyb + 1, :] = True
        centre0 = np.abs(np.arange(target.shape[1])[None, :] - tcx) < tw * 0.6
        belt_mask, _ = grown_belt_mask(target, tya, tyb, tcx, tw)
        erased = int(belt_mask.sum())
        keep = zone0 & centre0 & (target[:, :, 3] > 60) & ~belt_mask

        def kept_stats(arr):
            Ls = lum(arr[:, :, :3])
            return float(Ls[keep].mean()), float(Ls[keep].std())

        pre_mean, pre_std = kept_stats(target)
        t_tone = strap_lum(target, tya, tyb, tcx, tw) or model["tone"]
        # match the back view's leather-vs-fur contrast so the belt reads clearly
        fur_t = fur_median(target, tya, tyb, tcx, tw)
        t_tone = max(t_tone, fur_t + 26.0)
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
        x_left, x_right = min(body_cols), max(body_cols)

        def section_for(bank, core_med, frac):
            """Mirror-tile the donor's real strap sections along the strap."""
            n = len(bank)
            if n == 0:
                return None
            u = frac * (n - 1)
            tile = int(u) // (n - 1) if n > 1 else 0
            rem = int(u) % (n - 1) if n > 1 else 0
            idx = rem if tile % 2 == 0 else (n - 1 - rem)
            sec = bank[idx]
            while sec is None or sec[2] is None:      # skip occluded sections
                idx = (idx + 1) % n
                sec = bank[idx]
                if idx == int(u) % n and (sec is None or sec[2] is None):
                    return None
            return sec

        for xt in body_cols:
            rowA, rowB, s = strap_rows(model, tcx, r_t, y0, k_t, view, xt)
            delta = np.arcsin(np.clip((xt - tcx) / max(r_t, 1.0), -1.0, 1.0))
            shade = 0.86 + 0.14 * np.cos(delta)      # cylinder shading: lit centre
            frac = (xt - x_left) / max(x_right - x_left, 1)
            for (row, half, bank, core_med) in (
                    (rowA, model["halfA"], model["bankA"], model["coremedA"]),
                    (rowB, model["halfB"], model["bankB"], model["coremedB"])):
                pad = int(half) + 2
                lo, hi = int(round(row - half - 4)), int(round(row + half + 4))
                if 0 <= lo < H and 0 <= hi < H:
                    allowed[lo:hi + 1, xt] = True
                sec = section_for(bank, core_med, frac)
                if sec is None:
                    continue
                rgb_s, sof_s, med_s = sec
                # brightness-normalise the section (removes the donor's
                # shadow-side dip) but KEEPS its hand-painted texture
                norm = core_med / max(med_s, 1.0)
                for j, dy in enumerate(range(-pad, pad + 1)):
                    yti = int(round(row + dy))
                    if not (0 <= yti < H) or al[yti, xt] <= 60:
                        continue
                    a = float(np.clip(sof_s[j], 0.0, 1.0)) * 0.95
                    if a < 0.04:
                        continue
                    col = np.clip(rgb_s[j] * norm, 0, 255) * scale * shade
                    col = col + rng.normal(0, 2.0, 3)
                    a3 = a
                    target[yti, xt, :3] = np.clip(target[yti, xt, :3] * (1 - a3) + col * a3, 0, 255)
                    painted += 1
            # dark leather seam where the straps overlap
            if abs(rowA - rowB) < (model["halfA"] + model["halfB"]) * 1.4:
                ym = int(round((rowA + rowB) / 2))
                for dy in range(-1, 2):
                    yy = ym + dy
                    if 0 <= yy < H and al[yy, xt] > 60:
                        target[yy, xt, :3] *= 0.90

        # clean leftover bright pixels ONLY in a narrow band around the drawn
        # straps (never touch the rest of the fur - that is what flattened the
        # chest on the 8-dir set)
        L = lum(target[:, :, :3])
        fur = fur_median(target, tya, tyb, tcx, tw)
        centre = np.abs(np.arange(W)[None, :] - tcx) < tw * 0.95
        zone = np.zeros((H, W), bool)
        zone[tya + 1:tyb, :] = True
        near = allowed.copy()
        for _ in range(4):
            near = _dilate(near)
        stray = (L > fur + 14.0) & (al > 60) & centre & zone & near & ~allowed
        strays = int(stray.sum())
        if strays:
            inpaint(target, stray, rng)

        post_mean, post_std = kept_stats(target)
        d_mean, d_std = post_mean - pre_mean, post_std - pre_std
        texture_ok = (d_mean > -6.0) and (d_std > -7.0)
        if not texture_ok:
            print(f"    !! {name}: vest texture damaged (lum {pre_mean:.0f}->{post_mean:.0f}, "
                  f"std {pre_std:.0f}->{post_std:.0f}) - NOT saved")
        if draw and texture_ok:
            Image.fromarray(target.astype(np.uint8)).save(os.path.join(dir_path, name + ".png"))
        print(f"    {name:26s} erased {erased:5d}px, drew {painted:5d}px straps, cleaned {strays:4d}px; "
              f"vest lum {pre_mean:.0f}->{post_mean:.0f} std {pre_std:.0f}->{post_std:.0f} "
              f"{'ok' if texture_ok else 'TEXTURE DAMAGED'}")


def band_row_count(rgba, ya, yb, cx, w_med, fur):
    """Rows where bright pixels span the torso (old bands OR fur streaks OR
    the strap crossing) - compared against the pre-belt baseline."""
    al = rgba[:, :, 3]
    L = lum(rgba[:, :, :3])
    cnt = 0
    for y in range(ya, yb + 1):
        xs_ = np.where((al[y] > 60) & (np.abs(np.arange(L.shape[1]) - cx) < w_med * 0.85))[0]
        if len(xs_) < 20:
            continue
        if (L[y, xs_] > fur + 13).mean() > 0.60:
            cnt += 1
    return cnt


def verify_set(dir_path, names, front_idx, angles, label):
    """Front views: straps must appear at the MODEL-predicted rows (wherever
    the body actually has pixels), and no NEW horizontal bands vs pre-belt."""
    print(f"--- verify {label}")
    n = len(names)
    donor = np.asarray(Image.open(os.path.join(dir_path, names[CLEAN_DONOR[n]] + ".png"))
                       .convert("RGBA")).astype(float)
    model = measure_belt(donor)
    zpath = os.path.join(ROOT, "tools", "originals_backup.zip")
    zfile = zipfile.ZipFile(zpath) if os.path.exists(zpath) else None
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
        m = (L > fur + 13.0) & (al > 60) & centre & zone
        if i in front_idx:
            view = angles(i)
            k_t = model["k_rel"] * bh
            y0 = yt + model["y0_rel"] * bh
            r_t = w_med * 0.5
            good_cols = 0
            total_cols = 0
            band_rows = 0
            for x in range(int(cx - r_t * 0.95), int(cx + r_t * 0.95)):
                if (al[ya:yb, x] > 60).sum() < 8:
                    continue
                total_cols += 1
                rowA, rowB, s = strap_rows(model, cx, r_t, y0, k_t, view, x)
                col = np.where(m[:, x])[0]

                def strap_shows(row):
                    if any(abs(r - row) <= 7 for r in col):
                        return True
                    # not drawn: fine only if there is no body at that row
                    yy = np.arange(max(0, int(row) - 5), min(L.shape[0], int(row) + 6))
                    return not ((al[yy, x] > 60).any())

                near = strap_shows(rowA) and strap_shows(rowB)
                good_cols += 1 if near else 0
            # no NEW horizontal bands vs the pre-belt baseline (the art's own
            # fur streaks and the strap crossing both count, hence the margin)
            pre_n = 0
            if zfile is not None:
                try:
                    pre = np.asarray(Image.open(io.BytesIO(zfile.read(os.path.join(os.path.relpath(dir_path, ROOT), name + ".png"))))
                                     .convert("RGBA")).astype(float)
                    pya, pyb = zone_of(pre)[0]
                    pp = zone_of(pre)[1]
                    if pp is not None:
                        pre_n = band_row_count(pre, pya, pyb, pp[0], pp[1],
                                               fur_median(pre, pya, pyb, pp[0], pp[1]))
                except KeyError:
                    pass
            band_rows = band_row_count(rgba, ya, yb, cx, w_med, fur)
            if band_rows > pre_n + 8:
                status_bands = f"NEW HORIZONTAL BANDS ({band_rows} vs pre {pre_n})"
                ok = False
            else:
                status_bands = ""
            cover = good_cols / max(total_cols, 1)
            status = "ok"
            if cover < 0.70:
                status = f"STRAPS MISSING AT MODEL ROWS ({good_cols}/{total_cols})"
                ok = False
            status += status_bands
            print(f"    {name:26s} model-match {good_cols}/{total_cols} cols, band rows {band_rows}  {status}")
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
