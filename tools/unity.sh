#!/usr/bin/env bash
# Runs the project's pinned Unity editor in batch mode with the flags every call needs:
#   -batchmode -projectPath <project root> -logFile -
# Everything else is passed through, e.g.
#   tools/unity.sh -nographics -quit -executeMethod BabyChampion.Editor.DataValidator.Run
#
# The editor version comes from ProjectSettings/ProjectVersion.txt. The executable is found,
# in order, from: $UNITY_EDITOR (full path to the executable), `unity editors path <version>`
# (Unity CLI), then the default Unity Hub install folders.
#
# Unity refuses to open a project that another Editor already has open, so close the
# interactive Editor before calling this script.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"

editor_exe_in() {
  local dir="$1"
  for candidate in "$dir/Editor/Unity.exe" "$dir/Unity.app/Contents/MacOS/Unity" "$dir/Editor/Unity"; do
    if [[ -f "$candidate" ]]; then echo "$candidate"; return 0; fi
  done
  return 1
}

find_editor() {
  if [[ -n "${UNITY_EDITOR:-}" ]]; then echo "$UNITY_EDITOR"; return 0; fi

  if command -v unity >/dev/null 2>&1; then
    local dir
    dir="$(unity editors path "$VERSION" --no-banner --quiet 2>/dev/null | tr -d '\r' | tail -n 1 || true)"
    if [[ -n "$dir" ]]; then
      if command -v cygpath >/dev/null 2>&1; then dir="$(cygpath -u "$dir")"; fi
      editor_exe_in "$dir" && return 0
    fi
  fi

  for dir in "/c/Program Files/Unity/Hub/Editor/$VERSION" \
             "/Applications/Unity/Hub/Editor/$VERSION" \
             "$HOME/Unity/Hub/Editor/$VERSION"; do
    editor_exe_in "$dir" && return 0
  done
  return 1
}

EDITOR_EXE="$(find_editor)" || {
  echo "tools/unity.sh: Unity $VERSION not found. Install it (unity install $VERSION --module android --module webgl) or set UNITY_EDITOR." >&2
  exit 2
}

PROJECT="$ROOT"
if command -v cygpath >/dev/null 2>&1; then PROJECT="$(cygpath -w "$ROOT")"; fi

cd "$ROOT"
exec "$EDITOR_EXE" -batchmode -projectPath "$PROJECT" -logFile - "$@"
