# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

The repository is an empty scaffold: `README.md`, `LICENSE` (GPL-3.0), and Unity's standard
`.gitignore`. There is no `Assets/`, `Packages/`, or `ProjectSettings/` yet — the Unity project
still has to be created. Until it exists, most commands below have nothing to act on.

Purpose (from README): *"Be a champion parent! For demonstrating game-development via Unity CLI
and Claude Code."* The Unity CLI is the point of this repo, not an incidental tool — prefer it
over hand-editing scene/asset YAML or launching the GUI.

## The `unity` CLI

`unity` (v1.0.0-beta.12) is on PATH. It installs editors, creates projects, builds, tests, and —
most importantly — **drives a running Editor live**.

**Read the bundled agent skill before doing non-trivial work:** `unity skill show` prints a
task-oriented guide (~630 lines); `unity skill show --list` lists its reference files and
`unity skill show --path <file>` prints one. It covers the Play-mode verification loop, targeting
one of several Editors, and Safe Mode recovery — details not repeated here.

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

### Useful global flags

`--json` / `--format <human|json|tsv|ndjson|github>` for parseable output, `--no-banner` and
`--quiet` to keep logs clean, `--non-interactive` to disable prompts, `--verbose` for stack traces
on failure.

## Conventions

- Unity's `.gitignore` already excludes `Library/`, `Temp/`, `Logs/`, `Build(s)/`, `UserSettings/`,
  `.utmp/`, and generated `*.csproj`/`*.sln`. Never commit those; never hand-edit them.
- `.meta` files are part of the source — commit them alongside the assets they describe.

# Project guidelines

## Stack
- Unity 6.3.x, URP pipeline, new Input System.
- C# scripts live in Assets/Scripts/, one folder per system.
- Game spec in GameDesign.md. Milestones in PLAN.md.

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

## Rules
- Work on one milestone from PLAN.md at a time. Don't start the next one unasked.
- Don't commit. I commit after playtesting.
- If the editor isn't connected, say so and stop. Don't fall back to editing scene files.
- Use placeholder primitives for art (cubes, capsules, solid colours).
