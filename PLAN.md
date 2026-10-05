# Baby Champion - Milestone Plan

Based on GameDesignDetails.md v0.3. No code has been written yet; this plan describes the order of work, how each milestone is verified from the Unity command line, and what you should playtest.

---

## 1. Approach

- **Simulation first, visuals later.** The core rules (meters, time, economy, Joy, partner AI) live in plain C#, so they can be tested and balanced headlessly from the CLI before any art exists.
- **Vertical slice before breadth.** Levels 1, 2, 4, 8 and 10 are built end-to-end (milestones M1-M7) and playtested with the target audience before the remaining 31 levels are produced. This is the main go/no-go point.
- **Every milestone ends with a green CLI run** (build + tests + data validation + balance check) **and a short playtest by you.**
- **Greybox until the slice is proven.** Placeholder shapes and colors until M7; final art and audio come after the fun is confirmed.

---

## 2. CLI conventions (apply to every milestone)

`$UNITY` is the path to the Unity 6.3+ editor executable. All commands run from the project root. A wrapper script (`tools/ci.sh`, plus `tools/ci.ps1` for Windows) runs the full set in order and stops on the first failure.

| Purpose | Command (shape) | Pass condition |
|---|---|---|
| EditMode tests | `$UNITY -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults artifacts/editmode.xml -logFile artifacts/editmode.log` | Exit code 0; no failed tests in the XML. |
| PlayMode tests | `$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults artifacts/playmode.xml -logFile artifacts/playmode.log` | Exit code 0. |
| Data validation | `$UNITY -batchmode -nographics -quit -projectPath . -executeMethod BabyChampion.Editor.DataValidator.Run -logFile -` | Exit code 0; report in `artifacts/validation.txt`. |
| Balance simulator | `$UNITY -batchmode -nographics -quit -projectPath . -executeMethod BabyChampion.Editor.BalanceSim.Run -level <N> -bot <name> -runs <K> -seed <S> -logFile -` | Exit code 0; results in `artifacts/balance/*.csv`; targets in Section 5 met. |
| Web build | `$UNITY -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod BabyChampion.Editor.BuildScript.BuildWeb -out builds/web -logFile -` | Exit code 0; build size within budget. |
| Android build | `$UNITY -batchmode -quit -projectPath . -buildTarget Android -executeMethod BabyChampion.Editor.BuildScript.BuildAndroid -out builds/android -logFile -` | Exit code 0; APK (dev) or AAB (release) produced. |
| Screenshots | `$UNITY -batchmode -projectPath . -executeMethod BabyChampion.Editor.ScreenshotTool.Run -levels all -logFile -` | Images in `artifacts/screens/` for visual review (needs graphics, so no `-nographics`). |

Rules:
- Every executeMethod tool returns a **non-zero exit code on failure**, so CI and scripts can rely on it.
- The simulation uses a **seeded random generator**; tests and balance runs pass `-seed`, so every result is reproducible.
- PlayMode tests drive input through the Input System test fixture (simulated pointer and touch), so interaction is tested without a human.
- Development builds accept runtime arguments: `-level N -timescale X -seed S -skipIntro` (Web: `?level=N&timescale=X`).

---

## 3. Milestones

### M0 - Project foundation and pipeline
**Goal:** an empty but correctly configured project that builds for both platforms from the command line.

**Scope**
- Unity 6.3+ project from the 3D (URP) template; URP quality tiers *Web*, *Android Low*, *Android High*.
- Active Input Handling = Input System Package (New) only; empty Input Actions asset with pointer actions.
- Folder layout and assembly definitions: `Runtime/Simulation` (no UnityEngine dependency), `Runtime/Game`, `Editor`, `Tests/EditMode`, `Tests/PlayMode`.
- Build script, data validator stub, test runner setup, `tools/ci.sh`.
- Optional: CI service running `tools/ci.sh` on every push.
- A test scene: a floor plane and a cube that moves to where you click or tap.

