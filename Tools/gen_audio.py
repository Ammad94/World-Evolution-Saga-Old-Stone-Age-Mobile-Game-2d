#!/usr/bin/env python3
"""
World Evolution Saga — realistic audio generator (v2).

Complete replacement of the old chiptune-ish synth: 44.1 kHz layered sound
design (noise bodies, resonant impacts, formant creature voices, convolution
-free Schroeder reverb, seamless loop crossfades) and fully composed tribal
paleolithic music.

  * sfx      -> WAV 44.1 kHz mono   (short one-shots, 3D friendly)
  * music    -> OGG stereo          (long loops, small on disk)
  * ambience -> OGG stereo          (long loops, small on disk)

Same filenames as before, so runtime Resources.Load paths are unchanged.
Pure numpy + soundfile. Deterministic (seeded).
"""
import os, sys, math, random
import numpy as np
import soundfile as sf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Audio')
SR = 44100
RNG = random.Random(20260828)
NPG = np.random.default_rng(20260828)

AUDIO_META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    loadType: {load}
    sampleRateSetting: 0
    overrideSampleRate: 0
    sampleRateOptimize: 1
    forceToMono: {mono}
    normalize: 1
    preloadAudioData: 1
    loadInBackground: 0
    ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""

import re as _re, uuid as _uuid

def _audio_meta(path, mono=1, compressed=False):
    """Create or patch an audio .meta (music/ambience loops stream from disk)."""
    load = 2 if compressed else 1   # 0 decrompress-on-load, 1 compressed-in-memory, 2 streaming
    if compressed:
        load = 1  # small ogg loops: keep in memory for gapless looping
    if os.path.exists(path):
        src = open(path, encoding='utf-8').read()
        guid = _re.search(r'^guid: ([0-9a-f]{32})', src, _re.M)
        guid = guid.group(1) if guid else _uuid.uuid4().hex
        src = _re.sub(r'^guid: [0-9a-f]+', 'guid: ' + guid, src, count=1, flags=_re.M)
        src = _re.sub(r'forceToMono: \d+', f'forceToMono: {mono}', src)
        open(path, 'w', encoding='utf-8').write(src)
    else:
        open(path, 'w', encoding='utf-8').write(
            AUDIO_META.format(guid=_uuid.uuid4().hex, mono=mono, load=load))

def save_sfx(name, sig, peak=0.85):
    """Mono WAV under sfx/."""
    sig = np.asarray(sig, dtype=np.float64)
    if sig.ndim == 2:
        sig = sig.mean(axis=1)
    m = np.max(np.abs(sig)) or 1.0
    sig = sig * (peak / m)
    path = os.path.join(ROOT, 'sfx', name + '.wav')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, sig, SR, subtype='PCM_16', format='WAV')
    _audio_meta(path + '.meta', mono=1)

def save_loop(name, sig, peak=0.8):
    """Stereo OGG under music/ or ambience/ (name includes subfolder)."""
    sig = np.asarray(sig, dtype=np.float64)
    if sig.ndim == 1:
        sig = np.stack([sig, sig], axis=1)
    m = np.max(np.abs(sig)) or 1.0
    sig = sig * (peak / m)
    sub = 'ambience' if name.startswith('amb') else 'music'
    fname = name.split('/')[-1]
    path = os.path.join(ROOT, sub, fname + '.ogg')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    # NOTE: one-shot sf.write of long OGGs segfaults this libsndfile build;
    # writing in chunks is stable.
    with sf.SoundFile(path, 'w', SR, 2, format='OGG', subtype='VORBIS') as f:
        for i in range(0, len(sig), SR * 5):
            f.write(sig[i:i + SR * 5])
    _audio_meta(path + '.meta', mono=0, compressed=True)
    wav = path.replace('.ogg', '.wav')
    if os.path.exists(wav):
        os.remove(wav)
        if os.path.exists(wav + '.meta'):
            os.remove(wav + '.meta')

# ================================================================== DSP kit
def t(n):      return np.arange(n) / SR
def sec(d):    return int(SR * d)

def white(d):
    return NPG.standard_normal(sec(d))

def pink(d, n=7):
    x = white(d)
    out = np.zeros_like(x)
    for i in range(n):
        b = 2 ** i
        out += np.cumsum(np.where(np.arange(len(x)) % b == 0, x * b, 0)) / b
    out -= out.mean()
    return out / (np.abs(out).max() or 1)

def brown(d):
    x = white(d)
    return np.cumsum(x) * 0.02

def onepole_lp(x, cutoff):
    a = math.exp(-2 * math.pi * cutoff / SR)
    y = np.empty_like(x); acc = 0.0
    for i, v in enumerate(x):
        acc += (1 - a) * (v - acc)
        y[i] = acc
    return y

def onepole_hp(x, cutoff):
    return x - onepole_lp(x, cutoff)

def biquad(x, f0, q=0.707, kind='lp'):
    w0 = 2 * math.pi * f0 / SR
    cos, sin = math.cos(w0), math.sin(w0)
    a = sin / (2 * q)
    b0, b1, b2, a0, a1, a2 = (None,) * 6
    if kind == 'lp':
        b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = b0
    elif kind == 'hp':
        b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = b0
    elif kind == 'bp':
        b0 = q * a * 2; b1 = 0; b2 = -b0
    a0 = 1 + a; a1 = -2 * cos; a2 = 1 - a
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        out = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        y[i] = out
        x2, x1, y2, y1 = x1, v, y1, out
    return y

def bp(x, f0, q=1.0):  return biquad(x, f0, q, 'bp')
def lp(x, f0, q=0.707): return biquad(x, f0, q, 'lp')
def hp(x, f0, q=0.707): return biquad(x, f0, q, 'hp')

def env_ad(d, a=0.005, r=None, curve=2.0):
    n = sec(d); r = r if r is not None else d
    e = np.ones(n)
    na = min(n - 1, sec(a))
    e[:na] = np.linspace(0, 1, na)
    nr = min(n - 1, sec(r))
    e[-nr:] *= np.linspace(1, 0, nr) ** (1 / curve)
    return e

def perc(d, decay=0.08, curve=3.0):
    n = sec(d)
    return np.exp(-np.arange(n) / (SR * decay / curve))

def gain(x, g): return x * g
def S(*sigs):
    """Length-safe sum: pads shorter layers to the longest (mix at t=0)."""
    return mix(*sigs)
def mix(*sigs):
    n = max(len(s) for s in sigs)
    out = np.zeros(n)
    for s in sigs:
        out[:len(s)] += s
    return out

def mix_at(dst, src, at_s, g=1.0):
    at = sec(at_s)
    need = at + len(src)
    if need > len(dst):
        dst = np.concatenate([dst, np.zeros(need - len(dst))])
    dst[at:at + len(src)] += src * g
    return dst

def pan(sig, p):   # p in [-1, 1]
    th = (p + 1) * math.pi / 4
    return np.stack([sig * math.cos(th), sig * math.sin(th)], axis=1)

def stereo(sig, width=0.2, delay_ms=7):
    d = sec(delay_ms / 1000)
    r = np.zeros_like(sig)
    r[d:] = sig[:-d] if d else sig
    mid, side = sig, (r - sig) * width
    return np.stack([mid + side, mid - side], axis=1)

def reverb(sig, wet=0.25, spread=0.023):
    """Small Schroeder reverb, stereo output."""
    combs = [0.0297, 0.0371, 0.0411, 0.0437]
    aps = [0.005, 0.0017]
    l = r = sig
    dl = sec(spread); dr = 0
    for i, c in enumerate(combs):
        fb = 0.77
        d = sec(c)
        for ch, src in ((0, l), (1, r)):
            y = np.zeros(len(src) + d * 8)
            base = src.astype(np.float64)
            for k in range(8):
                seg = base * (fb ** k)
                y[k * d:k * d + len(base)] += seg
            if ch == 0: l = l + y[:len(l)] * 0.25
            else:       r = r + y[:len(r)] * 0.25
    for a in aps:
        d = sec(a)
        for arr in (l, r):
            y = np.zeros_like(arr)
            y[d:] = arr[:-d] if d else arr
            arr = -arr + y * 0.7
            if id(arr) == id(l): l = arr
            else: r = arr
    # simple Allpass via numpy convolution-free approximation above; mix wet/dry
    n = max(len(l), len(r))
    L = np.zeros(n); L[:len(l)] = l
    R = np.zeros(n); R[:len(r)] = r
    dryL, dryR = sig, sig
    outL = dryL * (1 - wet) + L * wet * 2.2
    outR = dryR * (1 - wet) + R * wet * 2.2
    return np.stack([outL, outR], axis=1)

