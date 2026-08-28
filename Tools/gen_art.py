#!/usr/bin/env python3
"""
World Evolution Saga — procedural pixel-art generator.
Generates: player/NPC animation sets, 15 animal animation sets, terrain
variants, animated water, vegetation, item icons, themed 9-slice UI, VFX
sheets + matching Unity .meta files. Pure stdlib.
"""
import random, os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from assetlib import (Canvas, write_png_meta, rgb, shade, mix, speckle)

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Sprites')
RNG = random.Random(7)

def P(name):
    return os.path.join(ROOT, name + '.png')

def save_sprite(cv, name, ppu=256, border=None):
    cv.save(P(name))
    write_png_meta(P(name) + '.meta', ppu=ppu, sprite_mode=1,
                   border=border if border else (0, 0, 0, 0))

def save_sheet(name, cv, frames, frame_px=256, ppu=256):
    """cv: logical canvas 32*frames x 32 -> one row sheet."""
    cv.save(P(name))
    write_png_meta(P(name) + '.meta', ppu=ppu, sprite_mode=2,
                   sheet_frames=frames, sheet_name=os.path.basename(name),
                   frame_w=frame_px, frame_h=frame_px)

# ================================================================ PALETTES
SKIN = rgb(206, 154, 108); SKIN_D = rgb(166, 116, 82)
HAIR = rgb(58, 40, 28); BAND = rgb(140, 62, 44)
FUR = rgb(126, 88, 56); FUR_D = rgb(94, 64, 40); FUR_L = rgb(152, 110, 72)
BOOT = rgb(84, 58, 38)
OUT = (24, 16, 14, 255)
BLOOD = rgb(156, 40, 34)

# ================================================================ HUMANOID
def draw_humanoid(cv, view, pose, pal=None):
    """view: 'side'|'front'|'back'; pose dict. Facing right for side."""
    p = pal or {}
    skin = p.get('skin', SKIN); skin_d = p.get('skin_d', SKIN_D)
    hair = p.get('hair', HAIR); cloth = p.get('cloth', FUR)
    cloth_d = p.get('cloth_d', FUR_D); cloth_l = p.get('cloth_l', FUR_L)
    band = p.get('band', BAND); boot = p.get('boot', BOOT)
    import math as m

    hip_y, sh_y, head_y, head_r = 76, 52, 38, 10
    lean = pose.get('lean', 0)  # px forward lean of upper body
    bob = pose.get('bob', 0)
    hip_x, sh_x, head_x = 60, 62 + lean, 64 + lean
    sh_x, hip_x = sh_x, 62 + lean * 0.4
    leg_a_f = pose.get('leg_f', 0.0)   # radians, front leg swing
    leg_a_b = pose.get('leg_b', 0.0)
    arm_a_f = pose.get('arm_f', 0.0)
    arm_a_b = pose.get('arm_b', 0.0)
    crouch = pose.get('crouch', 0)
    if crouch:
        hip_y += crouch; sh_y += crouch * 1.3; head_y += crouch * 1.6

    if view == 'side':
        # legs (back leg first, darker)
        for ang, col in ((leg_a_b, shade(cloth_d, 0.9)), (leg_a_f, cloth_d)):
            kx = hip_x + m.sin(ang) * 12
            ky = hip_y + m.cos(ang) * 14
            fx = hip_x + m.sin(ang) * 24
            fy = min(116, hip_y + m.cos(ang) * 26 + (0 if pose.get('lift_f', 0) == 0 else 0))
            fy = 116 - pose.get('lift_' + ('f' if col == cloth_d and ang is leg_a_f else 'b'), 0)
            cv.line(hip_x, hip_y, kx, ky, col, 4)
            cv.line(kx, ky, fx, fy, col, 3)
            cv.rect(fx - 2, fy - 1, fx + 5, fy + 2, boot)  # foot
        # torso
        cv.rect(hip_x - 8, sh_y, hip_x + 9, hip_y + 2, cloth)
        cv.rect(hip_x - 8, sh_y, hip_x + 9, sh_y + 4, cloth_l)
        cv.rect(hip_x - 8, hip_y - 4, hip_x + 9, hip_y + 2, cloth_d)
        cv.rect(hip_x - 9, hip_y - 2, hip_x + 10, hip_y + 2, shade(band, 0.9))  # belt
        # back arm
        ax = sh_x + m.sin(arm_a_b) * 14
        ay = sh_y + m.cos(arm_a_b) * 16
        cv.line(sh_x - 2, sh_y + 3, ax, ay, skin_d, 3)
        cv.circle(ax, ay, 2, skin_d)
        # head
        cv.circle(head_x, head_y, head_r, skin)
        cv.ellipse(head_x - 2, head_y - 3, head_r - 1, head_r - 4, hair)
        cv.rect(head_x - head_r, head_y - 2, head_x + head_r, head_y, band)  # band
        cv.set(head_x + 5, head_y + 3, (30, 22, 20, 255))  # eye
        # front arm (holds item)
        hx = sh_x + m.sin(arm_a_f) * 15
        hy = sh_y + m.cos(arm_a_f) * 17
        cv.line(sh_x + 3, sh_y + 3, hx, hy, skin, 3)
        cv.circle(hx, hy, 2, skin)
        item = pose.get('item')
        if item == 'spear':
            sa = pose.get('item_angle', 0.0)
            dx, dy = m.sin(sa) * 30, -m.cos(sa) * 30
            cv.line(hx - dx, hy + dy, hx + dx, hy - dy, rgb(140, 96, 54, 255), 2)
            tx, ty = hx + dx * 1.25, hy - dy * 1.25
            cv.tri((tx, ty), (tx + m.sin(sa) * 8 + 2, ty - m.cos(sa) * 8),
                   (tx + m.sin(sa) * 8 - 2, ty - m.cos(sa) * 8 + 4), rgb(196, 198, 204, 255))
        elif item == 'axe':
            sa = pose.get('item_angle', 0.0)
            ex, ey = hx + m.sin(sa) * 16, hy - m.cos(sa) * 16
            cv.line(hx, hy, ex, ey, rgb(140, 96, 54, 255), 2)
            cv.tri((ex, ey), (ex + 8, ey - 6), (ex + 2, ey + 6), rgb(170, 172, 178, 255))
        elif item == 'torch':
            ex, ey = hx + 8, hy - 14
            cv.line(hx, hy, ex, ey, rgb(120, 82, 46, 255), 2)
            cv.circle(ex, ey - 2, 3, rgb(255, 176, 60, 255))
            cv.circle(ex, ey - 4, 2, rgb(255, 230, 140, 255))
    elif view == 'front':
        # legs
        cv.rect(hip_x - 7, hip_y, hip_x - 3, 114, cloth_d)
        cv.rect(hip_x + 3, hip_y, hip_x + 7, 114, cloth_d)
        cv.rect(hip_x - 8, 112, hip_x - 2, 116, boot)
        cv.rect(hip_x + 2, 112, hip_x + 8, 116, boot)
        # torso
        cv.rect(hip_x - 9, sh_y, hip_x + 9, hip_y + 2, cloth)
        cv.rect(hip_x - 9, sh_y, hip_x + 9, sh_y + 4, cloth_l)
        cv.rect(hip_x - 9, hip_y - 4, hip_x + 9, hip_y + 2, cloth_d)
        cv.rect(hip_x - 10, hip_y - 2, hip_x + 10, hip_y + 2, shade(band, 0.9))
        # arms
        for sgn, col in ((-1, skin_d), (1, skin)):
            ax = sh_x + sgn * (11 + m.sin(arm_a_f) * 4 * sgn)
            ay = sh_y + 18 - abs(arm_a_f) * 6
            cv.line(sh_x + sgn * 7, sh_y + 3, ax, ay, col, 3)
            cv.circle(ax, ay, 2, col)
        # head
        cv.circle(head_x, head_y, head_r, skin)
        cv.ellipse(head_x, head_y - 4, head_r - 1, head_r - 5, hair)
        cv.rect(head_x - head_r, head_y - 2, head_x + head_r, head_y, band)
        for exx in (head_x - 4, head_x + 4):
            cv.set(exx, head_y + 3, (30, 22, 20, 255))
    else:  # back
        cv.rect(hip_x - 7, hip_y, hip_x - 3, 114, cloth_d)
        cv.rect(hip_x + 3, hip_y, hip_x + 7, 114, cloth_d)
        cv.rect(hip_x - 8, 112, hip_x - 2, 116, boot)
        cv.rect(hip_x + 2, 112, hip_x + 8, 116, boot)
        cv.rect(hip_x - 9, sh_y, hip_x + 9, hip_y + 2, cloth)
        cv.rect(hip_x - 9, hip_y - 4, hip_x + 9, hip_y + 2, cloth_d)
        cv.rect(hip_x - 10, hip_y - 2, hip_x + 10, hip_y + 2, shade(band, 0.9))
        for sgn, col in ((-1, skin_d), (1, skin)):
            ax = sh_x + sgn * (11 + m.sin(arm_a_f) * 4)
            ay = sh_y + 18 - max(0, m.sin(arm_a_f)) * 16
            cv.line(sh_x + sgn * 7, sh_y + 3, ax, ay, col, 3)
            cv.circle(ax, ay, 2, col)
        cv.circle(head_x, head_y, head_r, hair)
        cv.ellipse(head_x, head_y + 2, head_r - 3, head_r - 4, shade(hair, 1.25))