**CLI verification**
- `tools/ci.sh` passes: one trivial EditMode test, one PlayMode test that simulates a tap and checks the cube moved, web and Android builds succeed.
- A small editor test asserts that the legacy Input Manager is disabled and that the Simulation assembly has no reference to UnityEngine.
- Build size is printed and recorded as the baseline.

**Playtest (5 minutes)**
- Upload the web build to a private itch.io page. Open it on a desktop browser and on your phone's browser; install the Android APK.
- Check: it loads, tap/click moves the cube, landscape orientation, loading time feels acceptable.

---

### M1 - Simulation core (headless)
**Goal:** the rules of Section 7 of the design work and are measurable, with no graphics.

**Scope**
- Time model (compression, sleep fast-forward, pause).
- Baby meters (Hunger, Diaper, Tiredness, Tension, Boredom, Wellbeing) and crying triggers with cause hints.
- Parent meters (Energy, Hunger, Stress); Joy as a meter only (full system in M4).
- Supplies, money, ordering and delivery delay.
- Actions as simulation commands with durations and preconditions (feed, burp, change, soothe, sleep, eat, rest, order).
- Age-rate tables and prices as ScriptableObjects with JSON export.
- Save and load of simulation state (JSON).
- Balance simulator with three bots: **Greedy** (always handles the most urgent need), **Sloppy** (reacts late and makes mistakes), **Idle** (does nothing - sanity check).

**CLI verification**
- EditMode tests for each rule, e.g. "Hunger at 70 starts crying", "Stress 60+ blocks sleep", "crying doubles Energy drain", "dirty diaper for 2 h gives rash", "delivery arrives after 2 in-game hours", "save/load round-trip gives identical state".
- Data validator: every age band has all rates; no negative prices; ranges are sensible.
- Balance simulator on a newborn day: Greedy survives in at least 95% of runs, Idle fails in 100%, Sloppy is in between. Outputs crying minutes, Energy curve and supply use per run.

**Playtest (15 minutes)**
- A debug scene (text and bars only) runs one in-game day with buttons for each action.
- Check: does a 12-minute newborn day feel *busy but doable*? Is the frequency of feeds and diapers right? Note any moments that feel unfair or boring. This is the cheapest point to change the time compression.

---

### M2 - House, controls, camera and HUD (greybox)
**Goal:** the simulation becomes playable in a 3-D house.

**Scope**
- Greybox house with all rooms of design Section 13; NavMesh for walking.
- Click/tap to walk; tap object/baby for the radial action menu; greyed-out actions with reasons.
- Camera: 3/4 view, zoom, 90° rotation, edge indicators for off-screen events.
- HUD: baby need icons (shape-coded), parent bars, clock, money, supplies, phone, pause.
- Placeholder baby and parent with simple state animations (crying, sleeping, eating) and the visual cause hints.
- UI technology decision: UI Toolkit (proposed) or uGUI.

**CLI verification**
- PlayMode tests: the parent can path to every interactable object in the house; every action in the radial menu triggers the matching simulation command; pinch and two-finger rotate gestures work with simulated touch.
- Screenshot tool captures every room and the HUD at phone and desktop resolutions for review.
- Web and Android builds pass; build size compared with the baseline.

**Playtest (20 minutes, on phone and desktop)**
- Play a free day with a newborn.
- Check: can you tell *why* the baby is crying from the hints alone? Are tap targets big enough on the phone? Is the camera ever annoying? Is the radial menu fast enough when several needs pile up?

---

### M3 - Level framework, outcomes and saving
**Goal:** real levels with objectives, failure, stars and progress.

**Scope**
- Level definitions as data: objective, time limit, crying budget, level-specific fail conditions, star rules, available actions.
- Failure detection, gentle failure screen with hint, retry with restored state.
- Success screen with stars; level map by chapter; game setup (baby name, sex, feeding choice).
- Autosave at level end; up to 3 save slots.
- **Level 1 (First Day Home)** as a tutorial without the partner (partner added in M5), and **Level 2 (Night Shift)** with sleep fast-forward.

