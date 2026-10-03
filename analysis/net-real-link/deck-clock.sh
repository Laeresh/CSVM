#!/bin/bash
# Prints this machine's wall clock forty times, a tenth of a second apart, for the PC to stamp on
# arrival (clock-offset.ps1).
for i in $(seq 40); do date +%s.%N; sleep 0.1; done