def human_frames(view, frames, phase_of, pose_fn, pal=None):
    out = []
    for i in range(frames):
        ph = phase_of(i, frames)
        cv = Canvas(128, 128)
        draw_humanoid(cv, view, pose_fn(ph, i), pal)
        cv.outline(OUT)
        out.append(cv)
    return out

def gen_player():
    D = 'Player'
    def walk_pose(ph, i):
        return dict(leg_f=math.sin(ph) * 0.42, leg_b=-math.sin(ph) * 0.42,
                    arm_f=-math.sin(ph) * 0.5, arm_b=math.sin(ph) * 0.5,
                    bob=abs(math.cos(ph)) * -1, lean=1)
    def idle_pose(ph, i):
        return dict(bob=-i, arm_f=0.12, arm_b=-0.12)
    def attack_pose(ph, i):
        if i == 0: return dict(item='spear', item_angle=-2.4, arm_f=-2.2, lean=-2, crouch=2)
        if i == 1: return dict(item='spear', item_angle=1.35, arm_f=1.45, lean=5, leg_f=0.35, leg_b=-0.3)
        return dict(item='spear', item_angle=1.2, arm_f=1.2, lean=3)
    def gather_pose(ph, i):
        if i == 0: return dict(item='axe', item_angle=-2.9, arm_f=-2.6, crouch=1)
        if i == 1: return dict(item='axe', item_angle=0.9, arm_f=1.1, lean=4, crouch=4)
        return dict(item='axe', item_angle=0.8, arm_f=1.0, lean=3, crouch=3)
    def swim_pose(ph, i):
        return dict(arm_f=math.sin(ph) * 2.4 - 1.2, arm_b=-math.sin(ph) * 2.4 - 1.2)
    def climb_pose(ph, i):
        return dict(arm_f=(0.9 if i % 2 else 1.6), arm_b=(-0.9 if i % 2 else -1.6))
    def hit_pose(ph, i):
        return dict(lean=-4, arm_f=-0.9, arm_b=0.6, crouch=2)

    actions = {
        'walk': (6, walk_pose, 8), 'idle': (2, idle_pose, 3),
        'attack': (3, attack_pose, 10), 'gather': (3, gather_pose, 9),
        'swim': (4, swim_pose, 8), 'climb': (2, climb_pose, 6),
        'hit': (1, hit_pose, 1), 'die': (3, None, 1),
    }
    dirs = ['south', 'north', 'east', 'west', 'northeast', 'northwest', 'southeast', 'southwest']
    for action, (frames, pose_fn, fps) in actions.items():
        for d in dirs:
            view = {'south': 'front', 'north': 'back'}.get(d, 'side')
            if action == 'die':
                # dedicated fall (side view only, reused for all dirs)
                continue
            for i in range(frames):
                ph = i / frames * 2 * math.pi
                cv = Canvas(128, 128)
                draw_humanoid(cv, view, pose_fn(ph, i))
                cv.outline(OUT)
                save_sprite(cv, f'{D}/{action.capitalize()}/{d}/player_{d}_{action}_{i}', ppu=512)
    # death (side view)
    for i, ang in enumerate((12, 48, 78)):
        cv = Canvas(128, 128)
        draw_humanoid(cv, 'side', dict(lean=-6, arm_f=-1.4, arm_b=0.8, crouch=6 + i * 8))
        cv.outline(OUT)
        cv.rotate(ang, (64, 112))
        save_sprite(cv, f'{D}/Die/south/player_south_die_{i}', ppu=512)

def gen_npc(pal, name):
    for d in ['south', 'north', 'east', 'west', 'northeast', 'northwest', 'southeast', 'southwest']:
        view = {'south': 'front', 'north': 'back'}.get(d, 'side')
        for i in range(4):
            ph = i / 4 * 2 * math.pi
            cv = Canvas(128, 128)
            draw_humanoid(cv, view, dict(leg_f=math.sin(ph) * 0.4, leg_b=-math.sin(ph) * 0.4,
                                         arm_f=-math.sin(ph) * 0.4, arm_b=math.sin(ph) * 0.4), pal)
            cv.outline(OUT)
            save_sprite(cv, f'NPC/{name}/{d}/villager_{d}_{i}', ppu=512)

# ================================================================ QUADRUPED
def draw_quad(cv, sp, pose):
    """sp: species params dict. pose: dict(phase, attack=idx|None, dead=idx|None)."""
    import math as m
    facing_right = True
    body = sp['body']; belly = sp['belly']; light = sp['light']; dark = sp['dark']
    bx = sp.get('bx', 62); by = sp.get('by', 74)
    rx, ry = sp['rx'], sp['ry']
    ground = 118
    ph = pose.get('phase', 0.0)
    stride = sp.get('stride', 5)
    legw = sp.get('legw', 3)
    dead = pose.get('dead')
    attack = pose.get('attack')

    def leg_pair(x, hip_y, phase_off, lift):
        for poff, col in ((phase_off + math.pi, dark), (phase_off, body)):
            dx = m.sin(ph + poff) * stride
            l = lift * max(0.0, m.sin(ph + poff + math.pi / 2)) if dead is None else 0
            fx, fy = x + dx, ground - l
            kx, ky = x + dx * 0.25, (hip_y + fy) / 2
            cv.line(x, hip_y, kx, ky, col, legw)
            cv.line(kx, ky, fx, fy, col, legw)

    if dead is not None:
        if dead < 2:
            cv.rotate(0, (64, 90))  # no-op keeps API symmetrical
        # lying pose: body on ground, head flat
        cv.ellipse(bx, 108, rx * 1.05, ry * 0.62, body)
        cv.ellipse(bx, 112, rx * 0.9, ry * 0.34, belly)
        hx = bx + rx * 0.95
        cv.circle(hx, 104, sp['head_r'] * 0.9, body)
        cv.rect(hx + sp['head_r'] * 0.6, 104, hx + sp['head_r'] * 0.6 + 6, 109, body)  # muzzle
        cv.hline(hx - 2, hx + 2, 102, (30, 22, 20, 255))  # closed eye
        for lx in (bx - rx * 0.6, bx - rx * 0.2, bx + rx * 0.2, bx + rx * 0.5):
            cv.hline(lx, lx + 10, 114, dark)
        tail = sp.get('tail')
        if tail: tail(cv, bx - rx * 0.95, 106, dark, flat=True)
        return

    lunge = 0
    if attack is not None:
        lunge = (0, 7, 3)[attack]

    hip_y = by + ry * 0.55
    leg_pair(bx - rx * 0.55, hip_y, 0, 3)            # back legs
    leg_pair(bx + rx * 0.55, hip_y, math.pi, 3)      # front legs
    # body
    cv.ellipse(bx + lunge * 0.3, by, rx, ry, body)
    cv.ellipse(bx + lunge * 0.3, by + ry * 0.35, rx * 0.85, ry * 0.5, belly)
    cv.ellipse(bx + lunge * 0.3, by - ry * 0.45, rx * 0.8, ry * 0.32, light)
    if sp.get('hump'):
        cv.ellipse(bx + rx * 0.35, by - ry * 0.8, rx * 0.35, ry * 0.4, light)
    if sp.get('skirt'):  # muskox
        cv.rect(bx - rx * 0.9, by, bx + rx * 0.85, by + ry * 1.5, belly)
    if sp.get('wool'):
        for _ in range(26):
            wx = bx - rx + RNG.random() * rx * 2; wy = by - ry + RNG.random() * ry * 1.4
            cv.set(int(wx), int(wy), dark)
    if sp.get('spots'):
        for _ in range(9):
            wx = bx - rx * 0.7 + RNG.random() * rx * 1.4
            wy = by - ry * 0.5 + RNG.random() * ry
            cv.circle(int(wx), int(wy), 1, sp['dark'])
    if sp.get('mane_line'):
        cv.line(bx - rx * 0.3, by - ry * 0.9, bx + rx * 0.5, by - ry * 0.9, dark, 2)

    # neck + head
    head_r = sp['head_r']
    neck_from = (bx + rx * 0.72, by - ry * 0.35)
    head_dx = rx * 0.55 + head_r + (4 if sp.get('long_neck') else 0) + lunge
    head_dy = sp.get('head_dy', -ry * 0.9)
    if attack == 1:
        head_dy = -ry * 0.2
    hx, hy = neck_from[0] + head_dx * 0.6, neck_from[1] + head_dy * 0.55
    if sp.get('long_neck'):
        cv.line(neck_from[0], neck_from[1], hx, hy, body, sp.get('neckw', 4))
    else:
        cv.line(neck_from[0], neck_from[1], hx, hy, body, sp.get('neckw', 5))
    cv.circle(hx, hy, head_r, body)
    mz = sp.get('muzzle', 6)
    cv.rect(hx + head_r * 0.5, hy - 2, hx + head_r * 0.5 + mz, hy + 3, body)
    cv.rect(hx + head_r * 0.5 + mz - 2, hy, hx + head_r * 0.5 + mz, hy + 3, dark)  # nose
    cv.set(int(hx + 1), int(hy - 1), (26, 18, 16, 255))
    # ears / antlers / horns
    if sp.get('ears_round'):
        cv.circle(hx - 2, hy - head_r + 1, 2, dark)
        cv.circle(hx + 3, hy - head_r + 1, 2, dark)
    if sp.get('ears_point'):
        cv.tri((hx - 3, hy - head_r + 2), (hx - 5, hy - head_r - 5), (hx + 1, hy - head_r + 1), dark)
        cv.tri((hx + 2, hy - head_r + 2), (hx + 4, hy - head_r - 4), (hx + 6, hy - head_r + 2), dark)
    if sp.get('antlers'):
        ac = sp.get('antler_col', rgb(120, 92, 60, 255))
        for sgn, bx0 in ((-1, hx - 1), (1, hx + 3)):
            bx1, by1 = hx + bx0 - hx + sgn * 2, hy - head_r - 6
            cv.line(hx + sgn * 2, hy - head_r + 2, bx1 + sgn * 2, by1 - 4, ac, 2)
            cv.line(bx1 - 1, by1 - 1, bx1 + sgn * 7, by1 - 4, ac, 2)
            cv.line(bx1 + sgn * 1, by1 - 3, bx1 + sgn * 5, by1 - 9, ac, 2)
    if sp.get('horns_up'):
        ac = rgb(198, 182, 150, 255)
        cv.line(hx - 1, hy - head_r + 2, hx - 6, hy - head_r - 5, ac, 2)
        cv.line(hx + 4, hy - head_r + 2, hx + 9, hy - head_r - 5, ac, 2)
    if sp.get('horns_curve'):  # bison/muskox
        ac = rgb(190, 172, 140, 255)
        cv.line(hx, hy - head_r + 3, hx - 7, hy - head_r - 2, ac, 2)
        cv.line(hx - 7, hy - head_r - 2, hx - 10, hy - head_r + 4, ac, 2)
        cv.line(hx + 5, hy - head_r + 3, hx + 12, hy - head_r - 2, ac, 2)
    if sp.get('sabers'):
        cv.tri((hx + head_r * 0.5, hy + 2), (hx + head_r * 0.5 + 2, hy + 12), (hx + head_r * 0.5 + 4, hy + 2),
               rgb(232, 228, 214, 255))
    if sp.get('tusks'):  # mammoth
        ac = rgb(226, 218, 198, 255)
        cv.line(hx + head_r * 0.4, hy + 4, hx + head_r * 0.4 - 8, hy + 16, ac, 3)
        cv.line(hx + head_r * 0.4 + 4, hy + 3, hx - 2, hy + 18, ac, 3)
    if sp.get('trunk'):
        tc = dark
        pts = [(hx + head_r * 0.6, hy + 1), (hx + head_r * 0.8, hy + 8),
               (hx + head_r * 0.6 - 2 + lunge, hy + 15), (hx + head_r * 0.3, hy + 18)]
        cv.polyline(pts, tc, 3)
    if sp.get('horn_nose'):  # rhino
        ac = rgb(214, 200, 172, 255)
        cv.tri((hx + head_r * 0.8, hy - 1), (hx + head_r * 0.8 + 7, hy + 2), (hx + head_r * 0.8 + 1, hy + 4), ac)
        cv.tri((hx + head_r * 0.4, hy + 0), (hx + head_r * 0.4 + 4, hy + 3), (hx + head_r * 0.4 + 1, hy + 4), ac)
    if sp.get('mane'):  # lion
        cv.ellipse(hx, hy, head_r + 3, head_r + 3, sp['dark'])
        cv.circle(hx, hy, head_r, body)
        cv.set(int(hx + 1), int(hy - 1), (26, 18, 16, 255))
        cv.rect(hx + head_r * 0.5, hy - 2, hx + head_r * 0.5 + 5, hy + 3, body)
    if attack == 1:  # open mouth
        cv.rect(hx + head_r * 0.5, hy + 1, hx + head_r * 0.5 + mz, hy + 5, (90, 24, 20, 255))
        cv.set(int(hx + head_r * 0.5 + 1), int(hy + 2), (240, 236, 224, 255))

    # tail
    tail = sp.get('tail')
    if tail:
        tail(cv, bx - rx * 0.95, by - ry * 0.3, dark)
    if sp.get('beard'):
        cv.rect(hx + 1, hy + head_r - 2, hx + 4, hy + head_r + 5, belly)

