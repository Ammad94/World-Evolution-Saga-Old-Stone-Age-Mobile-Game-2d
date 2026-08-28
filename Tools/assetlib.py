#!/usr/bin/env python3
"""
Shared asset toolkit for World Evolution Saga's generated art pipeline.
Pure stdlib: PNG encoder, tiny pixel-art raster engine, .meta writer.
Coordinate system: logical art pixels scaled by S (default 8) onto a canvas.
"""
import struct, zlib, random, uuid, os

# ---------------------------------------------------------------- PNG writer
def _chunk(tag, data):
    c = struct.pack('>I', len(data)) + tag + data
    return c + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF)

def _write_png_indexed(path, w, h, pix, max_colors=255):
    """Reduce RGBA to a palette (<=256 colours) and encode as colour-type-3 PNG."""
    # Build palette from most frequent colours; merge overflow to nearest.
    counts = {}
    n = w * h
    for i in range(0, len(pix), 4):
        c = bytes(pix[i:i + 4])
        counts[c] = counts.get(c, 0) + 1
    # Always reserve slot 0 for transparent
    freq = sorted(counts.items(), key=lambda kv: -kv[1])
    transparent = b'\x00\x00\x00\x00'
    palette = [transparent]
    if transparent in counts:
        freq = [kv for kv in freq if kv[0] != transparent]
    for c, _cnt in freq[:max_colors]:
        palette.append(c)
    index = {c: i for i, c in enumerate(palette)}
    # Map every pixel
    idx_rows = bytearray()
    import math as _m
    def nearest(c):
        best, bd = 0, 1 << 30
        for i, p in enumerate(palette):
            d = (c[0] - p[0]) ** 2 + (c[1] - p[1]) ** 2 + (c[2] - p[2]) ** 2 + (c[3] - p[3]) ** 2
            if d < bd:
                bd, best = d, i
        return best
    stride = w
    row = bytearray(w)
    for y in range(h):
        base = y * stride * 4
        for x in range(w):
            c = bytes(pix[(y * w + x) * 4:(y * w + x) * 4 + 4])
            i = index.get(c)
            if i is None:
                i = nearest(c)
                index[c] = i
            row[x] = i
        idx_rows.append(0)  # filter none
        idx_rows += row
    def chunk(tag, data):
        return _chunk(tag, data)
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 3, 0, 0, 0))
    plte = b''.join(bytes(p[:3]) for p in palette)
    png += chunk(b'PLTE', plte)
    trns = bytes(p[3] for p in palette)
    png += chunk(b'tRNS', trns)
    png += chunk(b'IDAT', zlib.compress(bytes(idx_rows), 9))
    png += chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(png)

def write_png(path, w, h, pix):
    """pix: bytearray RGBA, length w*h*4."""
    _write_png_indexed(path, w, h, pix)

# ---------------------------------------------------------------- colours
def rgb(r, g, b, a=255):
    return (r, g, b, a)

def shade(c, f):
    """Multiply colour by factor f (0..inf), clamped."""
    return (max(0, min(255, int(c[0] * f))),
            max(0, min(255, int(c[1] * f))),
            max(0, min(255, int(c[2] * f))),
            c[3])

def mix(a, b, t):
    return (int(a[0] + (b[0] - a[0]) * t), int(a[1] + (b[1] - a[1]) * t),
            int(a[2] + (b[2] - a[2]) * t), int(a[3] + (b[3] - a[3]) * t))

