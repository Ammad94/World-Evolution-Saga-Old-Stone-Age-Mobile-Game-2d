# GTA V–Style Third-Person Setup (Perspective Camera)

This replaces the top-down 2D setup with a proper **third-person, behind-the-player
perspective camera** — the character is a billboarded sprite that always faces the
camera, and the correct 1 of 8 directional sprites is chosen automatically.

## New files in `unity_assets/`

| File | Where it goes |
|------|---------------|
| `PlayerController3D.cs` | On the **Player** (replaces `PlayerController.cs`) |
| `ThirdPersonCamera.cs` | On the **Main Camera** (replaces `CameraFollow.cs`) |
| `IdleAnimator.cs` | On the **Player** (plays the breathing / wind-sway animation) |

---

## Step 1 — Replace the scripts

1. Copy both new `.cs` files into `Assets/Scripts/`.
2. **On the Player** (Hierarchy → Player → Inspector):
   - **Remove** the old `PlayerController` component (right-click the component title → Remove Component).
   - **Remove** `Rigidbody 2D` and `Box Collider 2D` — they're for the old 2D physics and aren't needed here.
   - **Add Component → PlayerController3D**.
   - Open **Direction Sprites** (Size = 8) and drag the 8 sprites in the **same order as before**:

     | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
     |---|---|---|---|---|---|---|---|
     | 00_front | 01_front_right | 02_right | 03_back_right | 04_back | 05_back_left | 06_left | 07_front_left |

3. **On the Main Camera** (Hierarchy → Main Camera → Inspector):
   - **Remove** `CameraFollow` (and `ZoomOnScroll` if you added it earlier).
   - **Add Component → ThirdPersonCamera**.
   - Drag the **Player** into the `Target` field.

## Step 2 — Sprite pivot → Bottom

The character is now billboarded, so the sprite's pivot must be at his **feet**
(otherwise half of him sinks below the ground):

1. Select all 8 sprite PNGs in the Project window.
2. In the Inspector set **Pivot → Bottom** → **Apply**.

## Step 3 — Ground (strongly recommended)

With a perspective camera you'll see the skybox; without a ground the player
looks like he's floating in a blue void.

1. Right-click in the Hierarchy → **3D Object → Plane**. Rename it **Ground**.
2. Set its Transform: **Position (0, 0, 0)**, **Scale (5, 1, 5)** (a 50×50 unit floor).
3. Optional: give it a material (Project → Create → Material, set Base Map color to a
   brown/green, drag onto the plane). *I can also generate you a top-down grass/dirt
   texture if you want.*

## Step 4 — Position the player

- Select **Player**, set Transform **Position (0, 0, 0)**.

## Step 5 — Press Play ▶

### Controls (GTA-style)

| Input | Action |
|-------|--------|
| **W / A / S / D** (or arrows) | Move **relative to the camera** (W = run away from the camera, S = toward it) |
| **Left-click** | Walk to the clicked point on the ground |
| **Hold Right Mouse + drag** | Orbit **horizontally** around the player — with inertia, it glides to a stop |
| **Scroll wheel** | Zoom in / out |

The camera starts behind the player, so you'll first see his **back** — exactly like GTA.

---

## Smoother orbit + "not 2D sprites" upgrade

Two things cause the "2D sprite" feel: a steppy camera and too few facing directions.
Both are fixed below.

### A. Smooth camera orbit (update the scripts)

Copy the **updated** `ThirdPersonCamera.cs` and `PlayerController3D.cs` over your old ones.

- `ThirdPersonCamera` now **low-pass filters** the mouse (no jitter) and adds **inertia**,
  so the orbit glides to a stop when you release the right mouse button.
- `PlayerController3D` now supports **any number of sprites** (8, 16, …) and has a
  **"dead zone"** so the sprite doesn't flicker back and forth at angle boundaries.

