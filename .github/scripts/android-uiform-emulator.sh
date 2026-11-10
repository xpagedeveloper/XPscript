#!/usr/bin/env bash
set -euo pipefail

apk="out/android-uiform-emulator.apk"
log="android-uiform-logcat.txt"

trap 'adb logcat -d > "$log" 2>/dev/null || true' EXIT

adb wait-for-device
timeout 120s bash -c 'until [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d "\r")" = "1" ]; do sleep 2; done'
timeout 180s bash -c 'until [ "$(adb shell getprop dev.bootcomplete 2>/dev/null | tr -d "\r")" = "1" ] && [ "$(adb shell getprop init.svc.bootanim 2>/dev/null | tr -d "\r")" = "stopped" ] && adb shell cmd package list packages >/dev/null 2>&1; do sleep 2; done'

installed=false
for attempt in 1 2 3 4; do
  if adb install --no-streaming -r "$apk"; then
    installed=true
    break
  fi
  echo "APK install attempt $attempt failed; waiting for package service"
  adb shell getprop sys.boot_completed || true
  sleep 10
done
test "$installed" = true

package="$(aapt dump badging "$apk" | sed -n "s/^package: name='\([^']*\)'.*/\1/p")"
test -n "$package"
adb logcat -c
adb shell monkey -p "$package" 1
timeout 120s bash -c 'until adb logcat -d | grep -q "ANDROID-UIFORM=start"; do sleep 2; done'
grep -q 'ANDROID-UIFORM=start' "$log"