# ---------------------------------------------------------------- canvas
class Canvas:
    def __init__(self, w=128, h=128, s=8):
        self.w, self.h, self.s = w, h, s
        self.pix = bytearray(w * s * h * s * 4)

    def clear(self):
        self.pix = bytearray(len(self.pix))

    # -- low level
    def set(self, x, y, c):
        x = int(round(x)); y = int(round(y))
        if x < 0 or y < 0 or x >= self.w or y >= self.h or c[3] == 0:
            return
        s = self.s
        base = (y * self.w * s + x) * s * 4
        for dy in range(s):
            row = base + dy * self.w * s * 4
            for dx in range(s):
                i = row + dx * 4
                if c[3] >= 255:
                    self.pix[i:i + 4] = bytes(c)
                else:  # alpha-over
                    a = c[3] / 255.0
                    self.pix[i] = int(self.pix[i] * (1 - a) + c[0] * a)
                    self.pix[i + 1] = int(self.pix[i + 1] * (1 - a) + c[1] * a)
                    self.pix[i + 2] = int(self.pix[i + 2] * (1 - a) + c[2] * a)
                    self.pix[i + 3] = min(255, self.pix[i + 3] + c[3])

    def get(self, x, y):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return (0, 0, 0, 0)
        i = (y * self.w * self.s + x) * self.s * 4
        return tuple(self.pix[i:i + 4])

    # -- primitives (logical coords)
    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(min(y0, y1)), int(max(y0, y1)) + 1):
            for x in range(int(min(x0, x1)), int(max(x0, x1)) + 1):
                self.set(x, y, c)

    def hline(self, x0, x1, y, c):
        for x in range(int(min(x0, x1)), int(max(x0, x1)) + 1):
            self.set(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        if rx <= 0 or ry <= 0:
            return
        for y in range(int(cy - ry), int(cy + ry) + 1):
            t = (y - cy) / ry
            dx = rx * max(0.0, 1.0 - t * t) ** 0.5
            for x in range(int(cx - dx), int(cx + dx) + 1):
                self.set(x, y, c)

    def circle(self, cx, cy, r, c):
        self.ellipse(cx, cy, r, r, c)

    def line(self, x0, y0, x1, y1, c, w=1):
        steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 2 + 1)
        for i in range(steps + 1):
            t = i / steps
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            if w <= 1:
                self.set(round(x), round(y), c)
            else:
                r = w / 2.0
                self.circle(x, y, r, c)

    def polyline(self, pts, c, w=1):
        for a, b in zip(pts, pts[1:]):
            self.line(a[0], a[1], b[0], b[1], c, w)

    def tri(self, p0, p1, p2, c):
        minx = int(min(p0[0], p1[0], p2[0])); maxx = int(max(p0[0], p1[0], p2[0]))
        miny = int(min(p0[1], p1[1], p2[1])); maxy = int(max(p0[1], p1[1], p2[1]))
        for y in range(miny, maxy + 1):
            for x in range(minx, maxx + 1):
                d1 = (x - p1[0]) * (p0[1] - p1[1]) - (p0[0] - p1[0]) * (y - p1[1])
                d2 = (x - p2[0]) * (p1[1] - p2[1]) - (p1[0] - p2[0]) * (y - p2[1])
                d3 = (x - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (y - p0[1])
                neg = d1 < 0 or d2 < 0 or d3 < 0
                pos = d1 > 0 or d2 > 0 or d3 > 0
                if not (neg and pos):
                    self.set(x, y, c)

    def outline(self, c=(20, 14, 12, 255)):
        """1-logical-px dark outline around opaque pixels."""
        w, h = self.w, self.h
        snapshot = {}
        for y in range(h):
            for x in range(w):
                if self.get(x, y)[3] > 60:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and self.get(nx, ny)[3] > 60:
                        snapshot[(x, y)] = c
                        break
        for (x, y), col in snapshot.items():
            self.set(x, y, col)

    def mirror_x(self):
        """Flip horizontally in place (returns self)."""
        for y in range(self.h):
            for x in range(self.w // 2):
                a = self.get(x, y); b = self.get(self.w - 1 - x, y)
                # swap at native resolution
                s = self.s
                for dx in range(s):
                    ax, bx = x * s + dx, (self.w - 1 - x) * s + dx
                    for dy in range(s):
                        i1 = (y * s + dy) * self.w * s * 4 + ax * 4
                        i2 = (y * s + dy) * self.w * s * 4 + bx * 4
                        pa = bytes(self.pix[i1:i1 + 4]); pb = bytes(self.pix[i2:i2 + 4])
                        self.pix[i2:i2 + 4] = pa
                        self.pix[i1:i1 + 4] = pb
        return self

    def rotate(self, angle_deg, pivot=None):
        """Nearest-neighbour rotate about pivot (logical, default centre)."""
        import math
        if pivot is None:
            pivot = (self.w / 2.0, self.h / 2.0)
        a = math.radians(-angle_deg)  # screen y down
        ca, sa = math.cos(a), math.sin(a)
        s = self.s
        old = bytes(self.pix)
        W, H = self.w * s, self.h * s
        px, py = pivot[0] * s, pivot[1] * s
        new = bytearray(len(old))
        for yy in range(H):
            for xx in range(W):
                dx, dy = xx - px, yy - py
                sx = int(px + dx * ca - dy * sa)
                sy = int(py + dx * sa + dy * ca)
                if 0 <= sx < W and 0 <= sy < H:
                    i1 = (yy * W + xx) * 4
                    i2 = (sy * W + sx) * 4
                    new[i1:i1 + 4] = old[i2:i2 + 4]
        self.pix = new
        return self

    def save(self, path):
        write_png(path, self.w * self.s, self.h * self.s, self.pix)

# ---------------------------------------------------------------- texture helpers
def speckle(cv, rng, area, colors, n, rmin=0, rmax=1):
    for _ in range(n):
        x = rng.randint(area[0], area[2])
        y = rng.randint(area[1], area[3])
        c = rng.choice(colors)
        r = rng.randint(rmin, rmax)
        cv.circle(x, y, r, c)

# ---------------------------------------------------------------- .meta writers
def _guid():
    return uuid.uuid4().hex

PNG_META = """fileFormatVersion: 2
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
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: {sprite_mode}
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
    sprites:{sprites_yaml}
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
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

def _sprite_entry(name, x, y, w, h, guid):
    return (f"      - serializedVersion: 2\n"
            f"        name: {name}\n"
            f"        rect:\n"
            f"          serializedVersion: 2\n"
            f"          x: {x}\n"
            f"          y: {y}\n"
            f"          width: {w}\n"
            f"          height: {h}\n"
            f"        alignment: 0\n"
            f"        pivot: {{x: 0.5, y: 0.5}}\n"
            f"        border: {{x: 0, y: 0, z: 0, w: 0}}\n")

def write_png_meta(path, ppu=256, sprite_mode=1, border=None, sheet_frames=0,
                   sheet_name=None, frame_w=None, frame_h=None, tex_h=None):
    """sheet_frames > 0 => multiple mode with a single row of frames (bottom-left origin)."""
    sprites_yaml = ""
    if sheet_frames > 0:
        entries = []
        for i in range(sheet_frames):
            entries.append(_sprite_entry(f"{sheet_name}_{i}", i * frame_w, 0,
                                         frame_w, frame_h, _guid()))
        sprites_yaml = "\n" + "".join(entries)
    b = border or (0, 0, 0, 0)
    meta = PNG_META.format(guid=_guid(), ppu=int(ppu), sprite_mode=sprite_mode,
                           bx=b[0], by=b[1], bz=b[2], bw=b[3], sprites_yaml=sprites_yaml)
    with open(path, 'w') as f:
        f.write(meta)

WAV_META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    loadType: {load_type}
    sampleRateSetting: 0
    overrideSampleRate: 0
    sampleRateOptimize: 1
    forceToMono: 1
    normalize: 1
    preloadAudioData: 1
    loadInBackground: 0
    ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def write_wav_meta(path, loop=False):
    # loadType 0 = decompress on load (short sfx). Long loops use 1 (streaming).
    lt = 1 if loop else 0
    with open(path, 'w') as f:
        f.write(WAV_META.format(guid=_guid(), load_type=lt))

# ---------------------------------------------------------------- wav writer
def write_wav(path, samples, rate=22050):
    import wave
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = bytearray()
    for s in samples:
        v = max(-1.0, min(1.0, s))
        data += struct.pack('<h', int(v * 32767))
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(bytes(data))
