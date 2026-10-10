# Baby Champion - Milestone Plan

Based on 2-design-detailed.md v0.4.1. No code has been written yet; this plan describes the order of work, how each milestone is verified from the Unity command line, and what you should playtest.

Milestones M0-M12 build and release the base game (a single-parent game). Milestones L1-L3 add the later-stage features: the partner NPC, two-player mode and ads.

---

## 1. Approach

- **Simulation first, visuals later.** The core rules (meters, time, economy, Joy) live in plain C#, so they can be tested and balanced headlessly from the CLI before any art exists.
- **Vertical slice before breadth.** Levels 1, 2, 4, 8 and 10 are built end-to-end (milestones M1-M6) and playtested with the target audience before the remaining 31 levels are produced. This is the main go/no-go point.
- **Every milestone ends with a green CLI run** (build + tests + data validation + balance check) **and a short playtest by you.**
- **Greybox until the slice is proven.** Placeholder shapes and colors until M6; final art and audio come after the fun is confirmed.
- **Ready for the partner, without building it.** From M1, parent actions go through a controller interface (design Section 19.4), so the partner (L1) and a second player (L2) can be added later without changing the simulation.

---

## 2. CLI conventions (apply to every milestone)

All commands run from the project root through a small wrapper, **`tools/unity.sh`** (plus `tools/unity.ps1` for Windows). The wrapper finds the pinned Unity 6.3+ editor and adds the flags every call needs: `-batchmode`, `-projectPath .` and `-logFile -`. Unity's documentation doesn't define what happens without `-projectPath`, so the flag stays, but it is written once in the wrapper instead of in every command. A second script, `tools/ci.sh` (and `tools/ci.ps1`), runs the full set in order and stops on the first failure.

| Purpose | Command | Pass condition |
|---|---|---|
| EditMode tests | `tools/unity.sh -nographics -runTests -testPlatform EditMode -testResults artifacts/editmode.xml` | Exit code 0; no failed tests in the XML. |
| PlayMode tests | `tools/unity.sh -runTests -testPlatform PlayMode -testResults artifacts/playmode.xml` | Exit code 0. |
| Data validation | `tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run` | Exit code 0; report in `artifacts/validation.txt`. |
| Balance simulator | `tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.BalanceSim.Run -level <N> -bot <name> -runs <K> -seed <S>` | Exit code 0; results in `artifacts/balance/*.csv`; targets in Section 6 met. |
| Web build | `tools/unity.sh -quit -activeBuildProfile "Assets/Settings/Build Profiles/Web.asset" -build builds/web` | Exit code 0; build size within budget. |
| Android build | `tools/unity.sh -quit -activeBuildProfile "Assets/Settings/Build Profiles/Android.asset" -build builds/android/BabyChampion.apk` | Exit code 0; APK (dev profile) or AAB (release profile) produced. |
| Screenshots | `tools/unity.sh -quit -executeMethod BabyChampion.Editor.ScreenshotTool.Run -levels all` | Images in `artifacts/screens/` for visual review (needs graphics, so no `-nographics`). |

The test commands have no `-quit`: the test runner closes Unity itself, and `-quit` would make Unity exit before the tests run.

**Builds use Unity 6 Build Profiles, not a custom build script.** A Build Profile is an asset that stores the platform and its build settings. Unity 6 builds one directly from the command line with `-activeBuildProfile` and `-build`, so no build code is needed. There are four profiles: *Web Dev*, *Web Release*, *Android Dev* and *Android Release*. The table shows one of each for short. The only build code is a small editor hook that runs before every build and stamps the version number. Build size is checked by `tools/ci.sh` after the build.

**Who writes the scripts and tools:** I (Claude) write `tools/unity.sh`, `tools/ci.sh`, the Build Profiles and the version hook in M0. I write each editor tool (data validator, balance simulator, screenshot tool) in the milestone that first needs it. They are part of the code base like everything else, and you review them like any other change. Each milestone's CLI checks only count once I've actually run them and they pass.

