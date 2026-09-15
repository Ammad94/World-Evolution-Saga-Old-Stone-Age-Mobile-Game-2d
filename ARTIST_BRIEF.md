# Caveman Character Sprite Sheet — Artist Brief

**Purpose:** Complete reference document for generating/commissioning 2D sprite art for an Old Stone Age mobile game character. The existing 16-direction turntable (idle) sprites are at `Assets/Sprites/Player/00..15_*.png` and must be matched in style and quality.

---

## 1. CHARACTER REFERENCE (use for every prompt — copy-paste)

```
A young adult male caveman character for a 2D mobile game, rendered
as a high-detail realistic painted digital illustration (NOT cartoon,
NOT anime, NOT pixel art). The character is the protagonist of an Old
Stone Age survival game.

PHYSICAL APPEARANCE:
- Athletic muscular build, 6.5 heads tall, broad shoulders, V-shaped torso
- Skin: warm tan (#a47e5e to #8a6240) with subtle undertone variation,
  visible muscle definition, slight weathering/sun damage, subtle skin
  texture (pores, fine hair on arms)
- Hair: long, dark brown (#3a2818 to #1f1410), shoulder length, individual
  strands visible, slightly messy and windswept, not groomed
- Beard: same dark brown as hair, medium length, slightly unkempt but full
- Eyes: dark brown, intense gaze, slight weathering around them
- Facial structure: strong jaw, prominent brow ridge, defined cheekbones,
  slight stubble at cheek edges
- Expression baseline: serious, stoic, focused — not smiling, not angry

CLOTHING AND ACCESSORIES (mandatory, no substitutions):
- Bare chest with crossed leather straps forming an X-pattern from each
  shoulder to the opposite hip, aged brown leather (#5a3a22), worn
  texture, visible crude stitching, slightly frayed edges
- Fur loincloth / hide skirt, dark brown with lighter brown highlights,
  rough hairy animal-hide texture, ragged uneven hem, tied at waist with
  a leather cord
- Leather wrist wraps on both forearms (3-4 wraps each), dark brown
  (#4a2a18), weathered, overlapping
- Off-white cloth wrappings around upper arms/biceps, dirty cream color
  (#d4c4a0), wrapped tightly like mummy wraps, frayed edges, stained
- Animal tooth necklace — a string of small animal fangs/tusks, bone
  colored (#e8d8b8), visible at the chest
- Bare legs and bare feet, muscular calves, no footwear
- No modern items, no metal, no cloth shirts — only hide/leather/bone

ART STYLE:
- Realistic painted digital art, similar to high-quality game concept art
- Soft volumetric lighting from upper-front-left direction
- Subtle rim light / bounce light on right side and back
- Smooth anti-aliased edges (no pixel art, no harsh outlines)
- Color palette: warm earth tones — browns, tans, ochres, cream,
  subtle highlights in golden/warm white
- Slight painterly texture (subtle brush stroke feel) but not heavy
- Background MUST be fully transparent (PNG with alpha channel)
- Character must be CENTERED in frame with a small margin (about 5% on
  each side); no other elements in the image

TECHNICAL SPECIFICATIONS:
- Image size: 176 pixels wide × 392 pixels tall (vertical, portrait)
- Color depth: 32-bit RGBA
- Format: PNG with transparency
- Resolution: 72 DPI is fine (this is for screen, not print)
- Anti-aliased edges
- No drop shadows, no glow, no outline strokes around the character
```

---

## 2. CAMERA-ANGLE NAMING (turntable, 16 directions, clockwise from front)

The game uses a 16-direction turntable indexed 00-15. When the character
walks in any of 8 directions (W, S, A, D, AW, WD, AS, SD) or faces a
camera-relative direction, the appropriate sprite is shown. Use this
naming convention for ALL prompts:

| Index | Filename | Camera Position |
|---|---|---|
| 00 | `00_front.png` | Camera directly in front of character (face fully visible) |
| 01 | `01_front_right_slight.png` | Camera 22.5° clockwise from front (slight 3/4 right-front) |
| 02 | `02_front_right.png` | Camera 45° clockwise from front (clear 3/4 right-front) |
| 03 | `03_right_front.png` | Camera 67.5° clockwise (almost pure right profile, slight front) |
| 04 | `04_right.png` | Camera 90° clockwise — pure right profile (right side of body visible) |
| 05 | `05_right_back.png` | Camera 112.5° clockwise (3/4 right-back, mostly back but right side visible) |
| 06 | `06_back_right.png` | Camera 135° clockwise (back with right side visible) |
| 07 | `07_back_right_slight.png` | Camera 157.5° clockwise (almost back, slight right side) |
| 08 | `08_back.png` | Camera directly behind (back of head and body fully visible, no face) |
| 09 | `09_back_left_slight.png` | Camera 202.5° (mirror of 07, slight left side visible from back) |
| 10 | `10_back_left.png` | Camera 225° (mirror of 06, back with left side visible) |
| 11 | `11_left_back.png` | Camera 247.5° (mirror of 05) |
| 12 | `12_left.png` | Camera 270° — pure left profile (left side of body visible) |
| 13 | `13_left_front.png` | Camera 292.5° (mirror of 03) |
| 14 | `14_front_left.png` | Camera 315° (mirror of 02, clear 3/4 front-left) |
| 15 | `15_front_left_slight.png` | Camera 337.5° (mirror of 01) |

