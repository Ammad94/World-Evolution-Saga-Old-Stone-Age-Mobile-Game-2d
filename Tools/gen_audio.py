#!/usr/bin/env python3
"""
World Evolution Saga — procedural audio generator.
Pure-stdlib DSP: oscillators, noise, filters, envelopes, Karplus-Strong
plucks, formant-ish creature voices, and a small music sequencer.
Outputs WAV + Unity .meta under Assets/Audio/.
"""
import math, random, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from assetlib import write_wav, write_wav_meta

RATE = 22050
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources', 'Audio')
RNG = random.Random(1234)

def A(name):
    sub = 'sfx'
    if name.startswith('music/'):
        return os.path.join(ROOT, name + '.wav')
    if name.startswith('amb'):
        sub = 'ambience'
    return os.path.join(ROOT, sub, name + '.wav')

# ------------------------------------------------------------------ DSP kit
def silence(dur):
    return [0.0] * int(RATE * dur)

def env_ad(dur, a=0.005, d=None, curve=1.0):
    n = int(RATE * dur)
    d = d if d is not None else dur
    out = []
    for i in range(n):
        t = i / RATE
        if t < a:
            out.append(t / max(a, 1e-5))
        else:
            f = (t - a) / max(d - a, 1e-5)
            out.append(max(0.0, (1.0 - f)) ** curve)
    return out

def sine(dur, f0, f1=None, vib=0.0, vib_hz=5.0, a=0.005, curve=1.0):
    n = int(RATE * dur)
    out = [0.0] * n
    ph = 0.0
    f1 = f0 if f1 is None else f1
    e = env_ad(dur, a, dur, curve)
    for i in range(n):
        t = i / RATE
        k = t / dur
        f = f0 + (f1 - f0) * k
        if vib:
            f *= 1.0 + vib * math.sin(2 * math.pi * vib_hz * t)
        ph += 2 * math.pi * f / RATE
        out[i] = math.sin(ph) * e[i]
    return out

def square(dur, f0, duty=0.5, a=0.003, curve=1.0):
    n = int(RATE * dur); out = [0.0] * n; ph = 0.0
    e = env_ad(dur, a, dur, curve)
    for i in range(n):
        ph += f0 / RATE
        out[i] = (1.0 if (ph % 1.0) < duty else -1.0) * e[i]
    return out

def saw(dur, f0, f1=None, a=0.003, curve=1.0):
    n = int(RATE * dur); out = [0.0] * n; ph = 0.0
    f1 = f0 if f1 is None else f1
    e = env_ad(dur, a, dur, curve)
    for i in range(n):
        k = i / n
        ph += (f0 + (f1 - f0) * k) / RATE
        out[i] = (2.0 * (ph % 1.0) - 1.0) * e[i]
    return out

def noise(dur, lp=0.5, hp=0.0):
    n = int(RATE * dur); out = [0.0] * n
    y = 0.0
    for i in range(n):
        w = RNG.uniform(-1, 1)
        y += lp * (w - y)
        out[i] = y
    if hp > 0:
        prev = 0.0; prevl = 0.0
        for i in range(n):
            l = out[i]
            out[i] = l - prevl - hp * (l - prev) if False else (l - prevl) - hp * 0  # placeholder
        # simple high-pass: subtract slow moving average
        avg = 0.0
        for i in range(n):
            avg += 0.02 * (out[i] - avg)
            out[i] -= avg
    return out

def lowpass(buf, alpha):
    y = 0.0; out = [0.0] * len(buf)
    for i, x in enumerate(buf):
        y += alpha * (x - y)
        out[i] = y
    return out

def highpass(buf, alpha):
    lp = lowpass(buf, alpha)
    return [b - l for b, l in zip(buf, lp)]

def amp_mod(buf, mod):
    """Apply amplitude envelope/modulation (shorter buffer cycles)."""
    out = [0.0] * len(buf)
    for i, x in enumerate(buf):
        out[i] = x * mod[i % len(mod)]
    return out

def mix_at(dst, src, pos=0.0, gain=1.0):
    p = int(pos * RATE)
    end = min(len(dst), p + len(src))
    for i in range(max(0, p), end):
        dst[i] += src[i - p] * gain
    return dst

def combine(*bufs):
    n = max(len(b) for b in bufs)
    out = [0.0] * n
    for b in bufs:
        for i in range(len(b)):
            out[i] += b[i]
    return out

def gain(buf, g):
    return [x * g for x in buf]

def normalize(buf, peak=0.88):
    m = max(1e-6, max(abs(x) for x in buf))
    return [x / m * peak for x in buf]

def soft_clip(buf, drive=1.0):
    return [math.tanh(x * drive) for x in buf]

def fade_io(buf, ms=4):
    n = int(RATE * ms / 1000)
    for i in range(min(n, len(buf))):
        f = i / n
        buf[i] *= f
        buf[-1 - i] *= f
    return buf

def make_loop(buf, xfade_s=0.35):
    n = int(RATE * xfade_s)
    if len(buf) <= n * 2: return buf
    head = buf[:n]; tail = buf[-n:]
    out = buf[:-n]
    for i in range(n):
        t = i / n
        out[i] = head[i] * t + tail[i] * (1 - t)
    return out

def delay_fx(buf, time_s=0.12, fb=0.3, taps=3, wet=0.25):
    d = int(RATE * time_s)
    out = buf[:]
    g = wet
    for k in range(1, taps + 1):
        off = d * k
        for i in range(len(buf)):
            j = i + off
            if j < len(out):
                out[j] += buf[i] * g
        g *= fb
    return out

def karplus(dur, freq, decay=0.996, bright=0.6):
    n = int(RATE * dur)
    d = max(2, int(RATE / freq))
    line = [RNG.uniform(-1, 1) for _ in range(d)]
    out = [0.0] * n
    idx = 0
    for i in range(n):
        cur = line[idx]
        nxt = line[(idx + 1) % d]
        line[idx] = (cur * bright + nxt * (1 - bright)) * decay
        out[i] = cur
        idx = (idx + 1) % d
    return out

def drum_low(dur=0.28, f0=150.0, f1=48.0, punch=0.7):
    body = sine(dur, f0, f1, a=0.001, curve=2.0)
    nz = gain(noise(dur, lp=0.35), 0.4 * punch)
    nz = gain(env_ad(dur, 0.001, 0.05), 1.0) if False else nz
    e = env_ad(dur, 0.001, 0.06, 2.0)
    nz = [x * e[i] for i, x in enumerate(nz)]
    return combine(body, nz)

