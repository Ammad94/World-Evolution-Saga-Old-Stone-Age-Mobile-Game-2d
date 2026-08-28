#!/usr/bin/env python3
"""
World Evolution Saga — photorealistic art pipeline.

Takes AI-generated master sprite sheets from Tools/sheets/ and slices them into
the exact sprite files the game loads (same paths & filenames as the old
pixel-art generator, so no code or prefab changes are needed):

  * chroma-keys the flat magenta background to alpha (flood-fill from borders,
    so magenta-ish content inside a sprite survives) and despills the edges
  * stabilises every frame (content bbox -> consistent scale, bottom-centre
    anchored) so animation frames don't jitter
  * writes PNGs at the resolution/PPU each asset family needs to keep its
    in-game world size, with bilinear filtering for smooth realistic art
  * patches existing .meta files (preserving GUIDs -> prefab/scene references
    stay intact) or creates fresh ones for new files
  * mirrors runtime-loaded sprites (UI skin, VFX) into Assets/Resources where
    Resources.Load can reach them
  * builds seamless tileable ground/water textures and 4 rolled animation
    frames per water type

Usage:  python3 Tools/realart.py <family>     (player, animals, npc, terrain,
                                               vegetation, items, structures,
                                               ui, vfx, all)
        python3 Tools/realart.py list         (show sheet status)
"""
import os, sys, re, uuid, math, random
from PIL import Image, ImageFilter, ImageEnhance

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SPRITES = os.path.join(ROOT, 'Assets', 'Sprites')
SHEETS = os.path.join(ROOT, 'Tools', 'sheets')
RES_SPRITES = os.path.join(ROOT, 'Assets', 'Resources', 'Sprites')

# ---------------------------------------------------------------- meta I/O

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: {mode}
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: {bx}, y: {by}, z: {bz}, w: {bw}}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:{sprites}
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices: []
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SUB_SPRITE = """
    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: {w}
        height: {h}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}"""


def _read_guid(meta_path):
    with open(meta_path, 'r', encoding='utf-8') as f:
        m = re.search(r'^guid: ([0-9a-f]{32})', f.read(), re.M)
    return m.group(1) if m else None


def write_meta(png_path, ppu, border=(0, 0, 0, 0), mode=1, sub_sprites=None):
    """Write or patch the .meta next to `png_path`, preserving existing GUIDs."""
    meta_path = png_path + '.meta'
    if sub_sprites is not None:
        mode = 2
    sprites = ''.join(SUB_SPRITE.format(**s) for s in (sub_sprites or []))
    if os.path.exists(meta_path):
        src = open(meta_path, 'r', encoding='utf-8').read()
        guid = _read_guid(meta_path) or uuid.uuid4().hex
        src = re.sub(r'^guid: [0-9a-f]+', 'guid: ' + guid, src, count=1, flags=re.M)
        src = re.sub(r'filterMode: \d+', 'filterMode: 1', src)          # bilinear
        src = re.sub(r'spritePixelsToUnits: \d+', f'spritePixelsToUnits: {ppu}', src)
        src = re.sub(r'spriteMode: \d+', f'spriteMode: {mode}', src)
        if border != (0, 0, 0, 0):
            src = re.sub(r'spriteBorder: \{[^}]*\}',
                         'spriteBorder: {x: %d, y: %d, z: %d, w: %d}' % border, src)
        open(meta_path, 'w', encoding='utf-8').write(src)
    else:
        open(meta_path, 'w', encoding='utf-8').write(META_TEMPLATE.format(
            guid=uuid.uuid4().hex, ppu=ppu, mode=mode, sprites=sprites,
            bx=border[0], by=border[1], bz=border[2], bw=border[3]))


# ---------------------------------------------------------------- background keying

def is_bg(px):
    r, g, b = px[0], px[1], px[2]
    return r > 110 and b > 110 and g < 0.72 * min(r, b) and abs(r - b) < 110