def tail_stub(cv, x, y, col, flat=False):
    cv.circle(x - 2, y - 2, 3, col)

def tail_long(cv, x, y, col, flat=False):
    cv.polyline([(x, y), (x - 8, y + 4), (x - 14, y + 9)], col, 2)
    cv.circle(x - 15, y + 10, 2, col)

def tail_bushy(cv, x, y, col, flat=False):
    cv.polyline([(x, y), (x - 7, y + 3), (x - 12, y - 2)], col, 3)
    cv.circle(x - 13, y - 3, 3, col)

def tail_tuft(cv, x, y, col, flat=False):
    cv.line(x, y, x - 10, y + 3, col, 2)
    cv.circle(x - 11, y + 3, 2, col)

SPECIES = {
    'Mammoth': dict(body=rgb(124, 94, 70, 255), belly=rgb(96, 70, 52, 255), light=rgb(150, 118, 88, 255),
                    dark=rgb(84, 60, 46, 255), rx=30, ry=17, head_r=9, legw=5, stride=4,
                    trunk=True, tusks=True, wool=True, ears_round=True, tail=tail_stub, by=72),
    'Sabertooth': dict(body=rgb(196, 158, 104, 255), belly=rgb(168, 132, 84, 255), light=rgb(216, 182, 128, 255),
                       dark=rgb(140, 106, 66, 255), rx=22, ry=12, head_r=8, legw=3, stride=7,
                       sabers=True, spots=True, tail=tail_long, ears_point=True),
    'CaveBear': dict(body=rgb(104, 78, 58, 255), belly=rgb(80, 58, 44, 255), light=rgb(128, 98, 72, 255),
                     dark=rgb(70, 50, 38, 255), rx=26, ry=16, head_r=9, legw=4, stride=4,
                     ears_round=True, tail=tail_stub, by=76),
    'Bison': dict(body=rgb(110, 78, 56, 255), belly=rgb(84, 58, 42, 255), light=rgb(136, 98, 70, 255),
                  dark=rgb(70, 48, 36, 255), rx=28, ry=16, head_r=10, legw=4, stride=4,
                  hump=True, horns_curve=True, beard=True, wool=True, tail=tail_tuft, by=74),
    'WoollyRhino': dict(body=rgb(142, 128, 112, 255), belly=rgb(112, 100, 86, 255), light=rgb(166, 152, 134, 255),
                        dark=rgb(98, 88, 76, 255), rx=30, ry=17, head_r=9, legw=5, stride=3,
                        horn_nose=True, wool=True, ears_point=True, tail=tail_stub, by=72),
    'CaveLion': dict(body=rgb(198, 158, 106, 255), belly=rgb(170, 132, 86, 255), light=rgb(218, 182, 130, 255),
                     dark=rgb(116, 66, 38, 255), rx=24, ry=13, head_r=9, legw=3, stride=7,
                     mane=True, tail=tail_tuft, by=76),
    'DireWolf': dict(body=rgb(134, 130, 126, 255), belly=rgb(106, 102, 98, 255), light=rgb(160, 156, 150, 255),
                     dark=rgb(88, 84, 80, 255), rx=24, ry=12, head_r=8, legw=3, stride=8,
                     ears_point=True, tail=tail_bushy),
    'CaveHyena': dict(body=rgb(168, 142, 100, 255), belly=rgb(140, 116, 82, 255), light=rgb(190, 164, 120, 255),
                      dark=rgb(104, 84, 58, 255), rx=24, ry=13, head_r=9, legw=3, stride=6,
                      spots=True, ears_point=True, tail=tail_stub, by=78),
    'Reindeer': dict(body=rgb(150, 116, 88, 255), belly=rgb(122, 92, 70, 255), light=rgb(174, 138, 106, 255),
                     dark=rgb(104, 78, 60, 255), rx=26, ry=13, head_r=8, legw=3, stride=7,
                     antlers=True, long_neck=True, ears_point=True, tail=tail_stub, neckw=4),
    'MuskOx': dict(body=rgb(96, 84, 72, 255), belly=rgb(72, 62, 54, 255), light=rgb(120, 106, 92, 255),
                   dark=rgb(58, 50, 44, 255), rx=26, ry=15, head_r=9, legw=4, stride=3,
                   horns_curve=True, skirt=True, wool=True, hump=True, tail=tail_stub, by=76),
    'GiantElk': dict(body=rgb(158, 120, 84, 255), belly=rgb(130, 96, 68, 255), light=rgb(182, 142, 102, 255),
                     dark=rgb(110, 80, 56, 255), rx=28, ry=14, head_r=8, legw=3, stride=8,
                     antlers=True, long_neck=True, ears_point=True, tail=tail_stub, neckw=4),
    'WildBoar': dict(body=rgb(110, 88, 70, 255), belly=rgb(86, 68, 54, 255), light=rgb(134, 108, 86, 255),
                     dark=rgb(74, 58, 46, 255), rx=20, ry=13, head_r=8, legw=3, stride=5,
                     mane_line=True, ears_point=True, tail=tail_stub, horn_nose=False, by=84),
    'SnowHare': dict(body=rgb(226, 222, 214, 255), belly=rgb(200, 196, 188, 255), light=rgb(244, 240, 234, 255),
                     dark=rgb(178, 172, 164, 255), rx=11, ry=8, head_r=5, legw=2, stride=6,
                     ears_point=True, tail=tail_stub, by=94),
    'CavePtarmigan': dict(body=rgb(228, 226, 218, 255), belly=rgb(200, 196, 186, 255), light=rgb(246, 244, 238, 255),
                          dark=rgb(168, 162, 152, 255), rx=9, ry=7, head_r=4, legw=1, stride=2,
                          beak=True, by=100),
    'GreatAuk': dict(body=rgb(52, 54, 62, 255), belly=rgb(226, 226, 224, 255), light=rgb(80, 84, 94, 255),
                     dark=rgb(34, 36, 44, 255), rx=9, ry=8, head_r=5, legw=1, stride=2,
                     beak=True, by=98),
}