def sine(d, f0, f1=None, ph0=0.0):
    n = sec(d)
    f1 = f0 if f1 is None else f1
    freqs = np.linspace(f0, f1, n)
    ph = ph0 + 2 * np.pi * np.cumsum(freqs) / SR
    return np.sin(ph)

def saw(d, f0, f1=None):
    n = sec(d)
    f1 = f0 if f1 is None else f1
    freqs = np.linspace(f0, f1, n)
    ph = np.cumsum(freqs) / SR
    return 2 * (ph % 1.0) - 1

def glottal(d, f0, f1=None, jitter=0.0, seed=0):
    """Pulse-ish voice source with optional pitch jitter."""
    n = sec(d)
    f1 = f0 if f1 is None else f1
    rng = np.random.default_rng(seed)
    freqs = np.linspace(f0, f1, n)
    if jitter:
        freqs *= 1 + rng.normal(0, jitter, n)
    ph = np.cumsum(freqs) / SR
    frac = ph % 1.0
    pulse = np.exp(-frac * 12) * 2 - 1     # sharp decay pulse
    pulse += np.sin(2 * np.pi * ph) * 0.4
    return pulse

def formant_voice(d, f0, f1, formants, seed=0, breath=0.06, jitter=0.01, vib=0.0, vib_hz=5.5):
    src = glottal(d, f0, f1, jitter=jitter, seed=seed)
    rng = np.random.default_rng(seed + 1)
    src = src + white(d) * breath
    if vib:
        n = sec(d)
        f = np.linspace(f0, f1, n) * (1 + 0.02 * np.sin(2 * math.pi * vib_hz * n / SR))
        src = src  # (vibrato folded into f0 wobble below)
    out = np.zeros(sec(d))
    for i, (fm, g, q) in enumerate(formants):
        out += bp(src, fm, q) * g
    return out

def make_loop(sig, xfade=1.2):
    """Crossfade tail into head for a seamless loop."""
    if sig.ndim == 1:
        sig = sig[:, None]
    n = sec(xfade)
    head = sig[:n].copy()
    tail = sig[-n:]
    ramp = np.linspace(0, 1, n)[:, None]
    sig[-n:] = tail * (1 - ramp) + head * ramp
    return sig[:-n]

def stretch_repeat(sig, total):
    """Repeat (with small variations off) to reach total length."""
    out = np.zeros(total)
    pos = 0
    while pos < total:
        chunk = sig[:total - pos]
        out[pos:pos + len(chunk)] += chunk
        pos += len(sig)
    return out

def crackle(dur, rate=18, fmin=800, fmax=3200, seed=0):
    rng = np.random.default_rng(seed)
    out = np.zeros(sec(dur))
    n = rng.poisson(rate * dur)
    for _ in range(n):
        at = rng.uniform(0, dur - 0.05)
        f = rng.uniform(fmin, fmax)
        amp = rng.uniform(0.3, 1.0)
        pop = white(0.03) * perc(0.03, 0.012)
        pop = bp(pop, f, 2.5) * amp
        out = mix_at(out, pop, at)
    return out

# ================================================================== SFX
MAT = {  # material noise band (center, Q), body freq
    'dirt':   ((900, 0.8), 70),  'grass':  ((2600, 0.7), 75),
    'mud':    ((500, 1.2), 60),  'sand':   ((1500, 0.6), 65),
    'shallow':((1000, 0.9), 70), 'snow':   ((2400, 0.5), 68),
    'stone':  ((1400, 1.4), 85), 'wood':   ((700, 2.0), 110),
}

def sfx_step(mat, v):
    (fc, q), body = MAT[mat]
    d = 0.16
    thump = sine(d, body * 1.6, body * 0.7) * perc(d, 0.05) * 0.9
    scr = white(d)
    if mat == 'grass': scr = hp(scr, 1800)
    elif mat == 'snow': scr = lp(scr, 3400) * 1.4
    elif mat == 'stone': scr = hp(scr, 900)
    else: scr = bp(scr, fc, q)
    scr *= perc(d, 0.045)
    if mat == 'mud':  # squelch sweep
        scr = scr + bp(white(d), 700, 3) * np.linspace(1.4, 0.4, sec(d)) * perc(d, 0.06)
    out = thump * 0.8 + scr * 0.5
    return out * (0.85 + 0.3 * RNG.random())

def sfx_chop(v):
    d = 0.34
    knock = bp(white(d), 900, 1.2) * perc(d, 0.1) * 0.9
    res = sine(0.3, 260, 190) * perc(0.3, 0.09) * 0.6
    crack = hp(white(0.05), 2000) * perc(0.05, 0.012) * 0.5
    thump = sine(0.15, 120, 70) * perc(0.15, 0.06) * 0.8
    return S(knock, res, crack, thump * 0.7)

def sfx_mine(v):
    d = 0.3
    spark = hp(white(0.03), 3500) * perc(0.03, 0.008) * 0.7
    ring = sine(0.28, 2400, 2100) * perc(0.28, 0.06) * 0.12
    thock = bp(white(0.25), 1200, 1.5) * perc(0.25, 0.05)
    thump = sine(0.12, 130, 75) * perc(0.12, 0.05) * 0.9
    return S(spark, ring, thock, thump)

def sfx_dig(v):
    d = 0.4
    scrape = bp(white(0.3), 700, 0.6) * env_ad(0.3, 0.03, 0.2)
    grit = crackle(0.3, 24, 900, 2600, seed=v) * 0.3
    thump = sine(0.12, 100, 60) * perc(0.12, 0.06) * 0.7
    return S(scrape * 0.6, grit, thump)

def sfx_hammer(v):
    d = 0.22
    ring = (sine(d, 1700, 1500) * 0.25 + sine(d, 2600, 2400) * 0.12) * perc(d, 0.07)
    strike = bp(white(d), 1500, 1.0) * perc(d, 0.04)
    return strike + ring

def sfx_sharpen(v):
    d = 0.5
    metal = bp(white(d), 3000 + v * 400, 4) * env_ad(d, 0.02, 0.4)
    ring = sine(d, 3800, 3600) * perc(d, 0.2) * 0.05
    return metal * 0.8 + ring

def sfx_skin(v):
    d = 0.4
    wet = bp(white(d), 900, 0.8) * env_ad(d, 0.02, 0.3)
    peel = hp(white(0.12), 1200) * perc(0.12, 0.05) * 0.5
    return S(wet * 0.7, peel)

def sfx_knot(v):
    d = 0.22
    fiber = bp(white(d), 1800, 0.7) * env_ad(d, 0.01, 0.16)
    creak = sine(0.2, 300, 220) * perc(0.2, 0.08) * 0.2
    return S(fiber * 0.8, creak)

def sfx_swing(heavy, v):
    d = 0.30 + 0.1 * heavy
    whoosh = bp(white(d), 500, 0.9) * env_ad(d, 0.05, d * 0.7)
    sweep = bp(white(d), 1500, 1.4) * env_ad(d, 0.08, d * 0.8) * 0.5
    return (whoosh + sweep) * (1.2 if heavy else 0.9)

def sfx_hit_flesh(v):
    d = 0.22
    thud = sine(d, 160, 60) * perc(d, 0.05) * 1.1
    squish = bp(white(d), 600, 0.9) * perc(d, 0.04) * 0.6
    return thud + squish

