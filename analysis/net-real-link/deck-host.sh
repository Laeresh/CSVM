#!/bin/bash
# deck-host.sh NAME SECONDS -- CSVM args...
# Hosts one scripted match from the install this script sits in, headless, and stops it after
# SECONDS. The log lands in that install's logs/, the console in bl-out/NAME.out.
set -u
name="$1"; secs="$2"; shift 2
[ "${1:-}" = "--" ] && shift
root="$(cd "$(dirname "$0")" && pwd)"
out="$root/bl-out"
export CSVM_DATA_ROOT="${CSVM_DATA_ROOT:-/home/deck/CSVM}"
export XDG_DATA_HOME="$root/userdata-net-real-link"
mkdir -p "$XDG_DATA_HOME" "$out"
cd "$root"
timeout "$secs" ./CSVM.x86_64 --headless --render-thread safe -- "$@" > "$out/$name.out" 2>&1
echo "exit=$? $name"
ls -t "$root/logs" | head -3
