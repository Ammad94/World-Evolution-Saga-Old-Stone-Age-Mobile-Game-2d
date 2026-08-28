# 🎯 Gap Analysis — What's Pending to Reach "AAA Level"

*Honest assessment of the current repo (branch `arena/01a04693-world-evolution-saga-old-stone`, content merged to `main` @ `4de477e`), and the road from here to a premium, AAA-quality 2D mobile survival game.*

---

## 1. Where the project stands today

**The honest one-liner:** this is a *systems-complete prototype / vertical-slice skeleton*, not a game yet. It has an unusually strong **code foundation** (~8,600 LOC, 50 runtime scripts), but almost none of the things a player actually *sees, hears, or feels*.

| Layer | Status | Evidence |
|---|---|---|
| Architecture & systems code | 🟢 Strong | Chunk streaming, whole-earth procedural map (16,384×8,192), 15 biomes, FSM animal AI, survival stats, crafting, save/load, scene flow — all wired end-to-end |
| Art & animation | 🔴 Placeholder | Every sprite is **procedurally generated** by `Assets/Editor/ProjectSetup.cs` (1,153 lines of editor code). Single-frame 8-directional poses; `AnimalWalkAnimator` arrays hold **1 frame each** — "animation" is a stride-bob squash |
| Audio | 🔴 Placeholder | `AudioManager` routes 6 SFX types; the 6 `.wav` files are synthesized beeps/clicks. One ambient loop. No music, no mixer, no adaptive audio |
| Game feel / juice | 🟡 Barely started | `CombatFeedback.cs` exists (shake/hit-flash basics); no hit-stop, no tween/UI motion, no impactful VFX |
| Content depth | 🔴 Thin | 4 animals, ~5 items, 1 weapon tier, no quests, no progression arc, no NPC/tribe systems despite the "World Evolution Saga" name |
| Engineering quality | 🟡 | Clean namespaces/comments, but **zero tests, no CI, singletons everywhere, scenes are editor-generated** (Build Settings reference placeholder GUIDs `000…0`) |
| Product/shipping layer | 🔴 Absent | No analytics, crash reporting, IAP/monetization, localization, privacy/compliance, cloud save, store assets, FTUE/onboarding |
| Device QA | 🔴 Not started | Never run on a real phone; no perf budgets, no profiling, no thermal/battery tests |

**Reality check on the word "AAA":** true AAA is a studio-scale budget (50–200 people, years, millions of dollars). For a solo 2D mobile project the achievable — and correct — target is **"premium indie / polished commercial"** quality: the bar set by *Don't Starve: Pocket Edition*, *Stardew Valley* mobile, *Kingdom Two Crowns*, *Terraria* mobile. This document maps the gap to *that* bar, which is what players and stores will actually judge.

---

## 2. The gaps, by category

### A. Art, animation & visual identity — **the #1 gap (≈60% of the total distance)**
Right now the game renders colored shapes. A survival game lives or dies on atmosphere.

- [ ] **Art direction bible** — palette per biome/time-of-day, silhouette rules, one painterly or pixel-art style locked document (currently each system picks its own colors in code)
- [ ] **Real character art** — the player is `CreatePlaceholderPlayer()` in `GameBootstrap.cs`. Needs a proper character sheet: 8-direction idle/walk/run/attack/gather/swim/climb/die, equipment layering (clothing, torch, weapon)
- [ ] **Real animal art & skeletal animation** — 4 animals × 1 static frame today. Use Unity 2D Animation (bone rig, already in `manifest.json` and unused) for walk/attack/death/flee cycles at 24–30 fps
- [ ] **Environment art pass** — terrain is flat-color 64px tiles: needs auto-tiling with transitions, cliffs/cave interiors, water flow animation, 2–3 vegetation variants per biome, wind sway shaders
- [ ] **Lighting polish** — URP 2D lights exist but need global volume grading per biome/season/weather, normal-mapped sprites for the pseudo-3D look, glow/ volumetric "god rays" in forests, campfire light flicker radius tied to fuel
- [ ] **UI/UX design system** — HUD, menus and tooltips are `UIFactory`-generated gray boxes. Needs a themed UI kit (frames, fonts, icons, buttons with states), icon set for every item, world-map parchment styling
- [ ] **VFX library** — blood/hit sparks, harvest debris, footstep dust on 6 surfaces, snow/ash/rain occlusion, aurora/northern-lights night sky for ice biomes

