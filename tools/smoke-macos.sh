#!/bin/bash
# Starts a packaged "Usage Widget.app", waits for its window, saves a screenshot and quits it the way the
# app menu does. Fails if no window appears or the app does not exit. Usage: tools/smoke-macos.sh <app> <screenshot.png>
set -euo pipefail
app="$1"
shot="$2"
here="$(cd "$(dirname "$0")" && pwd)"

open "$app"
pid=""
for _ in $(seq 1 30); do
    pid="$(pgrep -f "$app/Contents/MacOS/UsageWidget" | head -1 || true)"
    [ -n "$pid" ] && swift "$here/macos-window.swift" "$pid" >/dev/null 2>&1 && break
    sleep 1
done
[ -n "$pid" ] || { echo "App did not start"; exit 1; }
swift "$here/macos-window.swift" "$pid" || { echo "No window for pid $pid"; kill "$pid"; exit 1; }
sleep 5
screencapture -x "$shot" || echo "Screenshot not allowed on this machine"

osascript -e 'quit app "Usage Widget"'
for _ in $(seq 1 15); do
    kill -0 "$pid" 2>/dev/null || { echo "Quit cleanly"; exit 0; }
    sleep 1
done
echo "App did not quit"
kill "$pid"
exit 1
