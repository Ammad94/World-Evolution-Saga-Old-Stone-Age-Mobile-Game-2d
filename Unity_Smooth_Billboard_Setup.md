# Smooth Billboard Upgrade — no more "2D sprite feel"

This upgrade makes the billboarded caveman feel three-dimensional and alive:

| Before | After |
|---|---|
| Camera orbit snaps between 16 flat cut-outs | The exact camera↔character angle **cross-fades the two neighbouring direction sprites** — the character visibly *morphs* through every in-between angle while you orbit |
| Character flips instantly to a new direction when you steer | The direction **glides** through in-between poses (turn smoothing) + the sprite **leans a few degrees into the motion** (fake rotational inertia) |
| 3-frame idle that steps at 4 fps | **Continuous GPU animation**: hair strands drift in a gusty breeze and visible **breathing** — chest expands, shoulders/head rise on the inhale, subtle exhale shading + idle body bob — plus a soft contact shadow under his feet and a walk bob while moving. (Loincloth flutter exists in the shader but is **off by default**.) |

A CPU reference simulation of the exact shader math is included — see
`preview/preview_smooth_billboard.gif` (left: smooth orbit, right: hair wind +
breathing — bottom cloth intentionally still).

## ⚠ Fixed artwork — re-copy your sprites

Several of the old direction sprites were **physically damaged** (missing
heads, missing feet, interior torso holes — the "vanishing body parts").
All sprite sets have been rebuilt/verified:

| Set | Status |
|---|---|
| `sprites_16/` (176×392) | **Rebuilt** from the complete green-screen raw sheet (`stone_age_sheet_16_raw.png`), feet baseline-aligned |
| `sprites_16_idle/` (176×392) | **Rebuilt** from the three idle raw sheets — all 48 frames complete |
| `sprites/` (8-dir) | Head-tops **repaired** by borrowing from the same-angle 16-dir sprites |
| Chest/back belt | **Made consistent, cleanly** — the SAME original dark-brown belt he was already wearing. All old front-band remnants are erased and the two straps are redrawn with a physical cylinder model (`row = y0 ± k·sin(view + δ)`, tilt/width/leather measured from the original back view): an X on chest and back, separated bands on the sides that converge at the silhouette edges, rotating continuously while you orbit. Back/side views untouched. See `preview/harness_before_after.png` |
| `sprites_16_masks/`, `sprites_masks/` | **Regenerated** from the fixed art |

`tools/rebuild_sprites_from_sheet.py` does all of this (it also applies
`tools/redesign_torso_straps.py`, the belt-consistency pass) and **verifies**
that no sprite has truncated tops/feet, interior holes, or vanishing parts at
any orbit cross-fade angle. Pre-repair originals are in `tools/originals_backup.zip`.

## Idle life (all GPU-side, on the BillboardCharacter component)

| Feature | Default | Knob |
|---|---|---|
| Hair sway in gusty breeze | on | **Hair Sway Pixels**, **Wind Strength/Speed** |
| Breathing (chest rise/expand + exhale shade + body bob) | on | **Breaths Per Second**, **Breath Amount**, **Idle Body Bob** |
| Loincloth flutter | **off** (by request) | **Cloth Flutter Pixels** |
| **Eye blinks** (random 2.5–6.5 s, quick close / slower open, occasional double-blink) | on | **Blink**, delays, **Double Blink Chance** |
| **Natural head sway** (slow, multi-frequency, per-instance phase) | on | **Head Sway Pixels** |
| **Finger curl** (slow fist clench while idle) | on | **Finger Curl Amount** |
| Contact shadow + walk bob | on | **Contact Shadow Strength**, **Walk Bob Amount** |

**If you already copied the old sprites into Unity, delete them from your
project and re-copy the fixed folders** (sprites + masks), then re-do the
mask import step below.

## New files

| File | Purpose |
|---|---|
| `unity_assets/BillboardCharacter.cs` | Replaces **PlayerController3D + IdleAnimator** (one component). Movement + continuous direction blending + drives the shader |
| `unity_assets/ThirdPersonCamera.cs` | Updated — smoother orbit (inertia, damped zoom, optional vertical orbit, touch + gamepad) |
| `unity_assets/BillboardBlendWind.shader` | The magic — **Built-in render pipeline** version (hair wind + breathing; cloth flutter off by default) |
| `unity_assets/BillboardBlendWindURP.shader` | Same shader for **URP** projects |
| `unity_assets/sprites_16_masks/*.png` | Generated sway masks (R = hair, G = cloth, B = torso) |
| `tools/generate_sway_masks.py` | Regenerates masks if you change the artwork |
| `tools/preview_shader_sim.py` | Renders the GIF preview / verifies the effect |

---

## Step 1 — copy files into Unity

1. Copy `BillboardCharacter.cs` and `ThirdPersonCamera.cs` to `Assets/Scripts/`
   (replace the old `ThirdPersonCamera.cs`).
