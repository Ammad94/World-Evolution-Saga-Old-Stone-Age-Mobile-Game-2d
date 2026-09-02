# What's in here — quick guide

**These are the CURRENT files to copy into Unity:**

| Folder | What it is |
|---|---|
| `sprites_16/` | The 16-direction character sprites (176×392) — use these on the player |
| `sprites_16_masks/` | Sway masks (R=hair, G=cloth, B=torso, A=head) → copy to `Assets/Resources/CharacterMasks/` |

**Optional alternates (also current):**

| Folder | What it is |
|---|---|
| `sprites/` | 8-direction legacy sprite set (only if you prefer 8 directions) |
| `sprites_16_idle/` | 48-frame idle sheet set (not needed — the shader does the idle procedurally) |
| `sprites_masks/` | Masks for the 8-direction set |

**Scripts & shaders:** `BillboardCharacter.cs`, `ThirdPersonCamera.cs`,
`BillboardBlendWind.shader` (built-in RP) / `BillboardBlendWindURP.shader` (URP).
Setup: see `../Unity_Smooth_Billboard_Setup.md`.

Other image locations in the repo:
- `raw_sheets/` — the original green-screen source sheets (keep! the rebuild
  tool `tools/rebuild_sprites_from_sheet.py` needs them)
- `preview/` — current previews only (`preview_smooth_billboard.gif`,
  `sprites_current_all16.png`)
- `tools/originals_backup.zip`, `tools/idle_pre_belt_backup.zip` — pre-belt
  sprite backups (safety net, not for import)
