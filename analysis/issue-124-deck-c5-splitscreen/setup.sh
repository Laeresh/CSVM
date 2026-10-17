#!/bin/sh
# usage: setup.sh <tarball>   Unpacks a fresh game folder under ~/CSVM-tmp/i124 and seeds its own user data.
# Copy run.sh, batch.sh, launch.sh, shot.sh, shots.sh, setup.sh and options.json into ~/CSVM-tmp/i124 first.
set -e
B=/home/deck/CSVM-tmp/i124
cd "$B"
for f in run.sh batch.sh launch.sh shot.sh shots.sh setup.sh options.json; do sed -i 's/\r//' "$f"; done
rm -rf "$B/game"
mkdir -p "$B/game" "$B/out" "$B/shots" "$B/xdg/godot/app_userdata/CSVM"
tar -xzf "$1" --no-same-owner -C "$B/game"
cp "$B/options.json" "$B/xdg/godot/app_userdata/CSVM/options.json"
ls -la "$B/game" | head -20
sed -n '1,8p' "$B/game/BUILD-INFO.txt"
