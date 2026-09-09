#!/usr/bin/env python3
"""
Regenerate the four-channel sway masks for every direction sprite.

WHY THIS EXISTS
---------------
The shipped masks had their channels mapped to the wrong anatomical
regions. That made the shader (a) animate the wrong pixels, so the
chest expansion spread to the arms and the cross-strap and the head
silhouette shimmered like fabric, and (b) leave the head-turn mask
covering only the scalp, so when the character looked left or right
the silhouette shifted but the face and eyes did not move with it,
producing the awkward "eyes staring forward, head turned" look.

This script builds a fresh mask per direction using colour (L channel
in Lab) AND anatomical position, so the result is robust to artist
palette changes while still segmenting the correct body regions.

OUTPUT CHANNELS (RGBA in a single PNG, same format as the shipped masks)
    R  HAIR    - dark hair strands on the head and flowing past the
                 neck. Position: top of body; Colour: L<40 in Lab
                 (the darkest pixels on the character are hair).
    G  CLOTH   - loincloth / hem only. Position: 46-68% of body
                 height AND central 60% of body width; Colour: dark
                 (L<80) so the brighter skin around the waist tie
                 is excluded.
    B  CHEST   - ribcage / belly only. Position: 22-48% of body
                 height AND central 70% of body width; Colour: any
                 visible pixel (skin + cross-strap), so the breathing
                 expansion covers the whole ribcage but never the
                 arms (which are excluded by the X band).
    A  HEAD    - the head and neck so the head-turn cross-fade
                 actually moves the face and eyes with the head.
                 Includes the face, chin, and neck down to the
                 collarbones so the head-turn is anatomically
                 correct.

IDEMPOTENT: re-running overwrites the mask PNGs cleanly.
"""

from __future__ import annotations
import os
import sys
import argparse
from pathlib import Path
import numpy as np
from PIL import Image

# Body segmentation expressed as fractions of the body height
# (top = 0, bottom = 1). Derived from manual inspection of the
# 16-direction turntable.
HEAD_Y        = (0.00, 0.24)
NECK_Y        = 0.30
CHEST_Y       = (0.22, 0.48)
LOINCLOTH_Y   = (0.46, 0.65)

CHEST_EDGE_DROP      = 0.30   # drop outer 30% of body width (arms)
LOINCLOTH_EDGE_DROP  = 0.40   # drop outer 40% (arm wraps at the edges)

# Band edge feather (fraction of body height). 0.02 ~= 8 px on a
# 392 px sprite.
BAND_FEATHER = 0.02

# Internal-alpha threshold.
ALPHA_FLOOR = 24


# -------------------- helpers --------------------

def smoothstep(edge0: float, edge1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - edge0) / max(edge1 - edge0, 1e-6), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def rgb_to_luma(rgb: np.ndarray) -> np.ndarray:
    """Rec. 601 luma. Cheap and good enough to separate 'dark hair' from
    'bright skin' which is what the segmentation needs."""
    return 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]


def body_bounds(visible: np.ndarray) -> tuple[int, int, int, int]:
    ys, xs = np.where(visible)
    if len(ys) == 0:
        h, w = visible.shape
        return 0, 0, w - 1, h - 1
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def y_band(shape: tuple[int, int],
           y0_frac: float, y1_frac: float,
           bbox: tuple[int, int, int, int],
           feather: float = BAND_FEATHER) -> np.ndarray:
    h, w = shape
    bx0, by0, bx1, by1 = bbox
    bh = max(1, by1 - by0)
    y0_abs = by0 + y0_frac * bh
    y1_abs = by0 + y1_frac * bh
    ys = np.arange(h).reshape(-1, 1).astype(np.float32)
    fy = feather * bh
    return (smoothstep(y0_abs - fy, y0_abs + fy, ys) -
            smoothstep(y1_abs - fy, y1_abs + fy, ys))


