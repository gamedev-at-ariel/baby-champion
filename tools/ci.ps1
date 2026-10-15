# Windows version of tools/ci.sh. Runs, in order, stopping at the first failure:
#   EditMode tests -> PlayMode tests -> data validation -> balance simulator -> screenshots -> Web build -> Android build -> build sizes
#
# Usage: tools\ci.ps1 [-Release] [-WebDev] [-SkipBuilds] [-VerifyBuildFailure]
#   (default)  build Web Release (the one for itch.io) and Android Dev (APK)
#   -Release   build Android Release (AAB) instead of Android Dev
#   -WebDev    build Web Dev (uncompressed, for local debugging) instead of Web Release
# Close the interactive Unity Editor first: batch mode cannot open a project that is already open.
param(
    [switch]$Release,
    [switch]$WebDev,
    [switch]$SkipBuilds,
    [switch]$VerifyBuildFailure
)
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$Unity = Join-Path $PSScriptRoot 'unity.ps1'
$Profiles = 'Assets/Settings/Build Profiles'
New-Item -ItemType Directory -Force 'artifacts/logs', 'builds' | Out-Null

function Step($text) { Write-Host ''; Write-Host "=== $text ===" }
function Fail($text) { Write-Host "CI FAILED: $text" -ForegroundColor Red; exit 1 }

function Invoke-Unity($name, [string[]]$unityArgs) {
    $log = "artifacts/logs/$name.log"
    # Windows PowerShell turns any native stderr line (e.g. "debugger-agent: Unable to listen on 3456")
    # into a terminating error under 'Stop'; the exit code and log checks decide success instead.
    $ErrorActionPreference = 'Continue'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $Unity @unityArgs *> $log
    $code = $LASTEXITCODE
    Write-Host "${name}: exit code $code (log: $log)"
    return $code
}

# Unity can exit 0 from a failed -build, so a build only counts when its log carries the success verdict.
function Test-BuildSucceeded($name) { Select-String -Path "artifacts/logs/$name.log" -SimpleMatch 'Build Finished, Result: Success.' -Quiet }

function Test-Xml($path) {
    if (-not (Test-Path $path)) { Fail "$path was not written" }
    $run = ([xml](Get-Content $path -Raw)).'test-run'
    Write-Host "${path}: total=$($run.total) passed=$($run.passed) failed=$($run.failed)"
    if ($run.failed -ne '0') { Fail "$path reports failed tests" }
}