**Effort:** XL — this is a professional pixel/painterly artist + animator engagement (3–6 months, or a large asset-store/commission budget). Code alone cannot close this.

### B. Audio & music — **gap #2**
- [ ] Real SFX library (200+ clips): footsteps per material, tool-on-material, animal vocalizations per state, weather, UI, crafting, ambient beds per biome × day/night × season
- [ ] **Music**: menu theme + adaptive gameplay layers (exploration / combat / danger / seasons) that crossfade — currently one static ambient loop
- [ ] **Audio Mixer architecture**: Music/SFX/Ambience/UI groups, side-chain ducking, settings persistence (the settings menu exists but has no audio options)
- [ ] Mobile loudness compliance (LUFS targets), compression, mute-on-background

**Effort:** L–XL (composer + sound designer, or premium licensed packs).

### C. Game feel ("juice")
- [ ] Hit-stop, screen shake tuning, knockback, damage numbers, kill cam micro-slow-mo
- [ ] Tween library for all UI (currently instant show/hide) — menu transitions, popup scale, count-up counters
- [ ] Haptics on iOS/Android for hits, harvest, low-health warning (`CombatFeedback` is the natural home)
- [ ] Controller + keyboard support for tablets/streaming (Input System is in the manifest, unused beyond touch)
- [ ] Juice the core verbs: chopping/mining/throwing need anticipation → strike → follow-through + camera feedback, or the loop feels dead regardless of art

**Effort:** M (mostly code — very cost-effective quality win).

### D. Gameplay depth & content — "saga" is currently 1 evening long
- [ ] **Era progression** (the title promises evolution): Stone → Advanced Stone → Early Metal with tech-tree unlocks driving recipes, buildings, capabilities
- [ ] **Quest/goal system** — README Phase 6 lists it; nothing exists. Needs: tutorial quest chain (FTUE), repeatable hunts, migration-event hunts (herd migration system exists and is unexploited), milestone challenges
- [ ] **Base building depth** — placement exists; add structure HP, decay, repair, storage containers, bed/spawn point, campfire fuel simulation
- [ ] **Tribe/NPC layer** — friendly NPC camps, trading, companions; (multiplayer is listed in the README future phase — recommend co-op only, post-launch)
- [ ] **Balance & economy pass** — hunger/thirst/energy rates, loot tables, tool durability, difficulty modes
- [ ] Content scale-up: 4 → 15+ animals (birds, fish, small game, boss megafauna), 5 → 60+ items, 15 → 25+ recipes, cave interiors, seasonal events

**Effort:** XL (design + code + content, 3–6 months solo).

### E. Performance & device quality (mobile = the platform)
- [ ] **Draw-call/sprite atlas pass** — every generated sprite is its own texture today; build atlases per biome/character, SRP Batcher audit
- [ ] **Object pooling audit** — chunks recycle, but projectiles/particles/loot need pooling too; cap allocations to stop GC spikes
- [ ] **Profiling on real devices** — target 60 fps on a 2019 mid-range Android (~Snapdragon 660 class), 30 fps floor on low-end; memory budget < 1 GB; thermal soak test (30 min session)
- [ ] IL2CPP + ARM64 builds, texture compression (ASTC), resolution scaling tiers, `AccessibilityAndPerformance.cs` expanded into a real quality-settings menu
- [ ] Load-time budget: cold start < 10 s on mid-range (Addressables would help — not currently used)

**Effort:** M–L (needs actual devices; code-side prep is M).

### F. Engineering quality & safety nets
- [ ] **Automated tests** — Unity Test Runner: zero tests today. Priority: seed-determinism of `WorldMap` (must be regression-tested!), save/load round-trip, inventory/crafting logic, biome classifier
- [ ] **CI/CD** — GitHub Actions: compile + run edit-mode tests on every PR, Android build artifact nightly
- [ ] **Save system hardening** — `SaveSystem.cs` is plain JSON, no version field, no migration path, no corruption recovery/backup-rotate; player position, world diffs and time must all round-trip (currently only stats/inventory)
- [ ] Replace God-singletons (`GameManager.Instance`, `AudioManager.Instance`…) with scoped DI or at least an explicit service registry before the codebase doubles
- [ ] Commit the generated scenes/prefabs (or move generation to build-time) — a fresh clone currently has **empty Build Settings GUIDs** and requires the editor ritual to even open gameplay

