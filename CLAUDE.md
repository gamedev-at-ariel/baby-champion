# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository. It is read automatically at the start of any session.

The milestone plan is `3-plan.md`; each milestone there has a *Notes* block (decisions, deviations, deferred items) worth reading first. Implemented milestones are ticked.

Layout:
- `Assets/Scripts/Simulation` — pure C# rules, assembly `BabyChampion.Simulation`, **no UnityEngine
  reference** (an editor test enforces this). The day simulation is `CareSimulation` (not
  `Simulation`, which would clash with the namespace); data classes `AgeRateTable`, `PriceList`,
  `Tuning`; saves and data export use its own `JsonCodec`; test bots and `BalanceRunner` in `Bots/`.
- `Assets/Scripts/Game` — Unity-side game code (`BabyChampion.Game`): data-asset wrappers
  (`Data/`) and the M1 debug scene runner (`DebugDay/`).
- `Assets/Data` — the tuning data (`AgeRates`, `Prices`, `Tuning` assets). Change values through
  `bc_import_data` or eval, never by editing the `.asset` YAML.
- `Assets/Editor` — editor tools and `[CliCommand]`s (`BabyChampion.Editor`): `ProjectSetup.cs`
  (`bc_setup_*`), `DataAssets.cs` (`bc_setup_data`, `bc_export_data`, `bc_import_data`),
  `BalanceSim.cs` (`bc_balance_sim`), `DebugDaySetup.cs` (`bc_setup_debug_scene`),
  `DataValidator.cs` (`bc_validate_data`), `VersionStamp.cs`, `AndroidBuildEnvironment.cs`,
  `BuildFailureProbe.cs`.
- `Assets/Tests/{EditMode,PlayMode}` — test assemblies.
- `Assets/Settings/Build Profiles` — *Web Dev*, *Web Release*, *Android Dev*, *Android Release*.
- `tools/` — `unity.sh`/`unity.ps1` (batch-mode wrapper) and `ci.sh`/`ci.ps1` (full pipeline).
- `docs/cli-setup.md` — machine setup and CLI commands; `docs/build-size-baseline.md` — size baseline.

## The `unity` CLI

The Unity CLI is the point of this repo, not an incidental tool — prefer it over hand-editing scene/asset YAML or launching the GUI.

`unity` is on PATH. It installs editors, creates projects, builds, tests, and —
most importantly — **drives a running Editor live**.

**Read the bundled agent skill before doing non-trivial work:** `unity skill show` prints a task-oriented guide (~630 lines); `unity skill show --list` lists its reference files and `unity skill show --path <file>` prints one. It covers the Play-mode verification loop, targeting one of several Editors, and Safe Mode recovery — details not repeated here.

```bash
unity templates list                      # template IDs for an editor version
unity projects new <name> --path . --template com.unity.template.3d --editor-version 6000.3.25f1
unity projects info                       # project details for the cwd
unity editors                             # installed editors (6000.2.8f1, 6000.3.25f1 locally)
unity status                              # connected Editors: port, state, project, PID
unity doctor                              # diagnose the local environment
```

### Driving a running Editor

Requires the project's `com.unity.pipeline` package — add it once with `unity pipeline install`.

```bash
unity status                              # state must read "ready"
unity command                             # list commands the Editor exposes
unity command editor_play                 # enter Play mode
unity command eval 'new UnityEngine.GameObject("Joe");'   # run arbitrary C#
unity recompile                           # recompile scripts, report compile errors
```

Pass `--project-path <path>` to every Editor-driving command whenever more than one Editor may be
running; without it the target follows the shell's cwd and can mismatch (`AMBIGUOUS_EDITOR`).

Entering Play mode is not proof the game runs — an unfocused Editor can freeze at frame 1 while
`unity status` still reports it playing. Follow the skill's verification loop.

If the Editor booted with C# compile errors it starts in **Safe Mode**, the Pipeline package never
loads, and `status`/`command`/`list`/`recompile` cannot connect at all. Confirm with
`unity pipeline list`, fix the compile errors, restart the Editor — do not fall back to blind
file-editing.

### Build and test

```bash
unity build                               # batch-mode build
unity test                                # whole suite; writes test-results.xml (NUnit)
unity test --mode EditMode                # or PlayMode
unity test --filter <pattern>             # run a single test / subset
unity test --rerun-failed                 # only what failed last run
unity test --affected --since <ref>       # only tests a change can reach
```

### Batch pipeline (`tools/`)

Full details in `3-plan.md` Section 2 and `docs/cli-setup.md`. Key points:

```bash
tools/ci.sh                    # tests + validation + Web Release + Android Dev APK; stops on first failure
tools/ci.sh --skip-builds      # tests and validation only
tools/ci.sh --release          # Android Release AAB instead of the APK
tools/ci.sh --verify-build-failure   # proves a failed build is caught
tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run
```

