# Prehistoric Survival - Unity 6 Project Setup Guide

## Project Overview
**Genre:** Hyper-Realistic 2.5D Open-World Prehistoric Survival Simulator  
**Engine:** Unity 6.3 LTS (6000.3.22f1) with DX12  
**Render Pipeline:** Universal Render Pipeline (URP 2D) with 2D Lighting & Shadow Caster 2D  
**Target Platforms:** Mobile (Android/iOS) + PC

---

## 📦 Installation & Setup

### 1. Prerequisites
- **Unity Hub** installed ([download](https://unity.com/download))
- **Unity 6.3 LTS** (6000.3.22f1) installed via Unity Hub
- **Android Build Support** module (for mobile builds)
- **iOS Build Support** module (Mac only, for iOS builds)

### 2. Opening the Project
1. Open **Unity Hub**
2. Click **Add** → navigate to the `PrehistoricSurvival` folder
3. Select the folder and click **Open**
4. Unity will import all assets and packages (this may take several minutes)

### 3. One-click project setup (required once)

Run **PrehistoricSurvival → Setup Entire Project** from the Unity menu bar. It will:

1. create every folder, item/recipe ScriptableObject and prefab (player, 4 animals,
   trees, bushes, rocks, pickups, campfire, raft),
2. build `Assets/Resources/GameLibrary.asset` — the runtime registry the game loads
   its art and prefabs from,
3. create `MainMenu.unity` and `GameplayWorld.unity` **with all buttons wired**,
4. add both scenes to Build Settings, set tags/layers, create the URP 2D pipeline
   asset and switch the input backend to *Both*.

Then open `Assets/Scenes/MainMenu.unity` and press **Play**.

> If Unity asks to restart after the input backend change, do it — the player
> controller uses `Input.GetAxisRaw` for keyboard support.
> If HUD text is invisible, run **Window → TextMeshPro → Import TMP Essential Resources**.

Useful extra menu items:

| Menu item | What it does |
|-----------|--------------|
| `Create Prefabs Only` | rebuild prefabs + GameLibrary |
| `Create Scenes Only` | rebuild both scenes and Build Settings |
| `Rebuild Game Library` | re-link sprites/prefabs after adding art |

### 4. Controls

| Action | Mobile | Keyboard |
|--------|--------|----------|
| Move | drag anywhere on the left half (joystick appears under your thumb) | WASD / arrows |
| Zoom | pinch | mouse wheel |
| Pause | ❚❚ button | Escape |
| World map | MAP button | MAP button |
| Set waypoint | tap the world map | click the world map |

### 5. Package Dependencies
The project uses the following Unity packages (auto-installed via `Packages/manifest.json`):
- **Universal RP** (17.3.0) – URP 2D rendering
- **2D Tilemap** & **2D Tilemap Extras** – tile-based world
- **2D Sprite** & **2D Animation** – sprite management
- **TextMeshPro** (5.0.0) – UI text
- **Input System** (1.11.2) – modern input handling
- **Cinemachine** (3.1.2) – camera system

---

## 🏗️ Project Architecture

### Folder Structure
```
Assets/
├── Scripts/
│   ├── Core/           # GameManager, EventManager, Inventory, SaveSystem
│   ├── Player/         # PlayerController, MobileJoystick, Camera, Footprints, Weight
│   ├── World/          # BiomeManager, ChunkManager, WaypointManager
│   ├── Environment/    # DestructibleTilemap, Mining, Vegetation, RootDigging
│   ├── Survival/       # SeasonManager, SurvivalStats, Weather, Consumables
│   ├── AI/             # AnimalAI (FSM), LootDropper
│   ├── Crafting/       # RecipeDatabase, CraftingSystem
│   ├── Traversal/      # ClimbingSystem
│   ├── Water/          # SwimmingSystem, RaftController
│   ├── Lighting/       # DayNightCycle, TorchLight, ShadowManager
│   └── UI/             # CompassHUD, TooltipUI
├── Prefabs/            # (Add your prefabs here)
├── Scenes/             # MainMenu, GameplayWorld
├── ScriptableObjects/  # Recipe databases, item definitions
├── Materials/          # URP materials
└── Editor/             # Custom editor scripts
```

### Module Dependencies
```
Core (GameManager, EventManager, InventorySystem)
  ↓
Player (PlayerController, MobileJoystick, CameraFollow)
  ↓
World (ChunkManager, BiomeManager) ← Environment (DestructibleTilemap, Vegetation)
  ↓
Survival (SeasonManager, SurvivalStats, WeatherController)
  ↓
AI (AnimalAI) ← Crafting (CraftingSystem) ← Traversal/Water/Lighting
```

---

## 🎮 Scene Setup

### MainMenu Scene
1. Create a new scene: **File → New Scene → Basic (URP)**
2. Save as `Assets/Scenes/MainMenu.unity`
3. Add UI Canvas with:
   - **Play** button → calls `GameManager.Instance.RestartGame()`
   - **Load** button → calls `SaveSystem.Instance.LoadGame()`
   - **Settings** button → opens settings panel
   - **Quit** button → calls `Application.Quit()`
4. Add a `GameManager` component to an empty GameObject

### GameplayWorld Scene
1. Create a new scene: **File → New Scene → Basic (URP)**
2. Save as `Assets/Scenes/GameplayWorld.unity`
3. Add the following GameObjects with components:

#### Player Setup
```
Player (Tag: "Player")
├── Rigidbody2D (Gravity Scale: 0, Freeze Rotation Z)
├── SpriteRenderer (Sorting Order: dynamic via script)
├── PlayerController
├── SurvivalStats
├── FootprintSystem
├── WeightCarrySystem
├── SwimmingSystem
├── ClimbingSystem
├── MiningSystem
├── RootDigging
├── ConsumableSystem
├── BoxCollider2D
└── MobileJoystick (on UI Canvas child)
```

#### World Management
```
WorldManager
├── ChunkManager
├── BiomeManager
├── WaypointManager
└── ShadowManager

SeasonManager
├── SeasonManager
└── WeatherController

DayNightCycle
├── DayNightCycle
└── (Reference to Global Light 2D)
```

#### UI Canvas
```
Canvas (Screen Space - Overlay)
├── CompassHUD
├── TooltipUI
├── HealthBar (Slider)
├── HungerBar (Slider)
├── ThirstBar (Slider)
├── EnergyBar (Slider)
├── StaminaBar (Slider)
├── MiningProgressBar (Image with fill)
└── MobileJoystick (on left side)
```

---

## 🎨 Asset Generation Prompts

### Player Sprites (8 Directions)
```
Prompt: "Prehistoric caveman character sprite sheet, 8 directional views 
(N, NE, E, SE, S, SW, W, NW), hyper-realistic 2D art style, detailed fur 
clothing, stone tools, muscular build, 128x128 pixels per frame, transparent 
background, isometric 2.5D perspective at 40° angle, photorealistic shading"
```

### Environment Tiles
```
Prompt: "Prehistoric terrain tileset, top-down isometric view at 40° angle,
includes: grass, dirt, sand, snow, stone, mud tiles, 64x64 pixels each,
hyper-realistic 2D art, detailed textures, seamless tiling, transparent
background, photorealistic lighting"
```

### Vegetation
```
Prompt: "Prehistoric vegetation sprites, isometric 2.5D perspective, includes:
timber trees (pine, oak), fruit trees (apple, fig), berry bushes, vines,
detailed leaves and bark textures, 256x256 pixels, transparent background,
photorealistic 2D art style"
```

### Animals
```
Prompt: "Prehistoric animal sprite sheet, Woolly Mammoth, hyper-realistic 2D
art, 8 directional views, detailed fur texture, massive tusks, 256x256 pixels
per frame, transparent background, isometric perspective, photorealistic
shading and lighting"
```

### UI Elements
```
Prompt: "Stone Age UI theme, primitive stone and wood textures, hand-drawn
icons for health/hunger/thirst/energy, cave painting style, earth tones
(brown, tan, ochre), 128x128 pixels per icon, transparent background"
```

---

## 🔧 Configuration

### URP 2D Setup
1. **Edit → Project Settings → Graphics**
2. Assign URP 2D Renderer asset
3. **Edit → Project Settings → Quality**
   - Set 2D Renderer for all quality levels
4. Add **Global Light 2D** to scene (GameObject → Light → Global Light 2D)

### Mobile Controls
1. Add **EventSystem** to scene (auto-created with Canvas)
2. Create UI Canvas with **MobileJoystick** on left half
3. Right half handles touch rotation (no UI needed)
4. Test with Unity Remote app on mobile device

### Chunk Streaming
- Adjust `ChunkManager.loadRadius` for performance (1 = 3×3 grid, 2 = 5×5 grid)
- Increase `updateInterval` for less CPU usage
- Reduce chunk size in `ChunkData.CHUNK_SIZE` for faster loading

### Season & Weather
- Modify `SeasonManager.dayDuration` to control day length
- Adjust `daysPerSeason` to change season length
- Tune weather probabilities in `WeatherController.badWeatherChance`

---

## 📱 Mobile Build Settings

### Android
1. **File → Build Settings → Android**
2. **Player Settings:**
   - Minimum API Level: Android 8.0 (API 26)
   - Target API Level: Automatic (highest)
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64
3. **Other Settings:**
   - Color Space: Linear
   - Graphics APIs: Vulkan, OpenGLES3

### iOS (Mac only)
1. **File → Build Settings → iOS**
2. **Player Settings:**
   - Target minimum iOS Version: 14.0
   - Scripting Backend: IL2CPP
   - Target SDK: Device SDK
3. Build and open in Xcode for signing and deployment

---

## 🐛 Troubleshooting

### Issue: Chunks not loading
- Ensure player has "Player" tag
- Check `ChunkManager.player` reference is set
- Verify `BiomeManager` bounds are configured

### Issue: `Light2D could not be found` (CS0246 in DayNightCycle.cs / TorchLight.cs)
- Make sure the project uses the Unity 6.3 URP package version: `Packages/manifest.json`
  should contain `"com.unity.render-pipelines.universal": "17.3.0"`.
- Both `Assets/Scripts/PrehistoricSurvival.asmdef` and
  `Assets/Editor/PrehistoricSurvival.Editor.asmdef` must reference
  `Unity.RenderPipelines.Universal.2D.Runtime`. In URP 17.3 (Unity 6.3), the 2D
  lighting types (`Light2D`, `ShadowCaster2D`, `Renderer2DData`) live in that assembly.
- After changing package versions, close Unity and delete the generated `Library`,
  `Temp` and `Obj` folders (and `Packages/packages-lock.json`), then reopen the project
  so Unity re-resolves all packages cleanly.

### Issue: `DirectoryNotFoundException ... Library\PackageCache\com.unity.collections@...\dll`
- This is stale local package cache data from a previous Unity session or another machine.
- Close Unity, delete the `Library`, `Temp` and `Obj` folders, then reopen the project.
- Unity will rebuild `Library` from the checked-in `Assets` and `Packages` folders.
- These "Host type is not matching" and Input Manager deprecation messages are harmless;
  the project intentionally keeps the old Input Manager enabled alongside the Input System.

### Issue: Lighting not working
- Confirm URP 2D Renderer is assigned in Graphics settings
- Add **Global Light 2D** to scene
- Check `DayNightCycle.globalLight` reference

### Issue: Mobile controls unresponsive
- Add **EventSystem** to scene
- Ensure Canvas has **CanvasScaler** set to "Scale With Screen Size"
- Test with Unity Remote app

### Issue: Performance lag
- Reduce `ChunkManager.loadRadius`
- Lower `ChunkData.CHUNK_SIZE` (e.g., 16×16)
- Disable unused particle systems
- Use object pooling for footprints and loot

---

## 🚀 Next Steps

1. **Create Prefabs:** Build player, animal, vegetation, and item prefabs
2. **Design Tilemaps:** Create ground and water tile palettes
3. **ScriptableObjects:** Define items and recipes
4. **UI Polish:** Design menus, inventory grid, crafting interface
5. **Audio:** Add ambient sounds, footsteps, animal calls
6. **Testing:** Playtest on target devices and optimize

---

## 📚 Additional Resources

- [Unity URP 2D Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/manual/renderer-features.html)
- [Unity 2D Tilemap](https://docs.unity3d.com/Manual/GridPackage.html)
- [Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/)
- [Cinemachine for 2D](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/)

---

**Project Version:** 1.0.0  
**Last Updated:** 2026-08-25  
**Unity Version:** 6000.3.22f1 LTS
