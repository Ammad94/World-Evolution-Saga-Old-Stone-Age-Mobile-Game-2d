#!/usr/bin/env python3
"""Offline replication of WorldMap.cs (Unity) for seed diagnostics.

Replicates:
  * System.Random(seed).NextDouble()  (Knuth subtractive, .NET compatible path)
  * Mathf.PerlinNoise (Ken Perlin improved noise, 3D at z=0, mapped to 0..1)
  * WorldMap.Sample / Elevation / Moisture / IsRiver / Classify
  * WorldMap.FindSpawnTile / IsGoodSpawn
  * TribeCampSystem.PlaceCamps sampling
"""
import math

MBIG = 2147483647
MSEED = 161803398

class NetRandom:
    def __init__(self, seed):
        seed = seed & 0xFFFFFFFF
        if seed >= 0x80000000:
            seed -= 0x100000000  # signed int32
        subtraction = (2147483647 if seed == -2147483648 else abs(seed))
        self.sa = [0] * 56
        mj = MSEED - subtraction
        self.sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            self.sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += MBIG
            mj = self.sa[ii]
        for _k in range(1, 5):
            for i in range(1, 56):
                self.sa[i] -= self.sa[1 + (i + 30) % 55]
                if self.sa[i] < 0:
                    self.sa[i] += MBIG
        self.inext = 0
        self.inextp = 21

    def _internal(self):
        self.inext += 1
        if self.inext >= 56: self.inext = 1
        self.inextp += 1
        if self.inextp >= 56: self.inextp = 1
        ret = self.sa[self.inext] - self.sa[self.inextp]
        if ret == MBIG: ret -= 1
        if ret < 0: ret += MBIG
        self.sa[self.inext] = ret
        return ret

    def next_double(self):
        return self._internal() * (1.0 / MBIG)

# ---------------- Ken Perlin improved noise (reference) ----------------
PERM = [151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180]
P = PERM * 2

def fade(t): return t * t * t * (t * (t * 6 - 15) + 10)
def lerp(t, a, b): return a + t * (b - a)
def grad(hash_, x, y, z):
    h = hash_ & 15
    u = x if h < 8 else y
    v = y if h < 4 else (x if (h == 12 or h == 14) else z)
    return (u if (h & 1) == 0 else -u) + (v if (h & 2) == 0 else -v)

def perlin3(x, y, z):
    fx = math.floor(x); fy = math.floor(y); fz = math.floor(z)
    X = int(fx) & 255; Y = int(fy) & 255; Z = int(fz) & 255
    x -= fx; y -= fy; z -= fz
    u = fade(x); v = fade(y); w = fade(z)
    A = P[X] + Y; AA = P[A] + Z; AB = P[A + 1] + Z
    B = P[X + 1] + Y; BA = P[B] + Z; BB = P[B + 1] + Z
    return lerp(w, lerp(v, lerp(u, grad(P[AA], x, y, z), grad(P[BA], x - 1, y, z)),
                           lerp(u, grad(P[AB], x, y - 1, z), grad(P[BB], x - 1, y - 1, z))),
                   lerp(v, lerp(u, grad(P[AA + 1], x, y, z - 1), grad(P[BA + 1], x - 1, y, z - 1)),
                           lerp(u, grad(P[AB + 1], x, y - 1, z - 1), grad(P[BB + 1], x - 1, y - 1, z - 1))))

def perlin_unity(x, y):
    """Mathf.PerlinNoise approximation: improved noise at z=0 mapped to 0..1."""
    return (perlin3(x, y, 0.0) + 1.0) / 2.0

def fbm(x, y, octaves):
    ssum = 0.0; amp = 0.5; freq = 1.0; norm = 0.0
    for _ in range(octaves):
        ssum += perlin_unity(x * freq, y * freq) * amp
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return ssum / norm if norm > 0 else 0.0