Rules:
- Every executeMethod tool returns a **non-zero exit code on failure**, so CI and scripts can rely on it. M0 also confirms that a failed `-build` returns non-zero.
- The simulation uses a **seeded random generator**; tests and balance runs pass `-seed`, so every result is reproducible.
- PlayMode tests drive input through the Input System test fixture (simulated pointer and touch), so interaction is tested without a human.
- Development builds accept runtime arguments: `-level N -timescale X -seed S -skipIntro`. On Web they are URL parameters (`?level=N&timescale=X`); on Android they are passed when launching the app through `adb` (exact command documented in M0).

---

## 3. Base game milestones

### M0 - Project foundation and pipeline
**Goal:** an empty but correctly configured project that builds for both platforms from the command line.

**Scope**
- Unity 6.3+ project from the 3D (URP) template; URP quality tiers *Web*, *Android Low*, *Android High*.
- Active Input Handling = Input System Package (New) only; empty Input Actions asset with pointer actions.
- Folder layout and assembly definitions: `Runtime/Simulation` (no UnityEngine dependency), `Runtime/Game`, `Editor`, `Tests/EditMode`, `Tests/PlayMode`.
- `tools/unity.sh` and `tools/ci.sh` (plus Windows versions); Build Profiles (*Web Dev*, *Web Release*, *Android Dev*, *Android Release*) and the version-stamping hook; data validator stub; test runner setup.
- Document how to run Unity from the CLI on your machine: where the pinned editor is installed, and how to activate the Unity license for batch mode.
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
**Goal:** the rules of design Section 7 work and are measurable, with no graphics.

**Scope**
- Time model (compression, sleep fast-forward, pause).
- Baby meters (Hunger, Diaper, Tiredness, Tension, Boredom, Wellbeing) and crying triggers with cause hints.
- Parent meters (Energy, Hunger, Stress); Joy as a meter only (full system in M4).
- Supplies, money, ordering and delivery delay.
- Actions as simulation commands with durations and preconditions (feed, burp, change, soothe, sleep, eat, rest, order).
- **Parent-controller interface:** every parent action is issued through it. In the base game only the player and the test bots use it.
- Age-rate tables and prices as ScriptableObjects with JSON export.
- Save and load of simulation state (JSON).
- Balance simulator with three bots: **Greedy** (always handles the most urgent need), **Sloppy** (reacts late and makes mistakes), **Idle** (does nothing - sanity check). Bots act through the parent-controller interface, exactly like the player.

**CLI verification**
- EditMode tests for each rule, e.g. "Hunger at 70 starts crying", "Stress 60+ blocks sleep", "crying doubles Energy drain", "dirty diaper for 2 h gives rash", "delivery arrives after 2 in-game hours", "save/load round-trip gives identical state".
- A test that the simulation accepts actions only through the parent-controller interface.
- Data validator: every age band has all rates; no negative prices; ranges are sensible.
- Balance simulator on a newborn day: Greedy survives in at least 95% of runs, Idle fails in 100%, Sloppy is in between. Outputs crying minutes, Energy curve and supply use per run.

**Playtest (15 minutes)**
- A debug scene (text and bars only) runs one in-game day with buttons for each action.
- Check: does a 12-minute newborn day feel *busy but doable* for one parent? Is the frequency of feeds and diapers right? Note any moments that feel unfair or boring. This is the cheapest point to change the time compression.

---

### M2 - House, controls, camera and HUD (greybox)
**Goal:** the simulation becomes playable in a 3-D house.

**Scope**
- Greybox house with all rooms of design Section 12; NavMesh for walking.
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
- Success screen with stars; level map by chapter; game setup (baby name, sex, appearance, feeding choice).
- *Call for help* (grandparent) and its 2-star cap.
- Autosave at level end; up to 3 save slots.
- **Level 1 (First Day Home)** as the tutorial, and **Level 2 (Night Shift)** with sleep fast-forward.