def bird_draw(cv, sp, pose):
    body = sp['body']; belly = sp['belly']; light = sp['light']; dark = sp['dark']
    by = sp['by']; ph = pose.get('phase', 0)
    flap = abs(math.sin(ph)) * 6
    dead = pose.get('dead')
    if dead is not None:
        cv.ellipse(62, 112, 10, 4, body)
        cv.circle(74, 110, 4, dark)
        return
    cv.ellipse(62, by, 10, 7 + flap * 0.2, body)
    cv.ellipse(62, by + 3, 8, 4, belly)
    # wing
    cv.ellipse(58, by - 4 + flap * 0.4, 7, 3 + flap * 0.5, dark)
    # head
    cv.circle(72, by - 8, 5, dark)
    cv.tri((76, by - 8), (82, by - 7), (76, by - 5), rgb(232, 170, 60, 255))
    cv.set(74, by - 9, (250, 250, 250, 255)); cv.set(74, by - 9, (20, 20, 24, 255))
    # tail
    cv.tri((52, by), (44, by - 3), (52, by + 4), dark)
    # feet
    cv.line(60, by + 7, 60, 112, rgb(214, 150, 60, 255), 1)
    cv.line(65, by + 7, 65, 112, rgb(214, 150, 60, 255), 1)

def gen_animal(name, sp):
    is_bird = sp.get('beak')
    D = f'Animals/{name}'
    low = name.lower()
    walk_frames, attack_frames, death_frames = 4, 3, 3

    def frame(pose):
        cv = Canvas(128, 128)
        if is_bird:
            bird_draw(cv, sp, pose)
        else:
            draw_quad(cv, sp, pose)
        cv.outline(OUT)
        return cv

    dirs = ['south', 'north', 'east', 'west', 'northeast', 'northwest', 'southeast', 'southwest']
    for i in range(walk_frames):
        ph = i / walk_frames * 2 * math.pi
        for d in dirs:
            pose = dict(phase=ph)
            if d == 'south':
                pose['view'] = 'front'
            elif d == 'north':
                pose['view'] = 'back'
            cv = frame(pose)
            if d == 'west':
                cv.mirror_x()
            elif d in ('southwest', 'northwest'):
                cv.mirror_x()
            save_sprite(cv, f'{D}/{d}/{low}_{d}_walk_{i}', ppu=256)
    for i in range(attack_frames):
        cv = frame(dict(phase=0, attack=i))
        save_sprite(cv, f'{D}/east/{low}_east_attack_{i}', ppu=256)
        cv2 = Canvas(128, 128); cv2.pix = bytearray(cv.pix); cv2.mirror_x()
        save_sprite(cv2, f'{D}/west/{low}_west_attack_{i}', ppu=256)
    for i in range(death_frames):
        if i < 2:
            cv = Canvas(128, 128)
            if is_bird:
                bird_draw(cv, sp, dict(dead=i))
            else:
                draw_quad(cv, sp, dict(phase=0, dead=i))
                cv.rotate(24 * (i + 1), (64, 100))
            cv.outline(OUT)
        else:
            cv = frame(dict(dead=2))
        save_sprite(cv, f'{D}/east/{low}_death_{i}', ppu=256)

# ================================================================ TERRAIN
GROUND_TYPES = ['dirt', 'grass', 'sand', 'snow', 'stone', 'mud']

def gen_ground(name, variant_seed):
    rng = random.Random(variant_seed + hash(name) % 1000)
    base = {
        'dirt': rgb(124, 92, 62, 255), 'grass': rgb(88, 132, 62, 255),
        'sand': rgb(214, 190, 132, 255), 'snow': rgb(232, 236, 242, 255),
        'stone': rgb(128, 128, 130, 255), 'mud': rgb(92, 72, 50, 255),
    }[name]
    cv = Canvas(128, 128)
    cv.rect(0, 0, 127, 127, base)
    tones = [shade(base, 0.88), shade(base, 1.08), shade(base, 0.96)]
    speckle(cv, rng, (0, 0, 127, 127), tones, 220, 1, 2)
    if name == 'grass':
        for _ in range(60):
            x, y = rng.randint(2, 125), rng.randint(2, 125)
            col = rng.choice([shade(base, 0.72), shade(base, 1.22)])
            cv.line(x, y, x, y - rng.randint(1, 3), col, 1)
    elif name == 'sand':
        for _ in range(14):
            y = rng.randint(4, 124); x0 = rng.randint(0, 60)
            cv.hline(x0, x0 + rng.randint(20, 60), y, shade(base, 0.9))
    elif name == 'snow':
        speckle(cv, rng, (0, 0, 127, 127), [rgb(250, 252, 255, 255), rgb(210, 222, 238, 255)], 60, 0, 1)
    elif name == 'stone':
        for _ in range(6):
            x, y = rng.randint(6, 120), rng.randint(6, 120)
            cv.polyline([(x, y), (x + rng.randint(-8, 8), y + rng.randint(4, 10))], shade(base, 0.72), 1)
            cv.circle(x, y, rng.randint(3, 6), shade(base, 1.1))
    elif name == 'mud':
        for _ in range(8):
            x, y = rng.randint(6, 120), rng.randint(6, 120)
            cv.ellipse(x, y, rng.randint(4, 9), rng.randint(2, 5), shade(base, 0.78))
    elif name == 'dirt':
        for _ in range(12):
            x, y = rng.randint(4, 122), rng.randint(4, 122)
            cv.circle(x, y, rng.randint(1, 2), shade(base, 1.16))
    return cv

def gen_water(name, frame_idx, deep, shallow, sparkle):
    rng = random.Random(frame_idx * 13 + hash(name) % 977)
    cv = Canvas(128, 128)
    for y in range(128):
        t = y / 127.0
        cv.hline(0, 127, y, mix(deep, shallow, t * 0.7))
    for band in range(6):
        yb = 8 + band * 20 + frame_idx * 2
        for x in range(0, 128, 2):
            yy = int(yb + math.sin((x + frame_idx * 6) * 0.09) * 2.4)
            if 0 <= yy < 128:
                cv.rect(x, yy, x + 3, yy, mix(shallow, rgb(255, 255, 255, 255), 0.35))
    for _ in range(14):
        x, y = rng.randint(2, 124), rng.randint(2, 124)
        cv.set(x, y, sparkle); cv.set(x + 1, y, sparkle)
    return cv

def gen_terrain():
    for gi, g in enumerate(GROUND_TYPES):
        for v in range(3):
            cv = gen_ground(g, v * 31 + gi)
            save_sprite(cv, f'Terrain/Ground/{g}_tile_{v}', ppu=1024)
    waters = [('ocean_water', rgb(24, 56, 110, 255), rgb(52, 100, 158, 255)),
              ('calm_water', rgb(58, 118, 168, 255), rgb(110, 170, 205, 255)),
              ('river_water', rgb(66, 130, 176, 255), rgb(126, 182, 214, 255))]
    for name, deep, shal in waters:
        for f in range(4):
            cv = gen_water(name, f, deep, shal, rgb(240, 250, 255, 255))
            save_sprite(cv, f'Terrain/Water/{name}_tile_{f}', ppu=1024)

# ================================================================ VEGETATION
def canopy(cv, cx, cy, r, greens, rng):
    cv.ellipse(cx, cy, r, r * 0.8, greens[0])
    cv.ellipse(cx - r * 0.3, cy - r * 0.3, r * 0.7, r * 0.55, greens[1])
    for _ in range(30):
        x = cx - r + rng.random() * r * 2; y = cy - r * 0.8 + rng.random() * r * 1.6
        cv.set(int(x), int(y), rng.choice(greens))