def drum_hi(dur=0.09):
    return gain(noise(dur, lp=0.75), env_ad(dur, 0.001, 0.05, 2.0) and 1.0) if False else \
        [x * e for x, e in zip(noise(dur, lp=0.75), env_ad(dur, 0.001, 0.045, 2.0))]

def shaker(dur=0.06):
    return [x * e for x, e in zip(highpass(noise(dur, lp=0.9), 0.15), env_ad(dur, 0.004, 0.05, 1.4))]

def save(name, buf, loop=False, peak=0.85):
    buf = normalize(fade_io(buf), peak)
    write_wav(A(name), [max(-1, min(1, s)) for s in buf])
    write_wav_meta(A(name) + '.meta', loop=loop)

# ================================================================= SFX
def gen_all_sfx():
    reg = {}
    def put(name, fn, loop=False):
        reg[name] = (fn, loop)

    # ---- footsteps (8 materials x 3 variants)
    def step(mat, v):
        def f():
            r = random.Random(v * 7 + hash(mat) % 100)
            dur = r.uniform(0.10, 0.16)
            if mat == 'snow':
                base = [x * e for x, e in zip(noise(dur, lp=0.5), env_ad(dur, 0.002, 0.09, 1.5))]
                crunch = gain(highpass(noise(dur, lp=0.95), 0.2), 0.5)
                base = combine(base, [c * e for c, e in zip(crunch, env_ad(dur, 0.002, 0.05, 2.0))])
                return gain(base, 0.9)
            if mat == 'stone':
                knock = sine(dur, r.uniform(160, 210), 70, a=0.001, curve=2.5)
                click = [x * e for x, e in zip(highpass(noise(dur, lp=0.9), 0.25), env_ad(dur, 0.001, 0.03, 2))]
                return gain(combine(knock, click), 0.55)
            if mat == 'wood':
                knock = sine(dur, 130, 65, a=0.001, curve=2.2)
                th = [x * e for x, e in zip(noise(dur, lp=0.3), env_ad(dur, 0.001, 0.05, 2))]
                return gain(combine(knock, th), 0.6)
            if mat == 'shallow':
                sp = sine(dur, 400, 120, a=0.002, curve=1.6)
                h2 = [x * e for x, e in zip(highpass(noise(dur, lp=0.85), 0.3), env_ad(dur, 0.002, 0.06, 1.6))]
                return gain(combine(sp, h2), 0.42)
            lp = {'grass': 0.55, 'dirt': 0.4, 'sand': 0.62, 'mud': 0.22}[mat]
            base = [x * e for x, e in zip(noise(dur, lp=lp), env_ad(dur, 0.003, 0.08, 1.6))]
            thump = gain(sine(dur, 95, 45, a=0.001, curve=2.4), 0.5)
            return gain(combine(base, thump), 0.7)
        return f
    for mat in ('grass', 'dirt', 'sand', 'snow', 'stone', 'mud', 'wood', 'shallow'):
        for v in range(3):
            put(f'step_{mat}_{v}', step(mat, v))

    # ---- resource actions
    def chop(v):
        r = random.Random(v + 40)
        d = 0.22
        crack = [x * e for x, e in zip(highpass(noise(d, lp=0.9), 0.22), env_ad(d, 0.001, 0.035, 2.2))]
        thk = gain(sine(d, 180, 60, a=0.001, curve=2.6), 0.8)
        return gain(combine(crack, thk), 0.85)
    for v in range(3): put(f'chop_{v}', lambda v=v: chop(v))
    def mine(v):
        d = 0.18
        clink = sine(d, random.Random(v).uniform(900, 1400), 300, a=0.001, curve=3.0)
        grit = [x * e for x, e in zip(highpass(noise(d, lp=0.95), 0.3), env_ad(d, 0.001, 0.04, 2.4))]
        return gain(combine(clink, grit), 0.6)
    for v in range(3): put(f'mine_hit_{v}', lambda v=v: mine(v))
    def dig(v):
        d = 0.24
        sco = [x * e for x, e in zip(noise(d, lp=0.3), env_ad(d, 0.01, 0.18, 1.2))]
        return gain(sco, 0.75)
    for v in range(3): put(f'dig_{v}', lambda v=v: dig(v))
    def hammer(v):
        d = 0.16
        r = random.Random(v * 3)
        hit = sine(d, r.uniform(300, 420), 90, a=0.001, curve=2.8)
        ring = gain(sine(d, r.uniform(1500, 2100), 900, a=0.001, curve=3.5), 0.25)
        return gain(combine(hit, ring), 0.7)
    for v in range(3): put(f'craft_hammer_{v}', lambda v=v: hammer(v))
    def skin(v):
        d = 0.3
        tear = [x * e for x, e in zip(highpass(noise(d, lp=0.9), 0.25), env_ad(d, 0.01, 0.22, 1.1))]
        return gain(tear, 0.55)
    for v in range(3): put(f'skin_animal_{v}', lambda v=v: skin(v))
    def sharpen(v):
        d = 0.28
        sw = [x * e for x, e in zip(highpass(noise(d, lp=0.95), 0.3), env_ad(d, 0.02, 0.2, 1.3))]
        return gain(sw, 0.45)
    for v in range(2): put(f'sharpen_{v}', lambda v=v: sharpen(v))
    def knot(v):
        d = 0.12
        tick = sine(d, 700, 250, a=0.001, curve=2.5)
        return gain(combine(tick, gain(noise(d, lp=0.9), 0.3)), 0.5)
    for v in range(2): put(f'tie_knot_{v}', lambda v=v: knot(v))
    put('thatch', lambda: gain([x * e for x, e in zip(noise(0.3, lp=0.5), env_ad(0.3, 0.01, 0.24, 1.2))], 0.6))
    put('stone_knock_0', lambda: gain(sine(0.15, 500, 150, a=0.001, curve=3), 0.55))
    put('stone_knock_1', lambda: gain(sine(0.13, 650, 180, a=0.001, curve=3), 0.5))

    # ---- swings
    def swing(heavy, v):
        d = 0.26 if heavy else 0.18
        r = random.Random(v * 11)
        body = noise(d, lp=0.85)
        body = highpass(body, 0.18)
        e = env_ad(d, 0.03, d, 1.6)
        body = [x * e for x, e in zip(body, e)]
        pitch_f = sine(d, 300 if heavy else 520, 90, a=0.02, curve=1.2)
        return gain(combine(body, gain(pitch_f, 0.2)), 0.6 if heavy else 0.45)
    for v in range(3): put(f'swing_{v}', lambda v=v: swing(False, v))
    for v in range(3): put(f'swing_heavy_{v}', lambda v=v: swing(True, v))

    # ---- combat impacts
    def hit_flesh(v):
        d = 0.2
        r = random.Random(v * 5 + 3)
        thud = sine(d, r.uniform(140, 190), 55, a=0.001, curve=2.4)
        squel = [x * e for x, e in zip(lowpass(noise(d, lp=0.25), 0.5), env_ad(d, 0.001, 0.07, 2))]
        return gain(combine(thud, squel), 0.85)
    for v in range(3): put(f'hit_flesh_{v}', lambda v=v: hit_flesh(v))
    def hit_stone(v):
        d = 0.14
        cl = sine(d, random.Random(v * 9).uniform(800, 1200), 200, a=0.001, curve=3)
        grit = [x * e for x, e in zip(highpass(noise(d, lp=0.95), 0.3), env_ad(d, 0.001, 0.035, 2.4))]
        return gain(combine(cl, grit), 0.7)
    for v in range(2): put(f'hit_stone_{v}', lambda v=v: hit_stone(v))
    def body_fall(v):
        d = 0.5
        thud = sine(d, 110, 35, a=0.002, curve=2.2)
        rumble = [x * e for x, e in zip(noise(d, lp=0.15), env_ad(d, 0.002, 0.4, 1.8))]
        return gain(combine(thud, rumble), 0.9)
    for v in range(2): put(f'body_fall_{v}', lambda v=v: body_fall(v))
    put('bone_crack', lambda: combine(
        gain(sine(0.18, 600, 180, a=0.001, curve=3), 0.6),
        gain([x * e for x, e in zip(highpass(noise(0.18, lp=0.95), 0.3), env_ad(0.18, 0.001, 0.05, 2.5))], 0.7)))

    # ---- eating / drinking
    def eat(v):
        d = 0.16
        cr = [x * e for x, e in zip(highpass(noise(d, lp=0.92), 0.28), env_ad(d, 0.004, 0.09, 1.8))]
        return gain(cr, 0.5)
    for v in range(3): put(f'eat_{v}', lambda v=v: eat(v))
    def gulp(v):
        f0 = 240 + v * 40
        b = sine(0.16, f0, f0 * 0.5, a=0.01, curve=1.5)
        b2 = sine(0.12, f0 * 0.7, f0 * 0.4, a=0.01, curve=1.5)
        buf = silence(0.3); mix_at(buf, b, 0); mix_at(buf, b2, 0.14)
        return gain(buf, 0.5)
    for v in range(2): put(f'drink_{v}', lambda v=v: gulp(v))
    put('snore', lambda: gain(combine(sine(0.9, 95, 70, vib=0.3, vib_hz=22, a=0.2, curve=1.2),
                                      gain(lowpass(noise(0.9, lp=0.2), 0.6), 0.6)), 0.4))
    put('cough', lambda: gain([x * e for x, e in zip(highpass(noise(0.3, lp=0.7), 0.12), env_ad(0.3, 0.01, 0.22, 1.4))], 0.5))
    put('laugh', lambda: _laugh())

    # ---- pickups
    def pick_core(f0, f1, d=0.2, vib=0.0):
        return gain(sine(d, f0, f1, vib=vib, vib_hz=9, a=0.004, curve=1.4), 0.55)
    put('pickup_berry', lambda: pick_core(880, 1180, 0.14))
    put('pickup_wood', lambda: gain(combine(pick_core(300, 260, 0.16), gain(noise(0.1, lp=0.6), 0.2)), 0.7))
    put('pickup_stone', lambda: gain(combine(pick_core(520, 420, 0.14), gain(noise(0.08, lp=0.9), 0.25)), 0.7))
    put('pickup_meat', lambda: gain(combine(pick_core(340, 300, 0.18), gain(noise(0.12, lp=0.4), 0.25)), 0.7))
    put('pickup_hide', lambda: gain([x * e for x, e in zip(lowpass(noise(0.2, lp=0.4), 0.7), env_ad(0.2, 0.005, 0.15, 1.3))], 0.5))
    put('pickup_fiber', lambda: gain([x * e for x, e in zip(highpass(noise(0.16, lp=0.9), 0.25), env_ad(0.16, 0.004, 0.1, 1.6))], 0.5))
    put('pickup_bone', lambda: gain(combine(sine(0.14, 700, 500, a=0.002, curve=2), gain(noise(0.08, lp=0.9), 0.3)), 0.5))
    put('pickup_flint', lambda: gain(combine(sine(0.12, 1100, 700, a=0.001, curve=2.6), gain(noise(0.06, lp=0.95), 0.3)), 0.5))
    put('pickup_obsidian', lambda: gain(combine(sine(0.14, 1500, 900, a=0.001, curve=2.6), gain(sine(0.14, 2250, 1350, a=0.001, curve=3), 0.4)), 0.45))
    put('pickup_ore', lambda: gain(combine(sine(0.16, 420, 300, a=0.002, curve=2.2), gain(noise(0.1, lp=0.85), 0.35)), 0.6))
    put('pickup_egg', lambda: gain(combine(sine(0.18, 600, 380, a=0.004, curve=1.6), gain(noise(0.1, lp=0.7), 0.2)), 0.55))
    for v in range(3):
        put(f'pickup_generic_{v}', lambda v=v: pick_core(660 + v * 120, 990 + v * 140, 0.16))
    put('equip', lambda: combine(gain(sine(0.12, 400, 300, a=0.002, curve=2), 0.5),
                                 gain([x * e for x, e in zip(noise(0.12, lp=0.8), env_ad(0.12, 0.002, 0.06, 2))], 0.4)))

    # ---- UI
    put('ui_click', lambda: gain(sine(0.07, 900, 500, a=0.001, curve=2.4), 0.5))
    put('ui_back', lambda: gain(sine(0.09, 500, 850, a=0.001, curve=2.2), 0.45))
    put('ui_hover', lambda: gain(sine(0.05, 1200, 1350, a=0.001, curve=1.8), 0.25))
    put('ui_error', lambda: gain(square(0.22, 160, 0.4, a=0.002, curve=1.2), 0.28))
    put('ui_page', lambda: gain([x * e for x, e in zip(highpass(noise(0.22, lp=0.95), 0.2), env_ad(0.22, 0.02, 0.18, 1.3))], 0.35))
    put('ui_map_zoom_in', lambda: gain(sine(0.24, 300, 700, a=0.01, curve=1.2), 0.4))
    put('ui_map_zoom_out', lambda: gain(sine(0.24, 700, 300, a=0.01, curve=1.2), 0.4))
    put('ui_waypoint_set', lambda: combine(gain(sine(0.1, 700, 700, a=0.002), 0.4), *(gain(sine(0.1, 1050, 1050, a=0.002), 0.4),) ))
    def wp_set():
        buf = silence(0.3); mix_at(buf, gain(sine(0.12, 660, 660, a=0.002), 0.45), 0); mix_at(buf, gain(sine(0.16, 990, 990, a=0.002), 0.4), 0.1)
        return buf
    def wp_clear():
        buf = silence(0.3); mix_at(buf, gain(sine(0.12, 990, 990, a=0.002), 0.4), 0); mix_at(buf, gain(sine(0.16, 660, 660, a=0.002), 0.4), 0.1)
        return buf
    put('ui_waypoint_clear', wp_clear)
    reg['ui_waypoint_set'] = (wp_set, False)
    put('craft_start', lambda: combine(gain(sine(0.14, 440, 440, a=0.004), 0.4), gain(sine(0.14, 554, 554, a=0.004), 0.3)))
    def craft_done():
        buf = silence(0.8)
        for i, f in enumerate((523, 659, 784, 1047)):
            mix_at(buf, gain(sine(0.5, f, f, a=0.004, curve=1.6), 0.35), i * 0.09)
        return delay_fx(buf, 0.1, 0.35, 2, 0.2)
    put('craft_complete', craft_done)
    def quest_offer():
        buf = silence(0.9)
        for i, f in enumerate((392, 494, 587)):
            mix_at(buf, gain(sine(0.4, f, f, vib=0.01, a=0.02, curve=1.4), 0.35), i * 0.14)
        return buf
    def quest_done():
        buf = silence(1.2)
        for i, f in enumerate((523, 659, 784, 1047, 1319)):
            mix_at(buf, gain(sine(0.45, f, f, a=0.004, curve=1.8), 0.34), i * 0.1)
        return delay_fx(buf, 0.14, 0.4, 3, 0.22)
    put('quest_offer', quest_offer)
    put('quest_complete', quest_done)
    def trade():
        buf = silence(0.7)
        for i, f in enumerate((440, 554, 659)):
            mix_at(buf, gain(sine(0.3, f, f, a=0.004, curve=1.6), 0.32), i * 0.1)
        return buf
    put('ui_trade', trade)
    def era_up():
        buf = silence(2.4)
        notes = (262, 330, 392, 523, 659, 784)
        for i, f in enumerate(notes):
            mix_at(buf, gain(sine(1.4, f, f, vib=0.008, vib_hz=6, a=0.01, curve=2.2), 0.3), i * 0.16)
            mix_at(buf, gain(drum_low(0.3, 180, 60), 0.5 if i % 2 == 0 else 0.0), i * 0.16)
        return delay_fx(buf, 0.22, 0.4, 4, 0.28)
    put('era_up', era_up)
    put('save_chime', lambda: combine(gain(sine(0.3, 784, 784, a=0.01, curve=2), 0.32), gain(sine(0.4, 1175, 1175, a=0.01, curve=2.4), 0.26)))

    # ---- water
    put('splash_small', lambda: gain(combine(sine(0.22, 700, 180, a=0.002, curve=1.5),
                                             gain([x * e for x, e in zip(highpass(noise(0.22, lp=0.9), 0.25), env_ad(0.22, 0.002, 0.1, 1.8))], 0.8)), 0.6))
    put('splash_big', lambda: gain(combine(sine(0.4, 420, 90, a=0.002, curve=1.4),
                                           gain([x * e for x, e in zip(highpass(noise(0.4, lp=0.85), 0.2), env_ad(0.4, 0.002, 0.22, 1.6))], 0.9)), 0.75))
    def stroke(v):
        d = 0.35
        body = [x * e for x, e in zip(highpass(noise(d, lp=0.8), 0.15), env_ad(d, 0.04, 0.3, 1.4))]
        return gain(body, 0.4)
    for v in range(2): put(f'swim_stroke_{v}', lambda v=v: stroke(v))
    put('dive', lambda: gain(combine(sine(0.6, 300, 80, a=0.02, curve=1.2), gain(lowpass(noise(0.6, lp=0.4), 0.8), 0.7)), 0.6))
    put('rowing', lambda: gain([x * e for x, e in zip(highpass(noise(0.4, lp=0.7), 0.12), env_ad(0.4, 0.05, 0.3, 1.3))], 0.4))
    put('raft_creak', lambda: gain(saw(0.5, 140, 90, a=0.05, curve=1.4), 0.16))

    # ---- weather (loops)
    def rain_loop(intense):
        d = 5.0
        base = noise(d, lp=0.9)
        hiss = highpass(base, 0.35)
        lfo = [0.75 + 0.25 * math.sin(2 * math.pi * 0.3 * i / RATE) for i in range(int(RATE * d))]
        body = [h * l for h, l in zip(hiss, lfo)]
        if intense:
            drops = [x * 1.6 for x in body]
        return make_loop(gain(body, 0.32 if not intense else 0.45), 0.5)
    put('rain_loop', lambda: rain_loop(False), loop=True)
    put('rain_heavy_loop', lambda: rain_loop(True), loop=True)
    def wind_loop(dur=6.0, f=0.18):
        body = lowpass(noise(dur, lp=0.35), 0.4)
        lfo = [0.55 + 0.45 * math.sin(2 * math.pi * f * i / RATE + math.sin(i / RATE * 0.7)) for i in range(int(RATE * dur))]
        return make_loop([b * l for b, l in zip(body, lfo)], 0.8)
    put('wind_loop', lambda: wind_loop(), loop=True)
    put('blizzard_loop', lambda: make_loop(gain(combine(
        wind_loop(6.0, 0.23),
        gain(highpass(noise(6.0, lp=0.9), 0.3), 0.35)), 0.8), 0.8), loop=True)
    def thunder(v):
        d = 2.2 + v * 0.4
        r = random.Random(v * 31 + 5)
        crack = [x * e for x, e in zip(highpass(noise(0.4, lp=0.8), 0.12), env_ad(0.4, 0.002, 0.18, 1.6))]
        rum = lowpass(noise(d, lp=0.08), 0.5)
        lfo = [1 - 0.4 * abs(math.sin(2 * math.pi * r.uniform(1.5, 3) * i / RATE)) for i in range(int(RATE * d))]
        rum = [x * l for x, l in zip(rum, lfo)]
        buf = silence(d + 0.5)
        mix_at(buf, gain(crack, 0.7), 0)
        mix_at(buf, gain(rum, 0.85), 0.25 + v * 0.15)
        return buf
    for v in range(3): put(f'thunder_{v}', lambda v=v: thunder(v), loop=False)
    put('wind_gust', lambda: make_loop(gain(combine(
        lowpass(noise(4.0, lp=0.3), 0.5), gain(highpass(noise(4.0, lp=0.9), 0.2), 0.4)), 0.6), 0.6), loop=True)

    # ---- fire
    def fire_loop():
        d = 4.0
        body = lowpass(noise(d, lp=0.5), 0.6)
        crackle = [0.0] * int(RATE * d)
        r = random.Random(9)
        for _ in range(90):
            p = r.randint(0, int(RATE * d) - 900)
            pop = [x * e for x, e in zip(highpass(noise(0.03, lp=0.95), 0.3), env_ad(0.03, 0.001, 0.02, 2))]
            for i, s in enumerate(pop):
                crackle[p + i] += s * r.uniform(0.3, 0.9)
        lfo = [0.8 + 0.2 * math.sin(2 * math.pi * 2.3 * i / RATE) for i in range(int(RATE * d))]
        return make_loop(combine(gain([b * l for b, l in zip(body, lfo)], 0.5), gain(crackle, 0.5)), 0.5)
    put('campfire_loop', fire_loop, loop=True)
    put('fire_ignite', lambda: combine(gain([x * e for x, e in zip(highpass(noise(0.7, lp=0.9), 0.2), env_ad(0.7, 0.05, 0.6, 1.2))], 0.6),
                                       gain(sine(0.7, 180, 320, a=0.05, curve=1.2), 0.25)))
    for v in range(3):
        put(f'ember_{v}', lambda v=v: gain([x * e for x, e in zip(highpass(noise(0.08, lp=0.95), 0.3), env_ad(0.08, 0.001, 0.05, 2.2))], 0.5))

    # ---- human
    def hurt(v):
        f0 = 300 + v * 60
        body = saw(0.3, f0, f0 * 0.6, a=0.01, curve=1.5)
        return gain(lowpass(combine(body, gain(noise(0.3, lp=0.5), 0.3)), 0.5), 0.4)
    for v in range(2): put(f'hurt_{v}', lambda v=v: hurt(v))
    put('human_death', lambda: gain(lowpass(combine(saw(1.1, 260, 70, a=0.02, curve=1.8), gain(noise(1.1, lp=0.3), 0.25)), 0.4), 0.45))
    def effort(v):
        d = 0.22
        body = combine(saw(d, 180 + v * 30, 130, a=0.02, curve=1.4), gain(noise(d, lp=0.6), 0.2))
        return gain(lowpass(body, 0.5), 0.3)
    for v in range(2): put(f'effort_{v}', lambda v=v: effort(v))
    def heartbeat():
        buf = silence(0.9)
        mix_at(buf, drum_low(0.22, 110, 42, 0.9), 0, 1.0)
        mix_at(buf, drum_low(0.22, 100, 40, 0.9), 0.28, 0.8)
        return buf
    put('heartbeat', heartbeat)

    # ---- creatures
    def growl(dur=0.9, f0=95, f1=65, rough=0.5, form=2.6):
        body = combine(saw(dur, f0, f1, a=0.05, curve=1.3), gain(noise(dur, lp=0.3), rough))
        lfo = [0.6 + 0.4 * abs(math.sin(2 * math.pi * 26 * i / RATE)) for i in range(int(RATE * dur))]
        body = [b * l for b, l in zip(body, lfo)]
        formant = sine(dur, f0 * form, f1 * form, a=0.05, curve=1.3)
        return gain(lowpass(combine(body, gain(formant, 0.4)), 0.5), 0.5)
    def roar(dur=1.4, f0=180, f1=90):
        body = combine(saw(dur, f0, f1, a=0.04, curve=1.4), gain(noise(dur, lp=0.45), 0.6))
        lfo = [0.55 + 0.45 * abs(math.sin(2 * math.pi * 30 * i / RATE)) for i in range(int(RATE * dur))]
        body = [b * l for b, l in zip(body, lfo)]
        body = soft_clip(body, 1.6)
        return gain(delay_fx(body, 0.09, 0.3, 2, 0.2), 0.55)
    def trumpet_voice(dur=1.2, f0=180, f1=300, wob=5.5):
        body = sine(dur, f0, f1, vib=0.06, vib_hz=wob, a=0.08, curve=1.2)
        body = combine(body, gain(saw(dur, f0, f1, a=0.08, curve=1.2), 0.25))
        return gain(soft_clip(lowpass(body, 0.6), 1.4), 0.5)
    def bird_chirp(f0, f1, d=0.14, rep=1, gap=0.08):
        buf = silence(d * rep + gap * (rep - 1) + 0.1)
        for i in range(rep):
            mix_at(buf, gain(sine(d, f0, f1, vib=0.04, vib_hz=40, a=0.01, curve=1.2), 0.4), i * (d + gap))
        return buf
    def howl():
        d = 2.2
        body = sine(d, 260, 480, vib=0.03, vib_hz=5.5, a=0.35, curve=0.7)
        body = combine(body, gain(sine(d, 520, 960, vib=0.03, vib_hz=5.5, a=0.4, curve=0.7), 0.3))
        # fall at end
        n = int(RATE * d)
        for i in range(n // 3, n):
            k = (i - n / 3) / (2 * n / 3)
            body[i] *= 1 - k * 0.25
        body2 = sine(d, 480, 300, a=0.3, curve=0.8)
        return gain(combine(body, gain(body2, 0.35)), 0.45)
    def hyenaLaugh(v):
        buf = silence(1.5)
        r = random.Random(v * 17)
        for i in range(8 + v * 2):
            f = r.uniform(500, 800) - i * 18
            mix_at(buf, gain(sine(0.09, f, f * 0.8, a=0.01, curve=1.4), 0.42), i * 0.11)
        return buf
    def squeal(f0=900, f1=1400, d=0.4):
        return gain(saw(d, f0, f1, a=0.01, curve=1.3), 0.22)
    def snort(dur=0.4, f=140):
        body = [x * e for x, e in zip(noise(dur, lp=0.45), env_ad(dur, 0.02, 0.3, 1.4))]
        pulse = [1 if (i // 90) % 2 == 0 else 0.35 for i in range(int(RATE * dur))]
        return gain([b * p for b, p in zip(body, pulse)], 0.6)
    def death_cry(f0, f1, d=1.0):
        body = combine(saw(d, f0, f1, a=0.03, curve=2.0), gain(noise(d, lp=0.4), 0.3))
        lfo = [0.6 + 0.4 * abs(math.sin(2 * math.pi * 20 * i / RATE)) for i in range(int(RATE * d))]
        return gain([b * l for b, l in zip(body, lfo)], 0.45)

    put('mammoth_call_0', lambda: trumpet_voice(1.3, 160, 280))
    put('mammoth_call_1', lambda: trumpet_voice(1.6, 140, 240, 4.2))
    put('mammoth_growl', lambda: growl(1.2, 70, 50, 0.6, 2.2))
    put('mammoth_death', lambda: combine(gain(trumpet_voice(1.0, 180, 90), 0.8), gain(death_cry(160, 50, 1.6), 0.8)))
    put('sabertooth_growl', lambda: growl(1.0, 110, 80, 0.55, 2.8))
    put('sabertooth_snarl', lambda: soft_clip(gain(combine(saw(0.5, 200, 130, a=0.01, curve=1.6), gain(noise(0.5, lp=0.6), 0.5)), 0.5), 1.8))
    put('sabertooth_roar', lambda: roar(1.5, 220, 100))
    put('sabertooth_death', lambda: death_cry(400, 120, 1.1))
    put('bear_growl', lambda: growl(1.3, 85, 60, 0.7, 2.4))
    put('bear_roar', lambda: roar(1.6, 160, 75))
    put('bear_huff', lambda: snort(0.5, 120))
    put('bear_death', lambda: death_cry(220, 60, 1.5))
    put('bison_bellow_0', lambda: trumpet_voice(1.1, 180, 120, 6))
    put('bison_bellow_1', lambda: trumpet_voice(1.3, 200, 130, 7))
    put('bison_snort', lambda: snort(0.45, 150))
    put('bison_death', lambda: death_cry(190, 70, 1.4))
    put('wolf_howl', howl)
    put('wolf_pack_howl', lambda: combine(howl(), gain(delay_fx(howl(), 0.35, 0.5, 2, 0.6), 0.8)))
    put('wolf_growl_0', lambda: growl(0.9, 120, 90, 0.5, 3.0))
    put('wolf_growl_1', lambda: growl(1.1, 100, 75, 0.55, 3.2))
    put('wolf_death', lambda: death_cry(500, 160, 0.9))
    put('lion_roar', lambda: roar(1.8, 140, 65))
    put('lion_death', lambda: death_cry(240, 55, 1.6))
    for v in range(3): put(f'boar_grunt_{v}', lambda v=v: growl(0.35 + v * 0.1, 130 - v * 15, 90, 0.6, 3.4))
    put('boar_squeal', lambda: squeal(800, 1500, 0.5))
    put('elk_bellow', lambda: trumpet_voice(1.0, 240, 160, 8))
    put('elk_alarm', lambda: bird_chirp(700, 420, 0.18, 2, 0.12))
    put('reindeer_bellow', lambda: trumpet_voice(0.9, 260, 190, 9))
    for v in range(3): put(f'hyena_{v}', lambda v=v: hyenaLaugh(v))
    put('rhino_snort', lambda: snort(0.6, 100))
    put('muskox_grunt', lambda: growl(0.6, 110, 85, 0.5, 2.6))
    put('hare_squeal', lambda: squeal(1200, 2000, 0.25))
    put('ptarmigan_0', lambda: bird_chirp(1400, 900, 0.12, 3, 0.1))
    put('ptarmigan_1', lambda: bird_chirp(1100, 1500, 0.1, 2, 0.12))
    put('auk_0', lambda: bird_chirp(700, 500, 0.16, 2, 0.15))
    put('auk_1', lambda: bird_chirp(600, 800, 0.14, 3, 0.1))
    for v in range(6):
        put(f'bird_chirp_{v}', lambda v=v: bird_chirp(1600 + v * 260, 2200 + v * 300, 0.1 + (v % 3) * 0.04, 1 + v % 3, 0.07))
    def flap(v):
        d = 0.3
        body = [x * e for x, e in zip(noise(d, lp=0.6), env_ad(d, 0.01, 0.2, 1.2))]
        lfo = [abs(math.sin(2 * math.pi * (9 + v * 2) * i / RATE)) for i in range(int(RATE * d))]
        return gain([b * l for b, l in zip(body, lfo)], 0.5)
    for v in range(2): put(f'bird_flap_{v}', lambda v=v: flap(v))
    put('owl_hoot_0', lambda: combine(gain(sine(0.35, 420, 380, a=0.04, curve=1.2), 0.4), gain(sine(0.3, 380, 340, a=0.04, curve=1.3), 0.35)))
    put('owl_hoot_1', lambda: gain(sine(0.5, 360, 330, vib=0.02, vib_hz=8, a=0.06, curve=1.3), 0.4))
    put('hawk_screech', lambda: gain(saw(0.5, 1800, 900, a=0.01, curve=1.5), 0.16))
    put('crow_0', lambda: bird_chirp(620, 420, 0.2, 2, 0.14))
    def flint_strike(v=0):
        return combine(gain(sine(0.1, 1900, 700, a=0.001, curve=3), 0.5),
                       gain([x * e for x, e in zip(highpass(noise(0.1, lp=0.95), 0.3), env_ad(0.1, 0.001, 0.04, 2.4))], 0.6))
    put('flint_strike', flint_strike)
    def wind_howl():
        d = 3.0
        body = sine(d, 220, 340, vib=0.05, vib_hz=1.7, a=0.8, curve=1.4)
        body = combine(body, gain(lowpass(noise(d, lp=0.3), 0.8), 0.4))
        return make_loop(gain(body, 0.3), 0.8)
    put('wind_howl', wind_howl, loop=True)

    # ---- ambience loops
    def amb_birds(d=8.0, density=14, base=1800):
        buf = silence(d)
        r = random.Random(77)
        for _ in range(density):
            p = r.uniform(0.2, d - 0.6)
            f = base * r.uniform(0.7, 1.4)
            mix_at(buf, gain(bird_chirp(f, f * r.uniform(0.7, 1.3), r.uniform(0.08, 0.16), r.randint(1, 3), 0.09), r.uniform(0.25, 0.5)), p)
        return make_loop(gain(lowpass(buf, 0.85), 0.8), 0.6)
    def amb_wind(d=8.0, lpf=0.3, f=0.14):
        return make_loop(wind_loop(d, f) if lpf == 0.3 else wind_loop(d, f), 0.8)
    def amb_drips(d=8.0):
        buf = silence(d)
        r = random.Random(31)
        for _ in range(9):
            p = r.uniform(0.3, d - 0.5)
            f = r.uniform(900, 1600)
            mix_at(buf, gain(sine(0.14, f, f * 2.2, a=0.002, curve=2.4), 0.3), p)
            mix_at(buf, gain(sine(0.1, f * 1.5, f * 2.8, a=0.002, curve=2.8), 0.15), p + 0.07)
        return make_loop(buf, 0.5)
    def amb_ocean(d=8.0):
        body = lowpass(noise(d, lp=0.25), 0.5)
        lfo = [0.45 + 0.55 * abs(math.sin(2 * math.pi * 0.12 * i / RATE)) for i in range(int(RATE * d))]
        hiss = gain(highpass(noise(d, lp=0.9), 0.3), 0.25)
        return make_loop(combine([b * l for b, l in zip(body, lfo)], gain([h * l for h, l in zip(hiss, lfo)], 0.6)), 0.8)
    def amb_insects(d=8.0):
        body = silence(d)
        for i in range(int(RATE * d)):
            body[i] = math.sin(2 * math.pi * 3400 * i / RATE) * (0.5 + 0.5 * math.sin(2 * math.pi * 34 * i / RATE)) * 0.1
        lfo = [0.5 + 0.5 * math.sin(2 * math.pi * 0.25 * i / RATE) for i in range(int(RATE * d))]
        return make_loop([b * l for b, l in zip(body, lfo)], 0.6)
    put('amb_forest_day', lambda: combine(gain(amb_wind(8.0, 0.3, 0.12), 0.8), gain(amb_birds(8.0, 12), 0.9)), loop=True)
    put('amb_forest_night', lambda: combine(gain(amb_wind(8.0, 0.3, 0.09), 0.9),
                                            gain(delay_fx(amb_birds(8.0, 4, 1100), 0.3, 0.4, 2, 0.3), 0.7)), loop=True)
    put('amb_birds', lambda: amb_birds(8.0, 18), loop=True)
    put('amb_savanna', lambda: combine(gain(amb_wind(8.0, 0.3, 0.16), 1.0), gain(amb_birds(8.0, 5, 1400), 0.5), gain(amb_insects(8.0), 0.5)), loop=True)
    put('amb_desert', lambda: combine(gain(amb_wind(8.0, 0.3, 0.2), 1.1), gain(amb_insects(8.0), 0.25)), loop=True)
    put('amb_tundra', lambda: gain(combine(amb_wind(8.0, 0.3, 0.24), gain(highpass(noise(8.0, lp=0.9), 0.25), 0.3)), 1.1), loop=True)
    put('amb_jungle', lambda: combine(gain(amb_birds(8.0, 22, 2100), 0.9), gain(amb_insects(8.0), 0.8), gain(amb_wind(8.0, 0.3, 0.08), 0.5)), loop=True)
    put('amb_swamp', lambda: combine(gain(amb_wind(8.0, 0.3, 0.07), 0.7), gain(amb_drips(8.0), 0.8),
                                     gain(delay_fx(amb_birds(8.0, 3, 800), 0.4, 0.4, 2, 0.4), 0.6)), loop=True)
    put('amb_cave', lambda: gain(delay_fx(amb_drips(8.0), 0.28, 0.45, 3, 0.35), 1.0), loop=True)
    put('amb_ocean', lambda: amb_ocean(8.0), loop=True)
    put('amb_steppe', lambda: combine(gain(amb_wind(8.0, 0.3, 0.18), 1.0), gain(amb_birds(8.0, 4, 1200), 0.4)), loop=True)

    # ---- write everything
    n = 0
    for name, (fn, loop) in sorted(reg.items()):
        try:
            buf = fn()
            save(name, buf, loop=loop)
            n += 1
        except Exception as e:
            print(f'WARN {name}: {e}')
    print(f'sfx written: {n}')
    return n

def _laugh():
    buf = silence(1.0)
    for i in range(5):
        f = 340 - i * 18
        mix_at(buf, gain(sine(0.12, f, f * 0.85, a=0.01, curve=1.3), 0.32), i * 0.15)
    return buf

# ================================================================= MUSIC
SEM = {'minPent': [0, 3, 5, 7, 10], 'majPent': [0, 2, 4, 7, 9], 'dor': [0, 2, 3, 5, 7, 9, 10],
       'min': [0, 2, 3, 5, 7, 8, 10], 'phry': [0, 1, 3, 5, 7, 8, 10]}

def nf(root, semis):
    return root * (2 ** (semis / 12.0))

def v_flute(dur, f, vol=0.5):
    body = sine(dur, f, f, vib=0.012, vib_hz=5.2, a=0.06, curve=1.1)
    breath = gain(highpass(noise(dur, lp=0.92), 0.3), 0.06)
    e = env_ad(dur, 0.06, dur, 1.2)
    return [(b + br) * ev * vol for b, br, ev in zip(body, breath, e)]

def v_pluck(f, vol=0.5, bright=0.55):
    return gain(karplus(0.7, f, decay=0.994, bright=bright), vol)

def v_bell(f, dur=1.8, vol=0.4):
    parts = [(1.0, 1.0), (2.76, 0.5), (5.4, 0.25)]
    out = silence(dur)
    for mult, g in parts:
        mix_at(out, gain(sine(dur, f * mult, f * mult, a=0.002, curve=2.6), g * vol), 0)
    return out

def v_pad(dur, f, vol=0.3):
    a = combine(saw(dur, f * 0.997, f * 0.997, a=dur * 0.4, curve=0.8),
                saw(dur, f * 1.004, f * 1.004, a=dur * 0.4, curve=0.8),
                sine(dur, f * 0.5, f * 0.5, a=dur * 0.4, curve=0.8))
    return gain(lowpass(a, 0.18), vol)

def v_drone(dur, f, vol=0.22):
    a = combine(saw(dur, f, f * 1.001, a=0.6, curve=0.6), sine(dur, f / 2, f / 2, a=0.8, curve=0.5))
    lfo = [0.8 + 0.2 * math.sin(2 * math.pi * 0.13 * i / RATE) for i in range(int(RATE * dur))]
    return gain([x * l for x, l in zip(lowpass(a, 0.12), lfo)], vol)

def seq_track(name, bpm, bars, root, scale, plan, loop=True, reverb=0.24):
    """plan: fn(bar) -> list of (voice, params) events; voices called as fn(dur_beats, freq, vol)."""
    spb = 60.0 / bpm
    total = bars * 4 * spb
    buf = silence(total + 0.5)
    for bar in range(bars):
        events = plan(bar)
        for ev in events:
            kind = ev[0]; beat = ev[1]
            pos = (bar * 4 + beat) * spb
            if kind == 'flute':
                dur_b, semi, vol = ev[2], ev[3], ev[4]
                mix_at(buf, v_flute(dur_b * spb * 0.95, nf(root, semi), vol), pos)
            elif kind == 'pluck':
                semi, vol = ev[2], ev[3]
                mix_at(buf, v_pluck(nf(root, semi), vol), pos)
            elif kind == 'bell':
                semi, vol = ev[2], ev[3]
                mix_at(buf, v_bell(nf(root, semi), 2.0, vol), pos)
            elif kind == 'dronelow':
                semi, vol = ev[2], ev[3]
                mix_at(buf, v_drone(4 * spb, nf(root, semi), vol), pos)
            elif kind == 'pad':
                semi, dur_b, vol = ev[2], ev[3], ev[4]
                mix_at(buf, v_pad(dur_b * spb, nf(root, semi), vol), pos)
            elif kind == 'dlow':
                mix_at(buf, drum_low(0.3, 150, 46), pos, ev[2])
            elif kind == 'dhi':
                mix_at(buf, drum_hi(), pos, ev[2])
            elif kind == 'shk':
                mix_at(buf, shaker(), pos, ev[2])
    out = delay_fx(buf[:int(total * RATE)], 0.19, 0.35, 3, reverb)
    save(name, make_loop(out, 0.5) if loop else out, loop=loop, peak=0.8)

def R(seed):
    return random.Random(seed)

def gen_all_music():
    # 1. menu theme — A minor pentatonic 72bpm, 16 bars
    r = R(11)
    def menu(bar):
        ev = []
        if bar % 4 == 0:
            ev += [('dronelow', 0, 0, 0.22), ('dronelow', 0, 7, 0.14)]
        if bar % 2 == 0:
            ev.append(('dhi', 0, 0.5)); ev.append(('dhi', 2, 0.4))
        ev.append(('dlow', 0, 0.8)); ev.append(('dlow', 2.5, 0.5))
        melody = [0, 3, 5, 7, 10, 12, 10, 7]
        for i in range(4):
            if r.random() < 0.8:
                s = melody[(bar * 2 + i) % len(melody)] + (12 if r.random() < 0.25 else 0)
                ev.append(('flute', i, 1.4, s, 0.42))
        if bar % 4 == 3:
            ev.append(('bell', 2, 12, 0.3))
        return ev
    seq_track('music/menu_theme', 72, 16, nf(220, 0), SEM['minPent'], menu)

    # 2. explore serene — 66bpm sparse
    def serene(bar):
        ev = []
        if bar % 4 == 0:
            ev.append(('pad', 0, 0, 8, 0.22))
            ev.append(('dronelow', 0, -12, 0.16))
        mel = [12, 10, 7, 5, 7, 3]
        if r2.random() < 0.7:
            ev.append(('flute', r2.choice([0, 1, 2]), 2.2, mel[(bar + r2.randint(0, 3)) % len(mel)], 0.4))
        if bar % 2 == 1:
            ev.append(('pluck', 1.5, 7, 0.24))
        return ev
    r2 = R(22)
    seq_track('music/explore_serene', 66, 16, nf(196, 0), SEM['majPent'], serene)

    # 3. explore tension — 84bpm dor
    def tension(bar):
        ev = []
        ev.append(('dronelow', 0, 0, 0.2))
        for b in (0, 1.5, 2.5, 3.5):
            ev.append(('dhi', b, 0.35 if b != 0 else 0.5))
        ev.append(('dlow', 0, 0.7))
        if bar % 2 == 1:
            ev.append(('pluck', 2, 1, 0.3)); ev.append(('pluck', 2.5, 0, 0.24))
        if bar % 4 == 3:
            ev.append(('flute', 0, 1.0, 15, 0.3))
        return ev
    seq_track('music/explore_tension', 84, 16, nf(196, 0), SEM['dor'], tension)

    # 4. combat — 132bpm aggressive phrygian
    def combat(bar):
        ev = []
        riff = [0, 0, 3, 0, 5, 3, 1, 0]
        for i in range(8):
            ev.append(('pluck', i * 0.5, riff[(bar + i) % len(riff)] - 12, 0.42 if i % 2 == 0 else 0.3))
        for b in (0, 1, 2, 3):
            ev.append(('dlow', b, 0.9))
            ev.append(('dhi', b + 0.5, 0.45))
        for b in (0.75, 2.25, 3.5):
            ev.append(('shk', b, 0.4))
        if bar % 4 == 3:
            ev.append(('flute', 2, 1.2, 13, 0.35)); ev.append(('flute', 3, 1.0, 12, 0.35))
        return ev
    seq_track('music/combat_battle', 132, 16, nf(220, 0), SEM['phry'], combat, reverb=0.18)

    # 5. hunt stalk — 60bpm heartbeat + plucks
    def stalk(bar):
        ev = [('dlow', 0, 0.8), ('dlow', 0.6, 0.55), ('dhi', 2, 0.3)]
        if bar % 2 == 0:
            ev.append(('pluck', 1, 0, 0.2)); ev.append(('pluck', 2.5, 3, 0.2))
        if bar % 4 == 2:
            ev.append(('flute', 2.5, 1.4, 10, 0.26))
        return ev
    seq_track('music/hunt_stalk', 60, 16, nf(185, 0), SEM['minPent'], stalk)

    # 6. winter — 58bpm bells D minor
    def winter(bar):
        ev = []
        if bar % 2 == 0:
            ev.append(('pad', 0, 0, 8, 0.2))
        seqn = [12, 10, 8, 7, 5, 3]
        ev.append(('bell', (bar % 3) * 1.0 + 0.5, seqn[bar % len(seqn)], 0.3))
        if bar % 4 == 0:
            ev.append(('dronelow', 0, -12, 0.15))
        return ev
    seq_track('music/season_winter', 58, 16, nf(174, 0), SEM['min'], winter)

    # 7. tribe dawn — 76bpm major, warm
    def dawn(bar):
        ev = []
        if bar % 4 == 0:
            ev.append(('pad', 0, 4, 8, 0.2))
            ev.append(('dronelow', 0, 0, 0.15))
        ev.append(('dhi', 0.5, 0.3)); ev.append(('dhi', 2, 0.3))
        mel = [4, 7, 9, 11, 9, 7]
        ev.append(('flute', 1, 1.6, mel[bar % len(mel)], 0.4))
        if bar % 2 == 0:
            ev.append(('pluck', 3, mel[(bar + 2) % len(mel)] - 12, 0.22))
        return ev
    seq_track('music/tribe_dawn', 76, 16, nf(233, 0), SEM['majPent'], dawn)

if __name__ == '__main__':
    which = sys.argv[1] if len(sys.argv) > 1 else 'all'
    total = 0
    if which in ('all', 'sfx'):
        total += gen_all_sfx()
    if which in ('all', 'music'):
        gen_all_music()
        print('music written: 7 tracks')