if (Get-Command unity -ErrorAction SilentlyContinue) {
    $status = (& unity status --no-banner 2>$null) -join "`n"
    if ($status -like "*$(Split-Path -Leaf $Root)*") { Fail "a Unity Editor has this project open. Close it first (unity close `"$Root`")." }
}

if ($VerifyBuildFailure) {
    Step 'Verify that a failed build returns non-zero'
    $env:BABYCHAMPION_FAIL_BUILD = '1'
    try { $code = Invoke-Unity 'build-failure-probe' @('-quit', '-activeBuildProfile', "$Profiles/Web Dev.asset", '-build', 'builds/failure-probe') }
    finally { Remove-Item Env:BABYCHAMPION_FAIL_BUILD }
    if (Test-BuildSucceeded 'build-failure-probe') { Fail 'the deliberately failed build was reported as a success' }
    if (-not (Select-String -Path 'artifacts/logs/build-failure-probe.log' -SimpleMatch 'Build Finished, Result: Failure.' -Quiet)) { Fail 'the probe build did not run (no build verdict in its log)' }
    Write-Host "Unity's exit code for the failed build: $code"
    Write-Host 'OK: the failed build is detected (its log says Result: Failure), so tools/ci.ps1 would fail.'
    exit 0
}

Step 'EditMode tests'
if ((Invoke-Unity 'editmode' @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', 'artifacts/editmode.xml')) -ne 0) { Fail 'EditMode tests' }
Test-Xml 'artifacts/editmode.xml'

Step 'PlayMode tests'
if ((Invoke-Unity 'playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testResults', 'artifacts/playmode.xml')) -ne 0) { Fail 'PlayMode tests' }
Test-Xml 'artifacts/playmode.xml'

Step 'Data validation'
if ((Invoke-Unity 'validation' @('-nographics', '-quit', '-executeMethod', 'BabyChampion.Editor.DataValidator.Run')) -ne 0) { Fail 'data validation' }
Get-Content 'artifacts/validation.txt'

# Every built level with every bot; fails when a 3-plan.md Section 6 target is missed.
# Breastfeeding plays like "both", so formula is the only other case worth running.
foreach ($level in '1', '2') {
    foreach ($feeding in 'both', 'formula') {
        Step "Balance simulator (level $level, $feeding)"
        if ((Invoke-Unity "balance-$level-$feeding" @('-nographics', '-quit', '-executeMethod', 'BabyChampion.Editor.BalanceSim.Run', '-level', $level, '-bot', 'all', '-runs', '500', '-seed', '1', '-feeding', $feeding)) -ne 0) { Fail "balance targets (level $level, $feeding; see artifacts/balance/)" }
        Select-String -Path "artifacts/logs/balance-$level-$feeding.log" -Pattern '\[BalanceSim\] (.*)' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Select-Object -Unique
    }
}

# Every room, the HUD, the radial menu and the phone at phone and desktop resolutions, for review.
# Needs graphics and enters Play mode, so no -nographics and no -quit: the tool exits by itself.
Step 'Screenshots'
if ((Invoke-Unity 'screenshots' @('-executeMethod', 'BabyChampion.Editor.ScreenshotTool.Run')) -ne 0) { Fail 'screenshots (see artifacts/logs/screenshots.log)' }
Select-String -Path 'artifacts/logs/screenshots.log' -Pattern '\[Screenshots\] (.*)' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Select-Object -Unique

if ($SkipBuilds) { Write-Host ''; Write-Host 'CI PASSED (builds skipped)'; exit 0 }

# Unity does not compress Development web builds, so Web Release is the default.
if ($WebDev) { $WebProfile = 'Web Dev' } else { $WebProfile = 'Web Release' }
if ($Release) { $AndroidProfile = 'Android Release'; $AndroidOut = 'builds/android/BabyChampion.aab' }
else { $AndroidProfile = 'Android Dev'; $AndroidOut = 'builds/android/BabyChampion.apk' }

Step "Web build ($WebProfile)"
if (Test-Path 'builds/web') { Remove-Item -Recurse -Force 'builds/web' }
if ((Invoke-Unity 'build-web' @('-quit', '-activeBuildProfile', "$Profiles/$WebProfile.asset", '-build', 'builds/web')) -ne 0) { Fail 'web build' }
if (-not (Test-BuildSucceeded 'build-web')) { Fail 'web build (log: Build Finished, Result is not Success)' }
if (-not (Test-Path 'builds/web/index.html')) { Fail 'web build produced no index.html' }

Step "Android build ($AndroidProfile)"
New-Item -ItemType Directory -Force 'builds/android' | Out-Null
if (Test-Path $AndroidOut) { Remove-Item -Force $AndroidOut }
if ((Invoke-Unity 'build-android' @('-quit', '-activeBuildProfile', "$Profiles/$AndroidProfile.asset", '-build', $AndroidOut)) -ne 0) { Fail 'android build' }
if (-not (Test-BuildSucceeded 'build-android')) { Fail 'android build (log: Build Finished, Result is not Success)' }
if (-not (Test-Path $AndroidOut)) { Fail "android build produced no $AndroidOut" }

Step 'Build sizes'
function KiB($path) { [math]::Ceiling(((Get-ChildItem $path -Recurse -File | Measure-Object Length -Sum).Sum) / 1KB) }
$WebKb = KiB 'builds/web'
$WebDownloadKb = KiB 'builds/web/Build'
$AndroidKb = KiB $AndroidOut
Write-Host "Web ($WebProfile):         $WebKb KiB total, $WebDownloadKb KiB download (Build/)"
Write-Host "Android ($AndroidProfile): $AndroidKb KiB ($AndroidOut)"
$csv = 'artifacts/build-sizes.csv'
if (-not (Test-Path $csv)) { 'date,commit,web_profile,web_total_kib,web_download_kib,android_profile,android_kib' | Out-File $csv -Encoding utf8 }
$commit = (& git rev-parse --short HEAD 2>$null); if (-not $commit) { $commit = 'none' }
"$((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')),$commit,$WebProfile,$WebKb,$WebDownloadKb,$AndroidProfile,$AndroidKb" | Out-File $csv -Append -Encoding utf8

Write-Host ''
Write-Host 'CI PASSED'