def key_background(im):
    """Flood the flat magenta background from the borders, despill the edges."""
    im = im.convert('RGBA')
    w, h = im.size
    px = im.load()
    bg = bytearray(w * h)          # 1 = border-connected background
    stack = []
    for x in range(w):
        stack += [(x, 0), (x, h - 1)]
    for y in range(h):
        stack += [(0, y), (w - 1, y)]
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= w or y >= h or bg[y * w + x]:
            continue
        if not is_bg(px[x, y]):
            continue
        bg[y * w + x] = 1
        stack += [(x+1, y), (x-1, y), (x, y+1), (x, y-1)]

    out = Image.new('RGBA', (w, h))
    opx = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if bg[y * w + x]:
                opx[x, y] = (r, g, b, 0)
                continue
            # despill magenta fringes: pull elevated R/B toward G
            if r > g + 24 and b > g + 24:
                spill = min(1.0, (min(r, b) - g - 24) / 70.0)
                r = int(r - spill * (r - g) * 0.85)
                b = int(b - spill * (b - g) * 0.85)
            opx[x, y] = (r, g, b, a)
    return out


# ---------------------------------------------------------------- sheet slicing

def load_sheet(name):
    p = os.path.join(SHEETS, name)
    if not os.path.exists(p):
        return None
    return key_background(Image.open(p))


def slice_grid(im, rows, cols):
    """Cut into rows x cols cells (row 0 = top). Returns list of images."""
    w, h = im.size
    cw, ch = w // cols, h // rows
    cells = []
    for r in range(rows):
        for c in range(cols):
            cells.append(im.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)))
    return cells


def content_bbox(im, alpha_min=8):
    bbox = im.getchannel('A').point(lambda a: 255 if a > alpha_min else 0).getbbox()
    return bbox


def normalize_frame(cell, out_px, content_frac=0.72, bottom_margin=0.03):
    """Scale content to `content_frac` of the canvas, bottom-centre anchored."""
    bbox = content_bbox(cell)
    if bbox is None:
        return Image.new('RGBA', (out_px, out_px), (0, 0, 0, 0))
    content = cell.crop(bbox)
    cw, chh = content.size
    # target size within a square canvas
    target = int(out_px * content_frac)
    if chh >= cw:
        nw = max(1, int(cw * target / chh)); nh = target
    else:
        nw = target; nh = max(1, int(chh * target / cw))
    content = content.resize((nw, nh), Image.LANCZOS)
    canvas = Image.new('RGBA', (out_px, out_px), (0, 0, 0, 0))
    x = (out_px - nw) // 2
    y = out_px - nh - int(out_px * bottom_margin)
    canvas.paste(content, (x, y), content)
    return canvas


def mirror(im):
    return im.transpose(Image.FLIP_LEFT_RIGHT)