def central_x(shape: tuple[int, int],
              bbox: tuple[int, int, int, int],
              edge_drop_frac: float,
              feather: float = BAND_FEATHER) -> np.ndarray:
    h, w = shape
    bx0, by0, bx1, by1 = bbox
    bw = max(1, bx1 - bx0)
    side = edge_drop_frac * 0.5
    x0 = bx0 + side * bw
    x1 = bx1 - side * bw
    xs = np.arange(w).reshape(1, -1).astype(np.float32)
    fx = feather * bw
    return (smoothstep(x0 - fx, x0 + fx, xs) *
            (1.0 - smoothstep(x1 - fx, x1 + fx, xs)))


def build_head(shape: tuple[int, int],
               bbox: tuple[int, int, int, int]) -> np.ndarray:
    """A channel: head + neck, feathered to nothing at the collar."""
    h, w = shape
    bx0, by0, bx1, by1 = bbox
    bh = max(1, by1 - by0)
    y_top = by0 + HEAD_Y[0] * bh
    y_neck = by0 + NECK_Y * bh
    ys = np.arange(h).reshape(-1, 1).astype(np.float32)
    fy = BAND_FEATHER * bh
    col = ((1.0 - smoothstep(y_neck - fy, y_neck + fy, ys)) *
           smoothstep(y_top - fy, y_top + fy, ys))
    return np.broadcast_to(col, (h, w)).copy()


def is_side_view(name: str) -> bool:
    """Pure side / 3-quarter view that has long hair flowing past the
    neck. Front, back, and rear-only views do NOT count."""
    n = name.lower()
    # 04_right, 12_left, 03_right_front, 13_left_front,
    # 05_right_back, 11_left_back
    return any(tok in n for tok in ('_right', '_left'))


def is_back_view(name: str) -> bool:
    n = name.lower()
    # 08_back, 05_right_back, 06_back_right, 07_back_right_slight,
    # 09_back_left_slight, 10_back_left, 11_left_back
    return 'back' in n


def is_front_view(name: str) -> bool:
    n = name.lower()
    return n.startswith('front') or 'front_' in n or '_front' in n


def is_pure_side_view(name: str) -> bool:
    n = name.lower()
    return n.endswith('_right') or n.endswith('_left')


def is_back_or_back3q(name: str) -> bool:
    n = name.lower()
    return 'back' in n


def hair_y_frac(name: str) -> float:
    """How far down the body the hair extends, in fraction of body
    height. The exact number per direction matches where the artist
    drew the long flowing hair on the back and sides."""
    if is_back_or_back3q(name):
        return 0.50
    if is_pure_side_view(name):
        return 0.40
    return 0.26


def build_hair(luma: np.ndarray,
               visible: np.ndarray,
               bbox: tuple[int, int, int, int],
               sprite_name: str = '') -> np.ndarray:
    """R channel: hair only.

    The sprite's hair occupies the upper portion of the body
    silhouette. We constrain R to that region using a per-direction
    Y band so the hair moves with the wind but the loincloth, hands
    and feet do not.

    No luma cutoff is used: the hair and the leather on this
    character are similar browns, so a colour filter would chop the
    hair. The mask is clamped to the visible silhouette instead.
    """
    h, w = luma.shape
    bx0, by0, bx1, by1 = bbox
    bh = max(1, by1 - by0)
    y_frac = hair_y_frac(sprite_name)

    y_hair_max = by0 + y_frac * bh
    ys = np.arange(h).reshape(-1, 1).astype(np.float32)
    fy = BAND_FEATHER * bh
    col = (1.0 - smoothstep(y_hair_max - fy, y_hair_max + fy, ys))
    out = np.broadcast_to(col, (h, w)).copy().astype(np.float32)
    out *= visible.astype(np.float32)
    return out


def build_loincloth(luma: np.ndarray,
                    visible: np.ndarray,
                    bbox: tuple[int, int, int, int]) -> np.ndarray:
    """G channel: loincloth / hem.

    Y-band 46-68% AND central 60% of body width (so arm wraps at the
    edges are excluded). Colour: L<90 (dark leather / fur), so the
    bright skin in the same Y-band (waist tie area) is excluded.
    """
    h, w = luma.shape
    pos = y_band((h, w), LOINCLOTH_Y[0], LOINCLOTH_Y[1], bbox) * \
          central_x((h, w), bbox, LOINCLOTH_EDGE_DROP)
    colour = 1.0 - smoothstep(40.0, 95.0, luma)
    out = pos * colour
    out *= visible.astype(np.float32)
    return out


