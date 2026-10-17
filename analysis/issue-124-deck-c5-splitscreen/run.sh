#!/bin/sh
# usage: run.sh NAME PLAYERS LEVERS SECONDS [extra game args...]
# One wall-clock (--no-det) C5 splitscreen flight, no --perf, stopped SECONDS after the first
# [perf] rate window of the session. Reads the game's own log file, since stdout is block-buffered.
NAME=$1; P=$2; LEV=$3; SECS=$4; shift 4
B=/home/deck/CSVM-tmp/i124
cd "$B/game" || exit 9
export CSVM_DATA_ROOT=/home/deck/CSVM
export XDG_DATA_HOME="$B/xdg"
export DISPLAY=:0
if [ "$LEV" = base ]; then export CSVM_I124=""; else export CSVM_I124="$LEV"; fi
OTHER=$(pgrep -f 'CSVM[.]x86_64' | wc -l)
echo "$NAME start other_csvm=$OTHER $(date +%T)" >> "$B/out/runs.txt"
touch "$B/out/.mark"
./CSVM.x86_64 --resolution 1280x800 -- --no-det --no-pads --no-vsync --chapter=C5 --vs --players=$P \
  --target=nearest --view=back "--pos=-9256,300,-3155" "--direction=0.588,0.07,-0.809" \
  --graphics=enhanced "$@" > "$B/out/$NAME.out" 2>&1 &
PID=$!
t=0; up=0; LOG=""
while kill -0 $PID 2>/dev/null; do
  sleep 1; t=$((t + 1))
  if [ -z "$LOG" ]; then LOG=$(find "$B/game/logs" -name '*.log' -newer "$B/out/.mark" | head -1); fi
  if [ $up = 0 ] && [ -n "$LOG" ] && awk '/live respawn/{u=1} u&&/rate fps/{f=1} END{exit !f}' "$LOG"; then up=$t; fi
  if [ $up != 0 ] && [ $((t - up)) -ge "$SECS" ]; then kill $PID; sleep 5; kill -9 $PID 2>/dev/null; break; fi
  if [ $t -ge 300 ]; then kill -9 $PID; echo "killed at 300 s" >> "$B/out/$NAME.out"; break; fi
done
wait $PID 2>/dev/null
if [ -n "$LOG" ]; then cp "$LOG" "$B/out/$NAME.log"; fi
echo "$NAME end t=${t}s up=${up}s log=$LOG $(date +%T)" >> "$B/out/runs.txt"