**Rule:** Odd-numbered sprites are 3/4 views (mix of front/back + side).
Even-numbered sprites are cardinal views (pure front, side, back).

---

## 3. ANIMATION CATEGORIES

For each animation, ALL 16 directions need their own sprite(s).

### 3.1 IDLE (1 sprite per direction = 16 total)

Standing still, weight slightly on one leg, relaxed arms, neutral
expression, subtle breathing implied. Existing `00..15_*.png` files are
this category — match them exactly.

### 3.2 WALK CYCLE (4 frames per direction = 64 total)

Each walk cycle has 4 frames that loop:
- **Frame 0 (contact):** Left leg forward and planted, right leg back.
  Left arm forward (counter-swing), right arm back. Body weight on left.
- **Frame 1 (down):** Both legs passing each other mid-stride, left leg
  starting to lift off, right leg starting to swing forward. Arms at
  mid-swing positions. Body at lowest point of step (slight squat).
- **Frame 2 (passing):** Right leg forward and planted, left leg back.
  Right arm forward, left arm back. Body weight on right. (Mirror of frame 0)
- **Frame 3 (up):** Both legs passing each other mid-stride, right leg
  starting to lift, left leg starting to swing forward. Arms at mid-swing.
  Body at highest point of step (slight rise).

Filenames: `{direction}_walk_0.png`, `{direction}_walk_1.png`,
`{direction}_walk_2.png`, `{direction}_walk_3.png`
Example: `00_front_walk_0.png`, `00_front_walk_1.png`, etc.

### 3.3 RUN/SPRINT (4 frames per direction = 64 total)

Similar to walk but exaggerated — bigger leg/arm swing, body leaning
forward, more dynamic pose. Faster feel.

### 3.4 JUMP (4 frames per direction = 64 total)

- Frame 0 (crouch): Crouched low, knees bent, arms back, ready to spring.
- Frame 1 (takeoff): Just leaving ground, legs extending, arms swinging up.
- Frame 2 (apex): Body tucked or extended in air, legs slightly bent.
- Frame 3 (land): About to touch down, knees bending to absorb impact.

### 3.5 CROUCH (1 frame per direction = 16 total)

Knees deeply bent, body lowered, arms slightly forward for balance.
Character in a ready/sneaking pose.

### 3.6 SLEEP (1 frame per direction = 16 total)

Character lying down on the ground (on side), eyes closed, peaceful
expression, body relaxed. Fur loincloth bunched slightly.

### 3.7 LAY DOWN (1 frame per direction = 16 total)

Character lying flat on back (or stomach), arms at sides, looking up at
sky. Different from sleep — eyes open, alert but resting.

### 3.8 SIT ON GROUND (1 frame per direction = 16 total)

Character sitting cross-legged or with legs out, relaxed pose, hands on
knees or in lap. Looking forward.

### 3.9 SIT ON ROCK (1 frame per direction = 16 total)

Character perched on a rock, legs hanging or one leg up, casual pose.

### 3.10 CLIMB UP (4 frames per direction = 64 total)

Character climbing upward — reaching up with one arm, pulling body up,
legs pushing off. Dynamic, action pose.

### 3.11 CLIMB DOWN (4 frames per direction = 64 total)

Character climbing down — reaching down, lowering body, feet finding
footholds.

### 3.12 ATTACK WITH AXE (3 frames per direction = 48 total)

- Frame 0 (wind-up): Character holding stone axe with both hands, axe
  raised over shoulder or behind head, body twisted slightly.
- Frame 1 (swing): Mid-swing, axe coming forward/down, body rotating
  with motion, dynamic pose.
- Frame 2 (follow-through): Axe has hit, body continuing rotation, slight
  momentum pose.

### 3.13 ATTACK WITH KNIFE (3 frames per direction = 48 total)