**CLI verification**
- Data validator checks every level definition (objective type exists, time limits positive, referenced actions exist).
- PlayMode tests: a scripted bot completes Level 1 and Level 2 at high timescale and gets at least 1 star; the Idle bot fails with the correct fail reason; retry restores money and supplies; save/load across a level boundary.
- Balance simulator: Level 1 Greedy 1-star rate at least 95%; Level 2 at least 85%.

**Playtest (20 minutes)**
- Play Levels 1 and 2 from a fresh save, including deliberately failing once.
- Check: are objectives clear before you start? Does failure feel fair and friendly? Does the night shift feel tiring but not miserable?

---

### M4 - Joy system
**Goal:** pillar 1 ("hard, then heart") becomes real gameplay.

**Scope**
- Joy meter effects (slower Energy drain, faster Stress recovery).
- Golden Moments: signature and random, with time window, sparkle, edge indicator, *Enjoy* action and close-up camera.
- Memory Album (stored as moment IDs; photos regenerated) and album screen.
- Play mini-games: Silly faces, Peekaboo, Tickles and raspberries.
- Celebrations for firsts; Milestone cards between levels.
- Third-star rule based on Golden Moment and Joy.
- **Level 3 (Witching Hour)** and **Level 4 (First Laughs)**; signature moments added to Levels 1-2.

**CLI verification**
- EditMode tests: Joy effects on Energy and Stress; Golden Moment window opens and closes on time; missed moments never cause failure; album save/load.
- Balance simulator with a fourth bot, **Joyful** (Greedy plus plays and enjoys moments when no need is urgent). **Pillar check:** Joyful must do at least as well as Greedy on success rate and better on Wellbeing; if playing with the baby makes winning harder, the tuning is wrong.
- Screenshot tool captures the album and each Milestone card.

**Playtest (30 minutes - the most important feeling test so far)**
- Play Levels 1-4.
- Check: do you *want* to stop for Golden Moments, even when busy? Did any moment make you smile? Is peekaboo/tickles fun more than twice? Do the Milestone cards feel celebratory rather than like homework?

---

### M5 - Partner NPC and "on my own" mode
**Goal:** the partner design of Section 9 works and helps without taking over.

**Scope**
- Parent-controller interface shared by player and AI.
- Partner utility AI: autonomous help, never the level's primary objective task.
- Requests radial menu and responses (accept / delay / decline with swap).
- Night plan at bedtime; partner Energy and Stress with mood face.
- Strengths and quirks (start with 2 + 2, rest later); couple moments (sit together, high five, hug); shared Golden Moments.
- Availability schedules by level; text messages while away.
- "On my own" mode with its compensations.
- Level 1 updated to the shared tutorial.
- **Before this milestone:** you decide the partner's look and voice direction (design open question 1).

**CLI verification**
- EditMode tests: partner never performs a primary-objective action; exhausted partner declines and goes to rest; night plan assigns night wakings correctly; partner never causes failure.
- Balance simulator, with partner: partner performs 20-30% of care actions in Levels 1-4. **Fairness check:** "on my own" success rates are within 5 percentage points of partner mode for each level.
- Seeded runs check that each quirk triggers within its intended frequency band.

**Playtest (30 minutes)**
- Play Levels 1-4 once with a partner and once on your own.
- Check: is the partner helpful or annoying? Are the quirks funny the third time? Is the night plan choice meaningful? Does "on my own" feel respectful, not like a punishment?

---

### M6 - Skill mini-games: Spoon Swatter and Mouse Thief
**Goal:** the two signature levels from your original draft work, completing the vertical slice.

**Scope**
- Mini-game framework (input mapping, difficulty affected by Stress and Energy, deterministic with seed).
- Spoon mini-game with swatting baby; Airplane-spoon bonus.
- Crawling baby movement and grab behavior; Work action at the desk.
- **Level 8 (Spoon Swatter)** and **Level 10 (Mouse Thief)**; Level 7 (First Spoon) as the easier introduction if time allows.

**CLI verification**
- PlayMode tests with simulated drag input: a "perfect" input script finishes the spoon level; a "no input" script fails it with the correct reason.
- Mouse Thief: a test bot retrieves the mouse three times and finishes the work; a bot that ignores the baby fails.
- Balance simulator: success-rate curves across difficulty settings (Relaxed / Normal / Realistic) recorded.