def sfx_hit_stone(v):
    d = 0.2
    crack = hp(white(0.025), 2600) * perc(0.025, 0.006) * 0.9
    chip = bp(white(0.04), 3400, 2) * perc(0.04, 0.01) * 0.5
    thud = sine(0.1, 190, 90) * perc(0.1, 0.04) * 0.7
    return S(crack, chip, thud)

def sfx_body_fall(v):
    d = 0.6
    thud = sine(d, 110, 45) * perc(d, 0.09) * 1.2
    debris = crackle(0.4, 20, 500, 1800, seed=90 + v) * 0.35
    return S(thud, debris)

def sfx_bone_crack():
    out = np.zeros(sec(0.5))
    for i, at in enumerate([0.0, 0.09, 0.2, 0.33]):
        c = hp(white(0.02), 2200) * perc(0.02, 0.007)
        c = S(c, bp(white(0.03), 1400 - i * 150, 2) * perc(0.03, 0.012) * 0.7)
        out = mix_at(out, c * (1 - i * 0.15), at)
    return out

def sfx_heartbeat():
    out = np.zeros(sec(1.0))
    for at, g in ((0.0, 1.0), (0.28, 0.75)):
        b = sine(0.16, 65, 40) * perc(0.16, 0.07)
        out = mix_at(out, b * g, at)
    return out

def sfx_voice(kind, d, f0, f1, formants, breath=0.08, seed=0, jitter=0.02, extra_noise=None):
    v = formant_voice(d, f0, f1, formants, seed=seed, breath=breath, jitter=jitter)
    v = v * env_ad(d, min(0.08, d * 0.2), d * 0.8)
    if extra_noise is not None:
        v = v + extra_noise
    return v

HURT_F = [(650, 1.0, 4), (1150, 0.6, 6), (2600, 0.25, 8)]
def sfx_hurt(v):
    d = 0.35
    return sfx_voice('hurt', d, 190 + v * 30, 120, HURT_F, seed=v)

def sfx_effort(v):
    d = 0.4
    return sfx_voice('effort', d, 130, 170 + v * 20, [(500, 1, 4), (900, 0.5, 5)], breath=0.2, seed=20 + v)

def sfx_human_death():
    d = 1.1
    return sfx_voice('death', d, 170, 70, HURT_F, breath=0.15, seed=44)

def sfx_cough():
    out = np.zeros(sec(0.6))
    for at, g in ((0, 1), (0.22, 0.8)):
        c = bp(white(0.12), 700, 0.8) * env_ad(0.12, 0.005, 0.1)
        c = c + formant_voice(0.12, 140, 110, [(500, 1, 3)], seed=3) * 0.5
        out = mix_at(out, c * g, at)
    return out

def sfx_laugh():
    out = np.zeros(sec(1.3))
    for i in range(5):
        s = formant_voice(0.14, 150 - i * 4, 120, [(620, 1, 4), (1050, 0.5, 6)], seed=60 + i)
        out = mix_at(out, s * (1 - i * 0.13), i * 0.19)
    return out

def sfx_snore():
    out = np.zeros(sec(2.6))
    for i in range(3):
        inh = formant_voice(0.5, 90, 120, [(400, 1, 3), (800, 0.3, 5)], breath=0.35, seed=70 + i)
        out = mix_at(out, inh, i * 0.9)
        out = mix_at(out, bp(white(0.6), 500, 0.7) * env_ad(0.6, 0.05, 0.5) * 0.4, i * 0.9 + 0.5)
    return out

def sfx_eat(v):
    out = np.zeros(sec(0.7))
    rng = random.Random(v)
    for i in range(4):
        c = bp(white(0.07), 800 + rng.randint(-200, 300), 0.9) * perc(0.07, 0.03)
        out = mix_at(out, c, i * rng.uniform(0.1, 0.18))
    return out

def sfx_drink(v):
    out = np.zeros(sec(0.8))
    for i in range(3):
        glug = bp(white(0.12), 500, 2.0) * np.linspace(1.2, 0.5, sec(0.12)) * perc(0.12, 0.05)
        glug = glug + sine(0.12, 300, 180) * perc(0.12, 0.05) * 0.3
        out = mix_at(out, glug, i * 0.22)
    return out

def sfx_splash(size, v):
    d = 0.7 + 0.4 * size
    body = white(d)
    sweep_env = np.linspace(1, 0.25, sec(d))
    body = lp(body, 3500) * sweep_env * env_ad(d, 0.005, d * 0.8)
    droplets = crackle(d * 0.8, 26 - 8 * size, 1500, 5000, seed=v) * 0.3
    thump = sine(0.18, 220, 80) * perc(0.18, 0.06) * 0.7
    return S(body * (0.6 + 0.4 * size), droplets, thump * size)

def sfx_swim_stroke(v):
    d = 0.6
    swish = bp(white(d), 800, 0.8) * env_ad(d, 0.08, 0.4)
    ripple = hp(white(0.3), 1800) * perc(0.3, 0.08) * 0.3
    return S(swish, ripple * 0.6)

def sfx_dive():
    whoosh = bp(white(0.4), 900, 0.7) * env_ad(0.4, 0.02, 0.35)
    return S(whoosh, sfx_splash(0.7, 3))

def sfx_rowing(v):
    creak = sine(0.4, 350, 240) * env_ad(0.4, 0.05, 0.3) * 0.3
    pull = bp(white(0.5), 700, 0.7) * env_ad(0.5, 0.06, 0.4) * 0.6
    return S(creak, pull)

def sfx_raft_creak():
    out = np.zeros(sec(2.0))
    rng = random.Random(9)
    for i in range(4):
        f = 220 + rng.randint(0, 160)
        c = bp(white(0.3), f, 6) * env_ad(0.3, 0.04, 0.25) * 0.8
        c = S(c, sine(0.3, f, f * 0.8) * perc(0.3, 0.12) * 0.3)
        out = mix_at(out, c, rng.uniform(0, 1.6))
    return out

def sfx_campfire_loop():
    d = 9.0
    bed = lp(brown(d), 480) * 0.5
    bed = bed + lp(white(d), 2200) * 0.10
    cracks = crackle(d, 14, 700, 3600, seed=5) * 0.8
    cracks2 = crackle(d, 5, 300, 900, seed=6) * 0.5
    hiss = hp(white(d), 4000) * 0.05
    out = bed + cracks + cracks2 + hiss
    out = out * (1 + 0.15 * np.sin(np.linspace(0, 2 * math.pi * 3, sec(d))))
    return make_loop(out, 1.5)[:, 0]

def sfx_fire_ignite():
    d = 1.1
    whoosh = bp(white(d), 600, 0.6) * env_ad(d, 0.05, 0.9) * 0.9
    cracks = crackle(0.9, 30, 800, 4000, seed=8) * 0.6
    return S(whoosh, cracks)

def sfx_ember(v):
    return S(crackle(0.5, 3, 900, 3000, seed=10 + v) * 0.9, lp(white(0.5), 800) * 0.1)

def sfx_pickup(kind, v):
    d = 0.3
    # core: soft wooden/bone tap
    core = sine(d, 620 + v * 40, 480) * perc(d, 0.05) * 0.5
    core = S(core, bp(white(0.02), 1400, 1.5) * perc(0.02, 0.008) * 0.5)
    flavor = np.zeros(sec(d))
    if kind in ('wood',):
        flavor = bp(white(0.12), 900, 2) * perc(0.12, 0.04) * 0.4
    elif kind in ('stone', 'ore', 'flint', 'obsidian'):
        flavor = S(hp(white(0.05), 2400) * perc(0.05, 0.012) * 0.5, sine(0.1, 1800, 1500) * perc(0.1, 0.03) * 0.1)
    elif kind in ('fiber', 'hide', 'pelt', 'cloak'):
        flavor = hp(white(0.16), 1500) * env_ad(0.16, 0.01, 0.13) * 0.5
    elif kind in ('meat', 'berry', 'apple', 'carrot', 'egg', 'herb'):
        flavor = bp(white(0.1), 500, 0.9) * perc(0.1, 0.03) * 0.5
    elif kind in ('water',):
        flavor = bp(white(0.15), 800, 1.2) * env_ad(0.15, 0.01, 0.12) * 0.4
    elif kind in ('chime', 'save', 'quest'):
        flavor = sine(0.3, 1320, 1318) * perc(0.3, 0.12) * 0.3 + sine(0.3, 1980, 1975) * perc(0.3, 0.1) * 0.15
    return S(core, flavor)