**CLI verification**
- Data validator checks every level definition (objective type exists, time limits positive, referenced actions exist).
- PlayMode tests: a scripted bot completes Level 1 and Level 2 at high timescale and gets at least 1 star; the Idle bot fails with the correct fail reason; retry restores money and supplies; *Call for help* caps the level at 2 stars; save/load across a level boundary.
- Balance simulator: Level 1 Greedy 1-star rate at least 95%; Level 2 at least 85%.

**Playtest (20 minutes)**
- Play Levels 1 and 2 from a fresh save, including deliberately failing once.
- Check: are objectives clear before you start? Does failure feel fair and friendly? Does the night shift feel tiring but not miserable, even alone?

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

### M5 - Skill mini-games: Spoon Swatter and Mouse Thief
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

### M6 - Vertical slice: polish and external playtest (go/no-go)
**Goal:** Levels 1, 2, 4, 8, 10 look and sound representative, run well on target devices, and are tested by real expecting parents.

**Scope**
- Art direction sample: final style for the baby, the parent and two rooms (nursery, kitchen); the other rooms stay greybox.
- Audio direction sample: cries, laughter, coos, day/night music, celebration sting.
- Performance work on the reference Android device and mobile browsers.
- Accessibility basics: crying volume slider, softer-alert option, subtitles.
- Content review of facts and Milestone cards by the consultant (design open question 3).
- Simple anonymous playtest telemetry in dev builds (level results, fail reasons, moments caught), written to a local file or exported manually.

**CLI verification**
- Full `tools/ci.sh` run.
- Performance tests (Unity Performance Testing package) in PlayMode for a busy scene; frame-time budget assertion; optionally run on the device with `-testPlatform Android`.
- Build-size budget check fails the build if the web download exceeds the agreed budget (proposed: 50 MB compressed; to confirm).

**Playtest (external, 5-10 people from the target audience)**
- Each plays the slice for 30-45 minutes, then answers a short questionnaire.
- Questions: Did you smile or laugh, and when? Did anything feel stressful in a bad way? Did you learn something useful? Do you feel more *excited* about having a baby? Would you play the next level? Did you miss having a partner in the game? (Input for L1.)
- **Go/no-go decision with you:** proceed to full production, or adjust the core loop first.

---

### M7 - Chapters 1-3 complete (Levels 1-9)
**Scope:** Levels 3, 5 (The Roller: hand-on-baby changing), 6 (Back to Work with money target), 7 (First Spoon), 9 (Crawler: babyproofing). Remaining play mini-games for these ages. Chapter structure on the level map.

**CLI verification:** data validation for all new levels; PlayMode bot run for each level; balance targets per level (Section 6); a test that every level has a signature Golden Moment and Milestone card.

**Playtest:** play Chapters 1-3 in order. Check the difficulty curve and whether each level introduces something new and memorable.

---

