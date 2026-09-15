# Character animation — what was fixed and how to extend it

This document covers the work done in this branch. It is written
so that the next person to touch the character (you, in two weeks)
can extend it without re-deriving everything from the code.

## TL;DR

| What was wrong | What we did |
| --- | --- |
| Sway masks were on the wrong pixels (R was on the face, B was on the arms, A was just the scalp). The chest "waved like fabric" because the breathing mask was on the cross-strap. | Re-authored all 16 sway masks with the correct anatomy. The new tool is `tools/regenerate_sway_masks.py`. |
| Head glance used a soft cross-fade between two 22.5°-apart faces, producing a blurry half-turn face whose eyes looked off-axis. | The head is now hard-switched between the two neighbouring direction sprites (with a narrow feathered band at the neck seam). The script biases glance targets past 0.5 so the gaze fully commits to one side. |
| Idle body-bob was 2 px per breath, visible as a constant up-down sway. | Reduced to ~0.6 px and changed the breathing curve to be asymmetric (quick inhale, slow exhale — what a real lung does). |
| Day / night / rain / dark scenes had no way to affect the character. | Added `_Daylight`, `_AmbientTint`, `_Wetness`, `_WetShine`, `_Darkness` shader properties + a `TimeOfDayWeather` singleton that drives them. |
| No data model for future character customization (skin, hair, age, gender). | Added `CharacterDefinition` ScriptableObject as a forward-looking container for the customization fields. |

## File map

```
Assets/
  Shaders/
    BillboardBlendWind.shader         built-in RP, all fixes here
    BillboardBlendWindURP.shader      URP version, mirrors built-in
  Scripts/
    BillboardCharacter.cs              the runtime character controller
    TimeOfDayWeather.cs               NEW: global day/night/weather state
    CharacterDefinition.cs            NEW: data model for the
                                      customization system
  Resources/CharacterMasks/           re-authored masks
  Sprites/Player/                     unchanged (the caveman turntable)
tools/
  regenerate_sway_masks.py            NEW: re-author the 16 masks
                                      from the sprites automatically
```

## How the bug fixes work

### 1. The sway masks were on the wrong pixels

The shipped masks had:
- R (hair) on the face, including the eyes and chin — this made
  the hair-sway warp the face.
- G (cloth) on the forearm wraps — this made loincloth flutter
  affect the wrists.
- B (chest) covering the whole torso including the arms — this
  made the breathing expand the arms, and the chest expansion
  pushed the cross-strap sideways so it looked like the chest was
  rippling.
- A (head zone) only on the top of the scalp — the head-glance
  cross-fade only changed the silhouette, not the face. This is
  the "awkward gaze" you reported.

`tools/regenerate_sway_masks.py` produces new masks where:
- R is a Y-band on the upper body, extended to 50% body height on
  the back / side views (where the long flowing hair lives) and
  26% on the front view (hair on top of the head only).
- G is the loincloth Y-band (46-65% of body height) intersected
  with the central 60% of body width so the arm wraps (which sit
  at the edges) are excluded.
- B is the chest Y-band (22-48% of body height) intersected with
  the central 70% of body width so the arms are excluded. Back
  views get an empty B mask (no chest from behind).
- A is the head + neck (0-30% of body height, feathered to nothing
  at the collar).

Run the tool after updating the sprites:

```bash
python3 tools/regenerate_sway_masks.py
```

### 2. Head glance: hard switch instead of soft fade

The previous shader did:

```hlsl
float4 headCol = lerp(bodyCol, cB, saturate(g));
headCol = lerp(headCol, tex2D(_TexHead), saturate(-g));
```

That soft lerp between two 22.5°-rotated faces produced a
half-rotation face with eyes pointing ~11° off-axis. The new
shader does:

```hlsl
float gHard = smoothstep(-0.18, 0.18, _HeadGlance);
float4 headCol = lerp(cA, cB, gHard);
headCol = lerp(headCol, tex2D(_TexHead), saturate(-_HeadGlance));
```

So when `_HeadGlance` is in the middle of its range, the head is
fully one view or the other (with a narrow 0.36-wide feathered
band for the seam at the neck). The script biases glance targets
to be at least 0.62 of the way to the side, so the hard switch
is fully crossed.

### 3. Breathing: less amplitude + asymmetric curve

The previous chest expansion was 1.6% of the body width and used
a symmetric `sin()` curve, so the chest swelled in and out
evenly and the cross-strap (which doesn't move) appeared to wave
relative to the body. The new curve:

```hlsl
float inhale = pow(max(br, 0.0), 0.65);
```

Quick rise, slower fall — a real lung. Amplitude dropped from
0.016 to 0.012 (~25% less), and the shoulder / head "rise" was
removed entirely so the head doesn't visibly bob with each breath.

### 4. Time of day / weather

`TimeOfDayWeather` is a singleton that exposes:

- `daylight` (0..2): brightness scalar
- `ambientTint`: multiplicative RGB tint
- `wetness` (0..1): darkens cloth + hair, adds a wet shine
- `darkness` (0..1): crushes detail (used for cave / night)
- `weather` enum: Clear / Overcast / Rain / Storm / Fog
- `timeOfDay` (0..1): 0 = midnight, 0.5 = noon

`BillboardCharacter.followGlobalEnvironment = true` (default)
picks up the singleton in `LateUpdate` and pushes everything into
the material. The same singleton can drive your terrain, sky, and
audio.

To use it in a scene, drop one `TimeOfDayWeather` component onto
any GameObject in the scene. The component auto-creates itself if
none is present (`TimeOfDayWeather.EnsureExists()`).

## Forward-looking data model

`CharacterDefinition` is a ScriptableObject that knows:
- `directionSprites[]` and `directionMasks[]` (already used by
  `BillboardCharacter`).
- `skinTint`, `hairTint`, `eyeTint`, `clothTint` for the
  customization UI.
- `bodyScale`, `headSize` for the age system (children have
  bigger heads; old characters are slightly hunched).
- `hairLength`, `beardLength`, `hairGrowthPerDay`,
  `beardGrowthPerDay` for the haircut + beard system.
- `gender`, `ageStage` for the male / female / age variants.
- `allowHaircut`, `canHaveBeard` for per-character restrictions.

Nothing in the existing scene reads these fields yet — they are
the contract for the future UI / age / haircut code. To wire them
up, add fields to `BillboardCharacter` that pull from a
`CharacterDefinition` asset and apply them in the same way the
sway-mask texture is applied today.

## Testing checklist (when the new masks + shader are imported)

1. In the editor, select the caveman and confirm the material
   uses `Game/BillboardBlendWind` (built-in) or
   `Game/BillboardBlendWindURP` (URP).
2. Press Play. The chest should NOT visibly move. The hair on the
   top of the head should drift slowly. The eyes should look
   straight ahead most of the time, then snap to a clean 22°
   angle and back.
3. Add a `TimeOfDayWeather` component to a GameObject in the
   scene. Set `timeOfDay = 0.0` (midnight): the character should
   appear in cold blue and ~20% brightness. Set
   `weather = WeatherKind.Rain`: the cloth + hair should look
   dampened with a subtle shimmer.
4. Re-run `python3 tools/regenerate_sway_masks.py` after any
   change to the sprite art. Unity auto-detects the new mask
   PNGs on next refresh.
