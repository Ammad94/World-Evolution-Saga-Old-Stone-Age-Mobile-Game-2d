# GTA V Reference Camera Setup - Matches https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg

This guide makes your camera look **exactly** like the GTA V reference screenshot you sent - while idle, walking, running, etc., with player follow.

## Reference Analysis

The image `https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg` is GTA V PC gameplay:

- **Low pitch, eye-level behind view**: Camera ~1.5m above ground, looking slightly down (8-12 deg), not top-down
- **Character lower in frame**: Character's feet near bottom 20%, head near middle, horizon near top 25% - you see ground ahead
- **Centred follow**: The player stays on the horizontal centre line; use shoulder offset only if you explicitly want an over-the-shoulder variant
- **Close framing**: Character fills ~60% of screen height (distance ~3.5)
- **FOV**: ~48 deg (cinematic, not fish-eye)
- **Same view while idle**: No camera snap or dramatic change between idle/move - stable

## Which script to use?

| Your Game Type | Script | Camera Projection |
|---|---|---|
| **3D Billboard (recommended for this project)** - Stone Age caveman is billboarded sprite in 3D world | `ThirdPersonCamera.cs` | **Perspective** |
| **2D Mobile Side-Scroller** - Pure 2D orthographic | `SideScrollerCamera.cs` | **Orthographic** |
| **2D Top-Down / Legacy** - Old 2D setup | `CameraFollow.cs` (updated) | **Orthographic** |

All three now have GTA reference preset.

---

## Setup A: 3D Billboard (Main - matches ref exactly)

This is the best match for your project (you already use `BillboardCharacter`).

### 1. Copy scripts
Copy to `Assets/Scripts/`:
- `ThirdPersonCamera.cs` (new version)
- `BillboardCharacter.cs` (existing)

### 2. Main Camera setup
1. Select **Main Camera** in Hierarchy
2. Inspector -> **Camera** component:
   - **Projection**: **Perspective**
   - **Field of View**: **50** (script will control this, but set initial)
   - **Clipping Planes**: Near 0.3, Far 1000
   - **Position**: (0, 2, -8) approx, will be overridden by script
3. Remove old `CameraFollow` if present
4. **Add Component -> ThirdPersonCamera**
5. Drag **Player** into **Target** field
6. Click **⋮ (three dots) on ThirdPersonCamera -> Reset** - this applies the GTA reference preset automatically:
   ```
   Distance = 3.5
   Pitch = 9
   Yaw = 0
   Look Height = 1.1
   Shoulder Offset = 0
   Lock Horizontal Centre = ON
   Vertical Offset = 0.15
   FOV = 50
   Smooth Speed = 6
   Allow Pitch Orbit = OFF (locks to ref angle)
   Auto Follow Facing = ON
   Idle Bob = ON
   ```

### Sprite import settings

For every direction PNG, use **Texture Type: Sprite (2D and UI)**, **Sprite
Mode: Single**, **Pixels Per Unit: 100**, **Pivot: Bottom**, **Mesh Type: Full
Rect**, **Wrap Mode: Clamp**, **Compression: None**, and **Alpha Is
Transparency: On**. Do not put the sprites in a Sprite Atlas. Reimport them
after changing these settings; the source art was keyed from a green screen and
needs the alpha setting to avoid green edge streaks.

### 3. Fine-tune to match screenshot pixel-perfect

| Setting | Value | Effect |
|---|---|---|
| `Distance` | **3.5** | Close like ref. Lower = closer (character bigger). GTA V ref is close. |
| `Pitch` | **9** | Low angle like ref. 8 = more eye-level, 12 = more top-down. Ref is ~9. |
| `Yaw` | **0** | Directly behind the player. Use a nonzero value for an angled orbit. |
| `Look Height` | **1.1** | Chest height. 1.0 = waist, 1.5 = head. Ref looks at chest. |
| `Shoulder Offset` | **0** | Player stays centred. Raise it for an over-the-shoulder variant. |
| `Vertical Offset` | **0.15** | Raises camera a bit so character is lower in frame (more sky/horizon). |
| `Field Of View` | **50** | Cinematic GTA. 60 = wider, more fish-eye. |

