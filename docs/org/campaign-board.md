# The campaign board: authored pixels on a modern window

The campaign's six out-of-mission screens (profile, cabin, previous missions, briefing, flight
check, ammo selection) are composed boards, not row lists: each places its art at the coordinates
the original authored, over that screen's own painted background, with the original's own button
plaques. This page holds the one decision every one of them inherits, which library each coordinate
came from, and what is deliberately not reproduced yet.

The code is `CSVM/src/UI/BoardFit.cs` (the mapping), `ComposedBoard.cs` (what a board is made of),
`CampaignBoards.cs` (the authored geometry and the composer) and `ComposedBoardView.cs` (the
renderer). The decodes the coordinates come from are
[`formats/campaign-screens.md`](../formats/campaign-screens.md) and
[`formats/briefing.md`](../formats/briefing.md).

## Contents

- [The authored space](#the-authored-space)
- [Decision: uniform fit, letterboxed, nearest](#decision-uniform-fit-letterboxed-nearest)
- [Where each coordinate comes from](#where-each-coordinate-comes-from)
- [Button plaques and their four frames](#button-plaques-and-their-four-frames)
- [Which player the profile screen opens on](#which-player-the-profile-screen-opens-on)
- [What is not reproduced](#what-is-not-reproduced)

## The authored space

Every campaign screen is authored against a fixed **800x600** dialog, and three independent
extractions agree on it. `PC_BackGround.png`, `FC_BackGround.jpg`, `OL_BackGround.jpg` and every
briefing map bitmap (`ha-m1map.png` and its twelve siblings) are exactly 800x600. `ASSETS\LAYOUT.CSV`
places the cabin's last button at `593,561` and the flight check's at `551,<GX>`, both of which only
sit on a bottom edge at that height. `Briefing.zrd`'s `BUTTONS` section puts its three plaques at
`y = 560` with a 32-pixel bitmap, which ends 8 pixels short of 600.

The reference screenshots under `OriginalScreenshots\Campaign *.png` are 802x634: an 800x600 client
area inside a one-pixel border under a 33-pixel title bar. Subtracting `(1, 33)` maps a screenshot
pixel onto an authored one, and that is the transform every measurement below was taken through.

## Decision: uniform fit, letterboxed, nearest

**One uniform scale on both axes, `min(viewportWidth / 800, viewportHeight / 600)`, the board
centred in the window, the remainder filled black, and every bitmap sampled nearest-neighbour.**
At the project's 1280x720 window that is a scale of 1.2, a 960x720 board and a 160-pixel bar on each
side.

The two alternatives were considered and rejected.

- **An integer scale of the authored resolution.** The pixel grid stays exact, but `floor` of the
  fit is 1 at 1280x720 and still 1 at 1920x1080, because 2x needs 1200 lines. That draws an
  800x600 island in the middle of a 1080p window with a black surround wider than the board is tall.
  A screen the player cannot read is worse than one whose pixels are unevenly doubled.
- **Fit to height with the background bled or cropped horizontally.** The composition does not
  survive it. The plaques hang off the authored bottom edge and the parchment starts at `x = 0`, so
  widening the frame either pulls those elements away from the edges they are anchored to or crops
  them off. There is also nothing to bleed: the background bitmaps are exactly 800x600 with no
  margin outside the composed frame.

Nearest-neighbour is the half of the decision that answers the "soft upscale reads as a bug"
objection. A fractional scale duplicates some source rows and not others, so the art reads as
chunky rather than blurred, which is how a scaled-up original reads and not how a resampled one
does. The filter is set once, on the view (`ComposedBoardView.Build`), so it covers every bitmap a
board draws.

Text is the exception and is drawn as a real face at `scale * authoredSize`, never as scaled
bitmap glyphs, because the original's own font table is not decoded and a bitmap font would have to
be invented.

## Where each coordinate comes from

Three sources, and a reader should know which one is under any given number.

- **`ASSETS\LAYOUT.CSV`**, for the five script-driven screens. Each widget row carries its art path
  and its X,Y directly, and those are used verbatim.
- **`Briefing.zrd`'s `BRIEFINGDIALOG`**, for the briefing's parchment (`POSITION [0, 295]`), its
  title (`[35, 315]`), its list (`[35, 335]`, `WORDWRAP [185, 240]`) and its three plaques
  (`[197, 560]`, `[397, 560]`, `[597, 560]`).
- **Measured off the reference screenshots**, for the handful of rows the shipped layout leaves as
  unresolved authoring macros. Each was found by matching the button's own bitmap against the
  screenshot at every offset and taking the best fit; the X the match returned agreed with the
  layout row's own X in every case, which is what makes the Y trustworthy.

| Macro | Screen | Resolved | How |
|---|---|---|---|
| `<GX>` | flight check, ammo selection | `y = 553` | matched `FC_B_ReturnToBriefing.png` at `x = 341` (layout's own X) |
| `<Y>` | profile | `y = 547` | matched `CM_B_DeletePlayer.png` at `x = 193` |
| `<V2>` / `<V3>` | flight check | `y = 131` / `y = 349` | the CHANGE AMMO plaque's edges in `Campaign Flight Check.png` |
| `<V4>` | flight check | `FC_B_PaperButton.png` | the only paper button whose 117-pixel width matches the drawn plaque |

Two positions are chosen rather than decoded, and both are marked as such in the code. The cabin's
memento window (`179, 330`, 73x84) is the block each `PC_P_HANGAR*.JPG` keys out for it, and the
picture inside it is always the campaign's opening keepsake because choosing one is not shipped.
The profile screen's title mark (`MM_Logo.png` at `134, 13`) is matched off `Campaign Player
Profile.png`.

## Button plaques and their four frames

Every screen-specific button ships as one PNG holding **four stacked frames of equal height**, in
the order `LAYOUT.CSV`'s own colour columns name them: **disabled, normal, rollover, depressed**.
`PC_B_ReturnMainMenu.png` is 200x128, so four 200x32 frames; `CM_B_Start.png` is 113x136, so four
113x34. The words are painted into the art, so the state is entirely in which frame is drawn.

The generic paper buttons (`FC_B_PaperButton.png`, `SB_B_PaperButton.png`) and the briefing's
`brief_button1` carry no words: their layout rows name an `IDS_*` string instead, and the label is
drawn over the plaque. `brief_button1` is a single frame, and its focus state lives entirely in the
label's face, which is what the dialog's `BtnLabelNormal` / `BtnLabelRollover` / `BtnLabelActivate`
triad is for.

On a pad, focus is the rollover frame and a held confirm is the depressed one. That is the whole of
what changed about focus: the cursor was a highlighted text row and is now a plaque in the state
the original's own art already carried.

## Which player the profile screen opens on

The screen opens with the cursor on the profile last played, already ticked, so continuing a
campaign is one press. The record is the profile **name**, held in `user://Profiles/last-played.json`
beside the profile directories, written when a profile is seated and cleared when it is deleted.

The name, and a store of our own, are both what the decode says. The original keeps the current
player as a name too, in `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Microsoft Games\Crimson Skies\1.0`
under `UIPlayerName`, read back into `UIData +0x314` at startup
([saved-games.md](../formats/saved-games.md), "Which player is current, across runs"). Nothing in
the save container carries it, so there is no file to read and the semantics are all the decode
supplies. Writing that registry key itself is rejected: it is the retail game's own live state, and
a remake that edits it changes what the original game does on the same machine.

⚠ **A row index and a directory timestamp are both wrong and neither is used.** The roster sorts
alphabetically, so a stored row moves whenever a profile is created or deleted; and every save
touches a profile's directory, while a restore or a copy rewrites all of them at once. A recorded
name is resolved against the roster on each visit, and one the roster no longer carries opens the
cursor on the name field instead.

## What is not reproduced

Named so nobody reads their absence as a decode gap.

- **The profile screen's animated flag** (`CM_MOVIE`, `CrimFlag.MPG`). No MPG reader exists here and
  the shipped still (`MM_BackGround.png`) is a placeholder image reading "CS BACKGROUND", so the
  title mark stands over a plain ground.
- **The cabin's map pins** (`PC_PIN0`-`PC_PIN4`, `PC_mappins.png`). The layout puts them at z 0,
  behind a background at z 4, which cannot be what a pin on the desk map means; the count rule is
  tested in `CampaignCabinPage.MapPinCount` and the drawing waits on that contradiction being
  settled.
- **Per-widget chrome**: dropdown boxes, scrollbars, listbox selection bars, the scrapbook's kill
  markers and stat card. The boards draw their screens' backgrounds, plaques, pictures and text;
  the widgets those text runs sit in are each their own fidelity question.
- **The flight check's objectives note.** The parchment is drawn from the background art, but the
  page does not load the mission's objectives, so it is blank.
