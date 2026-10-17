#!/bin/sh
# usage: batch.sh ROUNDS SECS "ARM ..."   ARM = <players>:<levers>[:<extra arg>]
# Runs every arm per round, the order reversed on even rounds. Waits for any other CSVM first.
B=/home/deck/CSVM-tmp/i124
cd "$B" || exit 9
ROUNDS=$1; SECS=$2; ARMS=$3
rm -f "$B/out/DONE"
REV=""
for a in $ARMS; do REV="$a $REV"; done
waitquiet() {
  wq=0
  while [ "$(pgrep -f 'CSVM[.]x86_64' | wc -l)" -gt 0 ] || [ -e /home/deck/CSVM-eg2/.decklock ]; do
    sleep 5; wq=$((wq + 1))
    if [ $wq -ge 360 ]; then echo "gave up waiting $(date +%T)" >> "$B/out/runs.txt"; exit 5; fi
  done
}
r=1
while [ $r -le "$ROUNDS" ]; do
  if [ $((r % 2)) = 1 ]; then LIST=$ARMS; else LIST=$REV; fi
  for a in $LIST; do
    P=$(echo "$a" | cut -d: -f1); L=$(echo "$a" | cut -d: -f2); X=$(echo "$a" | cut -s -d: -f3)
    waitquiet
    sh "$B/run.sh" "r$r-${P}p-$L$(echo "$X" | tr -d '=-')" "$P" "$L" "$SECS" $X
  done
  r=$((r + 1))
done
date +%T > "$B/out/DONE"
