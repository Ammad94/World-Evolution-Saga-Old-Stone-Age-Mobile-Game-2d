# Unity Setup Guide — Stone Age Playable Character (8-Direction Idle Sprites)

Everything in the `unity_assets/` folder is ready to drop into Unity.

## What's in the folder

```
unity_assets/
├── sprites/
│   ├── 00_front.png         (facing you)
│   ├── 01_front_right.png
│   ├── 02_right.png
│   ├── 03_back_right.png
│   ├── 04_back.png          (facing away)
│   ├── 05_back_left.png
│   ├── 06_left.png
│   ├── 07_front_left.png
├── PlayerController.cs
└── CameraFollow.cs
```

All 8 sprites are **transparent PNGs**, same scale, and their **feet are aligned to the same baseline** so they don't jump around when the direction changes.

---

## Step-by-step

### 1. Create a Unity project
- Unity Hub → New project → choose the **2D (Built-in Render Pipeline)** template → name it (e.g. `StoneAge`).
- (A URP 2D project also works fine.)

### 2. Import the sprites
- Copy the 8 `sprites/*.png` files into `Assets/Sprites/Player/` (just drag the folder into the Project window, or copy into the project folder on disk).
- Select all 8 PNGs in the Project window and set in the Inspector:
  - **Texture Type** → `Sprite (2D and UI)`
  - **Sprite Mode** → `Single`
  - **Pixels Per Unit** → `100`
  - **Filter Mode** → `Bilinear` (it's realistic art, not pixel art)
  - **Compression** → `None` (keeps edges crisp around the transparent outline)
  - **Pivot** → `Center`  *(Center keeps his body in the middle of the screen — best for a third-person feel. `Bottom` makes the camera center on his feet, so his body sticks upward in the frame.)*
  - Click **Apply**.

### 3. Import the scripts
- Drag `PlayerController.cs` and `CameraFollow.cs` into `Assets/Scripts/`.

### 4. Create the Player
> ⚠️ **Unity 6 note:** In Unity 6 (especially URP projects) the right-click `2D Object` menu can show only `Pixel Perfect Camera (URP)` and no `Sprite → Square`. Use method A or B below instead — they always work.

**Method A — drag & drop (fastest, recommended):**
1. In the **Project** window, find your `00_front` sprite (inside `Assets/Sprites/Player/`).
2. Drag it into the **Scene** view. Unity automatically creates a GameObject with a `SpriteRenderer`. Rename it **Player** (in the Inspector or by pressing F2).
3. Make sure its **Transform** Position = `(0, 0, 0)`.

**Method B — Create Empty + add SpriteRenderer:**
1. In the **Hierarchy**, right-click → **Create Empty**. Rename it **Player**.
2. With Player selected, **Add Component → Rendering → Sprite Renderer**.
3. Drag `00_front` into the Sprite Renderer's **Sprite** field.

Then continue (whichever method you used):
- Add a `BoxCollider2D` if one isn't already there (Methods A/B don't add one).
3. With Player selected, in the Inspector set `Sprite` = `00_front`.
4. **Add Component → Physics 2D → Rigidbody2D**:
   - **Gravity Scale** → `0` (top-down, no gravity)
   - **Freeze Rotation Z** → ✔ (so it never tips over)
5. **Add Component → Physics 2D → Box Collider 2D**:
   - Resize it to cover roughly the lower half of the body (his feet/legs), so he doesn't walk through walls at head height.
6. **Add Component → Scripts → PlayerController**.

### 5. Assign the 8 sprites to the controller
- On the `PlayerController` component, open the **Direction Sprites** array (set Size = 8) and drag the sprites in **this exact order**:

| Slot | Sprite file | Meaning |
|------|-------------|---------|
| 0 | 00_front | Facing you |
| 1 | 01_front_right | Down-right |
| 2 | 02_right | Right |
| 3 | 03_back_right | Up-right |
| 4 | 04_back | Away from you |
| 5 | 05_back_left | Up-left |
| 6 | 06_left | Left |
| 7 | 07_front_left | Down-left |