**To get EXACT reference framing:**
- Set `Allow Pitch Orbit = OFF` - locks pitch to 9 deg like ref (GTA V idle doesn't tilt up/down unless you drag)
- Set `Auto Follow Facing = ON` - camera swings behind when you run (very GTA)
- Set `Idle Bob Enabled = ON` - subtle breathing while idle, same view but alive

### 4. Player setup
Player should have:
- **Transform**: Position (0,0,0), Scale (0.45,0.45,0.45) (about 1.8 m tall)
- **SpriteRenderer**: with Billboard material
- **BillboardCharacter** component (not PlayerController3D)
- **Capsule Collider** or **CharacterController** (optional, for collision)

Ground:
- **3D Plane** at (0,0,0), Scale (5,1,5) - so you see ground like ref image

### 5. Press Play
- **WASD**: Move relative to camera (W = away from camera, like GTA)
- **Hold Right Mouse + drag**: Orbit horizontally (yaw) - inertia glide like GTA
- **Scroll**: Zoom in/out
- **Idle**: Camera stays the same, with subtle bob; slow idle orbit is off in the centred preset

---

## Setup B: 2D Side-Scroller Mobile (Orthographic)

For pure 2D mobile side-scrolling Old Stone Age game.

### 1. Camera
- **Projection**: **Orthographic**
- **Size**: **3.8** (close like GTA ref, character fills 70% height)
- Add **SideScrollerCamera** (new script)
- Target = Player

### 2. SideScrollerCamera settings (GTA ref)

```
Distance Z = -10
Vertical Framing = 1.2  (character lower, horizon near top like ref)
Horizontal Framing = 0
Follow Smooth = 5
Dead Zone X = 0.5 (stable cam, no jitter)
Dead Zone Y = 0.3
Use Look Ahead = true
Look Ahead Distance = 2 (camera looks ahead where you run, GTA style)
Idle Bob = true
Keep Same View On Idle = true (same view while idle/walk/run)
```

- For billboard in perspective, tick **Use Perspective Mode** and set:
  ```
  Perspective Pitch = 8
  Shoulder = 0.5
  Look Height = 1
  ```

### 3. Result
- Side-scrolling but with GTA framing: character lower, ground ahead visible
- Look-ahead when running
- Same view while idle (subtle bob, no snap)

---

## Setup C: Legacy CameraFollow (2D Top-Down)

If you still use the old 2D top-down:

1. Main Camera Orthographic Size 3.8
2. Add **CameraFollow** (updated version)
3. Settings:
   ```
   Offset = (0, 1, -10)
   Smooth Speed = 8
   Framing Offset Y = 0.8
   Use Look Ahead = true
   Look Ahead Distance = 1.5
   Dead Zone = 0.15
   Idle Bob = true
   ```

---

## Player Follow Camera Explained (player follow camera bhi)

Both new cameras are **player follow cameras**:

- **Position follow**: Camera SmoothDamp follows target every LateUpdate (filmic, no jitter)
- **Rotation follow**: In perspective mode, camera Slerp looks at player's chest
- **Auto follow facing**: When player moves, camera slowly swings behind (GTA style) - set `Auto Follow Facing = ON`
- **Look-ahead**: Camera looks slightly ahead in movement direction, so you see where you're going
- **Same view while idle**: `Keep Same View On Idle` / `Idle Bob` keeps framing identical idle vs moving, just adds subtle life

For mobile joystick:
- No right-mouse needed - camera auto-follows behind player as they move
- If using joystick, camera's `Auto Follow Facing` will keep player centered behind
- Touch drag still works for manual orbit

---

## Matching Reference Image Exactly - Checklist

To match https://i.ytimg.com/vi/oYlsmbxTVM4/maxresdefault.jpg pixel-perfect:

- [ ] Camera **Perspective**, FOV **50**
- [ ] **Pitch 9**, **Distance 3.5**, **Look Height 1.1**
- [ ] **Shoulder Offset 0**, **Yaw 0** (centred behind player)
- [ ] **Vertical Offset 0.15** (character lower)
- [ ] **Allow Pitch Orbit OFF** (locks angle like ref)
- [ ] **Auto Follow ON**, Delay 0.35, Speed 6
- [ ] **Idle Bob ON**, Amount 0.12; **Idle Slow Orbit OFF**
- [ ] Ground Plane visible, player at (0,0,0)
- [ ] BillboardCharacter with Bottom pivot sprites

Press Play - you should see the centred character from behind, ground ahead, and the horizon near the top - matching the GTA V-style framing.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Character too small / far | Keep Player scale at 0.45 and set Distance to about 3.5, or lower Camera Size (if ortho) to 3.2 |
| Green streaks / green edges | Re-copy `sprites_16` (not `raw_sheets`), enable **Alpha Is Transparency**, set Wrap Mode to **Clamp**, and use the updated billboard shader. |
| Character too big / close | Raise Distance above 3.5, or Size to 5 for the orthographic setup |
| Too top-down (see too much ground) | Lower Pitch to 8 or 6 |
| Too eye-level (no ground) | Raise Pitch to 12-15 |
| Character should be centred | Keep `Shoulder Offset = 0` and `Lock Horizontal Centre = ON`; raise Shoulder Offset only for an over-the-shoulder variant |
| Camera goes through ground | Enable Collision, set Collision Mask to Ground |
| Jitter when idle | Keep `Idle Slow Orbit = OFF`, and enable Idle Bob |
| Camera doesn't follow | Check Target assigned, Snap On Start true |
| Mobile touch not working | Enable Touch Orbit + Pinch Zoom, ensure Input System installed |

---

## Next Steps

- Add **Cinemachine**? Not needed - this is lightweight and mobile-friendly (no extra package)
- Want walk/run animations? The BillboardCharacter already blends directions smoothly
- Want mobile joystick UI? Add Unity's Joystick Pack, camera auto-follows joystick direction

---

## Update — centred follow + ghosting fix

### 1. Player is now dead-centre horizontally
- `shoulderOffset` default is **0** (was 0.55) and `yaw` default is **0** (was -6), so the
  camera sits directly behind the player like the reference screenshot.
- New **`lockHorizontalCentre`** (on by default): after the smoothed look-rotation, the
  camera yaw is snapped so the player's look point is exactly on the screen's vertical
  centre line. Vertical framing/pitch smoothing is untouched, so it still glides.
  (If you *want* an over-the-shoulder look, set `shoulderOffset > 0` — the lock disables
  itself automatically.)

### 2. Turning back with **S** no longer lands left *or* right at random
Cause: a 180° turn is exactly ambiguous for `Mathf.LerpAngle`, so tiny float noise flipped
the shortest path sign frame-to-frame — the camera swung left sometimes, right other times.
Fix: inside **`turnAroundDeadzone`** (default 18° around 180°) the swing direction is forced
to a fixed side, **`turnAroundSide`** (1 = right, -1 = left). Auto-follow is also now
frame-rate independent (`1 - exp(-speed*dt)`), faster (`autoFollowSpeed` 6, delay 0.35s),
and `idleSlowOrbit` is **off** by default so the camera can't drift off-centre while idle.

### 3. "Blurry / a second faint sprite showing through"
Cause: the direction cross-fade did a plain `lerp(spriteA, spriteB, blend)`. Mid-fade both
sprites are ~50% transparent, so their two silhouettes overlap and you see a faint ghost
caveman and a soft, blurry edge.

Fix in both shaders (`BillboardBlendWind` and `BillboardBlendWindURP`) — new `BlendDirs()`:
- **`_BlendSharp`** (script: `BillboardCharacter.blendSharpness`, default 0.8) compresses the
  fade into a narrow smoothstep window, so almost always exactly one sprite is on screen.
- **`_BlendAlphaUnion`** (script: `solidSilhouetteWhileBlending`) takes alpha as the *union*
  of the two silhouettes and weights colour by alpha, so the body never goes see-through.
- `turnSmoothTime` lowered to 0.08 so the character passes through the blend faster.

Set `blendSharpness = 1` for a hard snap (zero ghosting), lower it toward 0.4 if you prefer
a softer morph.