def gen_vegetation():
    rng = random.Random(99)
    trunk = rgb(104, 72, 46, 255); trunk_d = rgb(80, 54, 34, 255)
    g_temperate = [rgb(58, 110, 48, 255), rgb(88, 148, 62, 255)]
    g_dark = [rgb(30, 84, 44, 255), rgb(52, 118, 62, 255)]
    # oak (temperate)
    cv = Canvas(128, 128)
    cv.rect(58, 84, 68, 118, trunk); cv.rect(58, 84, 62, 118, trunk_d)
    canopy(cv, 62, 52, 34, g_temperate, rng)
    for fx, fy in ((48, 66), (76, 62), (62, 76)):
        cv.circle(fx, fy, 2, rgb(196, 120, 60, 255))
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Trees/oak_tree', ppu=256)
    # birch
    cv = Canvas(128, 128)
    cv.rect(59, 70, 65, 118, rgb(214, 210, 198, 255))
    for by_ in range(74, 118, 8):
        cv.rect(59, by_, 61, by_ + 2, (40, 40, 40, 255))
    canopy(cv, 62, 46, 26, [rgb(96, 138, 58, 255), rgb(130, 168, 76, 255)], rng)
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Trees/birch_tree', ppu=256)
    # palm
    cv = Canvas(128, 128)
    cv.polyline([(66, 118), (62, 92), (60, 66)], trunk, 4)
    for ang in (-2.6, -2.0, -1.2, -0.6, 0.0):
        pts = [(60, 64)]
        for k in range(1, 6):
            pts.append((60 + math.cos(ang - 1.57) * k * 6, 64 + math.sin(ang - 1.57) * k * 4 + k * k * 0.4))
        cv.polyline(pts, rgb(52, 128, 58, 255), 2)
    cv.circle(60, 64, 3, trunk_d)
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Trees/palm_tree', ppu=256)
    # dead tree
    cv = Canvas(128, 128)
    cv.rect(59, 76, 66, 118, rgb(118, 98, 82, 255))
    for pts in ([(62, 80), (48, 58), (40, 46)], [(63, 84), (80, 60), (88, 50)], [(62, 88), (56, 66)]):
        cv.polyline(pts, rgb(118, 98, 82, 255), 3)
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Trees/dead_tree', ppu=256)
    # jungle tree
    cv = Canvas(128, 128)
    cv.rect(52, 88, 74, 118, trunk_d)
    cv.polyline([(52, 118), (44, 108), (52, 96)], trunk_d, 3)
    cv.polyline([(74, 118), (82, 108), (74, 96)], trunk_d, 3)
    canopy(cv, 62, 50, 40, g_dark, rng)
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Trees/jungle_tree', ppu=256)
    # berry bush v2
    cv = Canvas(128, 128)
    cv.ellipse(64, 96, 26, 18, rgb(48, 96, 44, 255))
    cv.ellipse(56, 90, 14, 10, rgb(72, 128, 58, 255))
    for _ in range(10):
        cv.circle(rng.randint(46, 84), rng.randint(86, 106), 2, rgb(188, 52, 72, 255))
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Bushes/berry_bush', ppu=256)
    # flower bush
    cv = Canvas(128, 128)
    cv.ellipse(64, 98, 24, 15, rgb(66, 118, 52, 255))
    for _ in range(9):
        x, y = rng.randint(48, 80), rng.randint(90, 106)
        cv.circle(x, y, 2, rgb(226, 196, 90, 255)); cv.set(x, y, rgb(160, 110, 30, 255))
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Bushes/flower_bush', ppu=256)
    # reeds
    cv = Canvas(128, 128)
    for i in range(9):
        x = 44 + i * 5
        cv.polyline([(x, 118), (x + (2 if i % 2 else -2), 96), (x + (4 if i % 2 else -3), 84)],
                    rgb(96, 140, 62, 255), 1)
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Bushes/reeds', ppu=256)
    # boulder + flint
    cv = Canvas(128, 128)
    cv.ellipse(64, 100, 30, 18, rgb(124, 124, 128, 255))
    cv.ellipse(56, 92, 14, 9, rgb(150, 150, 154, 255))
    cv.polyline([(48, 108), (64, 96), (82, 106)], rgb(100, 100, 104, 255), 1)
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Rocks/large_rock', ppu=256)
    cv = Canvas(128, 128)
    cv.ellipse(64, 104, 24, 12, rgb(96, 96, 102, 255))
    for x0 in (50, 64, 78):
        cv.tri((x0, 104), (x0 + 6, 86), (x0 + 10, 104), rgb(58, 58, 66, 255))
    cv.outline(OUT); save_sprite(cv, 'Vegetation/Rocks/flint_outcrop', ppu=256)

# ================================================================ ICONS
def icon_canvas():
    cv = Canvas(128, 128)
    return cv

def save_icon(cv, name):
    cv.outline((30, 22, 18, 255))
    save_sprite(cv, f'Items/{name}', ppu=1024)

def gen_icons():
    wood = rgb(140, 96, 54, 255); stone_c = rgb(150, 150, 156, 255)
    flint = rgb(70, 70, 80, 255); bone_c = rgb(232, 226, 208, 255)
    hide = rgb(164, 118, 74, 255); sinew_c = rgb(222, 208, 172, 255)
    copper = rgb(196, 122, 62, 255)

    cv = icon_canvas()
    cv.circle(64, 74, 22, rgb(196, 120, 60, 255)); cv.rect(61, 40, 67, 58, rgb(96, 138, 58, 255))
    save_icon(cv, 'wild_apple')

    cv = icon_canvas()
    for x, y in ((52, 82), (68, 78), (60, 94)):
        cv.circle(x, y, 8, rgb(158, 44, 84, 255)); cv.set(x - 2, y - 3, (255, 255, 255, 255))
    save_icon(cv, 'berries')

    cv = icon_canvas()
    cv.polyline([(44, 96), (56, 70), (52, 46)], rgb(226, 152, 66, 255), 7)
    cv.polyline([(52, 50), (66, 42), (76, 48)], rgb(96, 158, 66, 255), 3)
    save_icon(cv, 'wild_carrot')

    cv = icon_canvas()
    for i in range(4):
        cv.polyline([(42, 60 + i * 10), (86, 52 + i * 12)], sinew_c, 2)
    save_icon(cv, 'fiber')

    cv = icon_canvas()
    cv.rect(56, 76, 72, 100, wood)
    cv.tri((64, 30), (42, 70), (86, 70), stone_c)
    cv.polyline([(50, 62), (64, 40), (78, 62)], shade(stone_c, 0.8), 2)
    save_icon(cv, 'stone_pickaxe')

    cv = icon_canvas()
    cv.rect(58, 70, 70, 100, wood)
    cv.rect(44, 52, 84, 66, stone_c); cv.rect(44, 52, 84, 58, shade(stone_c, 1.15))
    save_icon(cv, 'stone_axe')

    cv = icon_canvas()
    cv.rect(60, 52, 68, 96, wood)
    cv.circle(64, 42, 10, rgb(255, 160, 56, 255)); cv.circle(64, 38, 6, rgb(255, 226, 140, 255))
    save_icon(cv, 'torch')

    cv = icon_canvas()
    for i, x in enumerate((40, 52, 64, 76)):
        cv.rect(x, 84 - (3 if i % 2 else 0), x + 8, 96, stone_c)
    cv.polyline([(42, 82), (64, 46), (86, 82)], rgb(226, 140, 60, 255), 4)
    cv.circle(64, 52, 6, rgb(255, 220, 120, 255))
    save_icon(cv, 'campfire')

    cv = icon_canvas()
    for i in range(4):
        y = 56 + i * 12
        cv.rect(30, y, 98, y + 8, wood if i % 2 == 0 else shade(wood, 0.86))
    cv.polyline([(34, 60), (44, 40), (58, 60)], sinew_c, 2)
    cv.polyline([(94, 60), (84, 40), (70, 60)], sinew_c, 2)
    save_icon(cv, 'log_raft')

    cv = icon_canvas()
    cv.ellipse(64, 64, 10, 16, hide)
    cv.polyline([(58, 46), (50, 34), (58, 30)], hide, 2)
    save_icon(cv, 'footprint')

    cv = icon_canvas()
    cv.tri((58, 96), (64, 40), (72, 96), flint)
    cv.tri((64, 96), (72, 52), (78, 96), shade(flint, 1.3))
    save_icon(cv, 'flint_shard')

    cv = icon_canvas()
    cv.rect(40, 58, 88, 70, bone_c)
    cv.circle(38, 58, 6, bone_c); cv.circle(38, 70, 6, bone_c)
    cv.circle(90, 58, 6, bone_c); cv.circle(90, 70, 6, bone_c)
    save_icon(cv, 'bone')

    cv = icon_canvas()
    for i in range(3):
        cv.circle(48 + i * 16, 64 + (i % 2) * 10, 6, sinew_c)
    save_icon(cv, 'sinew')

    cv = icon_canvas()
    cv.tri((52, 92), (70, 34), (82, 92), rgb(24, 24, 30, 255))
    cv.tri((60, 88), (70, 48), (76, 88), rgb(60, 60, 76, 255))
    save_icon(cv, 'obsidian')

    cv = icon_canvas()
    cv.ellipse(60, 70, 26, 20, hide); cv.ellipse(56, 66, 16, 10, shade(hide, 1.2))
    cv.circle(84, 84, 4, shade(hide, 0.7))
    save_icon(cv, 'fur_pelt')

    cv = icon_canvas()
    cv.ellipse(64, 76, 22, 16, rgb(110, 88, 66, 255))
    for _ in range(8):
        cv.circle(RNG.randint(48, 80), RNG.randint(66, 88), 2, copper)
    save_icon(cv, 'copper_ore')

    cv = icon_canvas()
    cv.polyline([(44, 96), (58, 60), (66, 44)], wood, 4)
    cv.polyline([(58, 62), (84, 50), (92, 62)], bone_c, 3)
    cv.tri((88, 52), (100, 44), (92, 60), flint)
    save_icon(cv, 'bone_spear')

    cv = icon_canvas()
    cv.rect(56, 60, 70, 100, wood)
    cv.tri((72, 56), (92, 66), (74, 76), rgb(28, 28, 36, 255))
    save_icon(cv, 'obsidian_knife')

    cv = icon_canvas()
    cv.polyline([(38, 96), (50, 52), (78, 52), (90, 96)], hide, 6)
    cv.polyline([(50, 52), (64, 40), (78, 52)], shade(hide, 0.8), 4)
    for x in range(44, 88, 8):
        cv.set(x, 74, (70, 48, 30, 255))
    save_icon(cv, 'fur_cloak')

    cv = icon_canvas()
    cv.rect(44, 60, 60, 96, hide); cv.rect(68, 60, 84, 96, hide)
    for y in range(66, 96, 10):
        cv.hline(44, 60, y, shade(hide, 0.75)); cv.hline(68, 84, y, shade(hide, 0.75))
    save_icon(cv, 'hide_leggings')

    cv = icon_canvas()
    cv.ellipse(64, 70, 20, 24, hide); cv.circle(64, 44, 8, shade(hide, 0.8))
    cv.polyline([(52, 92), (50, 104)], sinew_c, 2)
    save_icon(cv, 'water_skin')

    cv = icon_canvas()
    cv.circle(64, 68, 20, rgb(200, 220, 160, 255)); cv.circle(58, 62, 8, rgb(230, 240, 190, 255))
    for _ in range(5):
        cv.circle(RNG.randint(52, 76), RNG.randint(56, 80), 2, rgb(96, 128, 60, 255))
    save_icon(cv, 'healing_salve')

    cv = icon_canvas()
    cv.ellipse(64, 78, 24, 14, rgb(150, 104, 58, 255)); cv.ellipse(64, 70, 24, 12, shade(rgb(150, 104, 58, 255), 1.2))
    save_icon(cv, 'wooden_bowl')

    cv = icon_canvas()
    cv.polyline([(40, 96), (64, 44), (88, 96)], wood, 5)
    cv.rect(52, 60, 76, 68, bone_c)
    save_icon(cv, 'atlatl')

    cv = icon_canvas()
    cv.rect(44, 40, 84, 88, wood)
    for y in range(48, 88, 10):
        cv.hline(44, 84, y, shade(wood, 0.8))
    cv.circle(64, 36, 6, rgb(220, 190, 120, 255))
    save_icon(cv, 'totem')

    cv = icon_canvas()
    cv.ellipse(64, 76, 26, 16, hide)
    cv.rect(60, 44, 68, 64, wood)
    cv.circle(64, 70, 4, bone_c)
    save_icon(cv, 'drum')

    cv = icon_canvas()
    cv.circle(64, 64, 18, copper); cv.circle(64, 64, 10, shade(copper, 0.7))
    cv.circle(64, 64, 4, rgb(255, 220, 160, 255))
    save_icon(cv, 'copper_amulet')

    cv = icon_canvas()
    cv.rect(56, 44, 72, 84, hide)
    cv.polyline([(56, 60), (40, 68), (56, 76)], shade(hide, 0.8), 4)
    save_icon(cv, 'herb_pouch')

    cv = icon_canvas()
    cv.rect(52, 56, 76, 92, rgb(120, 84, 52, 255))
    cv.rect(52, 56, 76, 66, rgb(150, 108, 66, 255))
    for x in (56, 72):
        cv.rect(x, 66, x + 6, 92, rgb(96, 64, 40, 255))
    cv.circle(88, 52, 5, flint); cv.rect(30, 62, 48, 68, wood)
    save_icon(cv, 'workbench')

    cv = icon_canvas()
    cv.tri((64, 34), (28, 96), (100, 96), hide)
    cv.polyline([(64, 34), (64, 96)], shade(hide, 0.75), 3)
    cv.rect(56, 78, 72, 96, (40, 28, 22, 255))
    save_icon(cv, 'tent')

    cv = icon_canvas()
    cv.ellipse(64, 84, 34, 22, rgb(150, 116, 80, 255))
    cv.ellipse(64, 90, 30, 16, shade(rgb(150, 116, 80, 255), 0.85))
    cv.rect(56, 68, 72, 96, (40, 28, 22, 255))
    for _ in range(8):
        cv.set(RNG.randint(40, 88), RNG.randint(70, 92), rgb(110, 82, 56, 255))
    save_icon(cv, 'hut')

    cv = icon_canvas()
    cv.ellipse(64, 70, 22, 14, rgb(178, 60, 50, 255))
    cv.polyline([(44, 66), (34, 58)], bone_c, 3)
    save_icon(cv, 'dried_meat')