**Playtest (20 minutes)**
- Play Levels 8 and 10 several times on the phone and with a mouse.
- Check: does the spoon timing feel skillful and funny rather than frustrating? Is high Stress making the spoon shaky noticeable but fair? Is chasing the crawling baby fun?

---

### M7 - Vertical slice: polish and external playtest (go/no-go)
**Goal:** Levels 1, 2, 4, 8, 10 look and sound representative, run well on target devices, and are tested by real expecting parents.

**Scope**
- Art direction sample: final style for the baby, parents and two rooms (nursery, kitchen); the other rooms stay greybox.
- Audio direction sample: cries, laughter, coos, day/night music, celebration sting.
- Performance work on the reference Android device and mobile browsers.
- Accessibility basics: crying volume slider, softer-alert option, subtitles.
- Content review of facts and Milestone cards by the consultant (design open question 4).
- Simple anonymous playtest telemetry in dev builds (level results, fail reasons, moments caught), written to a local file or exported manually.

**CLI verification**
- Full `tools/ci.sh` run.
- Performance tests (Unity Performance Testing package) in PlayMode for a busy scene; frame-time budget assertion; optionally run on the device with `-testPlatform Android`.
- Build-size budget check fails the build if the web download exceeds the agreed budget (proposed: 50 MB compressed; to confirm).

**Playtest (external, 5-10 people from the target audience)**
- Each plays the slice for 30-45 minutes, then answers a short questionnaire.
- Questions: Did you smile or laugh, and when? Did anything feel stressful in a bad way? Did you learn something useful? Do you feel more *excited* about having a baby? Would you play the next level?
- **Go/no-go decision with you:** proceed to full production, or adjust the core loop first.

---

### M8 - Chapters 1-3 complete (Levels 1-9)
**Scope:** Levels 3, 5 (The Roller: hand-on-baby changing), 6 (Back to Work with money target), 7 (First Spoon), 9 (Crawler: babyproofing). Remaining play mini-games for these ages. Chapter structure on the level map.

**CLI verification:** data validation for all new levels; PlayMode bot run for each level; balance targets per level (Section 5); a test that every level has a signature Golden Moment and Milestone card.

**Playtest:** play Chapters 1-3 in order. Check the difficulty curve and whether each level introduces something new and memorable.

---