### 6. Set up the camera
1. Select the **Main Camera** in the Hierarchy.
2. **Add Component → Scripts → CameraFollow**.
3. Drag the **Player** from the Hierarchy into the `Target` field.
4. Set the camera so it looks down at the player:
   - **Projection** → `Orthographic`, **Size** → `3.8` (smaller = closer. `8` = far away, `3.8` ≈ GTA-style close view where the character fills ~70% of the screen)
   - **Position** → `(0, 0, -10)`, **Rotation** → `(0, 0, 0)`
   - On CameraFollow set `Offset` = `(0, 0, -10)` — **the Z must stay negative (-10)**, otherwise the camera ends up in front of the scene and the character walks out of view.

### 7. Press Play ▶
- **WASD / arrow keys** move the character; the sprite automatically switches to the correct facing direction.

---

## Making it feel like GTA V

The sheet is an **8-direction turntable**, so there are two common ways to use it:

**A. Top-down orthographic (easiest, shown above)**
- Orthographic camera follows the player (CameraFollow).
- Best for top-down / 3/4-view games like classic GTA 1 & 2 or twin-stick games.

**B. GTA V-style third-person (behind-the-shoulder feel)**
- Use a **Perspective** camera placed behind/above the player, e.g. position `(0, 4, -7)`, rotation `(35, 0, 0)`.
- Add the `CameraFollow` script the same way.
- Point the sprite at the camera with a tiny "billboard" script so it always renders full-frontal, and let the controller pick the direction sprite from the 8 set. (Say the word and I'll write you this billboard script + a mouse-to-move scheme.)

---

## Important note — this is the IDLE sheet

Right now the character **slides** while showing the idle pose. For a real walking/running character you also need **walk/run sprites for each of the 8 directions** (and optionally jump/attack).

I can generate those next — for example:
- **8-direction walk cycle** (4–6 frames per direction)
- **8-direction run cycle**
- **Idle breathing animation** (2–4 subtle frames per direction)

Then you'd wire them up with Unity's **Animator** (an Animator Controller with Idle/Run states and a Blend Tree). I can walk you through that too.

---

## Common fixes

| Problem | Fix |
|---------|-----|
| Sprite looks blurry | Set Filter Mode to `Bilinear`, Compression `None`; raise Pixels Per Unit |
| Character overlaps trees/walls wrong | Use a **Sorting Layer** + set `Order in Layer` by Y, or enable Project Settings → Graphics → Transparency Sort Mode = `Custom Axis (0,1,0)` |
| Character walks through walls | Make sure the Box Collider 2D is sized to the body and walls also have colliders |
| No movement | Check you assigned the sprites and the Rigidbody2D has Gravity Scale 0 |
| "Assign exactly 8 sprites" error | The `directionSprites` array must have exactly 8 entries |
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...` | The project is on the **new Input System**. Re-copy the updated `PlayerController.cs` (it auto-detects and uses the Input System). Or switch **Project Settings → Player → Active Input Handling → Both**. |
| Character runs off screen / camera doesn't keep him centered | 1) Camera **Size** too small → set `8`. 2) **CameraFollow → Offset** must be `(0, 0, -10)` (negative Z). 3) Sprite **Pivot = Center** so the camera centers on his body, not his feet. 4) Set **Snap To Target = true** on CameraFollow to lock him dead-center. |
| Player too small / too far away | Camera **Size** is too big. Lower it — `3.8` gives a close GTA-style view (~70% of screen height). Formula: `Size ≈ spriteHeightUnits × 0.36`. You can also add the optional `ZoomOnScroll.cs` to zoom with the mouse wheel in Play mode. |
| Player too big / too close | Camera **Size** is too small. Raise it — e.g. `5`–`8`.

---

## Note on Input Systems

Unity 6 projects often ship with the **new Input System** active, which makes the old
`Input.GetAxisRaw()` throw an `InvalidOperationException`. The `PlayerController.cs` in this
folder handles **both** automatically:

- If **Active Input Handling** is `Input System` or `Both` → it reads keys via the new Input System.
- If it's `Input Manager (Old)` → it uses the classic `Input` class.

So just re-copy the script over your old one and the error is gone. (If you instead prefer
to keep the old script, set **Edit → Project Settings → Player → Other Settings → Active Input
Handling = Both**, and Unity will prompt a restart.)