### M8 - Chapter 4 complete (Levels 10-13)
**Scope:** Levels 11 (Drop It!), 12 (Don't Leave Me: line-of-sight mechanic, baby carrier), 13 (Happy Birthday: guest NPCs, first steps). Tower-and-crash mini-game.

**CLI verification:** as M7; additional PlayMode test that guest NPCs never block required paths.

**Playtest:** Chapter 4. Check: is separation anxiety understandable and not frustrating? Does the birthday feel like a celebration?

---

### M9 - Chapter 5 complete (Levels 14-24)
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

### M10 - Chapter 6 complete (Levels 25-36) and finale
**Scope:** **Level 25 (Splash Attack):** spray patterns, dodge, hose grab, Soak meter, water-temperature check. Levels 26-28 (Big Bed, Potty Training I-II), 30-31 (dressing, playground scene), 33 (Sick Day), 35-36 (kindergarten, finale montage from the player's album). Routine levels 29, 32, 34. Pretend-play mini-game.

**CLI verification**
- PlayMode tests: Splash Attack dodge bot finishes the bath; full Soak triggers a clothes change, not a failure; the water-temperature step cannot be skipped.
- Finale test: an album generated from a full simulated playthrough produces a montage without missing images.
- Full-game balance run: a bot plays all 36 levels in order with carried-over money and supplies; no level is unwinnable and the money safety net triggers as designed.

**Playtest:** Chapter 6 and the finale, ideally after playing the whole game across several days. Check: does the ending feel earned and emotional?

---

### M11 - Full art, audio, accessibility and settings
**Scope:** final art for all rooms, store, playground, kindergarten; baby growth models per chapter; nursery evolution; full audio and music; all accessibility options; difficulty settings; localization groundwork (strings in tables).

**CLI verification:** screenshot tool across all levels at phone and desktop resolutions; performance tests on the reference device for every scene; build-size budget; a test that no user-facing string is hardcoded outside the string tables.

**Playtest:** full game with sound off (is everything playable?), with Relaxed difficulty, and on the slowest supported phone.

---

### M12 - Release (base game)
**Scope:** release builds; itch.io page; Google Play listing and release AAB; privacy policy; audience declaration (adults); ad-service interface with no-op implementation in place; final consultant sign-off.

**CLI verification:** `tools/ci.sh` with release flags produces a signed AAB and the web build; version number embedded and checked; final full balance and test run archived with the release, as the **reference results** for the later-stage milestones.

**Playtest:** a fresh install on Android and a fresh browser on itch.io, from first launch through the first chapter. Check store-listing screenshots match the game.

---

## 4. Later-stage milestones

These start after the base game is released. L2 depends on L1; L3 is independent and can come first if ads are needed sooner.

### L1 - Partner NPC
**Goal:** the partner of design Section 22 helps without taking over, and the base game is unchanged when playing on your own.

**Scope**
- Setup choice *with a partner / on my own*; partner name, look, strength and quirk.
- Partner AI as a second controller on the parent-controller interface: utility scoring for autonomous help, never the level's primary objective task.
- Requests radial menu and responses (accept / delay / decline with swap); night plan at bedtime.
- Partner Energy and Stress with mood face; exhausted partner rests.
- Strengths and quirks (start with 2 + 2, rest later); couple moments (sit together, high five, hug); shared Golden Moments with the "Come quick" call.
- Availability schedules by level; text messages while away.
- All base-game changes listed in design Section 22.9 (Level 1 tutorial step, Level 2 night-plan objective, feeding by the partner, partner salary, HUD).
- Partner-mode tuning for all 36 levels (faster needs, more simultaneous events; design Section 22.7).
- **Before this milestone:** you decide the partner's look and voice (design open question 22.11.1).

**CLI verification**
- EditMode tests: partner never performs a primary-objective action; exhausted partner declines and goes to rest; night plan assigns night wakings correctly; partner never causes failure.
- **Base-game regression:** with partner mode off, the full test suite and balance results match the M12 reference results. This proves the base game does not depend on the partner.
- Balance simulator in partner mode (player bot + partner AI): partner performs 20-30% of care actions across all levels. **Fairness check:** partner-mode success rates are within 5 percentage points of single-parent mode for each level.
- Seeded runs check that each quirk triggers within its intended frequency band.

**Playtest (30-45 minutes)**
- Play Chapters 1-2 once with a partner and once on your own.
- Check: is the partner helpful or annoying? Are the quirks funny the third time? Is the night plan choice meaningful? Do both modes feel equally valid, with neither a "hard mode"?

---

### L2 - Two-player mode
**Goal:** two people play the two parents together (design Section 23).

**Scope**
- Second human controller on the parent-controller interface, replacing the partner AI.
- Networking for local network and/or online play (technology to choose before this milestone); host/join flow.
- Shared Golden Moments when both players tap *Enjoy*.
- Disconnect handling: if a player drops, the partner AI takes over that parent.
- Level retuning for two players.

**CLI verification**
- EditMode tests with two scripted controllers on one simulation: both players' actions apply correctly; conflicting actions (both picking up the baby) resolve consistently.
- Multi-instance test: CLI launches two development builds (`-host`, `-join`) each driven by a bot; the simulation state hash matches on both every N ticks; killing one instance hands its parent to the partner AI.
- Balance simulator with two bots: targets as in Section 6.

**Playtest**
- Play Chapter 1 with someone on two devices, on the same network and over the internet.
- Check: does coordinating feel fun? Is lag noticeable? Do you talk to each other while playing (a good sign)?

---

### L3 - Ads
**Goal:** ads earn money without damaging the warm experience (design Section 18).

**Scope**
- Ad SDK plugged in behind the existing ad-service interface (SDK choice: design open question 5).
- Consent flow (e.g. GDPR); family-friendly ad categories only; audience declaration re-checked against current Google Play policy.
- Interstitials between levels with the frequency cap; optional rewarded ads (free express delivery, extra *Call for help* without star cap, album stickers, mid-level checkpoint).
- Web ads evaluated separately.

**CLI verification**
- EditMode tests with a fake ad service: no ad is ever requested while a level is running; at most one interstitial per 3 levels; none right after a Golden Moment celebration or the finale; rewards are granted only on a completed rewarded ad.
- **No-ads run:** the full-game balance bot finishes all 36 levels with the fake ad service always returning "no ad available", proving ads are never required to progress.
- Build-size difference caused by the SDK is reported.

**Playtest**
- Play 10 levels on Android with test ads.
- Check: does any ad feel intrusive or badly timed? Is the consent screen clear? Do rewarded ads feel like a fair, optional bonus?

---

## 5. Decisions needed from you, and when

| Before | Decision |
|---|---|
| M0 | Exact Unity 6.3+ version to pin; CI service (or local-only scripts). |
| M2 | UI technology: UI Toolkit (proposed) or uGUI. |
| M6 | Reference Android device for performance; web build-size budget; who the consultant is; where to recruit playtesters. |
| M11 | Languages for localization. |
| L1 | Partner look and voice direction. |
| L2 | Networking technology; local network, online, or both. |
| L3 | Ad SDK and web ad strategy. |

---

## 6. Balance targets (initial)

Measured with the balance simulator, single-parent mode, Normal difficulty, at least 500 seeded runs per level.

| Bot | Chapter 1 | Chapters 2-4 | Chapters 5-6 |
|---|---|---|---|
| Greedy, 1-star rate | 95%+ | 85%+ | 75%+ |
| Joyful, 1-star rate | at least Greedy's | at least Greedy's | at least Greedy's |
| Joyful, 3-star rate | 40-70% | 30-60% | 25-50% |
| Sloppy, 1-star rate | 50-80% | 30-60% | 20-50% |
| Idle | 0% | 0% | 0% |

Partner mode (L1) and two-player mode (L2) must stay within 5 percentage points of these rates.

Bot numbers only check that levels are winnable, fair and consistent; human playtests decide whether they are fun. Targets will be adjusted after the M1 and M6 playtests.

---

## 7. Main risks

| Risk | Mitigation |
|---|---|
| Real-time care sim feels stressful rather than joyful. | Joy system in M4 before content production; pillar check in the balance simulator; external playtest at M6. |
| A single parent feels overwhelmed without a partner. | Levels tuned for one parent from M1; *Call for help* and Relaxed difficulty; the M6 questionnaire asks whether players missed a partner. |
| Adding the partner later breaks the base game. | Parent-controller interface from M1; base-game regression check against the M12 reference results in L1. |
| Partner AI feels annoying or does too much. | Utility AI with share limits verified by simulator (L1); quirks with frequency bands. |
| Web build too large or slow on mobile browsers. | Size budget enforced from M0; test on itch.io from the first milestone. |
| 36 levels is a lot of content. | Data-driven levels, reusable mini-games, routine levels from the event pool; scope can drop routine levels if needed. |
| Inaccurate parenting facts. | Consultant review at M6 and before release. |
