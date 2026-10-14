#!/usr/bin/env bash
# Full command-line check, in order, stopping at the first failure:
#   EditMode tests -> PlayMode tests -> data validation -> balance simulator -> screenshots -> Web build -> Android build -> build sizes
#
# Usage: tools/ci.sh [--release] [--web-dev] [--skip-builds] [--verify-build-failure]
#   (default)               build Web Release (the one for itch.io) and Android Dev (APK)
#   --release               build Android Release (AAB) instead of Android Dev
#   --web-dev               build Web Dev (uncompressed, for local debugging) instead of Web Release
#   --skip-builds           tests and validation only
#   --verify-build-failure  only check that a deliberately failed build returns a non-zero exit code
#
# Outputs: artifacts/ (test XML, validation report, logs, build-sizes.csv) and builds/.
# Close the interactive Unity Editor first: batch mode cannot open a project that is already open.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
UNITY="tools/unity.sh"
PROFILES="Assets/Settings/Build Profiles"

RELEASE=0; WEB_DEV=0; SKIP_BUILDS=0; VERIFY_FAILURE=0
for arg in "$@"; do
  case "$arg" in
    --release) RELEASE=1 ;;
    --web-dev) WEB_DEV=1 ;;
    --skip-builds) SKIP_BUILDS=1 ;;
    --verify-build-failure) VERIFY_FAILURE=1 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

mkdir -p artifacts/logs builds

step() { echo; echo "=== $* ==="; }
fail() { echo "CI FAILED: $*" >&2; exit 1; }

# Runs Unity, logging to artifacts/logs/<name>.log; returns Unity's exit code.
run_unity() {
  local name="$1"; shift
  local code=0
  "$UNITY" "$@" > "artifacts/logs/$name.log" 2>&1 || code=$?
  echo "$name: exit code $code (log: artifacts/logs/$name.log)"
  return $code
}

# Unity can exit 0 from a failed -build (verified in M0 with --verify-build-failure),
# so a build only counts when its log carries Unity's success verdict.
build_succeeded() { grep -q "Build Finished, Result: Success." "artifacts/logs/$1.log"; }

# Fails unless the NUnit XML exists and reports zero failed tests.
check_test_xml() {
  local xml="$1"
  [[ -f "$xml" ]] || fail "$xml was not written"
  local run
  run="$(grep -o '<test-run [^>]*>' "$xml" | head -n 1)"
  local total passed failed
  total="$(sed -n 's/.* total="\([0-9]*\)".*/\1/p' <<<"$run")"
  passed="$(sed -n 's/.* passed="\([0-9]*\)".*/\1/p' <<<"$run")"
  failed="$(sed -n 's/.* failed="\([0-9]*\)".*/\1/p' <<<"$run")"
  echo "$xml: total=$total passed=$passed failed=$failed"
  [[ "${failed:-1}" == "0" ]] || fail "$xml reports failed tests"
}

EDITORS="$(command -v unity >/dev/null 2>&1 && unity status --no-banner 2>/dev/null || true)"
PROJECT_NAME="$(basename "$ROOT")"
if [[ "${EDITORS,,}" == *"${PROJECT_NAME,,}"* ]]; then
  fail "a Unity Editor has this project open. Close it first (unity close \"$ROOT\")."
fi

if [[ $VERIFY_FAILURE == 1 ]]; then
  step "Verify that a failed build returns non-zero"
  code=0
  BABYCHAMPION_FAIL_BUILD=1 run_unity build-failure-probe -quit -activeBuildProfile "$PROFILES/Web Dev.asset" -build builds/failure-probe || code=$?
  if build_succeeded build-failure-probe; then fail "the deliberately failed build was reported as a success"; fi
  grep -q "Build Finished, Result: Failure." artifacts/logs/build-failure-probe.log || fail "the probe build did not run (no build verdict in its log)"
  echo "Unity's exit code for the failed build: $code"
  echo "OK: the failed build is detected (its log says Result: Failure), so tools/ci.sh would fail."
  exit 0
fi

step "EditMode tests"
run_unity editmode -nographics -runTests -testPlatform EditMode -testResults artifacts/editmode.xml || fail "EditMode tests"
check_test_xml artifacts/editmode.xml

