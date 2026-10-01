# Which in-engine suites can run without an extraction

**Question.** Of the 495 `[Suite]` bodies in `CSVM/src/Testing/`, which could run on CI, where no
install is present, and what would unblock the rest? `PLAN-ci-engine-suites` is built on the answer.

**Instrument.** `suites.tsv`, one row per suite: file, suite name, bucket, and a one-line reason
naming the install input the suite needs or what it uses a chapter world for. Classified by reading
each suite's opening lines, its `RequireData`/`WithWorld` calls, the helpers it reaches and the
quantities it asserts. The largest files (`AiSuites`, `CombatSuites`, `CampaignSuites`, the Menu
and Original-shell suites) were classified from those signals rather than read line by line, so
each row is **inferred**, and a bucket count is good to about ten.

**Buckets.**

| Bucket | Count | Meaning |
|---|---|---|
| A | 56 | Reads nothing from the install. Many still SKIP behind a `RequireData(ctx.ZrdrPath)` the body never uses. |
| B | 3 | Uses a chapter world only as ground or a scene host: `ground-contact`, `forward-rotation`, `launch-direction-cache`. |
| C | 83 | Needs a plane and its records (planes gamez, plane stats, weapons, shakes, messages, a texture archive, sometimes sounds) and no chapter world. |
| D | 87 | Needs a few small authored records that can be invented: Original-shell layout screens, a multiplayer spawn table, anim and effect defs, a weather block. |
| E | 266 | Checks the shipped data or a named chapter, mission or node. Install-bound by construction. |

**Measured.** The harness boots with no install: nothing between `_Ready` and the `--run-tests`
dispatch reads `extracted/`, `RequireData` ends a suite as SKIP, and `BuildWorld` requires the
chapter gamez and textures before building, so every world suite skips cleanly.

**Inferred, to confirm by running.** `MusicSuites.cs` loads `ctx.ZrdrPath` and `ctx.SoundsPath`
with no `RequireData`, and `NetTeamSuites.cs` evaluates `MatchScores.Load(ctx.ZrdrPath)` as an
argument before `MatchSpec` reaches its own gates, so both should FAIL rather than SKIP without an
install.

**Instrument caveats.** The reason column of a C or D row names the inputs as the classifier saw
them, not as a run proved them. Several C rows assert values of named real records (weapon ids and
speeds, damage ladders); the stand-in either reproduces those values or the assertion is retargeted,
and the row does not say which.
