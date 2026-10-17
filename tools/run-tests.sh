#!/usr/bin/env bash
# Run the EditMode tests headless with the project's own Unity version (a minute or two).
# Prints each test's result; exits non-zero if any fail. Close the Unity editor first.
set -euo pipefail
cd "$(dirname "$0")/.."
version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
unity="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
[ -x "$unity" ] || { echo "Unity $version not found at $unity (install it with Unity Hub)." >&2; exit 2; }
if [ -e Temp/UnityLockfile ] && pgrep -f "Unity.app/Contents/MacOS/Unity.*$(pwd)" >/dev/null; then
    echo "The Unity editor has this project open. Close it, or use Window → General → Test Runner." >&2
    exit 2
fi
results=$(mktemp -t soulmates-tests).xml
log=$(mktemp -t soulmates-tests-log)
status=0
"$unity" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
    -testResults "$results" -logFile "$log" || status=$?
if [ -s "$results" ]; then
    grep -oE '<test-case [^>]*' "$results" | sed -E 's/.*fullname="([^"]*)".*result="([^"]*)".*/\2  \1/'
    grep -oE '<test-run [^>]*' "$results" | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' '; echo
else
    grep -E "(error|warning) CS[0-9]+" "$log" | sed 's/^.*Assets/Assets/' | sort -u || true
    echo "No test results; see $log" >&2
fi
exit $status