Same as axe but with smaller stone knife in one hand (right hand
typically). Quicker, more subtle motion.

### 3.14 ATTACK WITH SPEAR (3 frames per direction = 48 total)

Character thrusting long wooden spear forward with both hands. Lunge
motion.

### 3.15 THROW AXE (3 frames per direction = 48 total)

- Frame 0 (raise): Axe in hand raised back, ready to throw.
- Frame 1 (throw): Arm forward, axe leaving hand.
- Frame 2 (follow-through): Empty hand extended forward, body twisted.

### 3.16 THROW KNIFE (3 frames per direction = 48 total)

Smaller version of throw axe, one-handed.

### 3.17 THROW SPEAR (3 frames per direction = 48 total)

Throwing a spear like a javelin — full body engaged.

### 3.18 EAT (2 frames per direction = 32 total)

- Frame 0: Bringing food (meat, fruit, etc.) to mouth with one hand.
- Frame 1: Chewing, hand lowered.

### 3.19 DRINK (2 frames per direction = 32 total)

- Frame 0: Lifting water skin or gourd to mouth.
- Frame 1: Drinking, head tilted back slightly.

### 3.20 TRIM HAIR (3 frames per direction = 48 total)

Character holding a sharp stone knife, cutting own hair. Self-grooming.
Some hair visibly falling.

### 3.21 TRIM BEARD (3 frames per direction = 48 total)

Character using sharp stone to trim beard. Self-grooming.

### 3.22 BLINK (2 frames per direction = 32 total)

- Frame 0: Eyes open (this is just the normal idle face but used as the
  "open" frame).
- Frame 1: Eyes closed (mid-blink).

The blink is meant to be cycled briefly during idle — most of the time
the eyes-open frame shows, and only briefly does frame 1 appear.

### 3.23 LAUGH (2 frames per direction = 32 total)

- Frame 0: Mouth open, head tilted back slightly, eyes squinted, smiling.
- Frame 1: Mouth wider open, full laugh, head back more.

### 3.24 TALK / SAY HI / SAY BYE (3 frames per direction = 48 total)

- Frame 0: Mouth slightly open, hand raised in greeting or waving.
- Frame 1: Mouth moving, hand position changed.
- Frame 2: Mouth closed, hand lowered.

For "hi" specifically: hand is raised in a wave. For "bye": same wave
but with more emphasis on turning away or stepping back.

### 3.25 ROAR (2 frames per direction = 32 total)

- Frame 0: Mouth wide open, head back, fierce expression, fists possibly
  raised.
- Frame 1: Peak roar, mouth fully open, body tensed.

### 3.26 CRY (2 frames per direction = 32 total)

- Frame 0: Face scrunched, mouth turned down, eyes closed, head down.
- Frame 1: Hands possibly covering face, more intense crying pose.

### 3.27 CUT TREE (4 frames per direction = 64 total)

Character holding a stone axe or hand axe, chopping at a tree. Frames
similar to attack but the action is downward chops on a tree (the tree
itself should NOT be drawn — character only).

### 3.28 PICK UP WOOD LOG (2 frames per direction = 32 total)

- Frame 0: Bending down toward ground, reaching for log.
- Frame 1: Standing back up with log in arms or hand.

### 3.29 SWIM (4 frames per direction = 64 total)

Character in water up to chest, arms swimming (one forward, one back),
legs kicking. Body slightly more horizontal than standing.

### 3.30 HUNT / STALK (4 frames per direction = 64 total)

Crouched/sneaking pose, body low, looking forward intensely, possibly
holding a weapon ready. Slow, deliberate motion.

### 3.31 HURT (2 frames per direction = 32 total)

- Frame 0: Reacting to pain, body recoiling, face grimacing.
- Frame 1: Falling backward or to the side.

### 3.32 DEATH (1 frame per direction = 16 total)

Character lying on the ground, eyes closed, body limp. Final death pose.

### 3.33 PICK UP ITEM (2 frames per direction = 32 total)

- Frame 0: Reaching down to grab an item from the ground.
- Frame 1: Standing with item in hand.

---

## 4. AGE PROGRESSION VARIANTS

For EACH of the above animations, generate FOUR age-stage variants:

| Stage | Description |
|---|---|
| Young Adult (default) | The character as described in section 1 — late teens to twenties, peak fitness |
| Adult | Late twenties to thirties, slightly more weathered, small scars maybe, same build but slightly more worn |
| Old | Fifties-plus, hair graying (mix of dark brown and gray/silver), beard fully gray, more wrinkles, slightly thinner muscle, posture slightly stooped |
| Death (skeleton) | Skeletal remains in same clothing (hide wraps, loincloth) — skull visible, no flesh |