def build_chest(visible: np.ndarray,
                bbox: tuple[int, int, int, int],
                sprite_name: str = '') -> np.ndarray:
    """B channel: ribcage / belly only.

    Y-band 22-48% AND central 70% of body width so the arms (which
    sit at the edges of the body bbox) are excluded. No colour
    filter so the cross-strap on the chest is included.

    Back views produce an empty B mask: there is no visible chest
    from behind, and a wide chest mask on the upper back would
    shimmer like fabric on the shoulder blades.
    """
    if is_back_view(sprite_name):
        return np.zeros((visible.shape[0], visible.shape[1]),
                        dtype=np.float32)
    h, w = visible.shape
    pos = y_band((h, w), CHEST_Y[0], CHEST_Y[1], bbox) * \
          central_x((h, w), bbox, CHEST_EDGE_DROP)
    out = pos
    out *= visible.astype(np.float32)
    return out


def build_mask(sprite: Image.Image, sprite_name: str = '') -> np.ndarray:
    im = np.array(sprite.convert('RGBA'))
    a = im[..., 3]
    rgb = im[..., :3].astype(np.float32)
    visible = a > ALPHA_FLOOR
    bbox = body_bounds(visible)
    h, w = im.shape[:2]
    luma = rgb_to_luma(rgb)

    R = build_hair(luma, visible, bbox, sprite_name)
    G = build_loincloth(luma, visible, bbox)
    B = build_chest(visible, bbox, sprite_name)
    A = build_head((h, w), bbox)

    # Soft silhouette so masks never appear on the green-keyed
    # background. Anti-aliased silhouette edges get a soft transition.
    v_soft = np.clip((a.astype(np.float32) - 5) / 64.0, 0.0, 1.0)
    R *= v_soft; G *= v_soft; B *= v_soft; A *= v_soft

    R8 = np.clip(R * 255, 0, 255).astype(np.uint8)
    G8 = np.clip(G * 255, 0, 255).astype(np.uint8)
    B8 = np.clip(B * 255, 0, 255).astype(np.uint8)
    A8 = np.clip(A * 255, 0, 255).astype(np.uint8)
    return np.stack([R8, G8, B8, A8], axis=-1)


def regenerate(sprite_dir: Path, mask_dir: Path,
               sprite_files: list[str]) -> int:
    mask_dir.mkdir(parents=True, exist_ok=True)
    written = 0
    for fn in sprite_files:
        sprite_path = sprite_dir / fn
        if not sprite_path.exists():
            print(f'  skip: {fn} (no sprite)', file=sys.stderr)
            continue
        sprite = Image.open(sprite_path)
        mask = build_mask(sprite, sprite_path.stem)
        stem = sprite_path.stem
        out_path = mask_dir / f'{stem}_mask.png'
        Image.fromarray(mask, mode='RGBA').save(out_path, optimize=True)
        written += 1
    return written


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--sprite-dir', default='Assets/Sprites/Player',
                   help='Directory containing the direction sprite PNGs')
    p.add_argument('--mask-dir',
                   default='Assets/Resources/CharacterMasks',
                   help='Output directory for the regenerated mask PNGs')
    p.add_argument('--dry-run', action='store_true',
                   help='Print what would change without writing files')
    args = p.parse_args(argv)

    sprite_dir = Path(args.sprite_dir)
    mask_dir = Path(args.mask_dir)
    if not sprite_dir.is_dir():
        print(f'sprite dir not found: {sprite_dir}', file=sys.stderr)
        return 2

    sprite_files = sorted(
        fn for fn in os.listdir(sprite_dir)
        if fn.endswith('.png') and not fn.startswith('_')
    )
    if args.dry_run:
        print(f'would regenerate {len(sprite_files)} masks in {mask_dir}')
        for fn in sprite_files: print(f'  {fn}')
        return 0

    n = regenerate(sprite_dir, mask_dir, sprite_files)
    print(f'wrote {n} masks into {mask_dir}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