def save_sprite(img, rel, ppu, border=(0, 0, 0, 0)):
    path = os.path.join(SPRITES, rel + '.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    write_meta(path, ppu, border)
    return path


# ---------------------------------------------------------------- tiles

def make_tileable(im):
    """Mirror-quilt a texture into a seamlessly tiling square (loses the edges)."""
    im = im.convert('RGBA').resize((512, 512), Image.LANCZOS)
    w, h = im.size
    half = im.crop((0, 0, w // 2, h // 2))
    q = Image.new('RGBA', (w, h))
    tl = half
    tr = mirror(half)
    bl = half.transpose(Image.FLIP_TOP_BOTTOM)
    br = mirror(bl)
    q.paste(tl, (0, 0)); q.paste(tr, (w // 2, 0))
    q.paste(bl, (0, h // 2)); q.paste(br, (w // 2, h // 2))
    # soften the inner seams
    seam = q.filter(ImageFilter.GaussianBlur(6))
    mask = Image.new('L', (w, h), 0)
    mpx = mask.load()
    for x in (w // 2, h // 2):
        for t in range(h):
            for d in range(-3, 4):
                if 0 <= x + d < w:
                    mpx[x + d, t] = 120
    for y in (h // 2,):
        for t in range(w):
            for d in range(-3, 4):
                if 0 <= y + d < h:
                    mpx[t, y + d] = max(mpx[t, y + d], 120)
    q = Image.composite(seam, q, mask)
    return q


def roll_frames(tex, n=4):
    """n animation frames by rolling a tileable texture (wrap-around)."""
    size = tex.size[0]
    frames = []
    for i in range(n):
        off = int(size * i / n)
        frames.append(tex.transform(tex.size, Image.AFFINE,
                                    (1, 0, -off, 0, 1, -off // 2),
                                    resample=Image.BICUBIC))
    return frames


def jitter(im, seed, amt=10):
    rng = random.Random(seed)
    r, g, b, a = im.split()
    r = r.point(lambda v: max(0, min(255, v + rng.randint(-amt, amt))))
    g = g.point(lambda v: max(0, min(255, v + rng.randint(-amt, amt))))
    b = b.point(lambda v: max(0, min(255, v + rng.randint(-amt, amt))))
    return Image.merge('RGBA', (r, g, b, a))


# ---------------------------------------------------------------- families

DIRS = ['north', 'northeast', 'east', 'southeast', 'south', 'southwest', 'west', 'northwest']
PLAYER_ACTIONS = {   # sheet, rows (canonical dirs), frames per row
    'walk':   (['south', 'north', 'east'], 6),
    'attack': (['south', 'north', 'east'], 3),
    'gather': (['south', 'north', 'east'], 3),
    'swim':   (['south', 'north', 'east'], 4),
    'climb':  (['north', 'east'], 2),
    'hit':    (['south', 'north', 'east'], 1),
}
PLAYER_PPU, PLAYER_PX, PLAYER_FRAC = 128, 512, 0.74


def process_player():
    walk_cells = None
    for action, (rows, frames) in PLAYER_ACTIONS.items():
        sheet = load_sheet(f'player_{action}.png')
        if sheet is None:
            print(f'  !! sheet missing: player_{action}.png'); continue
        cells = slice_grid(sheet, len(rows), frames)
        if action == 'walk':
            walk_cells = cells
        for ri, d in enumerate(rows):
            row = cells[ri * frames:(ri + 1) * frames]
            for fi, cell in enumerate(row):
                frame = normalize_frame(cell, PLAYER_PX, PLAYER_FRAC)
                save_sprite(frame, f'Player/{action.capitalize()}/{d}/player_{d}_{action}_{fi}', PLAYER_PPU)
                # diagonal / west fill: south covers S/SE/SW, north covers N/NE/NW,
                # west = mirrored east
                if d == 'south':
                    for dd in ['southeast', 'southwest']:
                        save_sprite(frame, f'Player/{action.capitalize()}/{dd}/player_{dd}_{action}_{fi}', PLAYER_PPU)
                elif d == 'north':
                    for dd in ['northeast', 'northwest']:
                        save_sprite(frame, f'Player/{action.capitalize()}/{dd}/player_{dd}_{action}_{fi}', PLAYER_PPU)
                elif d == 'east':
                    save_sprite(mirror(frame), f'Player/{action.capitalize()}/west/player_west_{action}_{fi}', PLAYER_PPU)

    # die: single row of 3
    sheet = load_sheet('player_die.png')
    if sheet is not None:
        cells = slice_grid(sheet, 1, 3)
        for fi, cell in enumerate(cells):
            save_sprite(normalize_frame(cell, PLAYER_PX, 0.66),
                        f'Player/Die/south/player_south_die_{fi}', PLAYER_PPU)

    # idle: reuse walk poses (subtle weight shift) for every direction
    if walk_cells:
        for ri, d in enumerate(['south', 'north', 'east']):
            for fi in (0, 2):
                frame = normalize_frame(walk_cells[ri * 6 + fi], PLAYER_PX, PLAYER_FRAC)
                for dd in DIRS:
                    if dd.startswith(d[:5]) or (d == 'south' and dd in ('southeast', 'southwest')) \
                       or (d == 'north' and dd in ('northeast', 'northwest')):
                        save_sprite(frame, f'Player/Idle/{dd}/player_{dd}_idle_{fi}', PLAYER_PPU)
                if d == 'east':
                    save_sprite(mirror(frame), f'Player/Idle/west/player_west_idle_{fi}', PLAYER_PPU)
    print('  player done')


ANIMALS = ['Mammoth', 'Sabertooth', 'CaveBear', 'Bison', 'WoollyRhino', 'CaveLion',
           'DireWolf', 'CaveHyena', 'Reindeer', 'MuskOx', 'GiantElk', 'WildBoar',
           'SnowHare', 'CavePtarmigan', 'GreatAuk']
BIRDS = {'CavePtarmigan', 'GreatAuk'}
ANIMAL_PPU, ANIMAL_PX = 80, 320


def animal_frac(sp):
    if sp in BIRDS: return 0.34
    if sp in {'SnowHare'}: return 0.5
    return {'Mammoth': 0.80, 'WoollyRhino': 0.76, 'Bison': 0.72,
            'MuskOx': 0.70, 'CaveBear': 0.68}.get(sp, 0.66)


def process_animals():
    for sp in ANIMALS:
        low = sp.lower()
        sheet = load_sheet(f'animal_{low}.png')
        if sheet is None:
            print(f'  !! sheet missing: animal_{low}.png'); continue
        frac = animal_frac(sp)
        cells = slice_grid(sheet, 5, 4)   # rows: E-walk, S-walk, N-walk, E-attack(3), E-death(3)
        e_walk = cells[0:4]; s_walk = cells[4:8]; n_walk = cells[8:12]
        e_atk = cells[12:15]; e_death = cells[15:18]
        for fi, cell in enumerate(e_walk):
            f = normalize_frame(cell, ANIMAL_PX, frac)
            save_sprite(f, f'Animals/{sp}/east/{low}_east_walk_{fi}', ANIMAL_PPU)
            save_sprite(mirror(f), f'Animals/{sp}/west/{low}_west_walk_{fi}', ANIMAL_PPU)
        for fi, cell in enumerate(s_walk):
            f = normalize_frame(cell, ANIMAL_PX, frac)
            save_sprite(f, f'Animals/{sp}/south/{low}_south_walk_{fi}', ANIMAL_PPU)
            save_sprite(f, f'Animals/{sp}/southeast/{low}_southeast_walk_{fi}', ANIMAL_PPU)
            save_sprite(f, f'Animals/{sp}/southwest/{low}_southwest_walk_{fi}', ANIMAL_PPU)
        for fi, cell in enumerate(n_walk):
            f = normalize_frame(cell, ANIMAL_PX, frac)
            save_sprite(f, f'Animals/{sp}/north/{low}_north_walk_{fi}', ANIMAL_PPU)
            save_sprite(f, f'Animals/{sp}/northeast/{low}_northeast_walk_{fi}', ANIMAL_PPU)
            save_sprite(f, f'Animals/{sp}/northwest/{low}_northwest_walk_{fi}', ANIMAL_PPU)
        for fi, cell in enumerate(e_atk):
            f = normalize_frame(cell, ANIMAL_PX, frac)
            save_sprite(f, f'Animals/{sp}/east/{low}_east_attack_{fi}', ANIMAL_PPU)
            save_sprite(mirror(f), f'Animals/{sp}/west/{low}_west_attack_{fi}', ANIMAL_PPU)
        for fi, cell in enumerate(e_death):
            save_sprite(normalize_frame(cell, ANIMAL_PX, frac),
                        f'Animals/{sp}/east/{low}_death_{fi}', ANIMAL_PPU)
        print(f'  {sp} done')


def process_npc():
    for who in ['villager', 'elder']:
        sheet = load_sheet(f'npc_{who}.png')
        if sheet is None:
            print(f'  !! sheet missing: npc_{who}.png'); continue
        cells = slice_grid(sheet, 3, 4)
        for ri, d in enumerate(['south', 'north', 'east']):
            for fi in range(4):
                f = normalize_frame(cells[ri * 4 + fi], 512, 0.72)
                save_sprite(f, f'NPC/{who.capitalize()}/{d}/villager_{d}_{fi}', 128)
                if d == 'south':
                    save_sprite(f, f'NPC/{who.capitalize()}/southeast/villager_southeast_{fi}', 128)
                    save_sprite(f, f'NPC/{who.capitalize()}/southwest/villager_southwest_{fi}', 128)
                elif d == 'north':
                    save_sprite(f, f'NPC/{who.capitalize()}/northeast/villager_northeast_{fi}', 128)
                    save_sprite(f, f'NPC/{who.capitalize()}/northwest/villager_northwest_{fi}', 128)
                else:
                    save_sprite(mirror(f), f'NPC/{who.capitalize()}/west/villager_west_{fi}', 128)
        print(f'  npc {who} done')


GROUNDS = ['dirt', 'grass', 'sand', 'snow', 'stone', 'mud']
WATERS = ['ocean_water', 'calm_water', 'river_water']


def process_terrain():
    sheet = load_sheet('ground.png')
    if sheet is not None:
        cells = slice_grid(sheet, 2, 3)
        for gi, g in enumerate(GROUNDS):
            tile = make_tileable(cells[gi])
            v0 = tile.resize((256, 256), Image.LANCZOS)
            save_sprite(v0, f'Terrain/Ground/{g}_tile_0', 256)
            save_sprite(roll_frames(tile, 4)[1].resize((256, 256), Image.LANCZOS),
                        f'Terrain/Ground/{g}_tile_1', 256)
            save_sprite(jitter(v0, hash(g) & 0xffff, 8),
                        f'Terrain/Ground/{g}_tile_2', 256)
        print('  ground done')
    sheet = load_sheet('water.png')
    if sheet is not None:
        cells = slice_grid(sheet, 3, 1)
        for wi, wname in enumerate(WATERS):
            tile = make_tileable(cells[wi])
            for fi, frame in enumerate(roll_frames(tile, 4)):
                save_sprite(frame.resize((256, 256), Image.LANCZOS),
                            f'Terrain/Water/{wname}_tile_{fi}', 256)
        print('  water done')
    mirror_tiles()

def mirror_tiles():
    """Mirror ground variants + water frames into Resources so GameLibrary can
    resolve them at runtime even from a stale library asset."""
    import glob
    pairs = []
    for g in GROUNDS:
        for v in range(3):
            pairs.append((f'Terrain/Ground/{g}_tile_{v}', f'{g}_tile_{v}'))
    for w in WATERS:
        for f in range(4):
            pairs.append((f'Terrain/Water/{w}_tile_{f}', f'{w}_tile_{f}'))
    n = 0
    for src, dst in pairs:
        s = os.path.join(SPRITES, src + '.png')
        if not os.path.exists(s):
            continue
        d = os.path.join(RES_SPRITES, 'Tiles', dst + '.png')
        os.makedirs(os.path.dirname(d), exist_ok=True)
        Image.open(s).save(d, optimize=True)
        write_meta(d, 256)   # fresh GUID inside write_meta (file is new)
        n += 1
    print(f'  tiles mirrored to Resources ({n})')


TREES = ['pine_tree', 'oak_tree', 'apple_tree', 'fig_tree',
         'birch_tree', 'palm_tree', 'jungle_tree', 'dead_tree']
BUSHES = ['berry_bush', 'vine', 'flower_bush', 'reeds']
ROCKS = ['large_rock', 'stone_cluster', 'flint_outcrop']


def process_vegetation():
    sheet = load_sheet('trees.png')
    if sheet is not None:
        for i, name in enumerate(TREES):
            f = normalize_frame(slice_grid(sheet, 2, 4)[i], 512, 0.94)
            save_sprite(f, f'Vegetation/Trees/{name}', 128)
        print('  trees done')
    sheet = load_sheet('bushes_rocks.png')
    if sheet is not None:
        cells = slice_grid(sheet, 2, 4)
        for i, name in enumerate(BUSHES):
            save_sprite(normalize_frame(cells[i], 512, 0.66), f'Vegetation/Bushes/{name}', 128)
        for i, name in enumerate(ROCKS):
            save_sprite(normalize_frame(cells[4 + i], 512, 0.6), f'Vegetation/Rocks/{name}', 128)
        if len(cells) > 7:
            save_sprite(normalize_frame(cells[7], 512, 0.5), 'Vegetation/Grass/grass_tuft', 128)
        print('  bushes/rocks done')


ITEMS_A = ['raw_meat', 'cooked_meat', 'wild_apple', 'berries', 'wild_carrot', 'wood_log',
           'stone', 'animal_hide', 'fiber', 'stone_pickaxe', 'stone_axe', 'torch',
           'flint_shard', 'bone', 'sinew', 'obsidian', 'copper_ore', 'fur_pelt']
ITEMS_B = ['bone_spear', 'obsidian_knife', 'fur_cloak', 'hide_leggings', 'water_skin',
           'healing_salve', 'wooden_bowl', 'dried_meat', 'atlatl', 'totem', 'drum',
           'copper_amulet', 'herb_pouch', 'workbench', 'tent', 'hut', 'campfire', 'footprint']


def process_items():
    for sheet_name, items in [('items_a.png', ITEMS_A), ('items_b.png', ITEMS_B)]:
        sheet = load_sheet(sheet_name)
        if sheet is None:
            print(f'  !! sheet missing: {sheet_name}'); continue
        cells = slice_grid(sheet, 3, 6)
        for i, name in enumerate(items):
            save_sprite(normalize_frame(cells[i], 256, 0.88), f'Items/{name}', 256)
        print(f'  {sheet_name} done')


STRUCT_CELLS = ['tent', 'workbench', 'hut', 'trade_post',
                'cave_entrance', 'cliff_face', 'mountain_peak', 'log_raft']


def process_structures():
    sheet = load_sheet('structures.png')
    if sheet is None:
        print('  !! sheet missing: structures.png'); return
    cells = slice_grid(sheet, 2, 4)
    targets = {'tent': 'Structures/tent', 'workbench': 'Structures/workbench',
               'hut': 'Structures/hut', 'trade_post': 'Structures/trade_post',
               'cave_entrance': 'Terrain/Mountain/cave_entrance',
               'cliff_face': 'Terrain/Mountain/cliff_face',
               'mountain_peak': 'Terrain/Mountain/mountain_peak',
               'log_raft': 'Items/log_raft'}
    for i, key in enumerate(STRUCT_CELLS):
        save_sprite(normalize_frame(cells[i], 512, 0.92), targets[key], 128)
    print('  structures done')


UI_PANELS = ['panel_parchment', 'panel_dark', 'panel_dialogue', 'tooltip', 'slot', 'divider']
UI_CONTROLS = ['button', 'button_pressed', 'knob', 'checkbox_on', 'checkbox_off',
               'bar_frame', 'bar_fill']
UI_ICONS = ['health_icon', 'hunger_icon', 'thirst_icon', 'energy_icon', 'stamina_icon']
WIDE = {'divider': (512, 96), 'bar_frame': (512, 128), 'bar_fill': (512, 128)}


def _ui_out(cell, name, default=512):
    if name in WIDE:
        w, h = WIDE[name]
        bbox = content_bbox(cell)
        if bbox:
            return cell.crop(bbox).resize((w, h), Image.LANCZOS)
    return normalize_frame(cell, default, 0.96, bottom_margin=0.02)


def process_ui():
    b = load_sheet('ui_banner.png')
    if b is not None:
        bbox = content_bbox(b)
        if bbox:
            img = b.crop(bbox).resize((1024, 384), Image.LANCZOS)
            save_sprite(img, 'UI/Skin/banner', 100, border=(52, 40, 52, 40))
            print('  banner done')
    sheet = load_sheet('ui_panels.png')
    if sheet is not None:
        cells = slice_grid(sheet, 2, 3)
        borders = {'panel_parchment': 46, 'panel_dark': 46, 'panel_dialogue': 46, 'tooltip': 34, 'slot': 40}
        for i, name in enumerate(UI_PANELS):
            bd = borders.get(name, 0)
            save_sprite(_ui_out(cells[i], name), f'UI/Skin/{name}', 100,
                        border=(bd, bd, bd, bd) if bd else (0, 0, 0, 0))
        print('  panels done')
    sheet = load_sheet('ui_controls.png')
    if sheet is not None:
        cells = slice_grid(sheet, 2, 4)
        for i, name in enumerate(UI_CONTROLS):
            bd = 40 if 'button' in name else (30 if 'bar' in name or 'checkbox' in name else 0)
            save_sprite(_ui_out(cells[i], name), f'UI/Skin/{name}', 100,
                        border=(bd, bd, bd, bd) if bd else (0, 0, 0, 0))
        print('  controls done')
    sheet = load_sheet('ui_icons.png')
    if sheet is not None:
        cells = slice_grid(sheet, 1, 5)
        for i, name in enumerate(UI_ICONS):
            save_sprite(normalize_frame(cells[i], 256, 0.9), f'UI/Icons/{name}', 256)
        print('  icons done')


VFX_A = ['blood', 'dust', 'ember', 'fire']
VFX_B = ['hitflash', 'leaf', 'puff', 'ring', 'slash', 'snow', 'spark', 'splash']


def process_vfx():
    frame_px, ppu = 256, 256
    for sheet_name, names in [('vfx_a.png', VFX_A), ('vfx_b.png', VFX_B)]:
        sheet = load_sheet(sheet_name)
        if sheet is None:
            print(f'  !! sheet missing: {sheet_name}'); continue
        rows = len(names)
        cells = slice_grid(sheet, rows, 4)
        for ni, name in enumerate(names):
            strip = Image.new('RGBA', (frame_px * 4, frame_px), (0, 0, 0, 0))
            subs = []
            for fi in range(4):
                cell = cells[ni * 4 + fi]
                bbox = content_bbox(cell)
                frame = Image.new('RGBA', (frame_px, frame_px), (0, 0, 0, 0))
                if bbox:
                    c = cell.crop(bbox)
                    cw, ch = c.size
                    target = int(frame_px * 0.82)
                    if ch >= cw:
                        nw = max(1, cw * target // ch); nh = target
                    else:
                        nw = target; nh = max(1, ch * target // cw)
                    c = c.resize((nw, nh), Image.LANCZOS)
                    frame.paste(c, ((frame_px - nw) // 2, (frame_px - nh) // 2), c)
                strip.paste(frame, (fi * frame_px, 0))
                subs.append({'name': f'{name}_{fi}', 'x': fi * frame_px, 'y': 0,
                             'w': frame_px, 'h': frame_px})
            path = os.path.join(SPRITES, f'VFX/{name}.png')
            strip.save(path, optimize=True)
            write_meta(path, ppu, sub_sprites=subs)
        print(f'  {sheet_name} done')


def mirror_to_resources():
    """Resources.Load can only see Assets/Resources — mirror UI skin + VFX there."""
    pairs = []
    for name in UI_PANELS + UI_CONTROLS + ['banner']:
        pairs.append((f'UI/Skin/{name}', f'UI/Skin/{name}'))
    for name in VFX_A + VFX_B:
        pairs.append((f'VFX/{name}', f'VFX/{name}'))
    for src, dst in pairs:
        s = os.path.join(SPRITES, src + '.png')
        if not os.path.exists(s):
            continue
        d = os.path.join(RES_SPRITES, dst + '.png')
        os.makedirs(os.path.dirname(d), exist_ok=True)
        Image.open(s).save(d, optimize=True)
        # fresh GUID for the copy (never duplicate GUIDs)
        open(d + '.meta', 'w', encoding='utf-8').write(
            META_TEMPLATE.format(guid=uuid.uuid4().hex, ppu=256, mode=2 if 'VFX' in dst else 1,
                                 sprites=''))
        # keep sub-sprite table for vfx strips
        if 'VFX' in dst:
            subs = [{'name': f"{os.path.splitext(os.path.basename(dst))[0]}_{i}",
                     'x': i * 256, 'y': 0, 'w': 256, 'h': 256} for i in range(4)]
            open(d + '.meta', 'w', encoding='utf-8').write(
                META_TEMPLATE.format(guid=uuid.uuid4().hex, ppu=256, mode=2,
                                     sprites=''.join(SUB_SPRITE.format(**s) for s in subs)))
    print('  resources mirror done')


FAMILIES = {
    'player': [process_player], 'animals': [process_animals], 'npc': [process_npc],
    'terrain': [process_terrain], 'vegetation': [process_vegetation],
    'items': [process_items], 'structures': [process_structures],
    'ui': [process_ui, mirror_to_resources], 'vfx': [process_vfx, mirror_to_resources],
    'all': [process_player, process_animals, process_npc, process_terrain,
            process_vegetation, process_items, process_structures, process_ui,
            process_vfx, mirror_to_resources],
}

if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'list'
    if cmd == 'list':
        have = sorted(os.listdir(SHEETS)) if os.path.isdir(SHEETS) else []
        print('sheets present:' if have else 'no sheets yet (Tools/sheets/ is empty)')
        for f in have:
            print('  ', f)
        sys.exit(0)
    for fn in FAMILIES.get(cmd, []):
        fn()
    print('done.')