def sfx_craft_tap():
    return S(bp(white(0.05), 1100, 1.2) * perc(0.05, 0.02), sine(0.06, 300, 200) * perc(0.06, 0.02) * 0.6)

def sfx_craft_start():
    out = np.zeros(sec(0.7))
    for i, at in enumerate([0, 0.18, 0.36]):
        out = mix_at(out, sfx_craft_tap() * (0.8 - i * 0.1), at)
    return out

def sfx_craft_complete():
    out = np.zeros(sec(0.9))
    for i, f in enumerate([520, 660, 880]):
        n = sine(0.25, f, f) * perc(0.25, 0.09) * 0.4
        n = S(n, bp(white(0.02), f * 3, 2) * perc(0.02, 0.006) * 0.2)
        out = mix_at(out, n, i * 0.13)
    return out

def sfx_equip():
    strap = hp(white(0.2), 1200) * env_ad(0.2, 0.01, 0.16) * 0.6
    buckle = bp(white(0.04), 2600, 2) * perc(0.04, 0.01) * 0.5
    return S(strap, buckle)

def sfx_era_up():
    out = np.zeros(sec(2.0))
    for i, f in enumerate([196, 262, 392]):
        horn = formant_voice(0.6, f, f * 1.01, [(420, 1, 3), (900, 0.4, 5), (1800, 0.2, 7)], breath=0.3, seed=i)
        out = mix_at(out, horn * 0.5, i * 0.35)
    drum = sine(0.4, 90, 55) * perc(0.4, 0.12)
    out = mix_at(out, drum, 1.3)
    return out

def sfx_ui(kind):
    d = 0.12
    if kind == 'hover':
        return bp(white(0.05), 2000, 2) * perc(0.05, 0.012) * 0.5
    if kind == 'click':
        tap = sine(0.09, 480, 380) * perc(0.09, 0.035) * 0.7
        return S(tap, bp(white(0.02), 1600, 1.5) * perc(0.02, 0.008) * 0.4)
    if kind == 'back':
        out = np.zeros(sec(0.3))
        out = mix_at(out, sine(0.08, 420, 340) * perc(0.08, 0.03) * 0.6, 0)
        out = mix_at(out, sine(0.08, 330, 260) * perc(0.08, 0.03) * 0.5, 0.11)
        return out
    if kind == 'error':
        out = np.zeros(sec(0.35))
        for i in range(2):
            out = mix_at(out, formant_voice(0.12, 130, 110, [(480, 1, 4)], seed=50) * 0.6, i * 0.15)
        return out
    if kind == 'page':
        return hp(white(0.18), 1400) * env_ad(0.18, 0.012, 0.14) * 0.5
    if kind == 'trade':
        out = np.zeros(sec(0.4))
        for i in range(3):
            out = mix_at(out, bp(white(0.03), 2200 + i * 300, 3) * perc(0.03, 0.01) * 0.4, i * 0.07)
        return out
    if kind == 'zoom_in':
        return bp(white(0.3), 700, 1.0) * env_ad(0.3, 0.04, 0.25)
    if kind == 'zoom_out':
        return bp(white(0.3), 1600, 1.0) * env_ad(0.3, 0.04, 0.25)
    if kind == 'wp_set':
        out = np.zeros(sec(0.4))
        out = mix_at(out, sine(0.15, 900, 1200) * perc(0.15, 0.07) * 0.3, 0)
        return S(out, bp(white(0.03), 2000, 2) * perc(0.03, 0.01) * 0.3)
    if kind == 'wp_clear':
        return sine(0.2, 1000, 600) * perc(0.2, 0.08) * 0.3
    return sfx_ui('click')

def sfx_weather(kind, loop=False):
    if kind == 'rain':
        d = 8.0 if loop else 4.0
        bed = hp(pink(d), 500) * 0.5
        drops = crackle(d, 40, 2000, 6000, seed=11) * 0.12
        out = bed + drops
    elif kind == 'rain_heavy':
        d = 8.0 if loop else 4.0
        bed = hp(pink(d), 380) * 0.85
        drops = crackle(d, 70, 1500, 6500, seed=12) * 0.2
        splash = crackle(d, 10, 400, 1400, seed=13) * 0.25
        out = bed + drops + splash
    elif kind == 'wind':
        d = 9.0 if loop else 5.0
        bed = bp(pink(d), 420, 0.5)
        lfo = 0.6 + 0.4 * np.sin(np.linspace(0, 2 * math.pi * (1.2 if loop else 0.8), sec(d)))
        out = bed * lfo
    elif kind == 'gust':
        d = 3.2
        out = bp(pink(d), 500, 0.6) * env_ad(d, 0.5, 2.4)
        out = out + bp(white(d), 1400, 0.8) * env_ad(d, 0.6, 2.2) * 0.4
    elif kind == 'howl':
        d = 3.5
        core = formant_voice(d, 240, 430, [(700, 1, 6), (1400, 0.4, 8)], seed=15, vib=0, breath=0.15)
        core *= env_ad(d, 0.7, 2.2)
        out = core + bp(white(d), 800, 1.2) * env_ad(d, 0.6, 2.4) * 0.3
    elif kind == 'blizzard':
        d = 9.0
        bed = bp(pink(d), 700, 0.5)
        hiss = hp(white(d), 3000) * 0.15
        lfo = 0.55 + 0.45 * np.sin(np.linspace(0, 2 * math.pi * 2.0, sec(d)) + 1.2)
        out = (bed + hiss) * lfo
    else:
        out = pink(4) * 0.4
    return make_loop(out, 1.8)[:, 0] if loop else out

def sfx_thunder(v):
    d = 3.5 + v
    rumble = lp(brown(d), 180) * env_ad(d, 0.12, d * 0.85) * 1.4
    body = lp(white(d), 400) * env_ad(d, 0.02, d * 0.7) * 0.7
    crack = hp(white(0.12), 1500) * perc(0.12, 0.02) * (0.6 if v < 2 else 1.0)
    return S(crack, body, rumble)

# ------------------------------------------------------------------ birds
def bird_chirp(v):
    rng = random.Random(100 + v)
    out = np.zeros(sec(0.5 + rng.random() * 0.4))
    notes = rng.randint(2, 5)
    for i in range(notes):
        f0 = rng.uniform(2400, 4200)
        f1 = f0 * rng.uniform(0.7, 1.5)
        d = rng.uniform(0.06, 0.16)
        n = sine(d, f0, f1) * env_ad(d, 0.01, d * 0.7)
        n = S(n, sine(d, f0 * 2, f1 * 2) * env_ad(d, 0.01, d * 0.7) * 0.2)
        out = mix_at(out, n, i * rng.uniform(0.06, 0.14))
    return out * 0.8

def bird_flap(v):
    out = np.zeros(sec(0.6))
    for i in range(4):
        w = bp(white(0.09), 900, 0.7) * env_ad(0.09, 0.012, 0.07)
        out = mix_at(out, w * (1 - i * 0.15), i * 0.11)
    return out

def owl_hoot(v):
    d = 0.5
    f = 340 + v * 30
    n = formant_voice(d, f, f * 0.96, [(420, 1, 5), (760, 0.4, 7)], seed=130 + v, breath=0.12)
    return n * env_ad(d, 0.06, 0.4)

def crow_caw():
    out = np.zeros(sec(0.8))
    for i in range(3):
        c = formant_voice(0.18, 640 - i * 30, 480, [(900, 1, 5), (1900, 0.5, 8)], seed=140 + i, breath=0.2)
        out = mix_at(out, c * (1 - i * 0.2), i * 0.2)
    return out

