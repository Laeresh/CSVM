# Career mode: a pirate air gang on a zeppelin (design concept)

This page is a design concept for an open-ended career mode, in the spirit of a mercenary-company
career: the player runs a pirate air gang from a zeppelin, flies procedurally generated contracts
for money, and spends that money keeping the zeppelin aloft, the crew paid and the planes flying.
No code implements it yet; it is planned as a new milestone after the story campaign. It is a remake-only feature (see "Remake-only rule" in
[`CONTEXT.md`](../CONTEXT.md)): the original has no career mode, so nothing here is a decode, and
every number below is a starting tune for playtesting, not a measurement.

The career is a separate mode beside the story campaign. It reads the same worlds, aircraft,
weapons, AI and zeppelins from the player's own install, and it never edits a campaign profile.

## Contents

- [Pillars](#pillars)
- [The flight](#the-flight)
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
- [Legends: the story campaign on the career's timeline](#legends-the-story-campaign-on-the-careers-timeline)
- [Failure and how a career ends](#failure-and-how-a-career-ends)
- [Career options](#career-options)
- [Co-op](#co-op)
- [Generated worlds (exploratory)](#generated-worlds-exploratory)
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
6. **The captain always flies.** Every sortie is flown by the player as the flight's leader. There
   is no auto-resolve and no sending the gang out alone; a contract the captain cannot fly (wounded,
   no plane) waits or expires.

## The flight

A sortie's player side is **at most four planes**: the captain plus up to three wingmen. Each
wingman seat is filled either by an AI pilot from the gang's roster or by a co-op human (see
[Co-op](#co-op)), in any mix: solo with three AI, host plus one human and two AI, up to four
humans. The cap holds for every job type, base defence included, and is what the opposition budget,
the AI and the frame budget are sized against. The gang can own more pilots and planes than four;
the flight check chooses which four go up.

### Wingman orders

The captain gives orders to the whole flight, not to single wingmen. AI wingmen obey them,
weighted by their Teamwork skill; a co-op human in a seat sees each order as a HUD message.
The controls are chosen when the orders are implemented: either an extra layer on the d-pad, or
context buttons that reuse a control idle at that moment, as the respawn control is.

| Order | AI wingmen |
|---|---|
| Attack my target | Switch to the captain's current target |
| Cover me | Fly escort on the captain and engage whatever attacks them |
| Engage at will | Pick targets by the normal target ranking |
| Use rockets | Shorten their wait between rocket salvos, spending ordnance faster |

### Shot down: the relaunch

Every **human** in the flight (the captain and each co-op guest) has **one relaunch per sortie**.
A relaunch needs the gang's zeppelin on the sortie's map (see [Launch and
recovery](#the-zeppelin)) and a spare plane aboard: a fit plane in the hangar that is not already
flying. The pilot launches from the trapeze in that plane, and its damage and spent stock count
like any other plane's. AI wingmen never relaunch.

A captain with no relaunch left (already used, no zeppelin on the map, or no spare plane) ends the
sortie when shot down: the surviving wingmen withdraw, the contract pays only for objectives
already met, and the captain rolls for injury like any pilot.

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

The map is a new illustrated map of the broken-up continent, drawn by the remake (not the
original's briefing art), with ports as pins and the rivals' last known positions marked. It is
an SVG traced from the nations map at
<https://commons.wikimedia.org/wiki/File:Crimson_skies_map.png>, whose author (Zaku Two, English
Wikipedia) released it into the public domain, with a grant to use it for any purpose where that
release is not legally possible. Each nation is one SVG region with its own id, so the career can
colour, outline and label it at runtime (standing, heat, a rival's territory). The traced file
records that source and release in its own header. Its playable regions are the five the
original's worlds cover, each a cluster of **ports**, listed from easiest to hardest:

| Region | Worlds it draws on | Character |
|---|---|---|
| Hawaii | `C3` | Island chains, open sea, naval patrols. The easiest region. |
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

The player **chooses the starting region** when the career begins. Each region has a **threat
floor**, the lowest threat rating its boards offer, so a later start region is a harder career,
shown as a difficulty label on the choice, and not a richer start.

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
| Ammunition and rockets | when bought | stock bought into the hold at market price |
| Repairs | per day of work | per armour unit restored, per destroyed part replaced |
| Fuel | per travel day | scales with the zeppelin's engines and hull size |
| Wages | weekly | each crew member's wage |
| Zeppelin upkeep | weekly | scales with the number and level of modules |
| Mooring fee | weekly | per port, waived at an owned haven |
| Debt payment | weekly | the moneylender's instalment, with interest |

Money is the original's dollar figure, so the hangar's prices stay meaningful: a Warhawk airframe
costs $2,423 and a .50 Barret wing gun $410 (see [`org/hangar.md`](org/hangar.md)). The career's
payouts are set against those prices (see [Economy tunes](#economy-tunes)).

**Ammunition is stock.** Rounds and rockets are bought into the hold, the flight check loads them
from the hold, and the debrief returns what was not fired. Slug rounds are always for sale.
Specialist ammunition (armour-piercing, magnesium, dum-dum) and every rocket type are limited by
each market's weekly stock and cost more, so a player hoarding seekers for a bounty fight is
making a real choice, and a market can run out.

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

**Launch and recovery.** The zeppelin is on the sortie's map **only for jobs near its mooring**.
Then the flight launches from the trapeze, can relaunch from it (see [Shot down: the
relaunch](#shot-down-the-relaunch)) and recovers onto the hook. A job far from the mooring starts
in the air at the area's edge, with no zeppelin and so no relaunch; moving the zeppelin closer
first costs travel days. Landing a damaged plane on the hook is a small skill test
that saves the cost of a ferry flight.

**The gang's livery.** When a career starts, the gang picks a name and a **livery**: a paint
pattern, its three colours and a nose or tail decal, from the paint system the hangar already has
([`formats/paint.md`](formats/paint.md)). Every plane the gang owns wears the livery, including a
plane bought, captured or won from a Legends mission, so a flight reads as one gang in the air. A
single plane may still be painted its own way in the hangar (an ace's personal scheme), and can be
set back to the livery at any time. The livery can be changed at a port for a fee, which repaints
every plane still wearing it. The zeppelin's hull and the gang's flag take the livery's colours, and
rival gangs roll liveries of their own, so a third party in a sortie is told apart at a glance.

## The crew

### Pilots

The captain is the player's own pilot: they gain fame but no skills, since the player's hands are
the skill. The other pilots are wingmen in the flight roster, flown by the AI pilot. Each pilot has four skills from 1
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
"Cover me"), *Ace of another gang* (comes with a rival's grudge attached).

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

**Traitors.** Every crew member carries a very small chance of being a traitor in a rival's or the
law's pay, rising as their morale falls. A traitor pilot turns on the flight mid-sortie and flies
as an enemy; a traitor among the ground crew sabotages a plane or tips off an ambush. A traitor
pilot always gives themselves away first with a **radio warning**, a suspicious line on the
mission radio a few seconds before they break formation, so an alert captain can turn first. A
good radio operator can catch the signs before the sortie.

## The hangar: aircraft, parts and condition

**Each plane is an instance**, not a type. It has an airframe, an engine, guns per slot, pylons,
armour per zone and a paint scheme, exactly the record the custom-plane hangar already edits
(`CustomPlaneDef`), plus career state:

- **Condition per armour zone** (nose, tail, left wing, right wing), carried from the sortie: a
  plane that came home with its left wing shredded starts the next sortie that way unless it is
  repaired. The four zone values already exist in the saved-plane record.
- **Damaged parts**: a gun or an engine that took a hit is marked damaged and must be replaced or
  repaired.
- **History**: the pilots who flew it, its kills, the sorties it survived, kept as flavour. A plane
  with a long history earns a name the player can paint on it. There is no airframe wear.

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

### Added ground targets

A generated sortie may place **new objects into the original's worlds**: depots, fuel tanks, flak
emplacements, radio masts, parked aircraft, rail cars, moored boats, a crashed plane to rescue. The
career is remake-only, so this is always allowed in a career sortie, and it never changes what a
campaign or Instant Action mission shows.

- **The models come from the install.** A placed object is a model the original already ships
  (mission entities, destructibles, turrets, vehicles), with its own damage and destroy
  choreography. The repo still ships no assets; a template names models, it does not carry them.
- **Placement is checked against the built world.** A site offers a footprint; the placer samples
  the ground through the world query, refuses slopes above a limit, keeps a clearance from every
  original node and from the AI nets' flight volumes, and snaps the object to the ground height.
  A failed placement drops that object and the generator picks another site; it never floats or
  buries one.
- **Placed groups are prefabs.** A "fuel depot" is a small authored layout (tanks, a hut, two flak
  guns, a fence) in remake data, placed as one unit, so a generated target looks built on purpose.
  Each prefab carries its own tags, footprint and value for the opposition budget.
- **The catalogue learns from placements.** A site where prefabs fit reliably is marked so, which
  keeps the generator from retrying sites that fail every time.

**Determinism.** The spec is generated from a seed stored with the contract, so a contract shows
the same mission whenever it is flown and a reported bug can be replayed with `--career-seed=`.
The generator is engine-free and unit-tested: given a site catalogue and a seed, it returns a
spec, and the tests assert on the spec, not on pixels.

**Difficulty scaling.** A region's threat floor sets the easiest work it offers. Above that, the
opposition budget for a given threat rating grows slowly with the gang's fame, not with the
player's force. A rich gang is not punished for being rich; it is
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
every prize, and it unlocks better employers and better pilots in the pub.
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

## Legends: the story campaign on the career's timeline

The original's 24 campaign missions sit on the career's calendar as five **Legends acts**, one per
act of the story, in the story's order: Hawaii, Northwest, Hollywood, Colorado, Manhattan.

- **Each act opens on a fixed date.** The acts are spaced so the gang has weeks of contract work
  between them, whatever its fame.
- **The gang is warned in advance.** The bridge and the strategic map show the next act's region
  and the date the zeppelin must be there. Arriving in time starts the act; missing the date counts
  as skipping it.
- **An act is flown back to back.** Its missions come in their authored order, with only repair,
  refit and recovery days between them and no contracts. Contract work resumes when the act ends.
- **Acts are optional.** The player can skip an act. Skipping costs only what the act would have
  given (its pay, its unique salvage and its fame); later acts stay on the calendar.
- **The gang flies its own planes and pilots,** with their stored damage and stock, but only in
  the wingman seats the original mission authored, so the authored balance holds. Co-op guests fill
  those seats under the existing campaign co-op rules.
- **Airframe swaps are rewards.** When an authored mission swaps the pilot into a new aeroplane,
  the gang keeps that plane after the mission, added to the hangar as a plane instance.

Legends reuse the campaign's mission data and objective scripts unchanged; the career adds the
calendar, the payment and the salvage around them. This gives the career a hand-authored spine
without writing new story missions, and gives players who finished the campaign a reason to fly it
again with a gang they built.

## Failure and how a career ends

- **Bankruptcy.** When the ledger cannot pay the weekly bill, the shortfall is added to the
  debt. A gang with no plane fit to fly repairs on credit, and slug rounds are free, so a broke
  gang can always fly its way out.
- **The debt collectors.** A missed payment sends the moneylender's collectors after the
  zeppelin: a base-defence sortie against the collectors' fighters. If the zeppelin falls, the
  career ends. If the gang wins, the downed collectors leave salvage to sell or fit to the
  gang's own planes. This replaces seizure by a debt limit.
- **The zeppelin falls.** A base-defence sortie lost with the hull destroyed ends the career.
- **The captain dies.** With *Ironman* on, the player character's death ends the career. Without
  it, the captain is badly hurt and the gang must wait (and pay) for their recovery.
- **Retirement.** A career has no fixed end. A gang that beats every rival leader and survives the
  heat-100 hunt is offered a choice of endings, after which the career can continue.

## Career options

Set when a career starts, shown in the career's save slot:

| Option | Choices |
|---|---|
| Starting region | Any of the five; the region's threat floor sets the difficulty |
| Starting position | Debt-ridden (default), Privateer (a letter of marque, low heat, less money), Notorious (rich, already wanted) |
| Economy | Generous, Standard, Harsh (scales fees, prices and upkeep) |
| Pilot death | On (default), Off (always wounded) |
| Ironman | One save slot, saved on every state change, captain death ends the career |
| Second chances | On, Off |
| Assisted flight | The existing assisted-flight settings, unchanged |

## Co-op

A co-op career is the career of one **seated profile**, the host's, flown with the existing
campaign co-op rules (see "Campaign co-op" in [`CONTEXT.md`](../CONTEXT.md)). One to three other
humans join as **guests** and take wingman seats, on team id `1`; any seat no human takes is
filled by an AI pilot from the gang's roster, so the flight is always at most four planes (see
[The flight](#the-flight)). A guest flies one of the gang's planes and never brings one from their
own career. Their kills, spent stock and damage count against the gang like an AI wingman's, but a
guest has no wage, no injury and no persistent skills, and cannot spend from the ledger. Each guest
has the same one relaunch as the captain.

## Generated worlds (exploratory)

The five regions the original's worlds cover are a small part of the setting's broken-up North
America. Generated worlds would let the strategic map grow past them: each new region is a
**biome** standing for one stretch of that map, and its worlds are generated from a seed instead
of read from a chapter. Generated areas also mix into the five original regions as **separate
worlds** reached from the region's ports, beside its chapter worlds and never joined onto them,
so a familiar region offers new ground as well. This section is a direction to prototype, not a commitment; the career
works without it.

### Biomes

Each biome is a recipe: a terrain shape, a ground palette, vegetation sets, settlement style,
water, weather odds and the job types and powers it favours. The list is by geography; each
region on the strategic map is a nation of the traced map, and the biome is the one its ground
suggests: desert mesas for Arixo and the Navajo Territory, the Great Plains for the People's
Collective, Lakota Territory and the Republic of Texas, river delta and bayou for French Louisiana,
Great Lakes industry for the Industrial States of America, Appalachian ridges for the Appalachian
Territory, barrier islands for the Protectorate of the Outer Banks, alpine high country for Utah
and the Disputed Western Territories.

| Biome | Terrain | Settlements and targets | Favoured jobs |
|---|---|---|---|
| Desert mesas | flat basins cut by mesas, arroyos and slot canyons | adobe towns, mines, rail water stops | smuggling runs, rail raids, canyon duels |
| Great Plains | rolling grassland, river breaks, rare buttes | grain towns, oil derricks, long rail lines | convoy raids, escorts, patrols |
| River delta and bayou | flat wetland, braided channels, cypress swamp | stilt towns, riverboats, fuel barges | strikes, smuggling, boat convoys |
| Great Lakes industry | lake shore, flat farmland, dense factory cities | factories, steel mills, freighters, airship docks | strikes, zeppelin assaults, base defence |
| Appalachian ridges | parallel forested ridges and narrow valleys | mining towns, stills, rail tunnels | smuggling, recon, rescue |
| Atlantic barrier islands | sand islands, inlets, shallow sound | fishing harbours, lighthouses, pirate havens | prize captures, sea convoys |
| Northern lakes and pine | glacial lakes, pine forest, low hills | logging camps, seaplane bases | patrols, rescue, races |
| Alpine high country | peaks, passes, glaciers, high plateaus | passes with forts, observatories, cable cars | bounties, recon, duels |

### How a world is generated

1. **Terrain.** A heightfield from layered noise shaped by the biome's recipe, with a hydraulic
   erosion pass for valleys and a carve pass for the features the recipe names (a canyon, a river,
   a coastline). It is meshed into chunks at a resolution close to the original worlds', so it
   reads as the same game and stays inside the frame budget.
2. **Ground.** Ground textures are picked from the **install's own textures** by slope, height,
   moisture and biome, and blended per chunk. Nothing new is painted or shipped; a biome names the
   textures it uses.
3. **Water.** Sea and lakes use the wave ocean (`Ocean`, `SeaState`) with a shore mask baked from
   the generated ground, the same way it is baked for a chapter.
4. **Vegetation and towns.** The Rich vegetation scatter and the Enhanced city's buildings already
   stamp models onto ground by rule; a generated world drives them from the biome's sets instead
   of from a chapter's sidecar. Towns are laid along roads and rail by a small layout grammar
   (main street, rail yard, airfield, docks).
5. **Flight data.** The generator writes what a chapter's data would hold: AI patrol nets along
   valleys and roads, landing approaches for every airfield, spawn tables, and the **site
   catalogue** the mission generator reads. A generated world then feeds the same contract and
   mission pipeline as an original one.
6. **Sky.** Weather odds, fog, cloud deck and light per biome through the existing weather and
   zone lighting.

A world is a function of its seed and biome, so it is never saved, only regenerated, and a cache
keeps the built meshes between sorties in the same region.

### Risks to prototype first

- **The look.** The original's worlds are hand-painted; generated terrain under the same textures
  can read as noise. The first prototype is one biome (desert mesas suit low-detail ground best)
  judged at the controls beside a chapter world.
- **Collision and AI.** The AI's terrain avoidance and the ground contact were tuned on the
  original's meshes. Generated ground must give them the same kind of surface.
- **Build time.** Generation must fit inside a loading screen, or happen on travel days with the
  result cached.
- **Variety.** Eight recipes and a seed give many worlds that look alike. Hand-placed **landmarks**
  per region (a dam, a giant studio sign, a ruined fort), placed by the generator as prefabs, give
  each region a memory.

## Screens

The career's out-of-mission screens are remake-only screens of the Original presentation, drawn
on the scrapbook like the join board, behind a CAREER door under JOIN BOARD on the main menu
(`OriginalCareerScreen`). Where a career screen matches an existing Original page (briefing, flight
check, ammo selection, hangar), that page is reused. The career's state and operations stay
engine-free in `CareerFeature` and `Session.Career`, and a screen only shows and edits them, so a
later presentation can draw the same career without touching its logic.

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
| Added ground targets | mission entities, destructibles, turrets, the world query | a placer and the prefab layouts |
| Generated worlds | `EmptyStage`, Rich vegetation and city stamps, the wave ocean | everything in [Generated worlds](#generated-worlds-exploratory) |
| Building a session from a description | `InstantActionDef` and its producers | a mission spec rich enough for convoys, targets and waves |
| Enemy aircraft and their AI | `AiPilot`, target ranking, AI nets, enemy generators | AI skill parameters exposed per pilot |
| Wingmen | the flight check's crew slots, `AiEscort` | the four flight orders, three wingmen in formation |
| Relaunch | the respawn rule, zeppelin trapeze positions | one relaunch per human from a spare plane |
| Zeppelins | broadside, damage, motion, replicas | a player-side zeppelin as the base |
| Ladder boarding | the ladder switch, the airframe swap | boarding a prize instead of a plane |
| Planes and parts | `CustomPlaneDef`, the hangar, prices and weights | per-instance condition and a parts hold |
| Ammunition | `FireControl`'s draw-down, the ammo selection screen | loading from and returning to the hold's stock |
| Damage | armour and health per zone | carrying zone state out of the sortie |
| Objectives and HUD messages | objectives, mission targets, `HudMessages`, `MissionRadio` | objectives from a generated spec |
| Courses for runs | stunt danger zones, the course runner, stunt race bots | courses as smuggling routes and recon checkpoints |
| Out-of-mission flow | `CampaignFlow`, board pages | the career pages, the strategic map |
| Co-op | campaign co-op over the net session | ledger charging for guests |
| Saving | campaign profiles, `user://` stores | a career save |

## Data model sketch

Engine-free types, unit-tested on their own, in a `Career` namespace beside `Session`:

- `CareerState`: the whole save. Calendar day, ledger, zeppelin, crew, hangar, hold, standing
  per power, heat, fame, rivals, the open contracts per visited port, the Legends timeline (each
  act's date and outcome), the event log, the options.
- `Ledger`: entries by day with a category and an amount, the cash balance and the debt, and the
  weekly bill computed from the rest of the state.
- `Zeppelin`: modules and levels, hull and cell health, the port it is moored at or its travel
  order.
- `CrewMember`: role, name, skills, traits, wage or share, morale, injury and the days left on it,
  experience; pilots also hold their kill and sortie history.
- `PlaneInstance`: the `CustomPlaneDef` record plus zone condition, damaged parts and history.
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
| Ammunition | slug rounds cheap and always stocked, AP and magnesium three times the price, rockets $40 to $250 each |
| Salvage | 40 % of the market price when sold at once, 60 % at a broker |

The intended feel: the first two weeks are tight and every sortie matters, a well-run gang is
comfortable by the end of the first region, and the zeppelin upgrades are the long-term money
sink.

## Roadmap

Each phase ends in a playable state, so the design is tested at the controls before the next
phase builds on it.

1. **The loop on one world.** `CareerState`, the ledger and the calendar, and a contract board
   with two job types (patrol and strike) on one chapter, generated by seed, with added ground
   targets. Ammunition is stock in the hold, zone damage and damaged parts carry between sorties,
   the debrief bills repairs. One port, no travel, no crew skills.
2. **Wingmen and the market.** Pilots with skills mapped to the AI, hiring, wages, injury. The
   market and the parts hold, the hangar fitting from stock. Salvage. The gang livery, picked through
   the hangar's paint screen.
3. **The zeppelin and travel.** The strategic map with all five regions, ports, travel days and
   fuel, zeppelin modules, launch and recovery, base defence.
4. **The full board.** The remaining job types, negotiation, modifiers, bonus objectives, prizes
   and the ladder boarding.
5. **The living sky.** Standing, heat and fame, rival gangs and named aces, events, the Articles
   and morale, mutiny and traitors, the debt collectors' attack on the zeppelin.
6. **Legends and co-op.** The Legends acts on the calendar, guests in the wingman seats with their
   relaunch, the career options (starting region included) and Ironman.
7. **Generated worlds (exploratory).** A single desert-mesa biome as a prototype outside the
   career, judged at the controls beside a chapter world. Only if it holds up: the generator's
   flight data and site catalogue, then new regions on the strategic map, one biome at a time.

The ground-target placer and the prefabs arrive in phase 1, since a strike contract needs them.
The four flight orders and the relaunch arrive with the wingmen and the zeppelin. Three AI wingmen in formation are proven in phase 2 against the frame budget, with the worst case
(four planes, a zeppelin and a full opposition budget) as the perf scenario.

## Open questions

None at present. The controls for the flight orders are decided when the orders are implemented
(see [Wingman orders](#wingman-orders)).
