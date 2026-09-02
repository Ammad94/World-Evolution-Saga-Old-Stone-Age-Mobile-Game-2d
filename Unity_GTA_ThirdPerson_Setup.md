# GTA V–Style Third-Person Setup (Perspective Camera) - REFERENCE MATCH

> **🔥 NEW - EXACT REFERENCE MATCH FOR https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg**
> This version now **exactly matches your reference image** + player follow camera.
> See **[unity_assets/GTA_Reference_Camera_Guide.md](unity_assets/GTA_Reference_Camera_Guide.md)** for pixel-perfect preset.
> Preset: Distance 3.5, Pitch 9, Yaw 0, Shoulder 0, FOV 50, Auto-Follow + Idle Bob.
> New scripts: `ThirdPersonCamera.cs` (updated), `SideScrollerCamera.cs` (new), `CameraFollow.cs` (updated).
>
> **Smooth Billboard** still available in [Unity_Smooth_Billboard_Setup.md](Unity_Smooth_Billboard_Setup.md)
> (BillboardCharacter + blend shader).

This replaces the top-down 2D setup with a proper **third-person, behind-the-player perspective camera** — the character is a billboarded sprite that always faces the camera, and the correct 1 of 8 directional sprites is chosen automatically. Now with GTA V reference framing.

## New files in `unity_assets/`

| File | Where it goes | Purpose |
|------|---------------|---------|
| `PlayerController3D.cs` | On the **Player** | Movement |
| `ThirdPersonCamera.cs` | On the **Main Camera** | **UPDATED - GTA reference match, player follow, idle cam** |
| `SideScrollerCamera.cs` | On the **Main Camera** (alternative for 2D side-scroller) | **NEW - GTA framing for side-scroller mobile** |
| `CameraFollow.cs` | On the **Main Camera** (alternative for 2D) | **UPDATED - GTA framing + look-ahead** |
| `BillboardCharacter.cs` | On the **Player** | Smooth blend version |
| `GTA_Reference_Camera_Guide.md` | Docs | **NEW - Exact steps to match screenshot** |

---

## Quick Start - Match Reference Image Exactly

### For 3D Billboard (Recommended)

1. **Main Camera**:
   - Projection: **Perspective**
   - FOV: **50**
   - Add **ThirdPersonCamera**, drag Player to Target
   - Click **⋮ -> Reset** on the component - this auto-applies GTA preset:
     ```
     Distance 3.5
     Pitch 9
     Yaw 0 (directly behind)
     Look Height 1.1
     Shoulder Offset 0
     Vertical Offset 0.15
     FOV 50
     Smooth Speed 6
     Allow Pitch Orbit OFF
     Auto Follow Facing ON
     Idle Bob ON
     ```

2. **Player**: Position (0,0,0), Scale (0.45,0.45,0.45), BillboardCharacter, Bottom pivot sprites

3. **Ground**: Plane at (0,0,0), Scale (5,1,5)

4. **Press Play** - You get the GTA V-style view from your screenshot: behind, slightly above, centred, with the character lower in frame and the horizon near the top. The framing stays stable while idle, walking, or running.

### For 2D Mobile Side-Scroller

1. Main Camera: **Orthographic**, Size 3.8
2. Add **SideScrollerCamera**, Target = Player
3. Settings:
   ```
   Vertical Framing 1.2
   Follow Smooth 5
   Dead Zone X 0.5
   Look Ahead 2
   Keep Same View On Idle true
   Idle Bob true
   ```

---

## Step-by-step (Original - still valid)

### Step 1 — Replace the scripts

1. Copy both new `.cs` files into `Assets/Scripts/`.
2. **On the Player** (Hierarchy → Player → Inspector):
   - **Remove** the old `PlayerController` component
   - **Remove** `Rigidbody 2D` and `Box Collider 2D`
   - **Add Component → PlayerController3D** or **BillboardCharacter** (recommended)
   - Assign Direction Sprites in order