def hawk_screech():
    d = 1.1
    n = formant_voice(d, 1500, 900, [(2400, 1, 6), (3800, 0.5, 9)], seed=150, breath=0.3)
    return n * env_ad(d, 0.08, 0.95)

def ptarmigan(v):
    out = np.zeros(sec(0.7))
    rng = random.Random(160 + v)
    for i in range(rng.randint(3, 6)):
        c = formant_voice(0.09, 900, 750, [(1400, 1, 6)], seed=160 + v, breath=0.15)
        out = mix_at(out, c, i * 0.1)
    return out

def auk(v):
    d = 0.4
    return formant_voice(d, 300 + v * 20, 210, [(500, 1, 5), (900, 0.4, 7)], seed=170 + v, breath=0.2) * env_ad(d, 0.03, 0.35)

# ------------------------------------------------------------------ mammals
def mammal(kind, mood):
    """Formant-synthesised mammal voices."""
    cfg = {
        # f0, dur, formants, breath
        ('mammoth', 'call'):   (95, 1.6, [(280, 1, 3), (620, 0.5, 5), (1200, 0.2, 7)], 0.10),
        ('mammoth', 'growl'):  (70, 1.2, [(220, 1, 3), (500, 0.6, 5)], 0.18),
        ('mammoth', 'death'):  (95, 1.8, [(260, 1, 3), (560, 0.5, 5)], 0.2),
        ('bear', 'growl'):     (85, 1.4, [(300, 1, 3), (700, 0.5, 5), (1500, 0.2, 7)], 0.22),
        ('bear', 'huff'):      (110, 0.5, [(450, 1, 3), (1000, 0.4, 6)], 0.5),
        ('bear', 'roar'):      (95, 1.9, [(350, 1, 3), (800, 0.7, 5), (1700, 0.3, 7)], 0.3),
        ('bear', 'death'):     (85, 1.7, [(280, 1, 3), (650, 0.5, 5)], 0.28),
        ('bison', 'bellow'):   (130, 1.5, [(380, 1, 4), (820, 0.5, 6)], 0.15),
        ('bison', 'snort'):    (None, 0.4, None, 0),   # noise burst
        ('bison', 'death'):    (120, 1.6, [(320, 1, 4), (700, 0.5, 6)], 0.2),
        ('boar', 'grunt'):     (140, 0.3, [(480, 1, 4), (1100, 0.4, 6)], 0.25),
        ('boar', 'squeal'):    (700, 0.9, [(1200, 1, 5), (2400, 0.5, 8)], 0.2),
        ('elk', 'alarm'):      (500, 0.5, [(1000, 1, 6), (2200, 0.4, 9)], 0.15),
        ('elk', 'bellow'):     (160, 1.6, [(400, 1, 4), (900, 0.6, 6), (1800, 0.2, 8)], 0.12),
        ('hare', 'squeal'):    (900, 0.5, [(1600, 1, 6), (3000, 0.4, 9)], 0.15),
        ('hyena', 'whoop'):    (380, 1.0, [(800, 1, 5), (1700, 0.5, 8)], 0.2),
        ('lion', 'roar'):      (110, 2.0, [(320, 1, 3), (750, 0.8, 5), (1600, 0.35, 7)], 0.25),
        ('lion', 'death'):     (100, 1.7, [(280, 1, 3), (650, 0.5, 5)], 0.25),
        ('muskox', 'grunt'):   (110, 0.5, [(350, 1, 4), (800, 0.4, 6)], 0.25),
        ('reindeer', 'bellow'):(150, 1.3, [(420, 1, 4), (950, 0.5, 6)], 0.12),
        ('rhino', 'snort'):    (None, 0.5, None, 0),
        ('sabertooth', 'growl'):(75, 1.5, [(260, 1, 3), (620, 0.6, 5), (1400, 0.25, 8)], 0.3),
        ('sabertooth', 'roar'):(90, 1.9, [(330, 1, 3), (780, 0.8, 5), (1650, 0.4, 7)], 0.35),
        ('sabertooth', 'snarl'):(85, 0.8, [(300, 1, 4), (720, 0.7, 6)], 0.4),
        ('sabertooth', 'death'):(80, 1.6, [(250, 1, 3), (600, 0.5, 5)], 0.3),
        ('wolf', 'growl'):     (90, 1.3, [(300, 1, 3), (680, 0.6, 6)], 0.28),
        ('wolf', 'howl'):      (300, 2.4, [(620, 1, 5), (1300, 0.45, 8), (2600, 0.2, 10)], 0.12),
        ('wolf', 'death'):     (85, 1.5, [(280, 1, 3), (650, 0.5, 5)], 0.3),
    }
    if kind == 'bison' and mood == 'snort' or kind == 'rhino' and mood == 'snort':
        d = 0.5
        n = lp(white(d), 1400) * env_ad(d, 0.02, 0.35)
        n = S(n, bp(white(d), 400, 1.2) * env_ad(d, 0.02, 0.4) * 0.8)
        return n * 0.9
    if kind == 'hyena':
        d = 1.0
        # laughing series
        out = np.zeros(sec(1.6))
        for i in range(5):
            h = formant_voice(0.14, 420 - i * 25, 300, [(750, 1, 5), (1600, 0.5, 8)], seed=180 + i, breath=0.3)
            out = mix_at(out, h * (1 - i * 0.12), i * 0.16)
        return out
    if kind == 'wolf' and mood == 'pack':
        out = np.zeros(sec(5.0))
        for i, (at, f) in enumerate([(0, 300), (0.7, 330), (1.5, 280)]):
            cfgw = cfg[('wolf', 'howl')]
            f0 = f
            f1 = f * 1.35
            h = formant_voice(2.2, f0, f1, cfgw[2], seed=190 + i, breath=cfgw[3])
            # falling end
            h2 = formant_voice(0.6, f1, f * 1.1, cfgw[2], seed=195 + i, breath=cfgw[3])
            full = np.concatenate([h * np.linspace(0.3, 1, len(h)), h2])
            out = mix_at(out, full * (0.55 if i else 0.8), at)
        return out
    f0, d, formants, breath = cfg[(kind, mood)]
    seed = hash((kind, mood)) & 0xffff
    f1 = f0 * (0.75 if mood == 'death' else (1.25 if mood in ('roar', 'call') else 1.0))
    v = formant_voice(d, f0, f1, formants, seed=seed, breath=breath, jitter=0.02)
    v = v * env_ad(d, min(0.12, d * 0.2), d * 0.75)
    if mood in ('growl', 'roar', 'snarl'):
        v = S(v, lp(white(d), 900) * env_ad(d, 0.05, d * 0.8) * 0.25)
    return v

# ================================================================== AMBIENCE
def amb_wind(dur, amp=0.4, cutoff=500):
    bed = bp(pink(dur), cutoff, 0.5)
    lfo = 0.55 + 0.45 * np.cumsum(NPG.normal(0, 0.002, sec(dur)))
    lfo = np.clip(lfo, 0.1, 1.2)
    return bed * lfo * amp

def amb_events(dur, maker, count, gmin=0.2, gmax=0.6, gap_min=0.4, gap_max=2.5, seed=0):
    rng = random.Random(seed)
    out = np.zeros(sec(dur))
    for _ in range(count):
        at = rng.uniform(0, max(0.1, dur - 3))
        out = mix_at(out, maker(rng) * rng.uniform(gmin, gmax), at)
    return out

def crickets(dur, seed=0, f=4300, rate=14):
    rng = random.Random(seed)
    out = np.zeros(sec(dur))
    at = 0.0
    while at < dur - 0.5:
        burst = np.zeros(sec(0.3))
        for i in range(3):
            burst = mix_at(burst, sine(0.03, f, f * 1.02) * perc(0.03, 0.01) * 0.5, i * 0.07)
        out = mix_at(out, burst * rng.uniform(0.3, 0.8), at)
        at += rng.uniform(0.25, 1.4)
    return out

