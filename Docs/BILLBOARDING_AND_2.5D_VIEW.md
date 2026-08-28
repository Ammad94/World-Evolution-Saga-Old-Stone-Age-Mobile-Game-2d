# 🎥 2.5D Diorama View & Billboarding System

This document explains the complete **2.5D Billboarding & Perspective Camera System** implemented for Prehistoric Survival, based on the technique demonstrated in [How To... Billboarding in Unity - 2D Sprites in 3D (gamesplusjames)](https://www.youtube.com/watch?v=_LRZcmX_xw0), adapted and optimized for **hyper-realistic 2D sprites**.

---

## 🌟 Overview

In standard 2D games, sprites lie flat on the screen. In a **2.5D / 3D diorama view**:
- The camera is placed at an elevated position and pitched downward (e.g. 45°–55°), viewing the world from a diorama perspective.
- The ground/terrain tiles extend horizontally.
- All 2D sprites (characters, animals, trees, bushes, rocks, buildings, items) stand upright in 3D space and are **billboarded** so they face the camera.

---

## 🛠️ The Billboarding Component (`Billboard.cs`)

The `Billboard` component (`Assets/Scripts/Art/Billboard.cs`) is attached to any sprite's visual transform to keep it facing the camera each frame in `LateUpdate()`.

### 1. Static Billboard (`useStaticBillboard = true`)
*Recommended for trees, props, buildings, fences.*
- **How it works**: The sprite matches the camera's view orientation (`transform.rotation = theCam.transform.rotation`).
- **Advantage**: Sprites stay perfectly parallel to the camera view plane and do **not** twist, spin, or distort as the character walks past them.

### 2. Cylindrical LookAt (`useStaticBillboard = false` / `BillboardMode.CylindricalLookAt`)
- **How it works**: Uses `transform.LookAt(theCam.transform)` but locks the **X (pitch)** and **Z (roll)** axes (`Quaternion.Euler(0, yaw, 0)`).
- **Advantage**: The sprite rotates around its vertical axis to point directly toward the camera's position in world space without tipping backward or forward.

### 3. Spherical LookAt (`BillboardMode.SphericalLookAt`)
- **How it works**: Completely aligns with the camera in all 3 axes.
- **Use case**: Floating icons, damage numbers, map pins, speech bubbles, and particle effects.

### 4. Diorama Tilt (`BillboardMode.DioramaTilt`)
- **How it works**: Pitches the sprite to stand perpendicular to the ground plane, matching the camera's exact pitch angle.

---

## 📹 2.5D Camera Controller (`CameraFollow.cs`)

The `CameraFollow` component (`Assets/Scripts/Player/CameraFollow.cs`) provides full control over the view:

| Setting | Type | Recommended Value | Description |
|---|---|---|---|
| **Projection Type** | `Perspective` / `Orthographic` | `Perspective` | `Perspective` gives true 3D depth, field of view, and realistic size scaling with distance (as in the video). `Orthographic` provides a clean axonometric diorama. |
| **Camera Mode** | Enum | `GTAChase` | `GTAChase`: Follows player heading with diorama tilt.<br>`DioramaIsometric`: Fixed isometric angle.<br>`FreeOrbit3D`: 360° user orbit.<br>`TopDown2D`: Classic flat 2D follow. |
| **Pitch Angle** | Float (`10°`–`75°`) | `45°`–`50°` | The downward tilt angle toward the ground. |
| **Chase Distance** | Float | `8.0`–`12.0` | Distance behind the character. |
| **Field of View** | Float (`25°`–`90°`) | `50°`–`60°` | Camera FOV in Perspective mode. |
| **Framing Bias** | Float | `2.0`–`2.5` | Looks ahead of the character to frame them in the lower third of the screen. |
| **Speed Zoom Out** | Float (`0`–`0.5`) | `0.15` | Smoothly pulls the camera back while the player runs at full sprint speed. |
| **Allow Manual Orbit** | Bool | `true` | Hold **Right Mouse Button** (PC) or swipe on the **right half of the screen** (Mobile) to orbit around the player. |

---

## 🎨 Best Practices for Hyper-Realistic Sprites

1. **Bottom-Center Pivot**:
   - Ensure trees, characters, and props have their sprite pivot set to **Bottom-Center** (or where the object touches the ground).
   - This ensures the sprite rotates around its base when billboarding.

2. **Visual Separation (`Fake3D.Ensure`)**:
   - To prevent billboarding from rotating physics colliders or rigidbodies, `Fake3D.Ensure(gameObject)` moves the `SpriteRenderer` into a child `"Visual"` object while keeping colliders on the root.

3. **8-Directional Animations**:
   - For characters and animals, `PlayerController` and `AnimalWalkAnimator` automatically compute the sprite direction **relative to camera yaw** (`moveAngle - CameraYawDeg`), ensuring sprites face the correct direction from any camera orbit angle.

4. **Wind Sway Integration**:
   - Billboarded trees and bushes automatically inherit gentle sinusoidal wind sway from `WindSystem` while remaining upright to the camera.

---

## 🚀 Quick Usage in Unity Editor

1. Select any sprite or prefab in the project.
2. In the Inspector, click **Add Component** → **Billboard**.
3. Choose your preferred mode:
   - Check `Use Static Billboard` for camera-aligned rendering.
   - Uncheck `Use Static Billboard` for cylindrical LookAt.
4. On your **Main Camera**, ensure `CameraFollow` is attached and set `Projection Type` to **Perspective** and `Camera Mode` to **GTAChase**.
5. Press **Play**!