For the Death variant, the clothing should be tattered and the pose
should be lying down (already in section 3.32).

**Total sprite count with age variants:** 33 animation categories × 16
directions × ~3 average frames × 4 age stages = **~6,300 sprites**

This is a LARGE project. For a first pass, prioritize:
1. **Idle × 4 ages × 16 directions = 64 sprites** (foundation)
2. **Walk × 4 ages × 16 directions × 4 frames = 256 sprites** (most-used)
3. **Run, jump, crouch × 4 ages × 16 directions = 192 sprites**
4. **Combat (attack/throw × 3 weapons × 3 frames) × 4 ages × 16 dirs = 576 sprites**
5. **Death × 4 ages × 16 directions = 64 sprites** (end-of-life)

**Minimum viable first batch:** ~1,150 sprites for playable prototype
with full animation system.

---

## 5. SWAY MASKS (separate set, same style)

The existing game uses sway masks in `Assets/Resources/CharacterMasks/`
to animate cloth, hair, and other parts of the character sprite via a
custom shader. For each generated sprite, a corresponding mask PNG is
needed at the same resolution (176×392). The mask is a grayscale image
where:
- **White (255)** = maximum sway motion (loose hair tips, cloth edges,
  loincloth fringe)
- **Black (0)** = no motion (rigid parts — body, leather straps, core
  of clothing)
- **Gray gradients** = partial motion

When commissioning or generating these:
- Hair (especially the tips and loose strands) = mostly white
- Cloth wrappings on arms = white at the loose ends, black where tightly
  bound
- Fur loincloth = white at the ragged hem, darker toward the waist tie
- Leather straps, wrist wraps, necklace = mostly black (no motion)
- Skin = mostly black (no motion)

---

## 6. PROMPT EXAMPLES

### Example prompt for `00_front.png` (idle, front view):

```
[PASTE THE CHARACTER REFERENCE BLOCK FROM SECTION 1 HERE]

POSE: Standing idle pose. Weight slightly on the right leg, left leg
relaxed. Arms hanging naturally at sides with a slight bend at the
elbows, hands relaxed with fingers slightly curled. Head facing camera
directly, eyes looking forward, serious neutral expression. Subtle
implied breathing — chest slightly raised.

VIEW: FRONT VIEW — camera is directly in front of the character. Both
arms visible symmetrically, both legs visible side by side, the full
face visible (both eyes, nose, mouth, beard).

OUTPUT:
- 176×392 pixels PNG with fully transparent background
- Character centered with about 5% margin on all sides
- Realistic painted style matching the reference exactly
- No outlines, no drop shadows, no background elements
```

### Example prompt for `08_back.png` (idle, back view):

```
[PASTE THE CHARACTER REFERENCE BLOCK FROM SECTION 1 HERE]

POSE: Standing idle pose. Weight slightly on right leg, left leg
relaxed. Arms hanging naturally at sides. Back of head showing long
dark hair falling down past shoulders.

VIEW: BACK VIEW — camera is directly behind the character. Back of
head visible, back of torso (X-pattern leather straps visible from
behind), fur loincloth visible from behind, both arms visible from
behind (slightly more of the back of the arms), both legs visible from
behind. NO FACE visible — only back of head and hair.

OUTPUT:
- 176×392 pixels PNG with fully transparent background
- Character centered with about 5% margin on all sides
- Realistic painted style matching the reference exactly
- No outlines, no drop shadows, no background elements
```

### Example prompt for `04_right.png` (idle, right profile):

```
[PASTE THE CHARACTER REFERENCE BLOCK FROM SECTION 1 HERE]

POSE: Standing idle pose. Weight slightly on the right leg (the leg
closer to camera), left leg further from camera. Arms relaxed at sides.

VIEW: RIGHT PROFILE VIEW — camera is directly to the character's right
side. The right arm and right leg are in the foreground (closer to
camera, larger). The left arm and left leg are in the background
(smaller, behind the right side). Right side of face visible in
profile, hair falls down the right side of the back. Fur loincloth
visible in profile.

OUTPUT:
- 176×392 pixels PNG with fully transparent background
- Character centered with about 5% margin on all sides
- Realistic painted style matching the reference exactly
- No outlines, no drop shadows, no background elements
```

### Example prompt for walk frame 0 (front view):

