#!/usr/bin/env bash
set -euo pipefail

apk="out/android-uiform-emulator.apk"
log="android-uiform-logcat.txt"

capture_logs() {
  status=$?
  timeout 15s adb logcat -d > "$log" 2>/dev/null || true
  if [ "$status" -ne 0 ]; then
    echo "APP_FAILURE_LOGCAT"
    timeout 10s adb logcat -d -b crash || true
    timeout 10s adb logcat -d -s XPScript AndroidRuntime || true
    tail -n 200 "$log" || true
  fi
}
trap capture_logs EXIT

adb wait-for-device
timeout 120s bash -c 'until [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d "\r")" = "1" ]; do sleep 2; done'
echo "BOOT_OK"

installed=false
for attempt in 1 2 3 4; do
  if timeout 60s adb install --no-streaming -r "$apk"; then
    installed=true
    break
  fi
  echo "APK install attempt $attempt failed; waiting for package service"
  adb shell getprop sys.boot_completed || true
  sleep 10
done
test "$installed" = true
echo "INSTALL_OK"

aapt_path="$(find "${ANDROID_HOME:?ANDROID_HOME is required}/build-tools" -type f -name aapt | sort -V | tail -n 1)"
test -x "$aapt_path"
package="$("$aapt_path" dump badging "$apk" | sed -n "s/^package: name='\([^']*\)'.*/\1/p")"
test -n "$package"
adb logcat -c
activity="$("$aapt_path" dump badging "$apk" | sed -n "s/^launchable-activity: name='\([^']*\)'.*/\1/p")"
test -n "$activity"
timeout 30s adb shell am start -W -S -n "$package/$activity" --ez xpscript.appdebug true
echo "ACTIVITY_START_RETURNED"
timeout 120s bash -c 'until adb logcat -d | grep -q "ANDROID-UIFORM=start"; do sleep 2; done'
timeout 15s adb logcat -d > "$log"
grep -q 'ANDROID-UIFORM=start' "$log"
echo "APP_START_OK"
