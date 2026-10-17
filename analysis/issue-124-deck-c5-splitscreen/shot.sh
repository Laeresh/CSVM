#!/bin/sh
# usage: shot.sh NAME PLAYERS LEVERS [SHADOW]   One --det screenshot of the measured pose at frame 600.
NAME=$1; P=$2; LEV=$3; SHADOW=${4:-high}
B=/home/deck/CSVM-tmp/i124
cd "$B/game" || exit 9
export CSVM_DATA_ROOT=/home/deck/CSVM
export XDG_DATA_HOME="$B/xdg"
export DISPLAY=:0
if [ "$LEV" = base ]; then export CSVM_I124=""; else export CSVM_I124="$LEV"; fi
timeout 300 ./CSVM.x86_64 --resolution 1280x800 -- --det --no-vsync --chapter=C5 --vs --players=$P \
  --target=nearest --view=back "--pos=-9256,300,-3155" "--direction=0.588,0.07,-0.809" \
  --graphics=enhanced --shadow-quality=$SHADOW --water-quality=flat --frames=600 "--screenshot=$B/shots/$NAME.png" > "$B/shots/$NAME.out" 2>&1
echo "$NAME rc=$?"
