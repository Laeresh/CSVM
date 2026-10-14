#!/bin/sh
# usage: launch.sh ROUNDS SECS "ARMS"   Starts batch.sh in its own user unit, outside the ssh session's scope.
# The previous out/ is kept as out-<time>.
cd /home/deck/CSVM-tmp/i124 || exit 9
if [ -d out ]; then mv out "out-$(date +%H%M%S)"; fi
mkdir -p out
systemctl --user reset-failed i124batch 2>/dev/null
systemd-run --user --unit=i124batch --working-directory=/home/deck/CSVM-tmp/i124 \
  sh -c "sh batch.sh '$1' '$2' '$3' > out/batch.out 2>&1"
echo "launched"
