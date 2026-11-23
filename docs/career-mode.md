# Career mode: a pirate air gang on a zeppelin (design concept)

This page is a design concept for an open-ended career mode, in the spirit of a mercenary-company
career: the player runs a pirate air gang from a zeppelin, flies procedurally generated contracts
for money, and spends that money keeping the zeppelin aloft, the crew paid and the planes flying.
No code implements it yet. It is a remake-only feature (see "Remake-only rule" in
[`CONTEXT.md`](../CONTEXT.md)): the original has no career mode, so nothing here is a decode, and
every number below is a starting tune for playtesting, not a measurement.

The career is a separate mode beside the story campaign. It reads the same worlds, aircraft,
weapons, AI and zeppelins from the player's own install, and it never edits a campaign profile.

## Contents

- [Pillars](#pillars)
- [The fantasy](#the-fantasy)
- [The core loop](#the-core-loop)
- [The strategic map](#the-strategic-map)
- [Time and money](#time-and-money)
- [The zeppelin](#the-zeppelin)
- [The crew](#the-crew)
- [The hangar: aircraft, parts and condition](#the-hangar-aircraft-parts-and-condition)
- [Contracts](#contracts)
- [Procedural mission generation](#procedural-mission-generation)
- [Salvage and prizes](#salvage-and-prizes)
- [Standing, heat and the law](#standing-heat-and-the-law)
- [Rival gangs and named aces](#rival-gangs-and-named-aces)
- [Between missions: events](#between-missions-events)
- [Legends: the story campaign as career contracts](#legends-the-story-campaign-as-career-contracts)
- [Failure and how a career ends](#failure-and-how-a-career-ends)
- [Career options](#career-options)
- [Co-op](#co-op)
- [Screens](#screens)
- [What CSVM already has, and what is new](#what-csvm-already-has-and-what-is-new)
- [Data model sketch](#data-model-sketch)
- [Economy tunes](#economy-tunes)
- [Roadmap](#roadmap)
- [Open questions](#open-questions)

## Pillars

1. **Every sortie has a cost.** Ammunition, rockets, fuel, repairs and a wounded pilot's absence
   are all paid for. A won fight can still lose money, and the player learns to fly economically.
2. **The zeppelin is home.** It is the hub screen, the thing the player upgrades, and the thing
   enemies come to burn. Losing it ends the career.
3. **The crew are people.** Wingmen have names, skills, wages, grudges and a share of the loot.
   They improve, get hurt, quit, and occasionally mutiny.
4. **The sky remembers.** Standing with each power, heat with the law and rival gangs carry from
   contract to contract, so the generated missions form a story the player authored by choice.
5. **Short sessions, long arcs.** One contract is one sortie of 5 to 15 minutes. A career runs
   for dozens of hours across all five regions.

## The fantasy

The player inherits a tired cargo zeppelin, two worn fighters, a mechanic who drinks and a debt to
a moneylender who expects weekly payments. Every nation, corporation and militia in the
fractured skies needs pilots who ask no questions. The gang takes their money, picks fights with
their rivals and slowly becomes either a feared pirate fleet, a respected privateer company, or a
wreck at the bottom of the Pacific.

Working title: **Free Skies**.

## The core loop

```
            +------------------------------------------------+
            |                                                |
            v                                                |
   Zeppelin bridge (hub) --> Strategic map: travel to a port |
            |                     (days pass, upkeep paid)   |
            v                                                |
   Contract board --> Negotiate terms --> Flight check       |
                                          (pilots, planes,   |
                                           loadouts)         |
                                              |              |
                                              v              |
                                     Sortie (generated)      |
                                              |              |
                                              v              |
   Debrief --> Salvage --> Repair, refit, hire, upgrade -----+
```

A day is the unit of time. Travel, repair, recovery and refits each cost days, and every week the
ledger pays wages, upkeep and the debt.

## The strategic map

The map is the five regions the original's worlds cover, each a cluster of **ports**:

| Region | Worlds it draws on | Character |
|---|---|---|
| Hawaii | `C3` | Island chains, open sea, naval patrols. Starting region. |
| Northwest | `C1`, `C1B`, `C1C` | Forests, rivers, rail and logging towns, frequent fog and rain. |
| Hollywood | `C2`, `C2B` | Studios, rich cargo, rich targets, a heavy police presence. |
| Colorado | `C4` | Canyons and mountain rail lines, smuggling country, rival gangs. |
| Manhattan | `C5` | Towers, docks, the highest payouts and the heaviest defences. |

A **port** is a place the zeppelin can moor: a city airfield, a hidden pirate haven, a militia
base or a floating market. Each port has:

- a contract board, whose size and quality depend on the port's type and the gang's standing with
  the port's owner;
- a market, with its own stock of airframes, guns, rockets and armour, refreshed weekly;
- a pub where pilots and crew can be hired;
- prices for fuel and repairs, and a **law level** that decides how much heat is tolerated before
  the port closes to the gang.

Travel between two ports in a region costs 1 to 2 days. Travel between regions costs 3 to 6 days
and fuel in proportion. While travelling, a road event can fire (an ambush, a storm, a distress
call, a merchant zeppelin worth boarding).

## Time and money

**The ledger** is the whole economy in one place. Each line is either per sortie, per day or per
week:

| Line | When | Size |
|---|---|---|
| Contract payment | per sortie | the negotiated fee plus bonuses |
| Salvage sales | per sortie | what the player chose to sell |
| Ammunition and rockets spent | per sortie | the rounds and ordnance actually fired, at market price |
| Repairs | per day of work | per armour unit restored, per destroyed part replaced |
| Fuel | per travel day | scales with the zeppelin's engines and hull size |
| Wages | weekly | each crew member's wage |
| Zeppelin upkeep | weekly | scales with the number and level of modules |
| Mooring fee | weekly | per port, waived at an owned haven |
| Debt payment | weekly | the moneylender's instalment, with interest |

Money is the original's dollar figure, so the hangar's prices stay meaningful: a Warhawk airframe
costs $2,423 and a .50 Barret wing gun $410 (see [`org/hangar.md`](org/hangar.md)). The career's
payouts are set against those prices (see [Economy tunes](#economy-tunes)).

**Ammunition is bought.** The flight check shows the cost of the loadout being taken up, and the
debrief charges only for what was fired. Specialist ammunition (armour-piercing, magnesium,
dum-dum) and specialist rockets cost more and are rarer at markets, so a player hoarding seekers
for a bounty fight is making a real choice.

## The zeppelin

The zeppelin is the gang's base, its hangar and its last line of defence. It is built from
**modules**, each with three levels. Upgrading costs money and days in a port with a shipyard.

| Module | What it does | Levels |
|---|---|---|
| Hangar deck | Number of aircraft carried | 3 / 5 / 8 |
| Machine shop | Repair speed and the part quality the crew can fit | basic / skilled / master |
| Crew quarters | Number of crew aboard, and morale while aboard | 6 / 10 / 16 |
| Gun deck | Number and calibre of the zeppelin's cannons and turrets | from the decoded broadside |
| Gas cells | Hull health and how many cells must die before it falls | more cells, armoured cells |
| Engines | Travel speed and fuel cost | |
| Radio room | Contract board size, intel on mission threat and rival movements | |
| Cargo hold | How much salvage and plunder can be carried before selling | |
| Brig | Holds captured pilots for ransom (see [Salvage and prizes](#salvage-and-prizes)) | |

**The zeppelin can be attacked.** A base-defence sortie happens when a rival gang or a law fleet
catches the gang at a port with high heat, or as a road event. The player defends their own hull
with whatever pilots are fit, while the gun deck fights with the crew assigned to it. The fight
uses the zeppelin systems CSVM already flies (`ZeppelinBroadside`, `ZeppelinDamage`,
`ZeppelinMotion`), with the player's hull on team id `1`. Gas cells lost in the fight must be
repaired before the zeppelin can travel at full speed.

**Launch and recovery.** Sorties begin and end at the zeppelin where the mission allows it: the
player launches from the trapeze and recovers onto the hook. A sortie flown far from home starts
in the air at the area's edge instead. Landing a damaged plane on the hook is a small skill test
that saves the cost of a ferry flight.

**Cosmetics.** The gang chooses a name, a flag and a paint scheme for every plane, using the paint
system the hangar already has ([`formats/paint.md`](formats/paint.md)). The zeppelin's hull takes
the gang's colours.

## The crew

### Pilots

Pilots are wingmen in the flight roster, flown by the AI pilot. Each pilot has four skills from 1
to 10, which map onto the AI pilot's existing parameters:

| Skill | Effect in flight |
|---|---|
| Gunnery | Aim error, lead accuracy, burst discipline (ammunition wasted) |
| Airmanship | Manoeuvre limits, energy management, ground avoidance |
| Nerve | When the pilot breaks off and when they run home damaged |
| Teamwork | How well the pilot follows orders and covers the leader |

Pilots gain experience per sortie and per kill and spend it on skills. Each also rolls one or two
**traits**: *Gunslinger* (better gunnery, uses more ammunition), *Careful* (repairs on their plane
cost less), *Greedy* (wants a bigger share), *Loyal* (never joins a mutiny), *Hothead* (ignores
the order to return), *Ace of another gang* (comes with a rival's grudge attached).

**Injury and death.** A pilot whose plane crashes rolls against the plane's state and whether they
bailed out. Outcomes are unhurt, wounded (out for days), badly hurt (out for weeks, possibly a
lasting penalty) or killed. A pilot who bails over enemy territory may be captured, which creates
a rescue contract.

### Ground and zeppelin crew

| Role | Effect |
|---|---|
| Mechanic | Repair days and repair cost. A master mechanic can fit rare parts. |
| Gunner | Crews the zeppelin's gun deck in a base-defence sortie, skill decides accuracy |
| Navigator | Travel days, and the chance of avoiding a road ambush |
| Quartermaster | Market prices, both buying and selling |
| Radio operator | Contract intel: threat rating accuracy, warning before an ambush |
| Surgeon | Recovery time for wounded pilots |

### Wages, shares and the Articles

At the start of a career, and again at any port, the captain sets the gang's **Articles**: how
the crew is paid.

- **Wages**: a fixed weekly wage per crew member. Predictable, and the crew do not care how rich a
  contract is.
- **Shares**: no wage, but a percentage of every contract payment and salvage sale is split among
  the crew. Cheap in a dry spell, expensive in a good week, and the crew push for richer and
  riskier contracts.
- **Mixed**: a lower wage plus a smaller share.

**Morale** sits between 0 and 100 for each crew member. It rises with pay, victories, rest and a
well-kept zeppelin, and falls with unpaid wages, deaths in the gang, long travel and contracts that
break a crew member's own code (some pilots refuse to strike civilian targets). Low morale lowers
skills in flight; very low morale means the crew member leaves at the next port. When enough of
the crew are angry at once, a **mutiny** event fires, which the captain can talk down, pay off or
fight (a sortie against the deserters stealing two planes).

## The hangar: aircraft, parts and condition

**Each plane is an instance**, not a type. It has an airframe, an engine, guns per slot, pylons,
armour per zone and a paint scheme, exactly the record the custom-plane hangar already edits
(`CustomPlaneDef`), plus career state:

- **Condition per armour zone** (nose, tail, left wing, right wing), carried from the sortie: a
  plane that came home with its left wing shredded starts the next sortie that way unless it is
  repaired. The four zone values already exist in the saved-plane record.
- **Damaged parts**: a gun or an engine that took a hit is marked damaged and must be replaced or
  repaired.
- **Airframe wear**: every sortie adds wear; a worn airframe costs more to repair and eventually
  must be overhauled.
- **History**: the pilots who flew it, its kills, the sorties it survived. A plane with a long
  history earns a name the player can paint on it.

**Parts are stock.** Guns, engines, armour and rockets are bought into the hold and fitted in the
hangar; removing a part returns it to the hold. Buying a new plane gives a bare airframe. The
hangar screen is the existing custom-plane hangar, with prices taken from the ledger and parts
taken from the hold instead of an unlimited catalogue.

**Markets have stock and availability.** The airframe table already carries an availability
value per airframe, which sets how often a market offers it. A port's region and owner skew the
stock (Hollywood has the fast fighters, Colorado has heavy turret planes and black-market
ordnance), and high standing with the owner unlocks the better shelf.

## Contracts

A **contract** is one job from one employer. The board shows the employer, the job type, the
region and port, the **threat rating** (one to five skulls), the base fee and the days until the
offer expires.

### Employers

| Employer | Offers | Standing effect |
|---|---|---|
| Nations | Patrols, escorts, strikes on smugglers and pirates | Legal work, lowers heat |
| Corporations | Cargo escort, competitor sabotage, recovery of stolen goods | Neutral |
| Militias | Raids on other militias, base defence | Raises one militia, lowers its enemy |
| Underworld brokers | Smuggling, heists, kidnappings, prize captures | Raises heat, pays best |
| Rival gangs | Temporary truces, shared heists that may be betrayals | Changes the rival's attitude |

### Job types

| Job | Sortie | Built on |
|---|---|---|
| **Cargo raid** | Intercept a convoy of transports and escorts; force the transports down or destroy them, and collect the dropped crates | AI nets for the convoy route, pickups for the crates |
| **Zeppelin assault** | Bring down an armed zeppelin: cannons first, then the gas cells | the decoded zeppelin systems |
| **Prize capture** | Cripple a target zeppelin or transport and board it by ladder; the prize is towed home and sold or kept | the ladder switch |
| **Escort** | Keep a transport, a train or a friendly zeppelin alive along its route | the escort formation law, AI nets |
| **Strike** | Destroy listed ground targets: depots, flak, bridges, a rail yard | mission targets and destructibles |
| **Patrol** | Fly a route of waypoints and deal with whatever is there | AI nets, enemy generators |
| **Bounty** | Find and kill a named ace with an escort | the target-ranking AI, a named-ace roster entry |
| **Smuggling run** | Fly a cargo plane through a valley or a city below the patrol altitude, against the clock | stunt danger zones as the route, a hidden detection volume |
| **Recon** | Photograph listed sites by flying through their zones, then get home | stunt zones as camera checkpoints |
| **Rescue** | Reach a downed pilot or a captive, hold the area while the pickup lands | pickups, landing approaches, wave generators |
| **Base defence** | Defend a client's airfield or the gang's own zeppelin against waves | enemy generators in wave shape |
| **Air race wager** | Bet money on a race through a course against rival pilots | stunt race bots and the course runner |
| **Duel** | An honour challenge: one plane each, no wingmen, guns only | the Dogfight rules, a single bot |

### Negotiation

Before accepting, the captain spends **negotiation points** across three terms. The gang's
standing with the employer sets the number of points:

- **Fee**: more money up front.
- **Salvage**: more picks from the wreck pool after the sortie.
- **Expenses**: the employer pays part of the ammunition and repair bill.

A fourth choice, **No questions**, trades points for a lower heat gain on underworld work.

A contract can also carry **bonus objectives** (no transport lost, finish under a time, capture
instead of destroy, a specific weapon kill) that pay extra and are revealed by a good radio room.

## Procedural mission generation

A generated sortie is a **mission spec**: an engine-free description of a world, a set of sites
and actors, objectives and modifiers, which the session builds the same way it builds Instant
Action from an `InstantActionDef`. The generator works in five steps.

1. **Pick the world and the area.** The contract's port decides the chapter world. The generator
   picks an area from that world's **site catalogue**: positions harvested once per chapter from
   data the remake already reads, such as AI patrol nets, landing approaches, spawn tables, danger
   zone ribbons, destructible ground objects and open water. Each site carries tags (`airfield`,
   `rail`, `canyon`, `city`, `open_sea`, `dock`) so a job type can ask for the sites it needs.
2. **Place the job's template.** Each job type is a template with roles: a cargo raid needs a
   route (an AI net leg or a rail line), a convoy group and an escort group; a strike needs two to
   five target sites and a flak ring. The template asks the site catalogue for sites with the
   right tags inside the chosen area.
3. **Budget the opposition.** The threat rating becomes an **opposition budget** in the same
   currency as the player's force. Every enemy plane is valued by its airframe, guns and its
   pilot's skill; every zeppelin and flak emplacement has a fixed value. The generator buys enemies
   until the budget is spent, picking from the employer's enemy's roster (a militia flies its own
   airframes and paint, see `MilitiaPaint`). Reinforcement waves come from the enemy generators
   with a delay and a trigger (an alarm raised, a time passed, the convoy's halfway point).
4. **Roll the modifiers.** Weather (fog, rain, storm, through the existing weather system), time
   of day, a hidden ambush, a third party arriving (a rival gang going for the same prize), a
   double-cross (the employer's own planes turn on the gang at the end). Each modifier changes the
   fee and the threat rating shown on the board, unless it is hidden; the radio room's level
   decides how many hidden modifiers the board reveals.
5. **Write the objectives.** The template's objectives become entries the existing objective and
   HUD message systems already show: primary (must be done for payment), secondary (paid extra),
   and the hidden bonus objectives.

**Determinism.** The spec is generated from a seed stored with the contract, so a contract shows
the same mission whenever it is flown and a reported bug can be replayed with `--career-seed=`.
The generator is engine-free and unit-tested: given a site catalogue and a seed, it returns a
spec, and the tests assert on the spec, not on pixels.

**Difficulty scaling.** The opposition budget for a given threat rating grows slowly with the
gang's fame, not with the player's force. A rich gang is not punished for being rich; it is
offered richer and harder contracts, and can still take easy ones for little money.

## Salvage and prizes

After the sortie, every enemy plane brought down over ground the gang controls at the end becomes
a **wreck** with a salvage value. Wrecks over the sea or deep in enemy ground are lost. The
salvage screen lists the pool: whole airframes (rare, only from planes that crash-landed or whose
pilot bailed), guns by calibre, unfired rockets, engines, armour and cargo crates.

The captain takes as many picks as the negotiation bought, first come first served, and the
employer takes the rest. A picked item goes into the hold, or is sold at once at a discount.

**Prizes** are the pirate's real fortune. A transport or a zeppelin whose crew surrenders (its
engines dead or its escort destroyed) can be boarded by ladder. A captured transport carries
cargo to sell. A captured zeppelin can be sold for a fortune, ransomed back to its owner, or, late
in a career, kept as a second hull. Captured pilots go into the brig and can be ransomed, recruited
or released, each with a standing and heat consequence.

## Standing, heat and the law

**Standing** is one number per power (each nation, corporation and militia), from -100 to 100. It
gates the contract board, market shelves and whether a port lets the zeppelin moor. Working for
one power usually costs standing with its enemies.

**Heat** is how wanted the gang is, from 0 to 100, and decays by a few points a week while the
gang lies low. Heat rises with underworld work, attacks on civilian targets, captures, and being
seen. At thresholds:

| Heat | Consequence |
|---|---|
| 25 | Lawful ports charge a "fee" to look away |
| 50 | Bounty hunters appear as road events and as third parties in sorties |
| 75 | Lawful ports are closed; a law fleet hunts the zeppelin across a region |
| 100 | A named law ace and a navy zeppelin come for the gang in a scripted base-defence sortie |

**Fame** is separate from heat: it rises with every completed contract, every famous kill and
every prize, and it unlocks better employers, better pilots in the pub and the Legends contracts.
A gang can be famous and clean (a privateer company) or famous and wanted (a pirate fleet). The
debrief shows which way the gang's reputation is leaning, and some pilots will only join one kind.

## Rival gangs and named aces

The career generates three to five **rival gangs** at the start. Each has a name, colours, a home
region, a leader ace and two or three lieutenant aces. Rivals act on the strategic map in a simple
weekly simulation:

- they take contracts from the same boards, so a contract can vanish before the gang arrives;
- they grow stronger when left alone and weaker when beaten;
- they keep a **grudge** against the gang, which rises when the gang kills their pilots or steals
  their prizes and makes them appear in the gang's sorties as a third party;
- they can be made allies for a while, which a broker's contract may offer and later betray.

A **named ace** is a roster entry with a portrait, a voice line pool from the existing combat voice
chain, a signature plane and paint, and skills above any generated pilot. An ace who escapes a
fight remembers it and comes back better armed. Beating a rival leader ends that gang and pays a
large bounty; their surviving pilots may ask to join.

## Between missions: events

Short text events fire on arrival at a port, during travel and at the weekly payday. Each offers
two or three choices with money, morale, standing or heat consequences, and some lead to a sortie.
Examples:

- A gas cell is leaking. Pay for repairs here at a high price, or limp to the next port at half
  speed and risk a storm.
- Two pilots are fighting over a card-game debt from the last port. Side with one, dock both a week's pay, or
  let them settle it in a duel (which the player flies).
- A stowaway is found in the cargo hold: a runaway mechanic from a rival gang, with their codebook.
- A merchant zeppelin signals distress. Help (fame, standing), rob it (money, heat) or ignore it.
- The moneylender's collectors are waiting at the mooring. Pay early for a discount, or fly off and
  owe interest and a grudge.
- A famous film studio wants to shoot the gang's next sortie from a camera plane: a bonus if the
  camera plane survives.

Events are data, not code: a list of entries with conditions, text and outcomes, so new ones are
added without touching the generator.

## Legends: the story campaign as career contracts

Once the gang's fame is high enough in a region, the original's campaign missions of that region
appear on the board as **Legends** contracts: the authored mission in its own world, flown by the
gang with its own planes and pilots, paid in career money and with unique salvage (a prototype
airframe, a famous ace's paint). Legends reuse the campaign's existing mission data and objective
scripts unchanged, and the career only adds the payment and the salvage table around them. A
Legends contract can be flown once per career.

This gives the career a hand-authored spine without writing new story missions, and gives players
who finished the campaign a reason to fly its best missions again with a gang they built.

## Failure and how a career ends

- **Bankruptcy.** When the ledger cannot pay the weekly bill, the gang goes into debt. Above a
  debt limit, the moneylender seizes planes, then the zeppelin. A seized zeppelin ends the career,
  unless the option *Second chances* is on, in which case the captain starts over with one plane
  and keeps their fame.
- **The zeppelin falls.** A base-defence sortie lost with the hull destroyed ends the career.
- **The captain dies.** With *Ironman* on, the player character's death ends the career. Without
  it, the captain is badly hurt and the gang must wait (and pay) for their recovery.
- **Retirement.** A career has no fixed end. A gang that beats every rival leader and survives the
  heat-100 hunt is offered a choice of endings, after which the career can continue.

## Career options

Set when a career starts, shown in the career's save slot:

| Option | Choices |
|---|---|
| Starting position | Debt-ridden (default), Privateer (a letter of marque, low heat, less money), Notorious (rich, already wanted) |
| Economy | Generous, Standard, Harsh (scales fees, prices and upkeep) |
| Pilot death | Off (always wounded), On |
| Ironman | One save slot, saved on every state change, captain death ends the career |
| Second chances | On, Off |
| Assisted flight | The existing assisted-flight settings, unchanged |

## Co-op

A co-op career is the career of one **seated profile**, flown with the existing campaign co-op
rules (see "Campaign co-op" in [`CONTEXT.md`](../CONTEXT.md)). Other humans join as **guests**
and fly in the wingman seats instead of AI pilots, on team id `1`. A guest's kills, ammunition
and damage are charged to the gang's ledger like an AI wingman's, but a guest has no wage, no
injury and no persistent skills, and cannot spend from the ledger. Whether a guest may claim a
named pilot slot from the gang's roster is an open question.

## Screens

The career's out-of-mission screens are a new set of pages in the campaign flow's stack, so both
menu presentations can draw them. The original presentation has no art for most of them, so they
draw in the modern presentation's style, reusing the campaign board's parchment, plaques and
fonts where it fits.

| Screen | Role | Closest existing page |
|---|---|---|
| Bridge | The hub: date, cash, heat, next payday, the crew's mood, buttons to every other screen | cabin |
| Strategic map | Regions, ports, rivals' last known positions, travel orders | briefing map |
| Contract board | The port's offers, with threat rating, fee and expiry | campaign mission list |
| Negotiation | The three terms and the points to spend | none |
| Briefing | The generated sortie's map, objectives and known threats | briefing |
| Flight check | Which pilot flies which plane, and the cost of each loadout | flight check |
| Hangar | Fit parts from the hold onto a plane, repair, paint | custom-plane hangar |
| Market | Buy and sell planes, parts, ammunition and salvage | hangar purchase |
| Crew | Pilots and crew, skills, morale, wages, hire and fire, the Articles | profile roster |
| Ledger | Income and spending by week, debt, upkeep | none |
| Debrief | Objectives, kills, the bill, the payment | debrief |
| Salvage | The wreck pool and the picks | none |
| Zeppelin | Modules, upgrades, repairs of the hull | none |
| Event | One text event and its choices | none |

## What CSVM already has, and what is new

| Need | Exists | Gap |
|---|---|---|
| Worlds, terrain, weather, time of day | chapter worlds, `Weather`, zone lighting | a per-chapter site catalogue |
| Building a session from a description | `InstantActionDef` and its producers | a mission spec rich enough for convoys, targets and waves |
| Enemy aircraft and their AI | `AiPilot`, target ranking, AI nets, enemy generators | AI skill parameters exposed per pilot |
| Wingmen | the flight check's crew slots, `AiEscort` | orders from the player, more than one wingman |
| Zeppelins | broadside, damage, motion, replicas | a player-side zeppelin as the base |
| Ladder boarding | the ladder switch, the airframe swap | boarding a prize instead of a plane |
| Planes and parts | `CustomPlaneDef`, the hangar, prices and weights | per-instance condition and a parts hold |
| Ammunition | `FireControl`'s draw-down, the ammo selection screen | billing what was fired |
| Damage | armour and health per zone | carrying zone state out of the sortie |
| Objectives and HUD messages | objectives, mission targets, `HudMessages`, `MissionRadio` | objectives from a generated spec |
| Courses for runs | stunt danger zones, the course runner, stunt race bots | courses as smuggling routes and recon checkpoints |
| Out-of-mission flow | `CampaignFlow`, board pages | the career pages, the strategic map |
| Co-op | campaign co-op over the net session | ledger charging for guests |
| Saving | campaign profiles, `user://` stores | a career save |

## Data model sketch

Engine-free types, unit-tested on their own, in a `Career` namespace beside `Session`:

- `CareerState`: the whole save. Calendar day, ledger, zeppelin, crew, hangar, hold, standing
  per power, heat, fame, rivals, the open contracts per visited port, the event log, the options.
- `Ledger`: entries by day with a category and an amount, the cash balance and the debt, and the
  weekly bill computed from the rest of the state.
- `Zeppelin`: modules and levels, hull and cell health, the port it is moored at or its travel
  order.
- `CrewMember`: role, name, skills, traits, wage or share, morale, injury and the days left on it,
  experience; pilots also hold their kill and sortie history.
- `PlaneInstance`: the `CustomPlaneDef` record plus zone condition, damaged parts, wear and
  history.
- `Contract`: employer, job type, seed, port, threat rating, fee, expiry, negotiated terms.
- `MissionSpec`: the generator's output, the one input the session needs to build a sortie.
- `SortieResult`: what the session reports back: objectives met, rounds and ordnance spent per
  plane, zone damage per plane, crashes and bail-outs, kills, the wreck pool, captured prizes.

The session reads a `MissionSpec` and writes a `SortieResult`, and nothing in flight reads
`CareerState`. That keeps the career logic out of the frame loop, lets a test run a hundred
simulated weeks in milliseconds, and lets `--career-seed=` fly one generated sortie from the
command line.

The save is JSON at `user://career/<name>.json`, versioned, with a backup written before each
overwrite. An Ironman career writes after every state change.

## Economy tunes

Starting tunes, to be balanced by simulation (a scripted gang taking random contracts for a
hundred weeks) and then at the controls.

| Item | Tune |
|---|---|
| Starting cash | $6,000 |
| Starting debt | $25,000, $1,000 a week, 2 % weekly interest on the remainder |
| Starting force | one worn Warhawk, one worn Brigand, two pilots of skill 3 to 4, a mechanic |
| Contract fee, one skull | $1,500 to $2,500 |
| Contract fee, five skulls | $10,000 to $16,000 |
| Pilot wage | $150 to $900 a week by skill |
| Crew wage | $100 to $400 a week by role and skill |
| Zeppelin upkeep | $500 a week, plus $150 per module level |
| Fuel | $200 per travel day, scaled by engine level |
| Repair | $5 per armour unit restored, half the part price for a damaged part |
| Ammunition | slug rounds cheap, AP and magnesium three times the price, rockets $40 to $250 each |
| Salvage | 40 % of the market price when sold at once, 60 % at a broker |

The intended feel: the first two weeks are tight and every sortie matters, a well-run gang is
comfortable by the end of the first region, and the zeppelin upgrades and the Legends contracts
are the long-term money sink.

## Roadmap

Each phase ends in a playable state, so the design is tested at the controls before the next
phase builds on it.

1. **The loop on one world.** `CareerState`, the ledger and a contract board with two job types
   (patrol and strike) on one chapter, generated by seed. Debrief bills ammunition and repairs,
   plane condition carries between sorties. One port, no travel, no crew skills.
2. **Wingmen and the market.** Pilots with skills mapped to the AI, hiring, wages, injury. The
   market and the parts hold, the hangar fitting from stock. Salvage.
3. **The zeppelin and travel.** The strategic map with all five regions, ports, travel days and
   fuel, zeppelin modules, launch and recovery, base defence.
4. **The full board.** The remaining job types, negotiation, modifiers, bonus objectives, prizes
   and the ladder boarding.
5. **The living sky.** Standing, heat and fame, rival gangs and named aces, events, the Articles
   and morale, mutiny.
6. **Legends and co-op.** The campaign missions as Legends contracts, guests in the wingman seats,
   the career options and Ironman.

## Open questions

- Is the captain always the human's plane, or can the player stay on the bridge and send the
  gang out alone, with the sortie resolved by simulation?
- How many wingmen can fly at once before the AI and the frame budget suffer? The original's
  missions fly one wingman; the career wants up to three.
- Does a generated sortie need more of the world than the site catalogue offers, such as new
  ground targets placed into a world that never had them, and would placing them be a change to the
  original's worlds that a Faithful setting should refuse?
- Should the strategic map be the original's briefing map art, a new illustrated map, or a
  zeppelin-window view of the region?
- How much of the career should run in the original presentation, which has no art for most of
  these screens?
- Should a guest in co-op be able to bring their own plane from their own career, as a hired
  mercenary with their own ledger?