def cicada(dur, seed=0):
    n = sec(dur)
    carrier = sine(dur, 5200, 5150) + sine(dur, 2600, 2580) * 0.5
    am = 0.5 + 0.5 * np.sign(np.sin(2 * math.pi * 9 * n / SR))
    bed = carrier * am * 0.06
    lfo = 0.4 + 0.6 * (0.5 + 0.5 * np.sin(np.linspace(0, 2 * math.pi * 0.13, n) + seed))
    return bed * lfo

def frog_croak(rng):
    d = rng.uniform(0.2, 0.5)
    f = rng.uniform(180, 420)
    return formant_voice(d, f, f * 0.85, [(400, 1, 5), (900, 0.4, 8)], seed=rng.randint(0, 9999), breath=0.15) * 0.7

def drip(rng):
    f = rng.uniform(1800, 3200)
    d = 0.15
    return sine(d, f, f * 0.6) * perc(d, 0.03) * 0.4

def wave_swell(rng):
    d = rng.uniform(6, 10)
    w = lp(pink(d), 900) * env_ad(d, d * 0.35, d * 0.6)
    w += hp(white(d), 1500) * env_ad(d, d * 0.45, d * 0.45) * 0.25   # foam
    return w

def birds_chatter(rng, exotic=False):
    out = np.zeros(sec(rng.uniform(0.4, 1.2)))
    for _ in range(rng.randint(1, 4)):
        f0 = rng.uniform(2000, 4600) * (0.8 if exotic else 1.0)
        d = rng.uniform(0.05, 0.18)
        n = sine(d, f0, f0 * rng.uniform(0.65, 1.6)) * env_ad(d, 0.012, d * 0.7)
        out = mix_at(out, n * rng.uniform(0.3, 0.8), rng.uniform(0, 0.3))
    return out

def build_ambience():
    D = 38
    def finish(sig, name):
        sig = make_loop(sig, 2.0)
        save_loop('amb_' + name, stereo(sig[:, 0], 0.35, 11), peak=0.55)

    # forest day
    s = amb_wind(D, 0.22, 420) + hp(pink(D), 800) * 0.05
    s += amb_events(D, lambda r: birds_chatter(r), 42, 0.15, 0.5, 0.3, 2.2, seed=1)
    s += amb_events(D, lambda r: hp(white(0.4), 1500) * env_ad(0.4, 0.05, 0.3), 8, 0.05, 0.15, seed=2)
    finish(s, 'forest_day')
    # forest night
    s = amb_wind(D, 0.15, 300) + crickets(D, seed=3) * 0.5
    s += amb_events(D, lambda r: owl_hoot(r.randint(0, 1)), 4, 0.1, 0.3, 3, 7, seed=4)
    finish(s, 'forest_night')
    # jungle
    s = amb_wind(D, 0.12, 350) + cicada(D, 5) * 0.8
    s += amb_events(D, lambda r: birds_chatter(r, exotic=True), 36, 0.1, 0.4, 0.2, 1.5, seed=6)
    s += amb_events(D, drip, 10, 0.05, 0.2, 1, 4, seed=7)
    finish(s, 'jungle')
    # ocean
    s = np.zeros(sec(D))
    for at in np.arange(0, D - 10, 4.7):
        s = mix_at(s, wave_swell(RNG) * 0.8, at)
    s += amb_events(D, lambda r: hawk_screech()[:sec(0.7)] * 0.2, 2, 0.1, 0.2, 5, 9, seed=8)
    finish(s, 'ocean')
    # desert
    s = amb_wind(D, 0.4, 650) + hp(white(D), 5000) * 0.02
    s += amb_events(D, lambda r: crickets(3, seed=r.randint(0, 99), f=5200, rate=8), 6, 0.1, 0.25, 3, 6, seed=9)
    finish(s, 'desert')
    # savanna
    s = amb_wind(D, 0.3, 520) + crickets(D, seed=10, f=5000, rate=6) * 0.18
    s += amb_events(D, lambda r: birds_chatter(r), 18, 0.1, 0.3, 1, 4, seed=11)
    finish(s, 'savanna')
    # steppe
    s = amb_wind(D, 0.45, 580)
    s += amb_events(D, lambda r: hp(white(0.6), 1200) * env_ad(0.6, 0.1, 0.5), 10, 0.05, 0.12, 1.5, 4, seed=12)
    s += amb_events(D, lambda r: hawk_screech()[:sec(0.6)] * 0.15, 2, 0.08, 0.15, 6, 10, seed=13)
    finish(s, 'steppe')
    # tundra
    s = amb_wind(D, 0.5, 700) + hp(white(D), 4000) * 0.05
    s += amb_events(D, lambda r: ptarmigan(r.randint(0, 1)) * 0.3, 3, 0.1, 0.2, 4, 8, seed=14)
    finish(s, 'tundra')
    # swamp
    s = amb_wind(D, 0.15, 380) + lp(pink(D), 600) * 0.08
    s += amb_events(D, frog_croak, 30, 0.15, 0.45, 0.4, 1.8, seed=15)
    s += amb_events(D, drip, 8, 0.05, 0.15, 2, 5, seed=16)
    finish(s, 'swamp')
    # cave
    s = lp(brown(D), 200) * 0.5 + lp(white(D), 300) * 0.1
    s += amb_events(D, drip, 12, 0.1, 0.3, 1, 4, seed=17)
    finish(s, 'cave')
    # birds (bright chorus)
    s = amb_wind(D, 0.15, 400)
    s += amb_events(D, lambda r: birds_chatter(r), 60, 0.2, 0.6, 0.2, 1.2, seed=18)
    finish(s, 'birds')
    print('  ambience done')

# ================================================================== MUSIC
def note_hz(semi): return 440.0 * 2 ** (semi / 12)

def ks_string(freq, dur, decay=0.9965, bright=0.55, seed=0):
    n = sec(dur)
    period = max(2, int(SR / freq))
    rng = np.random.default_rng(seed)
    buf = rng.uniform(-1, 1, period)
    buf = lp(buf, 3000, 0.7)
    out = np.zeros(n)
    idx = 0
    prev = 0.0
    for i in range(n):
        cur = buf[idx]
        nxt = buf[(idx + 1) % period]
        new = (cur * (1 - bright) + nxt * bright) * 0.5 + (cur + nxt) * 0.5 * decay - prev * 0.0
        new = ((1 - decay) * 0 + decay * ((cur + nxt) / 2))
        buf[idx] = new
        prev = cur
        out[i] = cur
        idx = (idx + 1) % period
    return out / (np.abs(out).max() or 1)

def flute(freq, dur, seed=0, breath=0.18, vib_hz=5.2):
    n = sec(dur)
    ramp = np.linspace(freq * 0.985, freq, min(n, sec(0.08)))
    f = np.concatenate([ramp, np.full(max(0, n - len(ramp)), freq)])
    vib = 1 + 0.006 * np.sin(2 * math.pi * vib_hz * np.arange(n) / SR) * np.minimum(1, np.arange(n) / SR)
    ph = 2 * np.pi * np.cumsum(f * vib) / SR
    body = np.sin(ph) + np.sin(2 * ph) * 0.25 + np.sin(3 * ph) * 0.08
    noise = bp(white(dur), freq * 2.4, 1.2) * breath
    envl = env_ad(dur, min(0.09, dur * 0.25), dur * 0.75)
    return (S(body / 1.33, noise)) * envl

def horn(freq, dur, seed=0):
    n = sec(dur)
    f = np.linspace(freq, freq * 1.005, n)
    ph = 2 * np.pi * np.cumsum(f) / SR
    src = 2 * (ph % (2 * math.pi)) / (2 * math.pi) - 1
    v = S(bp(src, freq * 2, 2) * 0.7, bp(src, freq * 4, 3) * 0.3)
    return v * env_ad(dur, min(0.2, dur * 0.3), dur * 0.8)

def skin_drum(dur=0.5, f0=130, f1=55, seed=0):
    n = sec(dur)
    body = sine(dur, f0, f1) * perc(dur, 0.22)
    skin = bp(white(dur), 800, 0.8) * perc(dur, 0.05) * 0.4
    boom = lp(white(dur), 300) * perc(dur, 0.1) * 0.3
    return S(body, skin, boom)