> ⚠️ **After overwriting the camera script, set `Mouse Sensitivity` to `0.25`.**
> The meaning of that field changed (it's now degrees-per-pixel), so an old value
> like 4 will make the orbit spin wildly. Easiest: click **⋮ → Reset** on the
> `ThirdPersonCamera` component, then re-drag the Player into `Target`.

Orbit tuning (all on `ThirdPersonCamera`):

| Setting | Effect |
|---------|--------|
| `Orbit Smoothing` | Higher = orbit follows the mouse faster; lower = floatier (default 12) |
| `Inertia Damping` | Glide length after release. ~4 = GTA-like glide (default 4) |
| `Mouse Sensitivity` | Orbit speed, degrees per pixel (default 0.25) |

### B. 16-direction sprites — CORRECTED (consistent outfit)

The first 16-direction set had an outfit inconsistency (the chest strap crossed on the
back but not the front). I regenerated it: the **X-crossed leather chest strap is now
identical on chest AND back**, and the hair/beard/necklace/arm-band/fur wraps are
consistent in all 16 views.

The corrected set lives in **`unity_assets/sprites_16_idle/`** — the files ending in
`_f0` are the corrected static 16-direction sprites (frame 0). Use those:

1. Import the 16 `*_f0.png` files from `unity_assets/sprites_16_idle/` into
   `Assets/Sprites/Player16/` (Texture Type = Sprite, PPU = 100, Compression = None,
   **Pivot = Bottom**, Apply).
2. On the Player, set `Direction Sprites` **Size = 16** and drag them in **this order**:

| Slot | File | Slot | File |
|------|------|------|------|
| 0 | 00_front_f0 | 8 | 08_back_f0 |
| 1 | 01_fr_slight_f0 | 9 | 09_bl_slight_f0 |
| 2 | 02_front_right_f0 | 10 | 10_back_left_f0 |
| 3 | 03_right_front_f0 | 11 | 11_left_back_f0 |
| 4 | 04_right_f0 | 12 | 12_left_f0 |
| 5 | 05_right_back_f0 | 13 | 13_left_front_f0 |
| 6 | 06_back_right_f0 | 14 | 14_front_left_f0 |
| 7 | 07_br_slight_f0 | 15 | 15_fl_slight_f0 |

(The old `sprites_16/` folder has the inconsistent set — you can ignore or delete it.)

3. That's it — the controller picks the right sprite automatically.

### C. Idle animation — breathing + hair/fur swaying in the wind

I generated a **3-frame idle loop** for all 16 directions: frame 0 = neutral,
frame 1 = inhale (chest up, hair & fur sway right), frame 2 = exhale (chest down,
hair & fur sway left). The files are in `unity_assets/sprites_16_idle/`.

1. Import **all 48** PNGs (the `_f0`, `_f1`, `_f2` files, but NOT `_preview_*.png`)
   into `Assets/Sprites/Player16Idle/` with the same import settings.
2. On the Player, **Add Component → IdleAnimator**.
3. Set on `IdleAnimator`:
   - `Direction Count` = **16**
   - `Frame Count` = **3**
   - `FPS` = **4** (raise for faster breathing)
   - `Frames` **Size = 48**, and drag the sprites in **direction-major order** — for
     direction 0 drag `00_front_f0, 00_front_f1, 00_front_f2`, then direction 1
     `01_fr_slight_f0…f2`, and so on through direction 15. Full order:

   | Direction | Files (3 each) | Direction | Files (3 each) |
   |-----------|----------------|-----------|----------------|
   | 0 | 00_front_f0..f2 | 8 | 08_back_f0..f2 |
   | 1 | 01_fr_slight_f0..f2 | 9 | 09_bl_slight_f0..f2 |
   | 2 | 02_front_right_f0..f2 | 10 | 10_back_left_f0..f2 |
   | 3 | 03_right_front_f0..f2 | 11 | 11_left_back_f0..f2 |
   | 4 | 04_right_f0..f2 | 12 | 12_left_f0..f2 |
   | 5 | 05_right_back_f0..f2 | 13 | 13_left_front_f0..f2 |
   | 6 | 06_back_right_f0..f2 | 14 | 14_front_left_f0..f2 |
   | 7 | 07_br_slight_f0..f2 | 15 | 15_fl_slight_f0..f2 |

4. That's it — press Play. The `IdleAnimator` takes over the sprite and loops the
   breathing + wind animation automatically. (If the static `Direction Sprites` array
   on `PlayerController3D` is also filled, it's ignored while the `IdleAnimator` is present.)

> The `_preview_idle_contact_sheet.png` in that folder shows all 48 sprites:
> rows are frames 0–2, columns are the 16 directions.

### Match the reference "camera angle" view

To get the exact look from your reference screenshot, set these on **ThirdPersonCamera**:

| Setting | Value | Why |
|---------|-------|-----|
| `Pitch` | **12** | The look-down angle from the reference (horizon sits near the top of the frame). Raise to ~20 for a higher, more top-down feel. |
| `Distance` | **7** | Puts the character at ~70% of screen height (close, like the reference). Scroll to fine-tune live. |
| `Look Height` | **1.5** | Aims at his chest. |
| `Yaw` | **0** | Directly behind him. The reference shows him slightly left-of-center (over-the-shoulder) — hold right-mouse and drag a tiny bit to the right to match, or set `Yaw ≈ -12`. |

The orbit is now **horizontal-only**: dragging up/down with the right mouse no longer
changes the pitch, so the camera never tilts away from the angle you set.

---

## Tuning

| Setting (on ThirdPersonCamera) | What it does |
|--------------------------------|--------------|
| `Distance` | How far behind the player the camera sits (default 7). Scroll wheel changes it live. |
| `Pitch` | **Fixed** look-down angle (default 12). The orbit no longer changes it. |
| `Look Height` | How high on the body the camera aims (1.5 = chest). |
| `Smooth Speed` | Higher = snappier follow. |
| `Auto Follow Facing` | ✔ = camera swings behind the player while they run (very GTA). |
| `Mouse Sensitivity` | Horizontal orbit speed when dragging with the right mouse. |
| `Yaw` | Horizontal angle around the player. |

On **PlayerController3D**: `Move Speed`, `Click To Move` (on/off), and
`Mirror Left Right` — tick this if he faces the wrong way when strafing left/right.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Character is invisible / camera is inside him | Check `Target` is assigned; make sure the camera's `Distance` isn't 0; in the Game view the camera should be behind and above him. |
| He floats / half body underground | Set the sprite **Pivot = Bottom** (Step 2) and Player Y = 0. |
| Walks the wrong direction when strafing | Tick `Mirror Left Right` on PlayerController3D. |
| Click-to-move doesn't work | You must click on the **ground** (below the horizon). If the camera looks straight down at the sky, clicks miss the y=0 plane. |
| Camera is orthographic (2D) and looks wrong | The script auto-switches it to **Perspective** on start; you can also set Projection → Perspective manually. |
| Player falls through / no collision | This setup uses direct movement (no physics). For walls/enemies we can add a CharacterController later — just ask. |

---

## Next upgrades (just ask)

- **8- or 16-direction walk/run cycles** wired to Unity's Animator (he currently slides while moving — the idle animation keeps playing, but the legs don't step)
- A **ground texture** (grass/dirt/stone-age dirt) generated to match the look
- **CharacterController-based movement** for real wall collision
- More idle animation frames (e.g. 6–8) for a smoother, slower breathing loop
