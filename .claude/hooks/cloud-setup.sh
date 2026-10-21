#!/bin/bash
# Installs the .NET 8 SDK in a Claude Code cloud container, which ships without it.
# Local sessions (Windows, or any machine without CLAUDE_CODE_REMOTE=true) skip it.
[ "$CLAUDE_CODE_REMOTE" = "true" ] || exit 0
command -v dotnet >/dev/null 2>&1 && exit 0
command -v apt-get >/dev/null 2>&1 || { echo "cloud-setup: no apt-get, .NET SDK not installed" >&2; exit 0; }
export DEBIAN_FRONTEND=noninteractive
apt-get update -q >/dev/null && apt-get install -y -q dotnet-sdk-8.0 >/dev/null \
    && echo "cloud-setup: installed .NET SDK $(dotnet --version)" \
    || echo "cloud-setup: .NET SDK install failed" >&2
exit 0
