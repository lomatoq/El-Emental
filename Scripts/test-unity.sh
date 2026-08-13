#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
UNITY_PATH="$(printenv UNITY_EDITOR_PATH || true)"
RESULTS_PATH="$PROJECT_PATH/TestResults"

if [ -z "$UNITY_PATH" ]; then
  echo "Set UNITY_EDITOR_PATH to the Unity executable." >&2
  exit 2
fi

mkdir -p "$RESULTS_PATH"

for PLATFORM in EditMode PlayMode; do
  "$UNITY_PATH" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_PATH" \
    -runTests \
    -testPlatform "$PLATFORM" \
    -testResults "$RESULTS_PATH/$PLATFORM.xml" \
    -logFile "$RESULTS_PATH/$PLATFORM.log"
done
