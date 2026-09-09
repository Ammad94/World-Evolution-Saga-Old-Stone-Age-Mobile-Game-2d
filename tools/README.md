# tools/

One-off Python tools for the Unity project. These do NOT need to be
shipped with the game — they are development aids. Run them from
the repository root.

## regenerate_sway_masks.py

Re-authors the 16 sway masks under
`Assets/Resources/CharacterMasks/` from the direction sprites
under `Assets/Sprites/Player/`.

The masks drive the `Game/BillboardBlendWind` shader's per-pixel
animation (hair sway, chest breathing, loincloth flutter, head
glance). They must be regenerated whenever the sprite art
changes — the old masks would otherwise animate the wrong pixels.

```bash
python3 tools/regenerate_sway_masks.py
```

Output channels:

| Channel | What it covers |
| --- | --- |
| R | hair (top of head + long flowing hair on back / side views) |
| G | loincloth / hem (central 60% of body width, 46-65% of body height) |
| B | chest (central 70% of body width, 22-48% of body height; empty on back views) |
| A | head + neck (0-30% of body height) |

The script is idempotent — re-running overwrites the mask PNGs.

Requires: `Pillow`, `numpy`.