# ================================================================ STRUCTURES
def gen_structures():
    hide = rgb(164, 118, 74, 255); wood = rgb(140, 96, 54, 255)
    cv = Canvas(128, 128)
    cv.tri((64, 28), (20, 112), (108, 112), hide)
    cv.polyline([(64, 28), (64, 112)], shade(hide, 0.8), 3)
    cv.rect(54, 84, 74, 112, (44, 30, 24, 255))
    cv.polyline([(30, 100), (20, 112)], wood, 2)
    cv.outline(OUT); save_sprite(cv, 'Structures/tent', ppu=256)

    cv = Canvas(128, 128)
    cv.rect(28, 78, 100, 88, wood)
    cv.rect(34, 88, 42, 116, shade(wood, 0.8)); cv.rect(86, 88, 94, 116, shade(wood, 0.8))
    cv.circle(76, 66, 8, rgb(70, 70, 80, 255)); cv.rect(72, 70, 80, 78, wood)
    cv.rect(40, 64, 58, 70, rgb(110, 110, 118, 255))
    cv.outline(OUT); save_sprite(cv, 'Structures/workbench', ppu=256)

    cv = Canvas(128, 128)
    cv.ellipse(64, 88, 38, 26, rgb(158, 124, 88, 255))
    cv.ellipse(64, 94, 34, 20, shade(rgb(158, 124, 88, 255), 0.85))
    cv.rect(54, 76, 74, 108, (44, 30, 24, 255))
    for _ in range(10):
        cv.set(RNG.randint(34, 94), RNG.randint(72, 102), rgb(118, 90, 62, 255))
    cv.outline(OUT); save_sprite(cv, 'Structures/hut', ppu=256)

    cv = Canvas(128, 128)
    cv.circle(60, 56, 12, rgb(196, 198, 206, 255))
    cv.circle(60, 56, 7, rgb(120, 122, 130, 255))
    cv.rect(52, 74, 68, 80, wood)
    cv.outline(OUT); save_sprite(cv, 'Structures/trade_post', ppu=256)

# ================================================================ UI SKIN
UI_PPU = 256
def ui_piece(draw, name, size=48, border=10, ppu=UI_PPU):
    cv = Canvas(size, size)
    draw(cv)
    cv.save(P(f'UI/Skin/{name}'))
    write_png_meta(P(f'UI/Skin/{name}') + '.meta', ppu=ppu, sprite_mode=1,
                   border=(border * 8, border * 8, border * 8, border * 8))

