#!/usr/bin/env python3
"""Statistical QA for generated master sheets: checks chroma background, grid
cell content coverage, and per-row frame consistency. Usage: qa_sheet.py <file> <rows> <cols> [rows_detail]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image
from realart import key_background, content_bbox, is_bg

path, rows, cols = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
im = Image.open(path).convert('RGBA')
w, h = im.size
px = im.load()

# 1) background check: sample border ring
border = [px[x, y] for x in range(0, w, 17) for y in (0, h-1)] + \
         [px[x, y] for y in range(0, h, 17) for x in (0, w-1)]
bg_frac = sum(1 for p in border if is_bg(p)) / len(border)
print(f"{os.path.basename(path)}: {w}x{h}, border magenta fraction: {bg_frac:.2%}")

# 2) key + per-cell coverage
keyed = key_background(im)
cw, ch = w // cols, h // rows
print(f"cell {cw}x{ch}")
for r in range(rows):
    stats = []
    for c in range(cols):
        cell = keyed.crop((c*cw, r*ch, (c+1)*cw, (r+1)*ch))
        bbox = content_bbox(cell)
        if bbox is None:
            stats.append("EMPTY")
            continue
        cw2, ch2 = bbox[2]-bbox[0], bbox[3]-bbox[1]
        cov = (cw2*ch2)/(cw*ch)
        alpha = cell.getchannel('A')
        hist = alpha.histogram()
        opaque = sum(hist[128:]) / (cw*ch)
        cx = (bbox[0]+bbox[2])/2/cw
        stats.append(f"c{c}:{int(cw2)}x{int(ch2)} op{opaque:.0%} cx{cx:.2f}")
    print(f" row{r}: " + " | ".join(stats))