3. **On the Main Camera**:
   - **Remove** `CameraFollow`
   - **Add Component → ThirdPersonCamera**
   - Drag the **Player** into the `Target` field

### Step 2 — Sprite pivot → Bottom

Select all sprite PNGs -> Inspector -> **Pivot → Bottom** -> Apply

### Step 3 — Ground

Hierarchy → 3D Object → Plane, Position (0,0,0), Scale (5,1,5)

### Step 4 — Position the player

Player Position (0,0,0)

### Step 5 — Press Play

| Input | Action |
|-------|--------|
| **W / A / S / D** | Move relative to camera (W = away) |
| **Left-click** | Walk to clicked point |
| **Hold Right Mouse + drag** | Orbit horizontally with inertia |
| **Scroll** | Zoom |

---

## Match the reference "camera angle" view - NEW PRESET

To get the exact look from https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg:

| Setting | Value | Why |
|---------|-------|-----|
| `Distance` | **3.5** | Close like ref, character ~60% height |
| `Pitch` | **9** | Low eye-level like GTA V ref, horizon near top |
| `Look Height` | **1.1** | Chest height |
| `Yaw` | **0** | Directly behind the player (deterministic centred follow) |
| `Shoulder Offset` | **0** | Player stays centred; raise it for an over-the-shoulder view. |
| `Vertical Offset` | **0.15** | Character lower in frame |
| `Field Of View` | **50** | Cinematic narrow like ref |
| `Allow Pitch Orbit` | **OFF** | Locks pitch to reference |
| `Auto Follow Facing` | **ON** | Swings behind while moving - GTA |
| `Idle Bob` | **ON** | Same view while idle but alive; Idle Slow Orbit is OFF in the centred preset |

Right-click Reset on ThirdPersonCamera applies all these automatically.

---

## Tuning

| Setting | What it does |
|---------|--------------|
| `Distance` | How far behind player (3.5 = ref) |
| `Pitch` | Look-down angle (9 = ref) |
| `Look Height` | Aim height (1.1 = chest) |
| `Shoulder Offset` | Horizontal camera offset (0 = centred) |
| `Smooth Speed` | Follow snappiness (6 = filmic) |
| `Auto Follow Facing` | Camera swings behind while moving |
| `Idle Bob` | Subtle breathing while idle |
| `Idle Slow Orbit` | Optional cinematic orbit after 3 sec idle (off in the centred preset) |

---

## Player Follow Camera (player follow camera bhi)

All cameras now have player follow:

- **SmoothDamp** position follow (filmic, no jitter)
- **Look-ahead** in movement direction (see where you're going)
- **Auto follow facing** (camera swings behind when you run)
- **Same view while idle** - no snap, just subtle bob
- **Dead zone** prevents micro jitter
- Works with joystick, touch, gamepad

For mobile joystick: enable Auto Follow, camera will auto-follow joystick direction.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Character invisible / camera inside him | Check Target assigned, Distance not 0 |
| Character is too small | With Player Scale `0.45`, set ThirdPersonCamera Distance to about `3.5` (or raise Player Scale to `0.7`). |
| Green streaks / green edges around him | **Delete** the old player PNGs in Unity and re-copy `unity_assets/sprites_16` (not `raw_sheets`). Also re-copy the shader and `BillboardCharacter.cs` — the script now forces the green-edge cut every frame. |
| Side profile appears at startup | Re-copy `ThirdPersonCamera.cs` and leave **Use Reference Framing On Play** ON. Remove `CameraFollow`/`SideScrollerCamera`, assign Target, keep the 16 sprites in order. |
| Half body underground | Pivot = Bottom, Player Y = 0 |
| Walks wrong direction | Tick Mirror Left Right |
| Click-to-move doesn't work | Click on ground below horizon |
| Camera orthographic looks wrong | Auto-switches to Perspective, or set manually |
| Want exact ref match | Click Reset on ThirdPersonCamera, see GTA_Reference_Camera_Guide.md |
