# What the AI aircraft defs actually carry

Question: which weapons does an AI aircraft fly in the original, and with what ammunition, refire
and engagement range? Run `python analysis/ai-ordnance-census/census.py` against an extracted
install. The 5-tuple's field order is decoded in
[`docs/org/aiPilot/aiWeapons.md`](../../docs/org/aiPilot/aiWeapons.md); this instrument only prints
the authored data under those names.

Counted 2026-08-16 over `extracted/zrdr/vehicle.zrd.json`: 39 defs carry a `weapons` block.
`player_airplane`'s is the 39-id buyable catalogue at a `10000` range sentinel, not a loadout, and
is excluded from every count below.

**Every AI aircraft carries exactly one gun.** `wep_00`, `wep_130` or `wep_140`, 8000–9000 rounds,
0.05–0.08 s between rounds, 1–900 m. `patrolboat` and `t_truck` carry one `wep_29` at 0.3 s over
1–500 m.

**All but three carry ordnance, one or two entries, 2–8 rounds.** `devastator`, `bswingman` and
`wingman` carry a gun only. Ten distinct ordnance ids are flown across the militias:

| Ordnance | Defs |
|---|---|
| `wep_04` | firebrand, bloodhawk, fury, autogyro, avenger, warhawk |
| `wep_05` (AP) | blakebloodhawk, bhatbrigand, bhatbrigand_5, bhatgyro, britpeace, hakestrel |
| `wep_06` (HE) | medbrigand, britbalmoral, habloodhawk, bsfury_5 |
| `wep_07` | the base fighters, 2–6 rounds |
| `wep_08` (SONIC) | blakepeace, blakepeace_2, blakepeace_3 |
| `wep_09` (FLASH) | bsfury |
| `wep_12` (TANGLER) | secfury, secgyro, stihellhound, stihellhound_5, germanhellhound |
| `wep_13` (SMOKER, rear-firing) | rusdevastator |
| `wep_14` (TORPEDO) | bhatwarhawk, bhatwarhawk_5 |
| `wep_15` (FLARE) | hkfirebrand, hafury |

**Ordnance refire is 30 s and minimum range 200 m on all 25 militia variants**, maximum range 800 m
throughout. Two exceptions:

- The Black Hat Warhawk's torpedo: 8 rounds, 5 s, 350–800 m.
- Five base defs, `firebrand`, `bloodhawk`, `brigand`, `fury`, `autogyro`, author `200, 30` where
  every variant authors `30, 200`, so they read as a 200-second refire at a 30 m minimum. The
  reader takes element 3 as the interval unconditionally, so this is what the engine runs.

Consequence for the remake: an AI's rocket is a rare event by authored design, and the engagement
window is a band (200–800 m), not a maximum. Any launch-rate tuning that ignores the 30 s interval
or the 200 m floor is tuning the wrong number.
