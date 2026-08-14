# Where is the pilot skill vector in `aiv.zrd.json`?

Instrument: `aiv_skill_slots.py` (read-only; run from the extraction root). It censuses every
per-mission AI vehicle table and histograms the candidate slots.

The full AI data inventory this sits inside — patrol graphs, turret specs, generators, zeppelins,
voice — is [`docs/plans/PLAN-M4-ai.md`](../../docs/plans/PLAN-M4-ai.md). This file answers one question.

## Verdict — the skill vector is slots 22..30, nine slots, and it is populated only for named pilots

Across 53 `aiv.zrd.json` files and 414 vehicle blocks, the dominant block shape is **81 fields**
(307 blocks; the rest are 42/65/66/67/68-field short forms — trailing fields are simply omitted, so a
fixed-width reader throws on a quarter of the data). Within the 81-field block:

| Slot(s) | What it is | Evidence |
|---|---|---|
| 20 | `MSG_*_NAME` display key | string slot, resolves through `Messages` |
| **22..30** | **pilot skill vector, 9 slots, range 1..9** | `-1` on ~380 of 414 blocks (unset); a complete 1..9 vector on exactly **29** blocks, and every one of those 29 also carries a named-pilot `MSG_*_NAME` key |
| 31 | engagement radius | `-1.0` on 384 blocks; otherwise 350 / 1100 / 1500 / 1550 / 1600 |
| 32 | flag bitfield | powers of two (1,2,4,…,65536) plus combinations (2064 = 2048\|16, 32896 = 32768\|128) |
| 33 | target-priority list | 1–9 entries, `[name, -1]` pairs |

(Slots outside 20..33 are decoded in the scoping document — notably **0 = patrol-net id** and
**6 = formation leader**, and 31 of the bare-number slots are constant across all 414 blocks.)

**Nine slots, not twelve.** The original design documentation describes twelve per-pilot skill
statistics. The shipped data has nine.

**Independent confirmation of the count, from a named key.** Every chapter's
`<Cx>/IA1/zrdr/ia.zrd.json` carries a key literally called **`ace_stats`**, holding exactly **nine**
values — `[9,9,9,9,9,9,9,9,9]`, identical in all 8 chapters, and present in **no other** mission
directory (`ia.zrd.json` is Instant-Action-only). A named key is much stronger evidence than any
positional inference: it fixes the vector length at nine and the ceiling at 9. **It says nothing
about the ordering.**

## The values scale with pilot fame — but NOT with chapter-directory order

The design documentation states skill ratings scale with a pilot's fame, and the recurring antagonist
(`MSG_BSWAN_NAME`) does exactly that: 6s at his first appearance in C1C/M01, 7s in C2/M03,
`(9,9,8,8,8,9,9,5,5)` through C4, and **`(9,9,9,9,9,9,9,9,9)` — maxed on all nine — in C5/M04**, the
final mission. A vector that tracks one character's narrative escalation this cleanly is the skill
vector.

⚠ **The progression is not monotonic in directory order.** C3's three named aces sit at **4–5**,
*below* C2's 6–7. The eight chapter directories are not story chapters (the combat voice-clip naming
maps directory `C2` onto the Hollywood chapter), so **do not read the directory sequence as a
difficulty curve** — the per-character trend is the evidence, not the per-directory one.

## Slot 27 is a discriminating case, not a near-identification

`C2/M02` appends two blocks keyed `MSG_STUNT_PLANE_NAME` to an otherwise ordinary roster of `sec*`
security aircraft; both carry `(1,1,1,1,1,9,1,1,1)` — every slot at minimum except **slot 27**.

The temptation is to call slot 27 the danger-zone-willingness stat, because a stunt aircraft would
plausibly be maxed on daredevilry. **Resist it — the two candidate readings sit at different
positions, which is exactly what makes this case useful:**

- If the nine shipped slots are the documented stats **in document order**, slot 27 is position 6 =
  the **composure** stat (a show plane that flies its routine unbothered by attacks). Under that
  reading daredevilry is position 1 = **slot 22**, which these blocks leave at minimum.
- So "stunt plane ⇒ daredevil" requires the slot order **not** to be document order.

And there is independent evidence that it *is* document order: `C5/M01`'s `MSG_CABBIE_NAME` autogyro
reads `(9,6,6,3,3,6,9,7,8)` — its two lowest values sit at slots 25 and 26, positions 4 and 5, which
in document order are gunnery and shot-angle ferocity. A cab driver who cannot shoot. Reinforcing it,
**89 generic blocks carry `-1` in eight slots and `1` in slot 25 alone** — mooks authored with exactly
one stat, and it is the gunnery position.

**Unresolved. Do not pick one.** The falsification test is cheap and needs no original: the documented
danger-zone check period is `100 − stat` seconds, so whichever slot really is daredevilry, a pilot at 9
attempts zones far more often than one at 1. That becomes testable the moment AI pilots fly. Note the
same test surfaces the **scale conflict** — shipped values are 1..9, so `100 − stat` gives a 91–99 s
interval for every pilot, i.e. a stat with no effect. The rescaling is a decision, not a detail.

## Slots 29 and 30 behave unlike the rest

Both read `5` on 25 of the 29 fully-populated blocks, where slots 22–28 vary per pilot. **The
exceptions are not noise:** they are the cabbie (7, 8) and the final-mission Black Swan (9, 9) — the
two most deliberately-characterised pilots in the game. So "the designers never tuned these" is too
strong; "tuned only where a designer cared" fits the data better. Under document order these are
positions 8 and 9, the two *signature* stats (favourite maneuvers, favourite approach), where a
default of "no strong preference" for everyone but a set-piece pilot is exactly what you would expect.

## What this does not establish

The order of slots 22–30, and whether the nine shipped stats are a subset of the documented twelve or
a re-cut of them. Settling it needs behavioural observation in the original, or a differential pass
against pilots whose behaviour is known to differ. **`ace_stats` bounds the problem to a nine-way
mapping question, which is why this is a bounded task rather than open-ended reverse engineering.**
