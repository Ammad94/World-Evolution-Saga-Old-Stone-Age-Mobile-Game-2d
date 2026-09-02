# Unity Beginner Guide — from zero to playing your caveman

**Start here if you have never used Unity.** This walks you from installing
Unity all the way to walking around with a smooth, living billboard character.
Every click is spelled out. Total time: ~20 minutes.

What you will end up with: a 3D scene where you move a stone-age character
with WASD, the camera orbits smoothly around him, and the character feels
alive — hair swaying in a breeze, breathing, blinking, glancing left/right —
with **no 2D-sprite "paper cut-out" flipping**.

> Already have a project? Jump straight to **Step 3**.

---

## Step 0 — Install Unity (once)

1. Download **Unity Hub** from `unity.com/download` and install it.
2. Open Unity Hub → **Installs** → **Install Editor** → pick the newest
   **Unity 2022.3 LTS** or **Unity 6** release (any recent one works).
3. When it asks which modules to install, you need nothing extra for this.

## Step 1 — Create the project

1. Unity Hub → **New project**.
2. Pick the template:
   - **"3D (Built-in Render Pipeline)"** ← simplest, recommended, **or**
   - **"Universal 3D"** (that's URP — also fine, one file differs later).
3. Name it `StoneAge`, click **Create project**. First open takes a minute.

> Which one did I pick? If the template name contains **Universal** you are
> in URP and will use `BillboardBlendWindURP.shader`. Otherwise you are in
> Built-in and use `BillboardBlendWind.shader`. Just remember this for Step 4.

## Step 2 — Copy the files in

Open the project folder: in Unity, right-click inside the **Project** window
(bottom panel) → **Show in Explorer** (Windows) / **Reveal in Finder** (Mac).
That opens the folder that contains `Assets`. Now copy from this repo:

| Copy this | Into this new folder | Why |
|---|---|---|
| `unity_assets/sprites_16/` (16 PNGs) | `Assets/Sprites/Player/` | the character, 16 directions |
| `unity_assets/sprites_16_masks/` (16 PNGs) | `Assets/Resources/CharacterMasks/` | hair/breath sway masks — **folder name must be exactly `Resources/CharacterMasks`** |
| `unity_assets/BillboardCharacter.cs` | `Assets/Scripts/` | movement + all the magic, one component |
| `unity_assets/ThirdPersonCamera.cs` | `Assets/Scripts/` | the orbiting camera |
| `unity_assets/BillboardBlendWind.shader` **or** `unity_assets/BillboardBlendWindURP.shader` | `Assets/Shaders/` | the wind/breathing shader (pick per Step 1) |

Back in Unity, watch the bottom-right corner — it imports for a few seconds.

*(Optional: `sprites_16_idle/` and `sprites/` are NOT needed — the idle life
is done on the GPU. Keep them out of Unity to keep the project small.)*

## Step 3 — Fix the sprite import settings (important)

1. In the **Project** window open `Assets/Sprites/Player`, click the **first**
   PNG (`00_front`), then Shift-click the **last** (`15_front_left_slight`) —
   all 16 selected.
2. In the **Inspector** (right panel) set:
   - Texture Type: **Sprite (2D and UI)**
   - Sprite Mode: **Single**
   - Pixels Per Unit: **100**
   - **Pivot: Bottom**
   - Mesh Type: **Full Rect**
   - Filter Mode: **Bilinear**
   - Compression: **None**
   - Wrap Mode: **Clamp**
   - **Alpha Is Transparency: on** (removes the green-screen colour from filtered transparent edges)
3. Click **Apply** (top-right of the Inspector).

Do **not** put these sprites in a Sprite Atlas — it breaks the blending.

For the masks in `Assets/Resources/CharacterMasks/`: leave them at default
settings (Texture Type: Default). Done.

## Step 4 — Create the character material

1. **Project** window → right-click `Assets` → **Create → Material**.
2. Name it `CavemanBillboard` (slow-double-click to rename).
3. With it selected, top of Inspector: **Shader** dropdown → **Game →
   BillboardBlendWind** (or **Game → BillboardBlendWindURP** in URP projects).
4. Leave all the numbers at their defaults.

## Step 5 — Put the character in the scene

1. In the **Project** window find `00_front` (in `Sprites/Player`).
2. **Drag it into the Scene view** (or the Hierarchy on the left). A GameObject
   appears — rename it **Player** (select it, press F2).
3. With Player selected, in the Inspector set **Transform**:
   - Position: `0, 0, 0`
   - Scale: `0.45, 0.45, 0.45`  *(makes him about 1.8 m tall — human-sized)*
4. Still with Player selected:
   - **Add Component** (big button at the bottom of the Inspector) → search
     `BillboardCharacter` → click it.
   - In the new component find **Direction Sprites** → set **Size = 16** →
     drag the 16 sprites into the slots **in this exact order**:
     `00_front, 01_front_right_slight, 02_front_right, 03_right_front,
     04_right, 05_right_back, 06_back_right, 07_back_right_slight, 08_back,
     09_back_left_slight, 10_back_left, 11_left_back, 12_left,
     13_left_front, 14_front_left, 15_front_left_slight`.
     (Tip: lock the Inspector — the tiny padlock top-right — so it doesn't
     lose focus while you drag.)
   - Leave **Direction Masks** empty — they auto-load from
     `Resources/CharacterMasks/`.
5. On the Player's **Sprite Renderer** component: drag `CavemanBillboard`
   into the **Material** slot (Materials → Element 0).

No Rigidbody, no Collider needed — movement is handled by the script.

## Step 6 — Ground and camera

1. **Hierarchy** → right-click → **3D Object → Plane**. Set its Scale to
   `10, 1, 10` (a big grass field later; grey is fine for now).
2. Click **Main Camera** in the Hierarchy:
   - **Remove** the `CameraFollow`/older camera scripts if it has any.
   - **Add Component → Third Person Camera**.
   - Drag the **Player** from the Hierarchy into its **Target** slot.

## Step 7 — Press Play ▶

- **WASD / arrow keys** — walk around (relative to the camera, GTA-style)
- **Hold right mouse button + drag** — orbit the camera around him
- **Mouse wheel** — zoom
- **Left-click the ground** — walk there

You should see: while you orbit, he turns **continuously** through all
in-between poses (never a paper-flip), his hair sways in a breeze, his chest
breathes, he blinks and glances left/right now and then, and a soft shadow
sits under his feet.

---

## Controls on mobile / gamepad

| Action | Touch | Gamepad |
|---|---|---|
| Move | tap ground = click-to-move | left stick |
| Orbit | one-finger drag | right stick |
| Zoom | two-finger pinch | — |

## Troubleshooting (the 6 classic problems)

| Symptom | Cause → fix |
|---|---|
| Character/material is **pink** | Wrong shader for your pipeline → Built-in project: use `BillboardBlendWind`, URP project: `BillboardBlendWindURP` (Step 4) |
| He doesn't sway/breathe, but turning works | Masks not found → the folder must be `Assets/Resources/CharacterMasks/` containing `00_front_mask.png` … `15_front_left_slight_mask.png` |
| Error "packed into a SpriteAtlas" | Remove the sprites from any atlas (Atlas Tag → None) |
| Green streaks / green edges around him | **Delete** the old `Assets/Sprites/Player` PNGs and re-copy `unity_assets/sprites_16` (NOT `raw_sheets` — those are still green-screen). The sprites were re-keyed so leftover lime fringe is gone. Also copy the updated shader + `BillboardCharacter.cs` (it now forces Clamp wrap and the edge-cut every frame, so an old material cannot leave it off). |
| He faces the wrong way when strafing | Tick **Mirror Left Right** on BillboardCharacter |
| He is side-on at startup / still far away | Re-copy `ThirdPersonCamera.cs`. Leave **Use Reference Framing On Play** ON — it reapplies Distance 3.5 / Pitch 9 / Yaw 0 every Play so Unity cannot keep old inspector values. Remove `CameraFollow`/`SideScrollerCamera`. |
| Input errors in the Console | **Edit → Project Settings → Player → Active Input Handling → Both** |
| He walks through the camera / feels too big | Adjust Player **Scale** (0.45 ≈ 1.8 m) and camera **Distance** on ThirdPersonCamera |
| He is too small in the camera | With Player Scale `0.45`, keep `ThirdPersonCamera.Distance` around `3.5`; alternatively raise Player Scale to `0.7` if you prefer a larger human model. Change only one of these. |

## Tuning cheatsheet (all on the BillboardCharacter component)

| Want... | Turn... |
|---|---|
| Stronger breeze | **Wind Strength** / **Wind Speed** |
| More hair motion | **Hair Sway Pixels** (3 ≈ subtle, 5+ = windy) |
| Deeper/faster breathing | **Breath Amount** / **Breaths Per Second** |
| Bigger head glances | **Head Look Blend** (0.85 ≈ 19°) / **Head Look Amount** |
| No glances at all | **Head Look Amount = 0** |
| No blinks | **Blink = off** |
| Loincloth flutter (off by default) | **Cloth Flutter Pixels ≈ 3.6** |
| Faster/slower walking | **Move Speed** (default 5) |
| Snappier turning | **Turn Smooth Time** 0.08–0.15 |
| No lean while orbiting | **Orbit Lean Degrees = 0** |
| Several characters | Duplicate the Player — each one randomizes its own wind/breathing phase |

## What each file does (for the curious)

- `BillboardCharacter.cs` — replaces the old PlayerController + IdleAnimator.
  Moves the character, picks the two neighbouring direction sprites for the
  current camera angle and cross-fades them, drives wind/breathing/blink/
  head-glance values into the shader every frame.
- `BillboardBlendWind(.URP).shader` — renders the character: cross-fade
  between the two views, hair/cloth sway, chest breathing, head glances
  (blending toward the neighbour view's head), contact shadow.
- `sprites_16_masks/` — per-pixel weights (R = hair, G = cloth, B = torso)
  telling the shader which body region may move.
- The 48 idle frames and the 8-direction set are **not needed** in Unity —
  the idle life is generated on the GPU from the 16 static sprites.
