#!/usr/bin/env bash
set -euo pipefail

# Install the runtimes used by .github/workflows/android-build.yml.
# The Codex Cloud install script should run:
#   bash ./.codex/install-android-environment.sh

DOTNET_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
SUDO=()

if [ "$(id -u)" -ne 0 ]; then
  if command -v sudo >/dev/null 2>&1 && sudo -n true 2>/dev/null; then
    SUDO=(sudo -n)
  else
    SUDO=()
  fi
fi

if ! java -version 2>&1 | grep -Eq 'version "17\.|openjdk version "17\.'; then
  if command -v apt-get >/dev/null 2>&1 && { [ "$(id -u)" -eq 0 ] || [ "${#SUDO[@]}" -gt 0 ]; }; then
    "${SUDO[@]}" apt-get update
    "${SUDO[@]}" apt-get install -y openjdk-17-jdk
  else
    echo "JDK 17 is required. Install openjdk-17-jdk, then rerun this script." >&2
    exit 1
  fi
fi

if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  mkdir -p "$DOTNET_DIR"
  curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_DIR"
fi

export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
export JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$(command -v javac)")")")"
export ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$HOME/.android}"

for profile in "$HOME/.bashrc" "$HOME/.profile"; do
  touch "$profile"
  grep -Fqx 'export DOTNET_ROOT="$HOME/.dotnet"' "$profile" || printf '\nexport DOTNET_ROOT="$HOME/.dotnet"\n' >> "$profile"
  grep -Fqx 'export PATH="$DOTNET_ROOT:$PATH"' "$profile" || printf 'export PATH="$DOTNET_ROOT:$PATH"\n' >> "$profile"
  grep -Fqx 'export JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$(command -v javac)")")")"' "$profile" || printf 'export JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$(command -v javac)")")")"\n' >> "$profile"
done

dotnet workload install android
dotnet --info
java -version
dotnet workload list
