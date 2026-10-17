#!/bin/sh
# The crop shots, one after another, all at the shipped 67% scale (the scale67 lever) with --det's TAA.
B=/home/deck/CSVM-tmp/i124
cd "$B" || exit 9
mkdir -p shots
for a in "2p-base 2 scale67 high" "2p-passesoff 2 scale67+notaa+nossao+nossr+noglow high" \
         "2p-noclutter 2 scale67+noclutter high" "2p-fogfar 2 scale67+fogfar high" \
         "4p-base 4 scale67 off" "4p-noclutter 4 scale67+noclutter off" "4p-fogfar 4 scale67+fogfar off"; do
  set -- $a
  sh "$B/shot.sh" "$1" "$2" "$3" "$4" >> shots/shots.txt 2>&1
done
date +%T > shots/DONE
