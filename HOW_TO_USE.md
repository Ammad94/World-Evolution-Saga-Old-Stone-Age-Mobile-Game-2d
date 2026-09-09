# Beginner's guide — using the changes I just made

This is written for someone who has never touched Unity before,
or who only knows the absolute basics. I will spell out every
single click.

If you only know how to open Unity, double-click a script, and
press the Play button — you can do everything in this guide.

---

## Before anything: re-open the project in Unity

1. Open the **Unity Hub** (the launcher you got when you installed Unity).
2. On the **Projects** tab, click **Open** (or **Add** if the project isn't listed yet).
3. Browse to `World-Evolution-Saga-Old-Stone-Age-Mobile-Game-2d` and open it.
   The first time, Unity will spend 2–5 minutes importing assets. Wait
   until the spinner in the bottom-right corner stops.
4. In the **Project** window (bottom panel), navigate to
   `Assets → Scenes` and **double-click `SampleScene`** to open it.
5. Press the **Play** button (▶ at the top centre of the editor).
   The character should appear and animate. Use **WASD** or **arrow keys**
   to move, and drag with the right mouse button to orbit the camera.

If the screen is just a solid colour, see "Nothing shows up" at the
end of this guide.

---

## 1. The fixes I made — what you should see now

Before the fix, in your screenshot, the chest and cross-strap
"waved like cloth" and the eyes looked off when the head turned.

After the fix:

- The chest still breathes, but the motion is now **just on the
  chest** — the cross-strap, shoulders, and arms do not move.
- The head glance now looks **decisive** — when the head turns
  22° to the side, the eyes clearly look that way (not blurry or
  off-axis). This happens roughly every 1.5–4 seconds.
- The whole body bobs **much less** than before. If the character
  looks like a statue now, that is correct — real people barely
  bob.

If you don't see these changes, the most common cause is that
Unity hasn't re-imported the new shader yet. Do:

> **Assets menu → Reimport All** (then wait 1–2 minutes).

---

## 2. How to make it night / rain / dark in your scene

This is the day-night + weather system I added. You can drive it
from any script, or just drag the values in the Inspector.

### Step-by-step: add a TimeOfDayWeather to your scene

1. In the **Hierarchy** window (left panel), right-click an empty
   area.
2. Choose **Create Empty**. A new GameObject called "GameObject"
   appears.
3. With the new GameObject selected, look at the **Inspector**
   window (right panel). At the bottom, click **Add Component**.
4. Type `Time Of Day Weather` in the search box.
5. Click the result that says **Time Of Day Weather**
   (made by `World Evolution`). It gets added to the GameObject.
6. **Rename** the GameObject to `TimeOfDayWeather` by clicking its
   name at the top of the Inspector.

That's it. The character will now react to the time of day.

### How to use it

Select the `TimeOfDayWeather` GameObject and look at the
**Inspector**. You'll see these fields:

| Field | What it does |
| --- | --- |
| `Time Of Day` (0..1) | 0 = midnight, 0.25 = 6 AM, 0.5 = noon, 0.75 = 6 PM, 1 = midnight again. |
| `Day Speed` | How fast time advances. 0 = frozen (you set it manually), 0.005 ≈ 1 hour every 5 real seconds. |
| `Weather` | A dropdown: Clear, Overcast, Rain, Storm, Fog. Pick one. |
| `Wetness` | 0..1, how wet the character's cloth + hair look. Auto-set by the weather, but you can override. |
| `Wet Shine` | 0..0.5, how strong the wet highlight is. |
| `Daylight` | Read-only — auto-computed. |
| `Ambient Tint` | Read-only — auto-computed, this is the colour tint applied to the character. |
| `Darkness` | Read-only — auto-computed. |

**To try it:**

1. With `TimeOfDayWeather` selected, drag the `Time Of Day`
   slider to **0.0**. The character should immediately look blue
   and dim (night).
2. Drag it to **0.5**. The character should look bright and
   warm (noon).
3. Change the `Weather` dropdown to **Rain**. The character's
   cloth and hair should darken slightly, with a subtle shine.

### How to drive it from a script

```csharp
using WorldEvolution;

public class MyGameLogic : MonoBehaviour
{
    void Start()
    {
        // Auto-creates one if none exists
        var env = TimeOfDayWeather.EnsureExists();
        env.SetTimeOfDay(0.30f);   // 7:20 AM
        env.SetWeather(TimeOfDayWeather.WeatherKind.Rain);
    }
}
```

---

## 3. How to add a second character (e.g. a friend, an NPC, an enemy)

This is the same workflow as adding the original character, but
using a fresh GameObject so the two characters don't share data.

1. **Right-click in the Hierarchy → 2D Object → Sprites → Square**
   (or any sprite). A new GameObject appears.
2. Rename it to `MySecondCharacter`.
3. In the Inspector, find the **Sprite Renderer** component.
   Click the small circle next to **Sprite** and pick one of the
   existing sprites from `Assets/Sprites/Player/00_front` to start
   with (you'll switch the array of sprites in step 5).
4. Click **Add Component → Rendering → Sprite Renderer** if it's
   not there.
5. Click **Add Component → Scripts → Billboard Character**.
6. The component appears. Drag the 16 sprites into `Direction Sprites`:
   - Click the small ⊕ icon next to "Direction Sprites" 16 times
     (or change the size to 16 in the field's number).
   - Drag each `00_front.png`, `01_front_right_slight.png`, ...
     `15_front_left_slight.png` from `Assets/Sprites/Player/` into
     a slot. **Order matters** — the order must match the
     "turntable" order, starting from the front.
7. The masks auto-load from `Assets/Resources/CharacterMasks/` —
   nothing to wire up.
8. **Material**: click on the GameObject's Sprite Renderer, find
   **Material**, and assign `Assets/CavemanBillboard.mat`. (You
   can also right-click the existing Player's Sprite Renderer →
   **Copy Component** → right-click the new one → **Paste
   Component As New**, which is faster.)
9. Press **Play**. You now have two characters.

If the second character is invisible: most likely the masks didn't
auto-load. Check the Console window (bottom panel) for an error
like "sway masks not found". The fix is to verify
`Assets/Resources/CharacterMasks/00_front_mask.png` etc. exist and
the names match the sprite names.

---

## 4. How to add a `CharacterDefinition` (the data model for the future)

This is the asset you'll plug into the future customisation UI.
**Nothing in your scene reads it yet** — but creating it now
means the data shape is locked in for the future code.

1. **Right-click in the Project window** (anywhere under `Assets/`).
2. Choose **Create → World Evolution → Character Definition**.
3. A new asset `Character.asset` appears. Rename it to
   `Character_Caveman_Default.asset`.
4. Click it. The Inspector shows a list of fields:
   - **Character Id**: a unique string, e.g. `caveman_default`.
   - **Gender**: Male / Female.
   - **Age Stage**: Toddler / Child / Teen / Young Adult / Adult / Old.
   - **Direction Sprites**: drag the 16 sprites here.
   - **Direction Masks**: leave empty (auto-loaded).
   - **Body Scale**: 0.45 for a child, 1.0 for an adult, 0.95 for old.
   - **Head Size**: 1.15 for a child, 1.0 for an adult, 1.05 for old.
   - **Skin Tint, Hair Tint, Eye Tint, Cloth Tint**: colour pickers.
   - **Hair Length, Beard Length, Hair Growth Per Day, Beard Growth Per Day**: numbers.
   - **Allow Haircut, Can Have Beard**: tick boxes.
5. **Save** with **Ctrl+S** (or it auto-saves on focus loss).

When the future customisation UI is built, it will read this
asset and apply the tints. The data model is in place; the UI is
the next step.

---

## 5. How to tweak the animation values (no scripting needed)

All the "how strongly does the chest breathe / head glance / hair
sway" values are exposed in Unity's Inspector. Select the
character's GameObject in the Hierarchy, and you'll see the
**Billboard Character** component in the Inspector with these
sliders:

| Group | Slider | What it does |
| --- | --- | --- |
| Movement | `Move Speed` | How fast WASD moves him. |
| | `Click To Move` | Tick to enable click-to-move with the mouse. |
| Direction blending | `Turn Smooth Time` | Higher = lazier turns. 0.08 is a good default. |
| | `Blend Sharpness` | 1 = almost-instant swap between direction sprites (no ghosting). 0 = soft fade. |
| Wind | `Wind Direction` | The world XZ direction the breeze blows. |
| | `Wind Strength` | 0 = no wind, 1 = full. |
| | `Hair Sway Pixels` | 3 = subtle, 6 = strong. |
| | `Cloth Flutter Pixels` | 0 = no flutter. Raise to ~3.5 to make the loincloth flap. |
| Breathing | `Breaths Per Second` | 0.22 ≈ 13 breaths/min (a calm adult). |
| | `Breathing Amount` | 0.85 default. Try 0.4 for a very subtle breath. |
| | `Idle Body Bob` | 0.45 default. 0 = statue-still. |
| Head + eyes | `Head Look Amount` | 0 = head never glances, 1 = default. |
| | `Head Look Max Blend` | How far the head turns. 0.85 = a natural glance. |
| | `Blink` | Tick to enable blinking. |
| | `Blink Min/Max Delay` | Random range between blinks. |
| Walking | `Walk Bob Amount` | How much the body bobs while walking. |
| | `Stride Rate` | Stride cycles per second at full speed. |
| Environment | `Daylight`, `Ambient Tint`, `Wetness`, `Wet Shine`, `Darkness` | These are auto-driven by `TimeOfDayWeather` (if the **Follow Global Environment** box is ticked). You can untick that box to set them by hand. |

**Try this:** set `Breathing Amount` to **2.5** and `Idle Body Bob`
to **0** to see the chest breathe strongly without any other
motion. Then set `Breathing Amount` back to **0.85**.

---

## 6. The Tools folder: regenerating the masks

If you (or your artist) ever update the 16 sprite PNGs in
`Assets/Sprites/Player/`, you must regenerate the masks too, or
the chest will start animating the wrong pixels again.

### One-time setup: install Python + Pillow

1. Install Python 3 from https://python.org (any 3.x version).
2. Open a terminal / command prompt and run:
   ```
   pip install Pillow numpy
   ```
   (On macOS / Linux you may need `pip3` instead of `pip`.)

### Regenerate

1. Open a terminal in the project root (the folder that contains
   `Assets/`, `ProjectSettings/`, etc.).
2. Run:
   ```
   python tools/regenerate_sway_masks.py
   ```
3. It writes 16 new mask PNGs into
   `Assets/Resources/CharacterMasks/`.
4. Back in Unity, the Project window shows the files updated
   (a small "⟳" icon may flash). They take effect on next Play.

If you ever need to **revert** the masks to the originals in
the git history, run:
```
git checkout HEAD -- Assets/Resources/CharacterMasks/
```

---

## 7. Common mistakes / FAQ

### "Nothing shows up when I press Play"

- Check the **Console** window (Window → General → Console). Red
  errors tell you exactly what's wrong.
- The most common reason is a missing material: the Sprite
  Renderer must use a material built from the
  `Game/BillboardBlendWind` (built-in) or
  `Game/BillboardBlendWindURP` (URP) shader. The shipped
  `CavemanBillboard.mat` already does this — make sure the
  character is using it.
- If the character is white, the sprite is loading but the shader
  is failing. Reimport the shader: right-click
  `Assets/Shaders/BillboardBlendWind.shader` → **Reimport**.

### "The chest is still moving too much"

Open the character's `BillboardCharacter` component and lower
`Breathing Amount` to **0.5**, and `Idle Body Bob` to **0.2**.
The defaults are already subtle, but you can make them even more
so.

### "I get an error 'shader Game/BillboardBlendWind not found'"

You are using URP and need the URP version of the shader. Either:
- Switch the material to use `Game/BillboardBlendWindURP`, OR
- Both shaders are shipped; pick the one matching your render
  pipeline.

### "The masks show the old (wrong) anatomy"

You have a `Direction Masks` field populated in the Billboard
Character component. The script auto-loads from
`Resources/CharacterMasks/`, but if you dragged old mask PNGs in
manually, those take priority. Clear the **Direction Masks**
field (set size to 0) so the auto-load works.

### "I added `TimeOfDayWeather` and the character is now pitch black"

The `TimeOfDayWeather` defaults to 7:12 AM with clear weather,
which should be daylight ≈ 1.0. If you see black, you may have
dragged the `Time Of Day` slider to 0 by accident. Or the script
is failing — check the Console for errors.

### "How do I add a new sprite without breaking the animation?"

1. Name it `NN_description.png` where `NN` is a 2-digit number
   matching the turntable position (e.g. `00_front` is the front,
   `04_right` is the right side, `08_back` is the back).
2. The size must be **176 × 392 pixels** to match the existing
   sprites. The shader uses these numbers in the
   `_TexSize` property; mismatched sizes cause a streaky look.
3. **Do not** pack the sprite into a Sprite Atlas — the blend
   shader needs the original texture. The shader already has
   `"CanUseSpriteAtlas" = "False"` in its tags, but the Unity
   import settings can still try to atlas it. If the chest
   starts showing through the strap, that's the cause.
4. Drag the new sprite into the matching `Direction Sprites`
   slot in the `BillboardCharacter` component.
5. Run `python tools/regenerate_sway_masks.py`.

---

## 8. What's next (for the future you, or whoever takes this over)

The customisation system is half-done. The data model
(`CharacterDefinition`) is in place but nothing reads it yet.
The natural next steps are:

1. **Wire `CharacterDefinition` into `BillboardCharacter`**:
   add a public `CharacterDefinition` field on the
   `BillboardCharacter` component, and in `LateUpdate`, push the
   tints into the shader as new properties
   (`_SkinTint`, `_HairTint`, `_EyeTint`, `_ClothTint`).
2. **Build a character customisation UI**: a `uGUI` canvas with
   sliders for the tints, dropdowns for gender / age, and a
   "save" button that writes the values back to the
   `CharacterDefinition` asset.
3. **Build the haircut / beard UI**: a button that decreases
   `hairLength` on the asset, and a daily tick that increases
   it by `hairGrowthPerDay`.
4. **Build the age system**: an `Age` MonoBehaviour that watches
   the in-game day count, transitions the character to the next
   `CharacterDefinition` (e.g. teen → young adult) when the
   threshold is reached, and disables the previous one.

The data model is the part that's hard to change later. Now
that it's in place, the UI work is a normal Unity project.