# ---------------- Unity Mathf helpers ----------------
def clamp01(v): return 0.0 if v < 0 else (1.0 if v > 1 else v)
def inv_lerp(a, b, v):
    if a != b:
        t = (v - a) / (b - a)
        return 0.0 if t < 0 else (1.0 if t > 1 else t)
    return 0.0
def repeat(t, length):
    return t - math.floor(t / length) * length

# ---------------- WorldMap ----------------
OCEAN, SHALLOW, BEACH, GLACIER, TUNDRA, TAIGA, TEMPFOREST, GRASS, STEPPE, DESERT, SAVANNA, RAINFOREST, SWAMP, MOUNTAIN, SNOWPEAK = range(15)
BIOME_NAMES = ["Ocean","ShallowWater","Beach","Glacier","Tundra","Taiga","TemperateForest","Grassland","Steppe","Desert","Savannah","Rainforest","Swamp","Mountain","SnowPeak"]

class Landmass:
    def __init__(self, name, cx, cy, rx, ry, s):
        self.name = name; self.cx = cx; self.cy = cy; self.rx = rx; self.ry = ry; self.s = s

CONTINENTS = [
    Landmass("North America",   0.160, 0.740, 0.115, 0.150, 1.00),
    Landmass("Central America", 0.215, 0.585, 0.030, 0.045, 0.75),
    Landmass("South America",   0.265, 0.360, 0.065, 0.135, 1.00),
    Landmass("Greenland",       0.325, 0.900, 0.040, 0.048, 0.85),
    Landmass("Europe",          0.520, 0.790, 0.070, 0.062, 0.90),
    Landmass("Africa",          0.535, 0.520, 0.095, 0.150, 1.00),
    Landmass("Arabia",          0.605, 0.600, 0.038, 0.042, 0.75),
    Landmass("Siberia",         0.720, 0.810, 0.165, 0.080, 0.95),
    Landmass("Central Asia",    0.680, 0.700, 0.110, 0.070, 0.95),
    Landmass("India",           0.690, 0.585, 0.045, 0.055, 0.85),
    Landmass("East Asia",       0.790, 0.690, 0.070, 0.075, 0.90),
    Landmass("Sundaland",       0.795, 0.500, 0.055, 0.030, 0.70),
    Landmass("Australia",       0.850, 0.330, 0.070, 0.055, 0.90),
    Landmass("Beringia",        0.935, 0.800, 0.045, 0.050, 0.70),
]
MOUNTAINS = [
    Landmass("Rockies",   0.130, 0.740, 0.022, 0.130, 1.0),
    Landmass("Andes",     0.230, 0.360, 0.016, 0.130, 1.0),
    Landmass("Alps",      0.530, 0.760, 0.050, 0.014, 0.8),
    Landmass("Atlas",     0.505, 0.640, 0.040, 0.010, 0.6),
    Landmass("Himalaya",  0.690, 0.650, 0.070, 0.016, 1.2),
    Landmass("Urals",     0.620, 0.800, 0.010, 0.070, 0.7),
    Landmass("GreatDivide",0.895,0.320, 0.012, 0.050, 0.6),
]

def blob(uv, lm):
    dx = abs(uv[0] - lm.cx)
    if dx > 0.5: dx = 1.0 - dx
    dx /= max(0.0001, lm.rx)
    dy = (uv[1] - lm.cy) / max(0.0001, lm.ry)
    d = (abs(dx) ** 2.3 + abs(dy) ** 2.3) ** (1.0 / 2.3)
    return clamp01(1.0 - d)