**Effort:** M.

### G. Product, monetization & live-ops (the invisible "AAA" layer)
- [ ] **FTUE/onboarding** — first 5 minutes must teach, hook and not kill the player; currently a raw spawn into a hostile planet
- [ ] **Analytics** (Unity Analytics/Firebase): funnel from install → tutorial-complete → day-7 retention; difficulty-death heatmaps
- [ ] **Crash reporting** (Backtrace/Crashlytics/Unity Cloud Diagnostics)
- [ ] **Monetization decision**: premium ($4.99–7.99, no IAP — fits the genre) vs F2P (IAP + rewarded ads). This decision reshapes FTUE and design — make it early
- [ ] **Cloud save + cross-device** (Unity Gaming Services Save/Authentication), leaderboards (survival days, hunts)
- [ ] **Localization** — all strings are hardcoded English in C#; move to Unity Localization with 8–12 languages (zh, ja, ko, de, fr, es, pt-BR, ru, tr…) — cheap installs, big win
- [ ] **Compliance** — privacy policy, GDPR/CCPA consent flows, COPPA/age-gate, data-safety forms for both stores, permissions justifications
- [ ] Remote Config for live balance tuning and feature flags

**Effort:** L.

### H. Release & store
- [ ] Store assets: icon, 5–8 screenshots, feature graphic, 30 s trailer (needs art from gap A first)
- [ ] App Store / Play Console full submission pipeline, beta tracks (TestFlight, Play Internal Testing)
- [ ] Soft launch in 1–2 test markets with KPI targets (D1 > 40%, D7 > 15% for premium-lite; adjust for premium)
- [ ] Press kit, devlog/social presence, wishlisting page

**Effort:** M (money + coordination more than code).

---

## 3. Prioritized milestone plan

| Milestone | Goal | Includes | Rough timeline (solo + contractors) |
|---|---|---|---|
| **M0 — "Feel" spike** *(start here, cheapest wins)* | Core loop stops feeling dead | C (juice) + F (tests/CI/save hardening) + real SFX pack + 1 fully-arted biome & character as the **vertical-slice visual target** | 4–8 weeks |
| **M1 — Vertical slice** | 30 min of play that looks/sounds premium | Art style locked across 3 biomes, animated player + 6 animals, music + mixer, FTUE quest chain, era-1 tech tree | 3–5 months |
| **M2 — Content complete beta** | Full arc playable | All biomes/animals/recipes, base building v2, era progression complete, perf pass on devices, analytics + crashes wired, localization | +4–6 months |
| **M3 — Launch-ready** | Shippable | Store assets, compliance, cloud save, balance from beta telemetry, soft launch → global | +2–3 months |
| **M4 — Live ops** | Keep players | Events, seasonal content, co-op evaluation, new eras/biomes as DLC | ongoing |

**Total realistic distance to "premium indie AAA-feel": ~12–18 months** with art/audio contracted, or ~2–3 years pure solo.

---

## 4. If you only do five things next

1. **Commission the player character + one biome art pack** — single highest-leverage move; every screenshot/video improves instantly.
2. **Juice the core verbs** (chop/mine/attack/eat) — pure code, weeks not months, transforms feel.
3. **Add the tutorial quest chain** — retention lives or dies in the first 5 minutes.
4. **Set up device profiling + CI with tests** — every later milestone gets cheaper and safer.
5. **Decide premium vs F2P now** — it constrains FTUE, monetization work, and store strategy.

---

## 5. What NOT to do

- Don't chase literal AAA scope (open worlds with thousands of NPCs, cinematic story, multiplayer day-one). This project's procedural whole-earth premise is genuinely distinctive — **depth-of-simulation per screenful** beats breadth.
- Don't add multiplayer before M3 — it multiplies every other cost.
- Don't keep generating placeholder content in `ProjectSetup.cs` past the vertical slice — the editor-generation approach is brilliant for scaffolding, but real assets must eventually replace it, and the longer the swap is deferred the more re-wiring it costs.

---

*Generated 2026-08-28 from a full repository audit (50 scripts, 8,577 LOC, manifest/quality/build settings, README roadmap, and `main` branch content at `4de477e`).*