step "PlayMode tests"
run_unity playmode -runTests -testPlatform PlayMode -testResults artifacts/playmode.xml || fail "PlayMode tests"
check_test_xml artifacts/playmode.xml

step "Data validation"
run_unity validation -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run || fail "data validation"
cat artifacts/validation.txt

# Newborn day (level 1) with every bot; fails when a 3-plan.md Section 6 target is missed.
# Breastfeeding plays like "both", so formula is the only other case worth running.
for feeding in both formula; do
  step "Balance simulator (level 1, $feeding)"
  run_unity "balance-$feeding" -nographics -quit -executeMethod BabyChampion.Editor.BalanceSim.Run -level 1 -bot all -runs 500 -seed 1 -feeding "$feeding" || fail "balance targets ($feeding; see artifacts/balance/)"
  grep "\[BalanceSim\]" "artifacts/logs/balance-$feeding.log" | sed 's/^.*\[BalanceSim\] //' | sort -u
done

# Every room, the HUD, the radial menu and the phone at phone and desktop resolutions, for review.
# Needs graphics and enters Play mode, so no -nographics and no -quit: the tool exits by itself.
step "Screenshots"
run_unity screenshots -executeMethod BabyChampion.Editor.ScreenshotTool.Run || fail "screenshots (see artifacts/logs/screenshots.log)"
grep "\[Screenshots\]" artifacts/logs/screenshots.log | sed 's/^.*\[Screenshots\] //' | sort -u

if [[ $SKIP_BUILDS == 1 ]]; then echo; echo "CI PASSED (builds skipped)"; exit 0; fi

# Unity does not compress Development web builds, so Web Release is the default.
if [[ $WEB_DEV == 1 ]]; then WEB_PROFILE="Web Dev"; else WEB_PROFILE="Web Release"; fi
if [[ $RELEASE == 1 ]]; then
  ANDROID_PROFILE="Android Release"; ANDROID_OUT="builds/android/BabyChampion.aab"
else
  ANDROID_PROFILE="Android Dev"; ANDROID_OUT="builds/android/BabyChampion.apk"
fi

step "Web build ($WEB_PROFILE)"
rm -rf builds/web
run_unity build-web -quit -activeBuildProfile "$PROFILES/$WEB_PROFILE.asset" -build builds/web || fail "web build"
build_succeeded build-web || fail "web build (log: Build Finished, Result is not Success)"
[[ -f builds/web/index.html ]] || fail "web build produced no index.html"

step "Android build ($ANDROID_PROFILE)"
rm -f "$ANDROID_OUT"
mkdir -p builds/android
run_unity build-android -quit -activeBuildProfile "$PROFILES/$ANDROID_PROFILE.asset" -build "$ANDROID_OUT" || fail "android build"
build_succeeded build-android || fail "android build (log: Build Finished, Result is not Success)"
[[ -f "$ANDROID_OUT" ]] || fail "android build produced no $ANDROID_OUT"

step "Build sizes"
kib() { du -sk "$1" | cut -f1; }
WEB_KB="$(kib builds/web)"
WEB_DOWNLOAD_KB="$(kib builds/web/Build)"
ANDROID_KB="$(kib "$ANDROID_OUT")"
echo "Web ($WEB_PROFILE):         ${WEB_KB} KiB total, ${WEB_DOWNLOAD_KB} KiB download (Build/)"
echo "Android ($ANDROID_PROFILE): ${ANDROID_KB} KiB ($ANDROID_OUT)"
[[ -f artifacts/build-sizes.csv ]] || echo "date,commit,web_profile,web_total_kib,web_download_kib,android_profile,android_kib" > artifacts/build-sizes.csv
echo "$(date -u +%Y-%m-%dT%H:%M:%SZ),$(git rev-parse --short HEAD 2>/dev/null || echo none),$WEB_PROFILE,$WEB_KB,$WEB_DOWNLOAD_KB,$ANDROID_PROFILE,$ANDROID_KB" >> artifacts/build-sizes.csv
if [[ -f docs/build-size-baseline.md ]]; then
  echo "Baseline: see docs/build-size-baseline.md"
fi

echo
echo "CI PASSED"