2. Copy **one** shader depending on your render pipeline
   (**Edit → Project Settings → Graphics** tells you which you use):
   - Built-in (default for a fresh 2D/3D template): `BillboardBlendWind.shader`
   - URP: `BillboardBlendWindURP.shader`
3. Copy the whole `sprites_16_masks` folder into
   `Assets/Resources/CharacterMasks/` — the exact folder name matters, the
   script auto-loads masks from there by sprite name.
   (Alternative: put them anywhere and assign them by hand in Step 4.)

**Mask import settings:** leave them at *Texture Type: Default*. Don't add them
to a Sprite Atlas.

## Step 2 — sprite import settings (the 16 direction sprites)

Same as before, plus two important rules:

1. Texture Type: **Sprite (2D and UI)**, Sprite Mode: **Single**, Pivot: **Bottom**,
   **Mesh Type: Full Rect**, Generate Mip Maps: **off**, Wrap Mode: **Clamp**.
2. **Do NOT put them in a Sprite Atlas** (Tag → None). The shader blends the raw
   textures; atlas packing breaks the UVs. `BillboardCharacter` checks this and
   logs an error if it detects packing.

## Step 3 — create the character material

1. Project window → right-click → **Create → Material**, name it `CavemanBillboard`.
2. Shader: **Game/BillboardBlendWind** (or `Game/BillboardBlendWindURP`).
3. Select the **Player** → Sprite Renderer → drag `CavemanBillboard` into
   **Material**. (Order in Layer / sorting layer settings stay as they were.)

## Step 4 — swap the components on the Player

1. On the **Player**:
   - **Remove** `PlayerController3D` and `IdleAnimator` (if present).
   - **Add Component → Billboard Character**.
2. Assign:
   - **Direction Sprites** (16): `00_front`, `01_front_right_slight`,
     `02_front_right`, `03_right_front`, `04_right`, `05_right_back`,
     `06_back_right`, `07_back_right_slight`, `08_back`, `09_back_left_slight`,
     `10_back_left`, `11_left_back`, `12_left`, `13_left_front`,
     `14_front_left`, `15_front_left_slight` — exactly that order.
   - **Direction Masks** (16): optional — the matching `_mask` textures in the
     same order. If you placed them in `Assets/Resources/CharacterMasks/` you
     can leave this empty (auto-load). Without masks a rough procedural
     fallback keeps him breathing and swaying.
3. If he faces the wrong way when strafing, tick **Mirror Left Right**.

## Step 5 — camera

`ThirdPersonCamera` on the **Main Camera** — assign the Player to **Target** as
before. What's new:

| Control | Action |
|---|---|
| Hold **right mouse** + drag | Orbit (yaw + vertical, both with inertia glide) |
| **Scroll** | Zoom (now smoothly damped) |
| **One finger drag** (mobile) | Orbit |
| **Two-finger pinch** (mobile) | Zoom |
| **Right stick** (gamepad) | Orbit |
| WASD / arrows / left stick | Move (camera-relative, GTA-style) |
| Left click on ground | Click-to-move |

Set **Allow Pitch Orbit** off if you want the old fixed look-down angle.

## Step 6 — press Play ▶

Orbit the camera slowly around him: he should turn *continuously* — like a
living creature standing in a breeze — never a paper cut-out flip. Watch the
hair, the loincloth hem and his chest.

---

## Tuning cheatsheet (all on the BillboardCharacter component)

| Want... | Turn... |
|---|---|
| Stronger breeze | **Wind Strength** (0 = dead calm), **Wind Speed** |
| More/less hair sway | **Hair Sway Pixels** (~3 ≈ subtle, 5+ = windy day) |
| Loincloth flutter (currently OFF) | **Cloth Flutter Pixels** — set to ~3.6 to make the hem flutter again |
| Wind from another direction | **Wind Direction** (world XZ — it's projected onto the camera, so orbiting changes the apparent sway naturally) |
| Faster/deeper breathing | **Breaths Per Second** (~0.23 = relaxed), **Breath Amount** |
| No idle bob | **Idle Body Bob = 0** |
| Calmer turns | **Turn Smooth Time** 0.15–0.25 |
| No lean while orbiting | **Orbit Lean Degrees = 0** |
| No shadow blob | **Contact Shadow Strength = 0** |
| Several characters | Just duplicate — each instance randomizes its wind/breathing phase so they don't breathe in sync |

## Troubleshooting

- **Pink material** → wrong shader for your pipeline: use the `...URP` file in
  URP projects, the plain one in Built-in.
- **Error about sprites being "packed into a SpriteAtlas"** → remove the
  sprites (and their folder) from any atlas, set their Tag to *None*.
- **He doesn't sway/breathe but blending works** → masks missing: check
  `Assets/Resources/CharacterMasks/` contains `<sprite name>_mask.png` for all
  16, or assign the **Direction Masks** array.
- **Snapping still happens** → make sure `IdleAnimator` was removed and the
  SpriteRenderer's material is the BillboardBlendWind one.
- **Regenerating masks** after artwork changes: `python3 tools/generate_sway_masks.py`
  (needs `pillow` + `numpy`), then re-copy the masks folder.
