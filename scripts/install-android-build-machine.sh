#!/usr/bin/env bash
set -euo pipefail

# Installs the toolchain used by the Android GitHub Actions build and local APK
# builds. This script is intentionally non-interactive and never starts an AVD.

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

: "${XPSCRIPT_TOOLS_ROOT:=$HOME/.xpscript-tools}"
: "${DOTNET_ROOT:=$XPSCRIPT_TOOLS_ROOT/dotnet}"
: "${ANDROID_SDK_ROOT:=$XPSCRIPT_TOOLS_ROOT/android-sdk}"
: "${ANDROID_HOME:=$ANDROID_SDK_ROOT}"
: "${JAVA_HOME:=$XPSCRIPT_TOOLS_ROOT/jdk21}"
: "${ANDROID_API_LEVEL:=30}"
: "${ANDROID_BUILD_TOOLS_VERSION:=35.0.0}"
: "${ANDROID_CMDLINE_TOOLS_VERSION:=13114758}"
: "${ANDROID_AVD_NAME:=Pixel_5}"
: "${ANDROID_SYSTEM_IMAGE:=system-images;android-30;google_apis;x86_64}"

log() { printf '[android-machine] %s\n' "$*"; }
die() { printf '[android-machine] ERROR: %s\n' "$*" >&2; exit 1; }

command -v curl >/dev/null || die "curl is required"
command -v unzip >/dev/null || die "unzip is required"
command -v tar >/dev/null || die "tar is required"

mkdir -p "$XPSCRIPT_TOOLS_ROOT" "$ANDROID_SDK_ROOT" "$HOME/.android"

if [[ ! -x "$DOTNET_ROOT/dotnet" ]]; then
  log "Installing .NET 10 SDK"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$XPSCRIPT_TOOLS_ROOT/dotnet-install.sh"
  bash "$XPSCRIPT_TOOLS_ROOT/dotnet-install.sh" --channel 10.0 --install-dir "$DOTNET_ROOT" --no-path
fi

if [[ ! -x "$JAVA_HOME/bin/java" ]]; then
  log "Installing Temurin JDK 21"
  jdk_archive="$XPSCRIPT_TOOLS_ROOT/jdk21.tar.gz"
  curl -fsSL -o "$jdk_archive" \
    https://api.adoptium.net/v3/binary/latest/21/ga/linux/x64/jdk/hotspot/normal/eclipse
  rm -rf "$XPSCRIPT_TOOLS_ROOT/jdk21.extract"
  mkdir "$XPSCRIPT_TOOLS_ROOT/jdk21.extract"
  tar -xzf "$jdk_archive" -C "$XPSCRIPT_TOOLS_ROOT/jdk21.extract"
  jdk_dir="$(find "$XPSCRIPT_TOOLS_ROOT/jdk21.extract" -mindepth 1 -maxdepth 1 -type d | head -1)"
  [[ -n "$jdk_dir" ]] || die "JDK archive did not contain a directory"
  rm -rf "$JAVA_HOME"
  mv "$jdk_dir" "$JAVA_HOME"
  rm -rf "$XPSCRIPT_TOOLS_ROOT/jdk21.extract" "$jdk_archive"
fi

sdkmanager="$ANDROID_SDK_ROOT/cmdline-tools/latest/bin/sdkmanager"
if [[ ! -x "$sdkmanager" ]]; then
  log "Installing Android command-line tools"
  archive="$XPSCRIPT_TOOLS_ROOT/android-cli.zip"
  curl -fsSL -o "$archive" \
    "https://dl.google.com/android/repository/commandlinetools-linux-${ANDROID_CMDLINE_TOOLS_VERSION}_latest.zip"
  rm -rf "$XPSCRIPT_TOOLS_ROOT/android-cli.extract"
  mkdir -p "$XPSCRIPT_TOOLS_ROOT/android-cli.extract"
  unzip -q "$archive" -d "$XPSCRIPT_TOOLS_ROOT/android-cli.extract"
  rm -rf "$ANDROID_SDK_ROOT/cmdline-tools/latest"
  mkdir -p "$ANDROID_SDK_ROOT/cmdline-tools/latest"
  mv "$XPSCRIPT_TOOLS_ROOT/android-cli.extract/cmdline-tools"/* "$ANDROID_SDK_ROOT/cmdline-tools/latest/"
  rm -rf "$XPSCRIPT_TOOLS_ROOT/android-cli.extract" "$archive"
fi

export DOTNET_ROOT ANDROID_SDK_ROOT ANDROID_HOME JAVA_HOME
export PATH="$DOTNET_ROOT:$JAVA_HOME/bin:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$ANDROID_SDK_ROOT/emulator:$PATH"

log "Accepting Android SDK licenses"
yes | sdkmanager --licenses >/dev/null || true
log "Installing Android SDK packages"
sdkmanager --install \
  "platform-tools" \
  "platforms;android-${ANDROID_API_LEVEL}" \
  "build-tools;${ANDROID_BUILD_TOOLS_VERSION}" \
  "emulator" \
  "$ANDROID_SYSTEM_IMAGE"

if ! avdmanager list avd 2>/dev/null | grep -q "Name: ${ANDROID_AVD_NAME}$"; then
  log "Creating AVD ${ANDROID_AVD_NAME} (the emulator is not started)"
  echo no | avdmanager create avd \
    --force \
    --name "$ANDROID_AVD_NAME" \
    --package "$ANDROID_SYSTEM_IMAGE" \
    --device "pixel_5" \
    --sdcard 512M
fi

log "Installing .NET Android workload"
"$DOTNET_ROOT/dotnet" workload install android --skip-manifest-update --disable-parallel

cat > "$XPSCRIPT_TOOLS_ROOT/android-build-env.sh" <<EOF
export DOTNET_ROOT="$DOTNET_ROOT"
export ANDROID_SDK_ROOT="$ANDROID_SDK_ROOT"
export ANDROID_HOME="$ANDROID_HOME"
export JAVA_HOME="$JAVA_HOME"
export PATH="$DOTNET_ROOT:$JAVA_HOME/bin:$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$ANDROID_SDK_ROOT/emulator:\$PATH"
EOF

log "Toolchain installed"
"$DOTNET_ROOT/dotnet" --version
sdkmanager --version
adb version | head -1
java -version 2>&1 | head -1
log "AVD configured: ${ANDROID_AVD_NAME} (not started)"
log "Source ${repo_root} can now use scripts/compile-android-uiform-controls.sh"
