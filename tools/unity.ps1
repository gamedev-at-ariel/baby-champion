# Windows version of tools/unity.sh: runs the project's pinned Unity editor in batch mode with
#   -batchmode -projectPath <project root> -logFile -
# and passes every other argument through, e.g.
#   tools\unity.ps1 -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run
#
# Editor lookup order: $env:UNITY_EDITOR, `unity editors path <version>`, the default Hub folder.
# Close the interactive Editor first: Unity cannot open a project that is already open.
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$VersionLine = Get-Content (Join-Path $Root 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' }
$Version = ($VersionLine -replace '^m_EditorVersion:\s*', '').Trim()

function Find-Editor {
    if ($env:UNITY_EDITOR) { return $env:UNITY_EDITOR }
    if (Get-Command unity -ErrorAction SilentlyContinue) {
        $dir = (& unity editors path $Version --no-banner --quiet 2>$null | Select-Object -Last 1)
        if ($dir) {
            $exe = Join-Path $dir.Trim() 'Editor\Unity.exe'
            if (Test-Path $exe) { return $exe }
        }
    }
    $exe = "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
    if (Test-Path $exe) { return $exe }
    return $null
}

$Editor = Find-Editor
if (-not $Editor) {
    Write-Error "tools/unity.ps1: Unity $Version not found. Install it (unity install $Version --module android --module webgl) or set UNITY_EDITOR."
    exit 2
}

Push-Location $Root
try {
    # Pipe so PowerShell waits for Unity and passes its log through.
    & $Editor -batchmode -projectPath $Root -logFile - @args | Out-Host
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