def gen_ui_skin():
    def panel_dark(cv):
        cv.rect(0, 0, 47, 47, rgb(56, 40, 28, 242))
        cv.rect(3, 3, 44, 44, rgb(44, 31, 22, 242))
        for i in range(4, 44, 6):
            cv.set(i, 1, rgb(96, 72, 48, 255)); cv.set(1, i, rgb(96, 72, 48, 255))
            cv.set(46 - i if False else 46, i, rgb(70, 52, 36, 255)); cv.set(i, 46, rgb(70, 52, 36, 255))
        for x, y in ((3, 3), (44, 3), (3, 44), (44, 44)):
            cv.circle(x, y, 1, rgb(150, 122, 84, 255))
    def panel_parchment(cv):
        cv.rect(0, 0, 47, 47, rgb(214, 192, 148, 255))
        cv.rect(2, 2, 45, 45, rgb(230, 210, 168, 255))
        cv.polyline([(6, 40), (20, 36), (30, 40)], rgb(196, 168, 120, 255), 1)
        cv.circle(38, 12, 4, rgb(206, 180, 132, 255))
        for i in range(2, 46, 5):
            cv.set(i, 0, rgb(150, 124, 84, 255)); cv.set(i, 47, rgb(150, 124, 84, 255))
            cv.set(0, i, rgb(150, 124, 84, 255)); cv.set(47, i, rgb(150, 124, 84, 255))
    def button(cv, pressed=False):
        base = rgb(96, 66, 40, 255) if not pressed else rgb(66, 46, 30, 255)
        cv.rect(0, 0, 47, 47, shade(base, 0.7))
        cv.rect(1, 1, 46, 46, base)
        cv.rect(2, 2, 45, 8, shade(base, 1.25))
        cv.rect(2, 40, 45, 45, shade(base, 0.8))
        for i in range(4, 44, 7):
            cv.hline(2, 45, i, shade(base, 0.92))
    def slot(cv):
        cv.rect(0, 0, 47, 47, rgb(40, 30, 22, 255))
        cv.rect(4, 4, 43, 43, rgb(58, 44, 32, 255))
        cv.rect(4, 4, 43, 6, rgb(30, 22, 16, 255))
        cv.rect(4, 41, 43, 43, rgb(74, 58, 42, 255))
    def bar_frame(cv):
        cv.rect(0, 10, 47, 37, rgb(28, 22, 18, 255))
        cv.rect(3, 13, 44, 34, rgb(16, 12, 10, 255))
        for x, y in ((2, 12), (45, 12), (2, 35), (45, 35)):
            cv.circle(x, y, 2, rgb(120, 100, 70, 255))
    def bar_fill(cv):
        cv.rect(0, 14, 47, 33, rgb(210, 210, 210, 255))
        cv.rect(0, 14, 47, 20, rgb(255, 255, 255, 255))
    def knob(cv):
        cv.circle(24, 24, 16, rgb(140, 140, 146, 255))
        cv.circle(20, 20, 7, rgb(176, 176, 182, 255))
    def tooltip(cv):
        cv.rect(0, 0, 47, 47, rgb(26, 20, 16, 220))
        for i in range(2, 46, 4):
            cv.set(i, 0, rgb(96, 76, 52, 255)); cv.set(i, 47, rgb(96, 76, 52, 255))
    def dialogue(cv):
        cv.rect(0, 0, 47, 47, rgb(210, 188, 144, 252))
        cv.rect(2, 2, 45, 45, rgb(228, 206, 162, 252))
        for i in range(2, 46, 5):
            cv.set(i, 0, rgb(140, 112, 76, 255)); cv.set(i, 47, rgb(140, 112, 76, 255))
    def divider(cv):
        for x in range(0, 48, 4):
            cv.set(x, 24, rgb(140, 112, 76, 255)); cv.set(x + 1, 24, rgb(96, 74, 48, 255))
    def checkbox(cv, tick=True):
        cv.rect(8, 8, 39, 39, rgb(58, 44, 32, 255))
        cv.rect(11, 11, 36, 36, rgb(84, 64, 46, 255))
        if tick:
            cv.polyline([(16, 24), (22, 32), (32, 16)], rgb(220, 200, 150, 255), 3)
    ui_piece(panel_dark, 'panel_dark')
    ui_piece(panel_parchment, 'panel_parchment')
    ui_piece(lambda cv: button(cv, False), 'button')
    ui_piece(lambda cv: button(cv, True), 'button_pressed')
    ui_piece(slot, 'slot')
    ui_piece(bar_frame, 'bar_frame')
    ui_piece(bar_fill, 'bar_fill')
    ui_piece(knob, 'knob')
    ui_piece(tooltip, 'tooltip')
    ui_piece(dialogue, 'panel_dialogue')
    ui_piece(divider, 'divider', border=2)
    ui_piece(lambda cv: checkbox(cv, True), 'checkbox_on')
    ui_piece(lambda cv: checkbox(cv, False), 'checkbox_off')

    # era banner 128x48 logical (wide)
    def banner(cv):
        cv.rect(0, 6, 127, 41, rgb(74, 52, 34, 255))
        cv.rect(2, 8, 125, 39, rgb(96, 68, 44, 255))
        cv.rect(2, 8, 125, 16, rgb(112, 80, 52, 255))
        for x in (6, 121):
            cv.line(x, 4, x, 12, rgb(180, 160, 120, 255), 2)
            cv.circle(x, 3, 2, rgb(200, 180, 140, 255))
    cv = Canvas(128, 48)
    banner(cv)
    cv.save(P('UI/Skin/banner'))
    write_png_meta(P('UI/Skin/banner') + '.meta', ppu=UI_PPU, sprite_mode=1,
                   border=(16, 16, 16, 16))

# ================================================================ VFX SHEETS
def gen_vfx():
    def sheet(name, frames_draw, ppu=256):
        cv = Canvas(32 * len(frames_draw), 32)
        for i, fn in enumerate(frames_draw):
            fn(cv, i * 32 + 16)
        save_sheet(f'VFX/{name}', cv, len(frames_draw), ppu=ppu)

    def spark(cv, cx, cy, t=0):
        r = 3 + t * 2
        col = mix(rgb(255, 240, 180, 255), rgb(255, 140, 40, 90), t / 3.0)
        cv.line(cx - r, cy, cx + r, cy, col, 1)
        cv.line(cx, cy - r, cx, cy + r, col, 1)
        r2 = r * 0.6
        cv.line(cx - r2, cy - r2, cx + r2, cy + r2, col, 1)
        cv.line(cx - r2, cy + r2, cx + r2, cy - r2, col, 1)
    def slash(cv, cx, cy, t=0):
        col = mix(rgb(255, 255, 255, 255), rgb(255, 200, 120, 60), t / 3.0)
        a0 = -2.2 + t * 0.9
        for k in range(14):
            a = a0 + k * 0.13
            cv.circle(cx + math.cos(a) * 11, cy + math.sin(a) * 11, 1, col)
    def dust(cv, cx, cy, t=0):
        col = mix(rgb(188, 164, 130, 200), rgb(160, 140, 110, 0), t / 3.0)
        for dx, dy in ((-4, 2), (4, 2), (0, -2), (-7, 4 + t), (7, 4)):
            cv.circle(cx + dx + t, cy + dy - t, 2 + t * 0.6, col)
    def puff(cv, cx, cy, t=0):
        col = mix(rgb(150, 150, 156, 190), rgb(120, 120, 126, 0), t / 3.0)
        cv.circle(cx, cy - t * 2, 3 + t * 2, col)
        cv.circle(cx - 4, cy - t * 2 + 2, 2 + t, shade(col, 0.9))
    def blood(cv, cx, cy, t=0):
        col = mix(BLOOD, shade(BLOOD, 0.6), t / 3.0)
        for dx, dy in ((-5, -3), (5, -2), (0, -6), (-2, 3), (3, 4)):
            cv.set(cx + dx + t * 2, cy + dy + t * 3, col)
            cv.set(cx + dx + t * 2 + 1, cy + dy + t * 3, col)
    def leaf(cv, cx, cy, t=0):
        col = mix(rgb(96, 150, 62, 255), rgb(60, 110, 44, 120), t / 3.0)
        x = cx - 6 + t * 4; y = cy - 4 + t * 3
        cv.rect(x, y, x + 3, y + 2, col)
    def splash(cv, cx, cy, t=0):
        col = mix(rgb(190, 226, 245, 230), rgb(120, 180, 220, 60), t / 3.0)
        for dx, dy in ((-6, -t * 4), (6, -t * 4), (0, -6 - t * 3), (-3, 2), (3, 2)):
            cv.circle(cx + dx, cy + dy, 1 + (2 - t) * 0.5, col)
    def fire(cv, cx, cy, t=0):
        import random as _r
        rr = _r.Random(t)
        h = 9 + rr.randint(0, 3)
        cv.ellipse(cx, cy + 2, 6 - t * 0.6, h * 0.6, rgb(226, 90, 30, 235))
        cv.ellipse(cx, cy - 2 + (t % 2), 4, h * 0.45, rgb(255, 160, 50, 235))
        cv.ellipse(cx, cy - 4 + (t % 2), 2, h * 0.3, rgb(255, 235, 160, 255))
    def ember(cv, cx, cy, t=0):
        col = mix(rgb(255, 190, 90, 255), rgb(255, 90, 30, 0), t / 3.0)
        cv.set(cx, cy - t * 3, col); cv.set(cx, cy - t * 3 - 1, col)
        cv.set(cx + 1, cy - t * 3 + 1, shade(col, 0.8))
    def ring(cv, cx, cy, t=0):
        r = 3 + t * 4
        col = mix(rgb(255, 255, 255, 220), rgb(255, 220, 160, 0), t / 3.0)
        steps = 20
        for k in range(steps):
            a = k / steps * 2 * math.pi
            cv.set(cx + math.cos(a) * r, cy + math.sin(a) * r, col)
    def snow(cv, cx, cy, t=0):
        col = rgb(250, 250, 255, 240)
        cv.set(cx, cy, col); cv.set(cx - 1, cy, col); cv.set(cx + 1, cy, col)
        cv.set(cx, cy - 1, col); cv.set(cx, cy + 1, col)
    def hitflash(cv, cx, cy, t=0):
        col = mix(rgb(255, 255, 255, 255), rgb(255, 200, 140, 40), t / 3.0)
        for dx, dy in ((0, 0), (3, 0), (-3, 0), (0, 3), (0, -3), (2, 2), (-2, -2)):
            cv.set(cx + dx, cy + dy, col)

    sheet('spark', [lambda cv, x: spark(cv, x, 16, t) for t in range(4)])
    sheet('slash', [lambda cv, x: slash(cv, x, 16, t) for t in range(4)])
    sheet('dust', [lambda cv, x: dust(cv, x, 16, t) for t in range(4)])
    sheet('puff', [lambda cv, x: puff(cv, x, 16, t) for t in range(4)])
    sheet('blood', [lambda cv, x: blood(cv, x, 16, t) for t in range(4)])
    sheet('leaf', [lambda cv, x: leaf(cv, x, 16, t) for t in range(4)])
    sheet('splash', [lambda cv, x: splash(cv, x, 16, t) for t in range(4)])
    sheet('fire', [lambda cv, x: fire(cv, x, 16, t) for t in range(4)])
    sheet('ember', [lambda cv, x: ember(cv, x, 16, t) for t in range(4)])
    sheet('ring', [lambda cv, x: ring(cv, x, 16, t) for t in range(4)])
    sheet('snow', [lambda cv, x: snow(cv, x, 16, t) for t in range(4)])
    sheet('hitflash', [lambda cv, x: hitflash(cv, x, 16, t) for t in range(4)])

