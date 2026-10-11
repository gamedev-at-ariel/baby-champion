# Running Baby Champion from the command line

How to build, test and run the project without the Unity GUI. The milestone plan (`3-plan.md`, Section 2) lists the commands. This page covers the machine setup they need.

## 1. The pinned editor

| Item | Value |
|---|---|
| Unity version | **6000.3.25f1** (from `ProjectSettings/ProjectVersion.txt`) |
| Install folder on this machine | `C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe` |
| Required modules | Web, Android (with Android SDK & NDK Tools and OpenJDK) |

Install it on a new machine with the Unity CLI:

```bash
unity install 6000.3.25f1 --module webgl --module android --module android-sdk-ndk-tools --module android-open-jdk --yes --accept-eula
unity editors --installed            # confirm
unity editors path 6000.3.25f1       # the folder tools/unity.sh uses
```

`tools/unity.sh` and `tools/unity.ps1` find the editor themselves, in this order:
1. the `UNITY_EDITOR` environment variable (the full path to `Unity.exe`), if it is set;
2. `unity editors path <version>`, using the version in `ProjectVersion.txt`;
3. the default Unity Hub folder.

Upgrading Unity means changing `ProjectVersion.txt` (open the project once in the new editor). The scripts follow automatically.

## 2. The license in batch mode

Batch mode uses the same license as the GUI. On this machine, **Unity Personal** is active and assigned to the signed-in account, so nothing extra is needed. Check with:

```bash
unity auth status
unity license status
```

On a new machine, or if batch mode exits with a license error:

```bash
unity auth login          # opens the browser once
unity license activate    # activates the Personal entitlement
```

For a CI server, use a service account instead (set `UNITY_SERVICE_ACCOUNT_ID` and `UNITY_SERVICE_ACCOUNT_SECRET`, then run `unity license activate`). See `unity skill show` → auth-license-cloud.

## 3. Close the Editor before batch runs

Unity allows only one Editor per project. If the project is open in the GUI, batch mode fails with *"another Unity instance is running with this project open"*. `tools/ci.sh` checks for this first and stops with a clear message. Close the Editor first:

```bash
unity close .            # or close it from the GUI
tools/ci.sh
unity open .             # reopen afterwards
```

While the Editor is open, the same checks can run inside it instead:
`unity recompile --strict`, `unity command run_tests`, `unity command bc_validate_data`.

## 4. Commands

| Purpose | Command |
|---|---|
| Everything, in order (builds Web Release + Android Dev APK) | `tools/ci.sh` (Windows: `tools\ci.ps1`) |
| Android Release AAB instead of the APK | `tools/ci.sh --release` (`-Release`) |
| Web Dev (uncompressed, local debugging) instead of Web Release | `tools/ci.sh --web-dev` (`-WebDev`) |
| Tests and validation only | `tools/ci.sh --skip-builds` |
| Check that a failed build returns non-zero | `tools/ci.sh --verify-build-failure` |
| EditMode tests | `tools/unity.sh -nographics -runTests -testPlatform EditMode -testResults artifacts/editmode.xml` |
| PlayMode tests | `tools/unity.sh -runTests -testPlatform PlayMode -testResults artifacts/playmode.xml` |
| Data validation | `tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run` |
| Web build | `tools/unity.sh -quit -activeBuildProfile "Assets/Settings/Build Profiles/Web Release.asset" -build builds/web` |
| Android build | `tools/unity.sh -quit -activeBuildProfile "Assets/Settings/Build Profiles/Android Dev.asset" -build builds/android/BabyChampion.apk` |

`tools/ci.sh` writes logs to `artifacts/logs/`, test results to `artifacts/*.xml`, and appends the build sizes to `artifacts/build-sizes.csv`. The baseline is in `docs/build-size-baseline.md`.

Project-setup commands (run them in the open Editor; they are idempotent):
`unity command bc_setup_all`, or one at a time: `bc_setup_player`, `bc_setup_quality`, `bc_setup_input`, `bc_setup_test_scene`, `bc_setup_build_profiles`.

## 5. Build profiles

| Profile | Platform | Output | Notes |
|---|---|---|---|
| Web Dev | Web | folder | Development build: runtime URL arguments active |
| Web Release | Web | folder | |
| Android Dev | Android | `.apk` | Development build: runtime arguments active |
| Android Release | Android | `.aab` | Signed with the debug key until a release keystore is set up (M12) |

Web builds use Gzip compression with **Decompression Fallback**, because itch.io does not send the `Content-Encoding` headers Unity's loader would otherwise need. **Unity does not compress Development builds**, so *Web Dev* is about 107 MB against about 16 MB for *Web Release* (see `docs/build-size-baseline.md`). Upload *Web Release* to itch.io. Android builds use IL2CPP and ARM64 and run in landscape only.

**Unity can exit with code 0 from a failed `-build`.** Verified in M0 with `tools/ci.sh --verify-build-failure`, which fails a build on purpose (`Assets/Editor/BuildFailureProbe.cs`). Never trust the exit code alone: `tools/ci.sh` also requires `Build Finished, Result: Success.` in the build log and checks that the output file exists.

**Android and non-ASCII user folders.** Gradle keeps its cache in `%USERPROFILE%\.gradle` by default. When that path contains non-ASCII characters (as on this machine), the Android build fails with *"Could not find or load main class com.google.prefab.cli.AppKt"*. `Assets/Editor/AndroidBuildEnvironment.cs` then points `GRADLE_USER_HOME` at `Library/Gradle` inside the project, for both GUI and batch builds. The first Android build downloads the Gradle dependencies into that folder. Setting `GRADLE_USER_HOME` yourself overrides it.

The version comes from `version.txt`. Each build is stamped `<version>+<git commit>` (for example `0.1.0+b3de17e`) by `Assets/Editor/VersionStamp.cs`. The game logs it at startup, and `Application.version` returns it.

## 6. Runtime arguments (development builds only)

Accepted: `-level N`, `-timescale X`, `-seed S`, `-skipIntro`. In M0 only `-timescale` has an effect; the others are parsed for later milestones.

**Web:** add URL parameters, for example `index.html?timescale=4&level=2`.

**Android:** pass them through the `unity` intent extra when launching with `adb`. `adb` ships with the editor at
`C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`.

```bash
adb install -r builds/android/BabyChampion.apk
adb shell am start -n com.erelsegalhalevi.babychampion/com.unity3d.player.UnityPlayerGameActivity -e unity "-timescale 4 -level 2"
adb logcat -s Unity        # shows "Baby Champion <version>" and "[DevArgs] timescale = 4"
```

## 7. Uploading the web build to itch.io

Run `tools/ci.sh` (it builds *Web Release* into `builds/web/`). Zip the *contents* of `builds/web/`, so that `index.html` sits at the top of the zip and not inside a `web/` folder, create an itch.io project of kind **HTML**, upload the zip, and tick *This file will be played in the browser*. Keep the page private (Draft or Restricted) until release.
