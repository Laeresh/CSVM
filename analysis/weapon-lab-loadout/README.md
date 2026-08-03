# Weapon lab B5 — the panel drives the live loadout

Evidence for `PLAN-weapon-lab` B5 (2026-08-03). The captures themselves are **not committed** —
they are renders of the game's own geometry, so they live in the git-ignored `Screenshots/`
(`lab_he.png`, `lab_torpedo.png`, `lab_g4.png`). Each is reproducible from the command below;
what is recorded here is what they showed.

All three are `--weapon-lab --plane=player_bhawk --chapter=C2` captures of the held aircraft.

- `--weapon-lab=wep_06 … --view=8` vs `--weapon-lab=wep_14 … --view=8` — the same wings with the
  HE rocket and the aerial torpedo mounted. The mounted ordnance models follow the panel's
  hardpoint selection: eight small HE bodies against eight red torpedoes.
- `--weapon-mount=g4 --weapon-fire` — gun group 4 firing from `firepoint1,firepoint2`, a mount
  the Bloodhawk's stock fit never names (it exists because the lab binds `Loadout.ForRig`). The
  HUD's `GUNS: GUN GROUP 4` line and the panel's mount readout agree.

The no-leak check is not a screenshot: `--weapon-cycle=4 --frames=1000 --log=weapons:debug` logs
`mounted=N ordnance_nodes=N` on every swap, and 130 consecutive swaps held both at 8.