# ================================================================ MAIN


# ================================================================ LEGACY IN-PLACE
def save_png_only(cv, name):
    cv.save(P(name))  # overwrite pixels only — existing .meta (and GUID) untouched

def gen_legacy_inplace():
    """Redraw pre-existing sprite paths with the new art style WITHOUT touching
    their .meta files (so prefab/SO references stay valid)."""
    rng = random.Random(5)
    trunk = rgb(104, 72, 46, 255); trunk_d = rgb(80, 54, 34, 255)
    # pine
    cv = Canvas(128, 128)
    cv.rect(58, 92, 68, 118, trunk); cv.rect(58, 92, 62, 118, trunk_d)
    for i, w in enumerate((30, 24, 18)):
        y = 88 - i * 22
        cv.tri((63, y - 22), (63 - w, y), (63 + w, y), rgb(38, 92, 52, 255) if i % 2 else rgb(48, 108, 58, 255))
    cv.outline(OUT); save_png_only(cv, 'Vegetation/Trees/pine_tree')
    # apple
    cv = Canvas(128, 128)
    cv.rect(58, 84, 68, 118, trunk); cv.rect(58, 84, 62, 118, trunk_d)
    canopy(cv, 62, 52, 32, [rgb(58, 110, 48, 255), rgb(88, 148, 62, 255)], rng)
    for fx, fy in ((48, 60), (74, 66), (60, 40), (80, 46)):
        cv.circle(fx, fy, 3, rgb(206, 54, 54, 255)); cv.set(fx - 1, fy - 2, (255, 255, 255, 255))
    cv.outline(OUT); save_png_only(cv, 'Vegetation/Trees/apple_tree')
    # fig
    cv = Canvas(128, 128)
    cv.rect(56, 86, 70, 118, trunk_d)
    canopy(cv, 62, 54, 38, [rgb(44, 96, 46, 255), rgb(70, 128, 56, 255)], rng)
    for fx, fy in ((44, 62), (78, 58), (62, 74)):
        cv.circle(fx, fy, 3, rgb(150, 70, 130, 255))
    cv.outline(OUT); save_png_only(cv, 'Vegetation/Trees/fig_tree')
    # vine
    cv = Canvas(128, 128)
    cv.polyline([(64, 20), (58, 50), (66, 82), (60, 112)], rgb(56, 104, 48, 255), 2)
    for y in (34, 56, 80, 100):
        x = 60 if y % 3 else 64
        cv.ellipse(x, y, 6, 4, rgb(72, 128, 58, 255))
    for y in (44, 68, 92):
        cv.circle(66, y, 2, rgb(150, 70, 130, 255))
    cv.outline(OUT); save_png_only(cv, 'Vegetation/Bushes/vine')
    # stone cluster
    cv = Canvas(128, 128)
    for x, y, r in ((50, 100, 12), (74, 104, 9), (62, 88, 8)):
        cv.circle(x, y, r, rgb(128, 128, 132, 255)); cv.circle(x - r // 3, y - r // 3, r // 2, rgb(158, 158, 162, 255))
    cv.outline(OUT); save_png_only(cv, 'Vegetation/Rocks/stone_cluster')
    # legacy items (PNG only; metas keep GUIDs)
    cv = Canvas(128, 128)
    for x, y, r in ((52, 84, 14), (72, 92, 11), (60, 64, 10)):
        cv.circle(x, y, r, rgb(146, 146, 152, 255)); cv.circle(x - r // 3, y - r // 3, r // 2, rgb(178, 178, 184, 255))
    cv.outline(OUT); save_png_only(cv, 'Items/stone')
    cv = Canvas(128, 128)
    cv.rect(38, 54, 92, 76, rgb(140, 96, 54, 255)); cv.rect(38, 54, 92, 62, rgb(164, 118, 68, 255))
    cv.circle(38, 65, 11, rgb(118, 80, 46, 255)); cv.circle(92, 65, 11, rgb(118, 80, 46, 255))
    cv.polyline([(44, 60), (60, 66), (80, 60)], rgb(110, 74, 42, 255), 1)
    cv.outline(OUT); save_png_only(cv, 'Items/wood_log')
    cv = Canvas(128, 128)
    cv.ellipse(64, 72, 26, 16, rgb(196, 74, 66, 255)); cv.ellipse(58, 66, 12, 8, rgb(226, 120, 108, 255))
    cv.polyline([(64, 56), (68, 44)], rgb(230, 214, 190, 255), 2)
    cv.outline(OUT); save_png_only(cv, 'Items/raw_meat')
    cv = Canvas(128, 128)
    cv.ellipse(64, 72, 26, 16, rgb(164, 96, 48, 255)); cv.ellipse(58, 66, 12, 8, rgb(200, 132, 74, 255))
    cv.polyline([(64, 56), (68, 44)], rgb(230, 214, 190, 255), 2)
    for _ in range(5):
        cv.set(RNG.randint(48, 80), RNG.randint(62, 82), rgb(120, 66, 30, 255))
    cv.outline(OUT); save_png_only(cv, 'Items/cooked_meat')
    cv = Canvas(128, 128)
    cv.polyline([(40, 96), (52, 48), (84, 44), (96, 90)], rgb(164, 118, 74, 255), 7)
    cv.polyline([(52, 48), (84, 44)], shade(rgb(164, 118, 74, 255), 0.7), 3)
    for _ in range(6):
        cv.set(RNG.randint(50, 88), RNG.randint(52, 88), rgb(120, 84, 50, 255))
    cv.outline(OUT); save_png_only(cv, 'Items/animal_hide')
    # stat icons
    heart = rgb(196, 56, 48, 255)
    cv = Canvas(128, 128)
    cv.circle(52, 56, 18, heart); cv.circle(76, 56, 18, heart)
    cv.tri((36, 64), (64, 100), (92, 64), heart)
    cv.circle(46, 50, 5, rgb(255, 160, 150, 255))
    cv.outline(OUT); save_png_only(cv, 'UI/Icons/health_icon')
    cv = Canvas(128, 128)
    cv.polyline([(46, 44), (52, 88), (60, 96), (68, 88), (74, 44)], rgb(206, 132, 56, 255), 5)
    cv.polyline([(48, 46), (64, 30), (80, 46)], rgb(140, 88, 40, 255), 4)
    cv.outline(OUT); save_png_only(cv, 'UI/Icons/hunger_icon')
    cv = Canvas(128, 128)
    cv.polyline([(64, 34), (44, 70), (52, 76), (46, 96), (82, 96), (76, 76), (84, 70), (64, 34)],
                rgb(66, 134, 208, 255), 4)
    cv.circle(58, 66, 4, rgb(160, 208, 244, 255))
    cv.outline(OUT); save_png_only(cv, 'UI/Icons/thirst_icon')
    cv = Canvas(128, 128)
    cv.circle(64, 64, 22, rgb(240, 200, 80, 255))
    for a in range(8):
        x0 = 64 + math.cos(a * math.pi / 4) * 26; y0 = 64 + math.sin(a * math.pi / 4) * 26
        x1 = 64 + math.cos(a * math.pi / 4) * 36; y1 = 64 + math.sin(a * math.pi / 4) * 36
        cv.line(x0, y0, x1, y1, rgb(240, 200, 80, 255), 3)
    cv.circle(58, 58, 7, rgb(255, 236, 170, 255))
    cv.outline(OUT); save_png_only(cv, 'UI/Icons/energy_icon')
    cv = Canvas(128, 128)
    cv.polyline([(46, 90), (58, 40), (66, 70), (74, 48), (84, 90)], rgb(120, 190, 90, 255), 5)
    cv.outline(OUT); save_png_only(cv, 'UI/Icons/stamina_icon')

if __name__ == '__main__':
    which = sys.argv[1] if len(sys.argv) > 1 else 'all'
    if which in ('all', 'player'):
        gen_player()
        print('player done')
    if which in ('all', 'npc'):
        gen_npc(dict(skin=rgb(198, 148, 104, 255), hair=rgb(72, 50, 34, 255),
                     cloth=rgb(110, 92, 62, 255), cloth_d=rgb(84, 70, 48, 255),
                     cloth_l=rgb(136, 114, 78, 255)), 'Villager')
        gen_npc(dict(skin=rgb(186, 138, 96, 255), hair=rgb(206, 200, 190, 255),
                     cloth=rgb(92, 74, 66, 255), cloth_d=rgb(70, 56, 50, 255),
                     cloth_l=rgb(116, 96, 84, 255)), 'Elder')
        print('npc done')
    if which in ('all', 'animals'):
        for name, sp in SPECIES.items():
            gen_animal(name, sp)
        print('animals done')
    if which in ('all', 'terrain'):
        gen_terrain()
        print('terrain done')
    if which in ('all', 'veg'):
        gen_vegetation()
        print('veg done')
    if which in ('all', 'icons'):
        gen_icons()
        print('icons done')
    if which in ('all', 'structures'):
        gen_structures()
        print('structures done')
    if which in ('all', 'ui'):
        gen_ui_skin()
        print('ui done')
    if which in ('all', 'vfx'):
        gen_vfx()
        print('vfx done')
    if which in ('all', 'legacy'):
        gen_legacy_inplace()
        print('legacy done')