### M9 - Chapter 4 complete (Levels 10-13)
**Scope:** Levels 11 (Drop It!), 12 (Don't Leave Me: line-of-sight mechanic, baby carrier), 13 (Happy Birthday: guest NPCs, first steps). Tower-and-crash mini-game.

**CLI verification:** as M8; additional PlayMode test that guest NPCs never block required paths.

**Playtest:** Chapter 4. Check: is separation anxiety understandable and not frustrating? Does the birthday feel like a celebration?

---

### M10 - Chapter 5 complete (Levels 14-24)
**Scope**
- Walking toddler: movement, chase, climbing, tantrums.
- Levels 15 (Toilet Dash), 16 (Climber), 18 ("No!"), 20 (Bath Time), 21 (Picky Eater), 24 (Chatterbox).
- **Level 22 (Hide and Seek):** hiding-spot system, directional giggle audio with visual equivalent.
- **Level 23 (Cart Sneak):** grocery store scene, look-away grab mechanic, cart inspection, return-to-shelf.
- Routine levels "A Day Together" (14, 17, 19) and the random-event pool.
- Dance party, story time, chase-and-catch mini-games.

**CLI verification**
- Data validation also checks: no hiding spot is an appliance; every store item has a valid home shelf; random-event pools exist for each age band.
- PlayMode tests: the hide-and-seek bot finds the toddler from every spot; every hiding spot is reachable; cart bot detects and returns every extra item.
- Balance simulator on routine levels across 1,000 seeds to catch rare unwinnable event combinations.

**Playtest:** Chapter 5. Check: Hide and Seek clues fair with sound off? Is Cart Sneak funny, and are the extras findable on a phone screen? Do routine days feel special or like filler?

---

### M11 - Chapter 6 complete (Levels 25-36) and finale
**Scope:** **Level 25 (Splash Attack):** spray patterns, dodge, hose grab, Soak meter, water-temperature check. Levels 26-28 (Big Bed, Potty Training I-II), 30-31 (dressing, playground scene), 33 (Sick Day), 35-36 (kindergarten, finale montage from the player's album). Routine levels 29, 32, 34. Pretend-play mini-game.

**CLI verification**
- PlayMode tests: Splash Attack dodge bot finishes the bath; full Soak triggers a clothes change, not a failure; the water-temperature step cannot be skipped.
- Finale test: an album generated from a full simulated playthrough produces a montage without missing images.
- Full-game balance run: a bot plays all 36 levels in order with carried-over money and supplies; no level is unwinnable and the money safety net triggers as designed.

**Playtest:** Chapter 6 and the finale, ideally after playing the whole game across several days. Check: does the ending feel earned and emotional?

---

### M12 - Full art, audio, accessibility and settings
**Scope:** final art for all rooms, store, playground, kindergarten; baby growth models per chapter; nursery evolution; full audio and music; all accessibility options; difficulty settings; localization groundwork (strings in tables).

**CLI verification:** screenshot tool across all levels at phone and desktop resolutions; performance tests on the reference device for every scene; build-size budget; a test that no user-facing string is hardcoded outside the string tables.

**Playtest:** full game with sound off (is everything playable?), with Relaxed difficulty, and on the slowest supported phone.

---

### M13 - Release
**Scope:** release builds; itch.io page; Google Play listing and release AAB; privacy policy; audience declaration (adults); ad-service interface with no-op implementation in place; final consultant sign-off.

**CLI verification:** `tools/ci.sh` with release flags produces a signed AAB and the web build; version number embedded and checked; final full balance and test run archived with the release.

**Playtest:** a fresh install on Android and a fresh browser on itch.io, from first launch through the first chapter. Check store-listing screenshots match the game.

---

### Later (after release)
- Ads integration following design Section 19 (consent flow, interstitial cap, rewarded options).
- Two-player mode through the parent-controller interface.
- Additional levels and partner traits.

---

## 4. Decisions needed from you, and when

| Before | Decision |
|---|---|
| M0 | Exact Unity 6.3+ version to pin; CI service (or local-only scripts). |
| M2 | UI technology: UI Toolkit (proposed) or uGUI. |
| M5 | Partner look and voice direction. |
| M7 | Reference Android device for performance; web build-size budget; who the consultant is; where to recruit playtesters. |
| M12 | Languages for localization. |
| Later | Ad SDK and web ad strategy. |

---

## 5. Balance targets (initial)

Measured with the balance simulator, Normal difficulty, at least 500 seeded runs per level.

| Bot | Chapter 1 | Chapters 2-4 | Chapters 5-6 |
|---|---|---|---|
| Greedy, 1-star rate | 95%+ | 85%+ | 75%+ |
| Joyful, 1-star rate | at least Greedy's | at least Greedy's | at least Greedy's |
| Joyful, 3-star rate | 40-70% | 30-60% | 25-50% |
| Sloppy, 1-star rate | 50-80% | 30-60% | 20-50% |
| Idle | 0% | 0% | 0% |

Bot numbers only check that levels are winnable, fair and consistent; human playtests decide whether they are fun. Targets will be adjusted after the M1 and M7 playtests.

---

## 6. Main risks

| Risk | Mitigation |
|---|---|
| Real-time care sim feels stressful rather than joyful. | Joy system in M4 before content production; pillar check in the balance simulator; external playtest at M7. |
| Partner AI feels annoying or does too much. | Utility AI with share limits verified by simulator (M5); quirks with frequency bands. |
| Web build too large or slow on mobile browsers. | Size budget enforced from M0; test on itch.io from the first milestone. |
| 36 levels is a lot of content. | Data-driven levels, reusable mini-games, routine levels from the event pool; scope can drop routine levels if needed. |
| Inaccurate parenting facts. | Consultant review at M7 and before release. |
