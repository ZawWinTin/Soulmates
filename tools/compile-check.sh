#!/usr/bin/env bash
# Compile the project with its own Unity version, headless (checkpoint 13). Prints every C# error
# and warning; exits non-zero on errors. Takes about a minute. Close the Unity editor first.
set -euo pipefail
cd "$(dirname "$0")/.."
version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
unity="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
[ -x "$unity" ] || { echo "Unity $version not found at $unity (install it with Unity Hub)." >&2; exit 2; }
if [ -e Temp/UnityLockfile ] && pgrep -f "Unity.app/Contents/MacOS/Unity.*$(pwd)" >/dev/null; then
    echo "The Unity editor has this project open. Close it, or check the editor Console instead." >&2
    exit 2
fi
log=$(mktemp -t soulmates-compile)
status=0
"$unity" -batchmode -nographics -quit -projectPath . -logFile "$log" || status=$?
grep -E "(error|warning) CS[0-9]+" "$log" | sed 's/^.*Assets/Assets/' | sort -u || true
echo "warnings: $(grep -E 'warning CS[0-9]+' "$log" | sort -u | wc -l | xargs) · errors: $(grep -E 'error CS[0-9]+' "$log" | sort -u | wc -l | xargs) · full log: $log"
exit $status
