#!/bin/bash
# Installs what CI's checks need in a Claude Code cloud container, which ships without them:
# the .NET 8 SDK, PowerShell 7 (the gate scripts), and the Godot build the engine job runs,
# with curl and unzip to fetch and unpack them.
# Local sessions (Windows, or any machine without CLAUDE_CODE_REMOTE=true) skip it.
[ "$CLAUDE_CODE_REMOTE" = "true" ] || exit 0
command -v apt-get >/dev/null 2>&1 || { echo "cloud-setup: no apt-get, nothing installed" >&2; exit 0; }
export DEBIAN_FRONTEND=noninteractive
log() { echo "cloud-setup: $*" >&2; }

# One package per install, so a name the feeds lack (powershell without Microsoft's) fails alone.
# The lists are refreshed before the first install and again once a feed is added.
fresh=""
apt_add() {
    [ -n "$fresh" ] || { apt-get update -q >/dev/null || log "apt-get update failed"; fresh=1; }
    apt-get install -y -q "$1" >/dev/null && log "installed $1" || log "apt install of $1 failed"
}

# curl fetches the Microsoft feed below, so it goes in first.
command -v curl >/dev/null 2>&1 || apt_add curl
if ! command -v pwsh >/dev/null 2>&1 && [ ! -f /etc/apt/sources.list.d/microsoft-prod.list ]; then
    deb=$(mktemp --suffix=.deb)
    . /etc/os-release
    if curl -fsSL -o "$deb" "https://packages.microsoft.com/config/ubuntu/$VERSION_ID/packages-microsoft-prod.deb" \
        && dpkg -i "$deb" >/dev/null; then
        fresh=""
    else
        log "Microsoft package feed not added"
    fi
    rm -f "$deb"
fi
want=""
command -v unzip >/dev/null 2>&1 || want="$want unzip"
command -v dotnet >/dev/null 2>&1 || want="$want dotnet-sdk-8.0"
command -v pwsh >/dev/null 2>&1 || want="$want powershell"
ldconfig -p | grep -q 'libfontconfig\.so\.1' || want="$want libfontconfig1"
for package in $want; do
    apt_add "$package"
done

# The release and build .github/workflows/checks.yml pins for its engine job, verified the same way.
release=4.7-stable
build=Godot_v4.7-stable_mono_linux_x86_64
dir="$HOME/godot"
if [ ! -x "$dir/$build/Godot_v4.7-stable_mono_linux.x86_64" ]; then
    base="https://github.com/godotengine/godot/releases/download/$release"
    mkdir -p "$dir" && cd "$dir" || exit 0
    if curl -fsSL --retry 3 -o "$build.zip" "$base/$build.zip" \
        && curl -fsSL --retry 3 -o SHA512-SUMS.txt "$base/SHA512-SUMS.txt" \
        && [ "$(grep " $build.zip\$" SHA512-SUMS.txt | awk '{print $1}')" = "$(sha512sum "$build.zip" | awk '{print $1}')" ] \
        && unzip -q -o "$build.zip"; then
        chmod +x "$build/Godot_v4.7-stable_mono_linux.x86_64"
        log "installed Godot at $dir/$build"
    else
        log "Godot download or SHA-512 check failed"
    fi
    rm -f "$build.zip" SHA512-SUMS.txt
fi
exit 0