def frame_drum(dur=0.25, f0=200, f1=140):
    body = sine(dur, f0, f1) * perc(dur, 0.1)
    return S(body, bp(white(dur), 1200, 0.9) * perc(dur, 0.03) * 0.3)

def shaker_hit(seed=0):
    rng = random.Random(seed)
    d = 0.09
    return hp(white(d), 5000) * env_ad(d, 0.005, 0.07) * rng.uniform(0.5, 1)

def rattle_hit(seed=0):
    return crackle(0.12, 30, 2000, 6000, seed=seed) * 0.5

def stomp_clap(seed=0):
    d = 0.3
    return S(sine(d, 95, 50) * perc(d, 0.09), bp(white(d), 1400, 0.7) * perc(d, 0.04) * 0.4)

PENT_MINOR = [0, 3, 5, 7, 10]        # minor pentatonic
PENT_MAJOR = [0, 2, 4, 7, 9]         # major pentatonic
DORIAN = [0, 2, 3, 5, 7, 9, 10]

def compose(name, bpm, bars, root_semi, scale, mood, seed=0):
    """Render one loopable stereo track. Returns (L, R) arrays."""
    rng = random.Random(seed)
    beat = 60.0 / bpm
    dur = bars * 4 * beat
    L = np.zeros(sec(dur)); R = np.zeros(sec(dur))

    def place(sig, at_beat, gl=1.0, pan=0.0):
        nonlocal L, R
        at = sec(at_beat * beat)
        need = at + len(sig)
        if need > len(L):
            pad = need - len(L)
            L = np.concatenate([L, np.zeros(pad)]); R = np.concatenate([R, np.zeros(pad)])
        th = (pan + 1) * math.pi / 4
        L[at:at + len(sig)] += sig * gl * math.cos(th)
        R[at:at + len(sig)] += sig * gl * math.sin(th)

    # ---- drone (root + fifth, low)
    drone_root = note_hz(root_semi - 24)
    for mult, g in ((1, 0.16), (1.5, 0.10), (2, 0.08)):
        d = ks_string(drone_root * mult, dur + 1.5, decay=0.99985, bright=0.3, seed=seed)
        place(d[:sec(dur)], 0, g, -0.1)
    if mood in ('tension', 'combat', 'hunt'):
        # tritone-ish color pulsing
        for b in range(bars * 2):
            n = flute(note_hz(root_semi - 12 + 6), beat * 1.8, seed=seed + b, breath=0.4)
            place(n, b * 2, 0.05 if mood != 'combat' else 0.07, 0.3)

    # ---- percussion
    if mood == 'combat':
        pattern = [(i, 1.0 if i % 4 == 0 else 0.6) for i in range(bars * 8)]
        for i, g in pattern:
            if i % 2 == 0:
                place(skin_drum(0.4, 150, 55, seed + i), i * 0.5, g * 0.9)
            if i % 4 == 3:
                place(frame_drum(0.22, 260, 170), i * 0.5, 0.5)
        for i in range(bars * 16):
            if i % 2 == 1:
                place(shaker_hit(seed + i), i * 0.25, 0.35, 0.25)
        for b in range(bars):
            if b % 4 == 3:
                place(stomp_clap(seed + b), b * 4 + 3.5, 0.5)
    elif mood in ('serene', 'dawn'):
        for b in range(bars):
            if b % 2 == 1:
                place(frame_drum(0.25, 210, 150), b * 4 + 1, 0.3)
                place(frame_drum(0.22, 190, 140), b * 4 + 3, 0.22)
            for e in range(8):
                place(shaker_hit(seed + b * 8 + e), b * 4 + e * 0.5, 0.12 if e % 2 else 0.2, 0.3)
    elif mood in ('tension', 'hunt'):
        for b in range(bars):
            place(skin_drum(0.35, 120, 48, seed + b), b * 4, 0.55)
            place(skin_drum(0.3, 130, 52, seed + 100 + b), b * 4 + 2.5, 0.4)
            if rng.random() < 0.4:
                place(rattle_hit(seed + b), b * 4 + rng.choice([1.0, 2.0, 3.0]), 0.3, -0.3)
    elif mood == 'winter':
        for b in range(0, bars, 2):
            place(frame_drum(0.3, 170, 120), b * 4 + 2, 0.18)

    # ---- melody / phrases
    def phrase(instrument, oct_shift, notes_per_phrase, note_len, g, pan, rest_prob=0.25):
        t_beats = 0.0
        pi = 0
        while t_beats < bars * 4 - note_len:
            if rng.random() < rest_prob:
                t_beats += note_len * rng.choice([1, 2])
                continue
            n_notes = rng.randint(2, notes_per_phrase)
            step = rng.choice(scale)
            for _ in range(n_notes):
                if t_beats >= bars * 4 - note_len:
                    break
                semi = root_semi + oct_shift * 12 + step
                sig = instrument(note_hz(semi), note_len * beat * rng.uniform(0.8, 1.4), seed=seed + pi)
                place(sig, t_beats, g * rng.uniform(0.7, 1.0), pan)
                t_beats += note_len
                pi += 1
                step = rng.choice(scale)
            t_beats += note_len * rng.choice([0, 1, 2])

    if mood == 'serene':
        phrase(flute, 0, 6, 1.0, 0.30, -0.25)
        phrase(flute, 1, 4, 2.0, 0.14, 0.35, rest_prob=0.5)
    elif mood == 'dawn':
        phrase(flute, 0, 7, 0.5, 0.28, -0.2)
        phrase(flute, 1, 5, 1.0, 0.16, 0.3)
        phrase(horn, -1, 3, 4.0, 0.10, 0.0, rest_prob=0.6)
    elif mood == 'tension':
        phrase(flute, 0, 3, 0.5, 0.16, 0.2)
    elif mood == 'hunt':
        phrase(horn, 0, 3, 1.5, 0.12, -0.15, rest_prob=0.5)
        phrase(flute, 0, 4, 0.5, 0.13, 0.3)
    elif mood == 'winter':
        phrase(flute, 1, 4, 2.0, 0.16, 0.0, rest_prob=0.55)
    elif mood == 'combat':
        phrase(horn, 0, 4, 1.0, 0.16, -0.2, rest_prob=0.35)
        phrase(horn, 0, 3, 0.5, 0.10, 0.25, rest_prob=0.4)

    n = min(len(L), len(R))
    L, R = L[:n], R[:n]
    # gentle glue: soft clip
    L = np.tanh(L * 1.2); R = np.tanh(R * 1.2)
    return np.stack([L, R], axis=1)

def build_music():
    tracks = [
        ('menu_theme',      66, 14, 2,  PENT_MINOR, 'serene', 11),   # D minor-ish
        ('explore_serene',  60, 16, 0,  PENT_MAJOR, 'serene', 22),   # A maj-ish
        ('explore_tension', 72, 16, 5,  PENT_MINOR, 'tension', 33),
        ('combat_battle',   132, 24, 2, PENT_MINOR, 'combat', 44),
        ('hunt_stalk',      84, 16, 7,  PENT_MINOR, 'hunt', 55),
        ('season_winter',   52, 12, 9,  DORIAN,     'winter', 66),
        ('tribe_dawn',      96, 16, 0,  PENT_MAJOR, 'dawn', 77),
    ]
    for name, bpm, bars, root, scale, mood, seed in tracks:
        sig = compose(name, bpm, bars, root, scale, mood, seed)
        sig = make_loop(sig, 2.5)
        save_loop('music/' + name, sig, peak=0.72)
        print(f'  music {name} done ({len(sig)/SR:.0f}s)')