- **Batch runs need the interactive Editor closed** (one Editor per project). While it is open, use
  the live equivalents: `unity recompile --strict`, `unity command run_tests -- --mode editor`,
  `unity command bc_validate_data`, `unity command bc_balance_sim`. PlayMode tests in the open
  Editor only run asynchronously (`run_tests -- --mode playmode --async_tests true`, then poll
  `test_status`); the synchronous call reports 0 tests. Never log errors into the Editor while
  they run (e.g. by calling a command that does not exist): it fails the running test.
- `editor_set_run_in_background` does not exist in this Pipeline version. Setting
  `Application.runInBackground` via eval also flips `PlayerSettings.runInBackground` in memory —
  set it back to `false` after Play-mode checks.
- **Unity can exit 0 from a failed `-build`.** A build counts only if the log says
  `Build Finished, Result: Success.` and the output file exists — `tools/ci.sh` checks both.
- Builds use Unity 6 Build Profiles (`-activeBuildProfile ... -build`), not a custom build script.
  Upload **Web Release** to itch.io; Development web builds are uncompressed (~107 MB vs ~16 MB).
- Outputs go to `artifacts/` (test XML, logs, `validation.txt`, `balance/*.csv`, `screens/`) and
  `builds/`.

### Useful global flags

`--json` / `--format <human|json|tsv|ndjson|github>` for parseable output, `--no-banner` and
`--quiet` to keep logs clean, `--non-interactive` to disable prompts, `--verbose` for stack traces
on failure.

## Conventions

- Unity's `.gitignore` already excludes `Library/`, `Temp/`, `Logs/`, `Build(s)/`, `UserSettings/`,
  `.utmp/`, and generated `*.csproj`/`*.sln`, plus the project's `artifacts/`. Never commit those;
  never hand-edit them.
- `.meta` files are part of the source — commit them alongside the assets they describe.

# Project guidelines

## Stack
- Unity 6000.3.25f1 (pinned), URP pipeline, new Input System only (legacy Input Manager disabled).
- Targets: Web (itch.io) and Android (app ID `com.erelsegalhalevi.babychampion`, IL2CPP/ARM64, landscape).
- C# scripts live in Assets/Scripts/, one folder per system.
- Game spec in 2-design-detailed.md (revision history in GameDesignRevisionHistory.md).
  Milestones in 3-plan.md. (`PLAN.md` and `1-design-raw.md` are older drafts.)

## Architecture rules (from 3-plan.md)
- **Simulation first.** Core rules (meters, time, economy, Joy) live in `BabyChampion.Simulation`
  as plain C#, testable headlessly; the Game assembly only presents and drives it.
- **Parent-controller interface (from M1).** Every parent action goes through it — the player, test
  bots, and later the partner AI (L1) and second player (L2). Never let the simulation be mutated
  around it.
- **Seeded randomness.** The simulation uses a seeded RNG; tests and balance runs pass a seed so
  every result is reproducible.
- **Every `-executeMethod` tool** returns a non-zero exit code on failure **and has a `[CliCommand]`
  twin** (`bc_*`) so it also runs in the open Editor.
- Game data (rates, prices, levels) lives in ScriptableObjects and is checked by the data validator;
  extend the validator whenever you add a data type.
- PlayMode input tests use the Input System `InputTestFixture`; add simulated devices **after** the
  test scene has loaded (devices added before the load lose their state in batch mode).
- Development builds accept `-level N -timescale X -seed S -skipIntro` (URL params on Web).

## Working with the editor
- The editor is usually open with the Pipeline package connected. Check with `unity status`.
- Use the Unity CLI for anything that touches the editor. Never hand-edit .unity, .prefab or .asset YAML.
- Create and change scenes, GameObjects, components and prefabs with `unity command eval` or `eval_file`.
- For setup you will repeat, write a static method with [CliCommand] in Assets/Editor/ and call it with `unity command <name>`.
- Save scenes after changing them (EditorSceneManager.SaveOpenScenes).

## Definition of done for every change
1. `unity recompile --strict` passes with no errors or warnings.
2. `unity test` passes (exit code 0). Add or update tests for new game logic.
3. For gameplay changes: enter Play mode via eval, check the state you expect, report it, and exit Play mode.
4. Summarize what changed and what I should playtest by hand.

When a milestone is finished, also run its *CLI verification* list from 3-plan.md (normally a green `tools/ci.sh` with the Editor closed — ask me before closing it), then mark it ✅ in 3-plan.md with the date and version and add a *Notes* block (decisions, deviations, deferred).

## Rules
- Work on one milestone from 3-plan.md at a time. Don't start the next one unasked.
- Some milestones need a decision from me first (3-plan.md Section 5) — ask, don't assume.
- Don't commit. I commit after playtesting.
- If the editor isn't connected, say so and stop. Don't fall back to editing scene files.
- Use placeholder primitives for art (cubes, capsules, solid colours).