```
[PASTE THE CHARACTER REFERENCE BLOCK FROM SECTION 1 HERE]

POSE: WALKING — left leg stepping forward and planted on the ground,
right leg pushing off behind. Left arm forward (swinging), right arm
back (swinging opposite to legs). Body weight shifted forward and
slightly onto the left leg. Head facing camera, slight forward lean.

VIEW: FRONT VIEW — camera is directly in front of the character.

OUTPUT:
- 176×392 pixels PNG with fully transparent background
- Character centered
- Realistic painted style matching the reference exactly
- Subtle motion blur on the swinging arms
- No outlines, no drop shadows, no background elements
```

---

## 7. HOW TO USE THIS DOCUMENT

### Option A: Commission a professional artist
Post this entire document (or relevant sections) on Fiverr, Upwork, or
ArtStation Jobs. Specify you want a 16-direction turntable sprite sheet
matching the existing style. Budget: $500-2000+ for full set, expect
2-6 weeks delivery for an experienced 2D game artist.

### Option B: AI generation (free, lower quality)
Use the prompts above with an AI image generator (Midjourney, DALL-E,
Stable Diffusion). Realistic expectation:
- 60-80% style match best case
- Consistency drift between sprites (each generation slightly different)
- May need many regeneration attempts to get usable sprites
- Best results with consistent seed/style reference if the tool supports
  it (e.g., Midjourney `--sref` or character reference images)
- Will not match the polished quality of the existing reference sprites

### Option C: Hybrid
Generate base sprites via AI, then have a human artist (Fiverr $50-150)
clean up, recolor, and harmonize the set. Faster and cheaper than full
commission but still polished.

---

## 8. RECOMMENDED FIRST PASS

For a playable prototype with most-used animations:

1. Idle × 4 ages × 16 directions = **64 sprites**
2. Walk × 4 ages × 16 directions × 4 frames = **256 sprites**
3. Run × 4 ages × 16 directions × 4 frames = **256 sprites**
4. Jump × 4 ages × 16 directions × 4 frames = **256 sprites**
5. Attack-axe × 4 ages × 16 directions × 3 frames = **192 sprites**
6. Attack-spear × 4 ages × 16 directions × 3 frames = **192 sprites**
7. Attack-knife × 4 ages × 16 directions × 3 frames = **192 sprites**
8. Hurt × 4 ages × 16 directions × 2 frames = **128 sprites**
9. Death × 4 ages × 16 directions = **64 sprites**

**Subtotal: ~1,600 sprites + matching sway masks = ~3,200 images**

Estimated time for a professional artist: 4-8 weeks full-time.
Estimated cost: $1,500-3,000 on Fiverr/Upwork.

If you only want ONE age stage (young adult) to ship first, divide the
above by 4: ~400 sprites + 400 masks = 800 images, 1-2 weeks,
$500-1,200.

---

## 9. CONSISTENCY TIPS FOR AI GENERATION

If using AI image generation:
- Always include the full CHARACTER REFERENCE BLOCK (section 1) in every
  prompt — this is critical for consistency
- Use the same AI tool, same version, same settings for every sprite
- If your tool supports image references, include 1-2 existing reference
  sprites (e.g., `00_front.png` and `08_back.png`) as style anchors
- Generate multiple candidates per sprite (4-8 variations), then pick
  the best one
- Expect to regenerate 30-50% of sprites to get acceptable consistency
- Final consistency pass: open all sprites side by side and flag any
  that look off (different hair length, beard style, skin tone, etc.)

---

## 10. FILE STRUCTURE FOR DELIVERED ASSETS

When the sprites are ready, organize as:

```
Assets/Sprites/Player/
  00_front.png                  (idle, young adult)
  00_front_walk_0.png           (walk frame 0)
  00_front_walk_1.png
  00_front_walk_2.png
  00_front_walk_3.png
  ... (etc for all 16 directions, all animations, all age stages)

Assets/Resources/CharacterMasks/
  00_front_mask.png             (sway mask for idle front view)
  00_front_walk_0_mask.png      (sway mask for walk frame 0)
  ... (matching masks for all sprites)
```

The game code already loads sprites and masks from these locations and
expects a `directionSprites` array on the `BillboardCharacter` component.

---

## 11. HONEST EXPECTATIONS

- **AI generation (free, fast):** 60-80% style match, inconsistent,
  needs many regen attempts, will look slightly amateur next to existing
- **Hybrid AI + artist cleanup ($100-300):** 85-90% style match, mostly
  consistent, playable quality
- **Professional artist only ($500-3000+):** 95%+ style match, fully
  consistent, production-ready
- **Time:** 1-8 weeks depending on scope and approach

Choose based on your budget, timeline, and quality needs. The game
itself is fully playable with the existing 16 idle sprites — new
animations are an enhancement, not a blocker.
