#!/usr/bin/env python3
"""
confine_harness_zone.py
=======================
After the AI harness edit of the green-screen sheets, the rebuilt sprites are
composited so that ONLY the harness zone (chest/shoulder band) is taken from
the edited art. Every pixel outside that feathered zone stays EXACTLY the
current (original) art: faces, hair, arms, loincloth, feet and outlines are
guaranteed untouched.

Zone (per sprite, from its own geometry):
    rows   : top + 0.24 .. top + 0.50 of body height  (shoulders + chest)
    columns: |x - body centre| < 0.95 * torso half-width
    feather: 4 px linear ramp on every edge

Usage (library):  confine(gen_rgba, cur_rgba) -> rgba
"""
import numpy as np

FEATHER = 4.0


def _zone_weight(shape, ya, yb, cx, w):
    h, wd = shape[:2]
    yy, xx = np.mgrid[0:h, 0:wd]
    wr = np.clip((yy - (ya - FEATHER)) / FEATHER, 0, 1) * np.clip(((yb + FEATHER) - yy) / FEATHER, 0, 1)
    wc = np.clip((w * 0.95 + FEATHER - np.abs(xx - cx)) / FEATHER, 0, 1)
    return wr * wc


def confine(gen, cur, zone):
    """gen/cur: float RGBA 0..255 arrays; zone: ((ya,yb),(cx,w),top,bodyH)."""
    (tya, tyb), (cx, w), top, bh = zone
    ya = int(top + 0.24 * bh)
    yb = int(top + 0.50 * bh)
    W = _zone_weight(gen.shape, ya, yb, cx, w)[..., None]
    return gen * W + cur * (1.0 - W)