# ================================================================== MAIN
def gen_all_sfx():
    for mat in ['dirt', 'grass', 'mud', 'sand', 'shallow', 'snow', 'stone', 'wood']:
        for v in range(3):
            save_sfx(f'step_{mat}_{v}', sfx_step(mat, v))
    save_sfx('stone_step', sfx_step('stone', 0))
    for v in range(3):
        save_sfx(f'chop_{v}', sfx_chop(v))
        save_sfx(f'mine_hit_{v}', sfx_mine(v))
        save_sfx(f'dig_{v}', sfx_dig(v))
        save_sfx(f'craft_hammer_{v}', sfx_hammer(v))
        save_sfx(f'skin_animal_{v}', sfx_skin(v))
        save_sfx(f'hit_flesh_{v}', sfx_hit_flesh(v))
    for v in range(2):
        save_sfx(f'sharpen_{v}', sfx_sharpen(v))
        save_sfx(f'tie_knot_{v}', sfx_knot(v))
        save_sfx(f'stone_knock_{v}', sfx_hammer(v + 1))
    for v in range(3):
        save_sfx(f'swing_{v}', sfx_swing(False, v))
        save_sfx(f'swing_heavy_{v}', sfx_swing(True, v))
        save_sfx(f'eat_{v}', sfx_eat(v))
        save_sfx(f'pickup_generic_{v}', sfx_pickup('generic', v))
    for v in range(2):
        save_sfx(f'drink_{v}', sfx_drink(v))
        save_sfx(f'swim_stroke_{v}', sfx_swim_stroke(v))
        save_sfx(f'hit_stone_{v}', sfx_hit_stone(v))
        save_sfx(f'body_fall_{v}', sfx_body_fall(v))
        save_sfx(f'hurt_{v}', sfx_hurt(v))
        save_sfx(f'effort_{v}', sfx_effort(v))
        save_sfx(f'ember_{v}', sfx_ember(v))
    save_sfx('bone_crack', sfx_bone_crack())
    save_sfx('heartbeat', sfx_heartbeat())
    save_sfx('human_death', sfx_human_death())
    save_sfx('cough', sfx_cough())
    save_sfx('laugh', sfx_laugh())
    save_sfx('snore', sfx_snore())
    save_sfx('dive', sfx_dive())
    save_sfx('splash_big', sfx_splash(1.0, 1))
    save_sfx('splash_small', sfx_splash(0.3, 2))
    save_sfx('water_splash', sfx_splash(0.6, 3))
    save_sfx('rowing', sfx_rowing(0))
    save_sfx('raft_creak', sfx_raft_creak())
    save_sfx('campfire_loop', sfx_campfire_loop())
    save_sfx('fire_ignite', sfx_fire_ignite())
    save_sfx('flint_strike', S(sfx_sharpen(1), hp(white(0.05), 4000) * perc(0.05, 0.01)))
    save_sfx('thatch', hp(white(0.3), 1000) * env_ad(0.3, 0.01, 0.25) * 0.6)
    save_sfx('impact', sfx_hit_stone(0))
    pickups = ['berry', 'bone', 'chime', 'egg', 'fiber', 'flint', 'hide', 'meat',
               'obsidian', 'ore', 'stone', 'wood']
    kinds = {'berry': 'meat', 'egg': 'meat', 'fiber': 'fiber', 'hide': 'hide',
             'meat': 'meat', 'flint': 'flint', 'obsidian': 'obsidian', 'ore': 'ore',
             'stone': 'stone', 'wood': 'wood', 'bone': 'stone', 'chime': 'chime'}
    for p in pickups:
        save_sfx(f'pickup_{p}', sfx_pickup(kinds.get(p, 'generic'), pickups.index(p)))
    save_sfx('craft_tap', sfx_craft_tap())
    save_sfx('craft_start', sfx_craft_start())
    save_sfx('craft_complete', sfx_craft_complete())
    save_sfx('equip', sfx_equip())
    save_sfx('era_up', sfx_era_up())
    _qo = np.zeros(sec(0.7))
    _qo = mix_at(_qo, sfx_pickup('quest', 1), 0)
    _qo = mix_at(_qo, flute(note_hz(12), 0.4, seed=9) * 0.3, 0.05)
    save_sfx('quest_offer', _qo)
    save_sfx('quest_complete', sfx_craft_complete())
    save_sfx('save_chime', sfx_pickup('save', 2))
    for ui in ['click', 'hover', 'back', 'error', 'page', 'trade']:
        save_sfx(f'ui_{ui}', sfx_ui(ui))
    save_sfx('ui_map_zoom_in', sfx_ui('zoom_in'))
    save_sfx('ui_map_zoom_out', sfx_ui('zoom_out'))
    save_sfx('ui_waypoint_set', sfx_ui('wp_set'))
    save_sfx('ui_waypoint_clear', sfx_ui('wp_clear'))
    # weather
    for nm, lo in [('rain_loop', True), ('rain_heavy_loop', True), ('wind_loop', True),
                   ('blizzard_loop', True)]:
        kind = nm.replace('_loop', '') if nm != 'blizzard_loop' else 'blizzard'
        save_sfx(nm, sfx_weather(kind if kind != 'rain_heavy' else 'rain_heavy', loop=True), peak=0.5)
    save_sfx('wind_gust', sfx_weather('gust'))
    save_sfx('wind_howl', sfx_weather('howl'))
    for v in range(3):
        save_sfx(f'thunder_{v}', sfx_thunder(v))
    # birds
    for v in range(6):
        save_sfx(f'bird_chirp_{v}', bird_chirp(v))
    for v in range(2):
        save_sfx(f'bird_flap_{v}', bird_flap(v))
        save_sfx(f'owl_hoot_{v}', owl_hoot(v))
        save_sfx(f'ptarmigan_{v}', ptarmigan(v))
        save_sfx(f'auk_{v}', auk(v))
    save_sfx('crow_0', crow_caw())
    save_sfx('hawk_screech', hawk_screech())
    # mammals
    mammal_sets = [
        ('mammoth_call_0', ('mammoth', 'call')), ('mammoth_call_1', ('mammoth', 'call')),
        ('mammoth_growl', ('mammoth', 'growl')), ('mammoth_death', ('mammoth', 'death')),
        ('bear_growl', ('bear', 'growl')), ('bear_huff', ('bear', 'huff')),
        ('bear_roar', ('bear', 'roar')), ('bear_death', ('bear', 'death')),
        ('bison_bellow_0', ('bison', 'bellow')), ('bison_bellow_1', ('bison', 'bellow')),
        ('bison_snort', ('bison', 'snort')), ('bison_death', ('bison', 'death')),
        ('boar_grunt_0', ('boar', 'grunt')), ('boar_grunt_1', ('boar', 'grunt')),
        ('boar_grunt_2', ('boar', 'grunt')), ('boar_squeal', ('boar', 'squeal')),
        ('elk_alarm', ('elk', 'alarm')), ('elk_bellow', ('elk', 'bellow')),
        ('hare_squeal', ('hare', 'squeal')),
        ('hyena_0', ('hyena', 'whoop')), ('hyena_1', ('hyena', 'whoop')), ('hyena_2', ('hyena', 'whoop')),
        ('lion_roar', ('lion', 'roar')), ('lion_death', ('lion', 'death')),
        ('muskox_grunt', ('muskox', 'grunt')), ('reindeer_bellow', ('reindeer', 'bellow')),
        ('rhino_snort', ('rhino', 'snort')),
        ('sabertooth_growl', ('sabertooth', 'growl')), ('sabertooth_roar', ('sabertooth', 'roar')),
        ('sabertooth_snarl', ('sabertooth', 'snarl')), ('sabertooth_death', ('sabertooth', 'death')),
        ('wolf_growl_0', ('wolf', 'growl')), ('wolf_growl_1', ('wolf', 'growl')),
        ('wolf_howl', ('wolf', 'howl')), ('wolf_pack_howl', ('wolf', 'pack')),
        ('wolf_death', ('wolf', 'death')),
    ]
    for nm, args in mammal_sets:
        save_sfx(nm, mammal(*args))
    print('  sfx done')

if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    if what in ('sfx', 'all'):
        gen_all_sfx()
    if what in ('amb', 'ambience', 'all'):
        build_ambience()
    if what in ('music', 'all'):
        build_music()
    print('audio generation complete.')