class WorldMap:
    def __init__(self, seed=20260827, w=16384, h=8192, sea_level=0.42, raggedness=0.55):
        self.seed = seed; self.w = w; self.h = h
        self.sea = sea_level; self.rag = raggedness
        rng = NetRandom(seed)
        self.oxA = rng.next_double() * 10000; self.oyA = rng.next_double() * 10000
        self.oxB = rng.next_double() * 10000; self.oyB = rng.next_double() * 10000
        self.oxC = rng.next_double() * 10000; self.oyC = rng.next_double() * 10000
        self.oxD = rng.next_double() * 10000; self.oyD = rng.next_double() * 10000

    def elevation(self, uv, wx, wy):
        warpU = (fbm(wx * 0.00035 + self.oxA, wy * 0.00035 + self.oyA, 4) - 0.5) * 0.16
        warpV = (fbm(wx * 0.00035 + self.oxD, wy * 0.00035 + self.oyD, 4) - 0.5) * 0.12
        wuv = (repeat(uv[0] + warpU, 1.0), clamp01(uv[1] + warpV))
        land = 0.0
        for c in CONTINENTS:
            land = max(land, blob(wuv, c) * c.s)
        polar = inv_lerp(0.085, 0.02, uv[1])
        polar = max(polar, inv_lerp(0.955, 0.995, uv[1]))
        land = max(land, polar * 0.95)
        warp = fbm(wx * 0.0012 + self.oxA, wy * 0.0012 + self.oyA, 5)
        detail = fbm(wx * 0.010 + self.oxB, wy * 0.010 + self.oyB, 4)
        land += (warp - 0.5) * self.rag
        land += (detail - 0.5) * 0.10
        island = fbm(wx * 0.0035 + self.oxD, wy * 0.0035 + self.oyD, 3)
        if island > 0.80: land = max(land, (island - 0.80) * 3.2)
        e = 0.16 + 0.68 * clamp01(land)
        mountains = 0.0
        for m in MOUNTAINS:
            mountains = max(mountains, blob(wuv, m) * m.s)
        if mountains > 0 and land > 0.25:
            ridged = 1.0 - abs(fbm(wx * 0.006 + 77, wy * 0.006 + 31, 4) * 2 - 1)
            e += mountains * (0.28 + ridged * 0.28)
        e += (fbm(wx * 0.025 + 500, wy * 0.025 + 500, 3) - 0.5) * 0.06
        return clamp01(e)

    def moisture(self, uv, wx, wy, e, is_water, lat):
        if is_water: return 1.0
        band = math.cos(lat * math.pi * 2.35) * 0.5 + 0.5
        m = 0.15 + (0.9 - 0.15) * band
        m += (fbm(wx * 0.0018 + self.oxB, wy * 0.0018 + self.oyB, 4) - 0.5) * 0.55
        m -= max(0.0, e - 0.72) * 1.2
        return clamp01(m)

    def is_river(self, wx, wy, e):
        if e < self.sea + 0.01 or e > 0.88: return False
        n = fbm(wx * 0.0045 + 913, wy * 0.0045 + 271, 4)
        ridge = abs(n - 0.5)
        thr = 0.010 + (0.0035 - 0.010) * inv_lerp(0.85, self.sea, e)
        return ridge < thr

    def classify(self, is_r, is_w, e, t, m):
        if is_r: return SHALLOW
        if is_w:
            if t < -6: return GLACIER
            return SHALLOW if e > self.sea - 0.045 else OCEAN
        if e < self.sea + 0.012 and t > -2: return BEACH
        if e > 0.90: return SNOWPEAK
        if e > 0.80: return SNOWPEAK if t < -12 else MOUNTAIN
        if t < -14: return GLACIER
        if t < -4: return TUNDRA
        if t < 4: return TAIGA if m > 0.42 else TUNDRA
        if t < 18:
            if m < 0.22: return STEPPE
            if m < 0.48: return GRASS
            return TEMPFOREST
        if m < 0.18: return DESERT
        if m < 0.42: return SAVANNA
        if m < 0.72: return RAINFOREST
        return SWAMP if e < self.sea + 0.05 else RAINFOREST

    def sample(self, x, y):
        wx = x % self.w
        wy = max(0, min(self.h - 1, y))
        uv = (wx / self.w, wy / self.h)
        e = self.elevation(uv, wx, wy)
        is_w = e < self.sea
        lat = abs(uv[1] - 0.5) * 2
        t = 30 - 58 * (lat ** 2.6)
        if not is_w:
            above = max(0.0, e - self.sea) / (1 - self.sea)
            t -= above * 38
        t += (fbm(wx * 0.0009 + self.oxC, wy * 0.0009 + self.oyC, 3) - 0.5) * 8
        m = self.moisture(uv, wx, wy, e, is_w, lat)
        is_r = (not is_w) and self.is_river(wx, wy, e)
        biome = self.classify(is_r, is_w or is_r, e, t, m)
        return dict(e=e, t=t, m=m, biome=biome, water=is_w or is_r, river=is_r)

    def is_good_spawn(self, x, y):
        s = self.sample(x, y)
        if s["water"]: return False
        if s["biome"] in (MOUNTAIN, SNOWPEAK): return False
        if s["t"] < 5 or s["t"] > 34: return False
        if s["m"] < 0.25: return False
        return True

    def find_spawn_tile(self):
        ox = round(0.565 * self.w); oy = round(0.545 * self.h)
        r = 0
        while r < 5000:
            for a in range(32):
                ang = a / 32.0 * math.pi * 2
                x = ox + round(math.cos(ang) * r); y = oy + round(math.sin(ang) * r)
                if self.is_good_spawn(x, y): return (x % self.w, max(0, min(self.h-1, y)))
            r += 40
        best = (ox, oy); bestE = -1.0
        for y in range(self.h // 6, self.h * 5 // 6, 64):
            for x in range(0, self.w, 64):
                e = self.sample(x, y)["e"]
                if e > bestE: bestE = e; best = (x % self.w, y)
                if self.is_good_spawn(x, y): return (x % self.w, y)
        for y in range(self.h // 8, self.h * 7 // 8, 48):          # pass 3: any walkable land
            for x in range(0, self.w, 48):
                s = self.sample(x, y)
                if not s["water"] and s["biome"] not in (MOUNTAIN, SNOWPEAK) and s["t"] > -10:
                    return (x % self.w, y)
        for y in range(self.h // 12, self.h * 11 // 12, 32):       # pass 4: any dry land
            for x in range(0, self.w, 32):
                if not self.sample(x, y)["water"]: return (x % self.w, y)
        return best

    def try_find_nearest_land(self, wx, wy, max_radius=8000):
        """Mirror of WorldMap.TryFindNearestLand. Returns (tile, samples_taken) or None."""
        if not self.sample(wx, wy)["water"]: return ((wx, wy), 1)
        step = 4; samples = 1
        radius = step
        while radius <= max_radius:
            points = max(16, min(256, round(math.pi * 2 * radius / step)))
            for a in range(points):
                ang = a / points * math.pi * 2
                x = wx + round(math.cos(ang) * radius); y = wy + round(math.sin(ang) * radius)
                samples += 1
                if not self.sample(x, y)["water"]:
                    return ((x % self.w, max(0, min(self.h - 1, y))), samples)
            if radius >= 64: step = min(step * 2, 512)
            radius += step
        return (None, samples)


def simulate_camp_placement(m, origin, camp_count=3):
    """Mirror of TribeCampSystem.PlaceCamps (anchor + phases + sweep). Pure python Random."""
    import random
    rng = random.Random(1234)
    anchor = origin
    s0 = m.sample(origin[0], origin[1])
    if s0["water"]:
        found = m.try_find_nearest_land(origin[0], origin[1], 8000)
        if found and found[0]:
            anchor = found[0]
            print(f"  anchor moved to nearest land {found[0]} "
                  f"({math.hypot(origin[0]-anchor[0], origin[1]-anchor[1]):.0f} tiles away, "
                  f"{found[1]} samples)")
        else:
            print("  no land within 8000 tiles!")
    camps = []
    def try_place(c):
        s = m.sample(c[0], c[1])
        if s["water"]: return False
        if math.hypot(c[0] - anchor[0], c[1] - anchor[1]) < 0: return False
        for x, y in camps:
            if math.hypot(c[0]-x, c[1]-y) < 60: return False
        camps.append(c); return True
    for phase in range(3):
        rmax = 130 * (1, 3, 6)[phase]
        strict = phase == 0
        for _ in range(250):
            if len(camps) >= camp_count: break
            ang = rng.uniform(0, math.pi * 2); dist = rng.uniform(40, rmax)
            c = (anchor[0] + math.cos(ang) * dist, anchor[1] + math.sin(ang) * dist)
            s = m.sample(c[0], c[1])
            if s["water"]: continue
            if strict and s["biome"] in (MOUNTAIN, SNOWPEAK): continue
            if any(math.hypot(c[0]-x, c[1]-y) < 60 for x, y in camps): continue
            camps.append(c)
        if len(camps) >= camp_count: break
    if len(camps) < camp_count:                                 # final sweep
        step = 20
        radius = 40
        while radius <= 6000 and len(camps) < camp_count:
            points = max(24, min(180, round(math.pi * 2 * radius / step)))
            for a in range(points):
                if len(camps) >= camp_count: break
                ang = a / points * math.pi * 2
                c = (anchor[0] + math.cos(ang) * radius, anchor[1] + math.sin(ang) * radius)
                s = m.sample(c[0], c[1])
                if s["water"]: continue
                if any(math.hypot(c[0]-x, c[1]-y) < 60 for x, y in camps): continue
                camps.append(c)
            if radius >= 200: step = 40
            radius += step
    return camps


if __name__ == "__main__":
    import sys
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 20260827
    m = WorldMap(seed)
    print(f"seed {seed}: offsets A=({m.oxA:.1f},{m.oyA:.1f}) B=({m.oxB:.1f},{m.oyB:.1f}) C=({m.oxC:.1f},{m.oyC:.1f}) D=({m.oxD:.1f},{m.oyD:.1f})")
    ox = round(0.565 * m.w); oy = round(0.545 * m.h)
    s0 = m.sample(ox, oy)
    print(f"East Africa origin tile ({ox},{oy}): biome={BIOME_NAMES[s0['biome']]} water={s0['water']} e={s0['e']:.3f} t={s0['t']:.1f} m={s0['m']:.2f}")
    sp = m.find_spawn_tile()
    print(f"FindSpawnTile -> {sp}")
    ssp = m.sample(sp[0], sp[1])
    print(f"  spawn biome={BIOME_NAMES[ssp['biome']]} water={ssp['water']} t={ssp['t']:.1f} m={ssp['m']:.2f}")

    print("\n[scenario 1] fresh spawn camp placement:")
    camps = simulate_camp_placement(m, sp)
    print(f"  -> {len(camps)} camps placed at {[f'({c[0]:.0f},{c[1]:.0f})' for c in camps]}")

    print("\n[scenario 2] player stranded mid-Pacific (old save):")
    stranded = (round(0.05 * m.w), round(0.50 * m.h))
    s2 = m.sample(stranded[0], stranded[1])
    print(f"  stranded at {stranded}: {BIOME_NAMES[s2['biome']]} water={s2['water']}")
    camps = simulate_camp_placement(m, stranded)
    print(f"  -> {len(camps)} camps placed at {[f'({c[0]:.0f},{c[1]:.0f})' for c in camps]}")

    print("\n[scenario 3] rescue scan cost from mid-Pacific:")
    res = m.try_find_nearest_land(stranded[0], stranded[1], 8000)
    if res and res[0]:
        print(f"  nearest land {res[0]} ({math.hypot(stranded[0]-res[0][0], stranded[1]-res[0][1]):.0f} tiles, {res[1]} samples)")
        ls = m.sample(res[0][0], res[0][1])
        print(f"  land biome={BIOME_NAMES[ls['biome']]} water={ls['water']}")
    else:
        print(f"  none found ({res[1] if res else '?'} samples)")
