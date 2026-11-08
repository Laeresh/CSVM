# Making an air race course

CSVM has a built-in course editor. You fly a free camera over a chapter's world, click to place
gates, set the order they are flown in, and save. The result is one small file you can race, and
share by copying it.

## Opening the editor

Start CSVM with `--course-edit=` and a chapter. The extra `--` on its own is required. On Windows,
from a terminal in the CSVM folder:

```
CSVM.exe -- --course-edit=C1
```

On Linux or the Steam Deck:

```
./CSVM.x86_64 -- --course-edit=C1
```

This starts a new course over that chapter. To open a course, or start a new one with a chosen
name, add the name:

```
CSVM.exe -- --course-edit=C3/Valley_Run
```

The courses that ship with CSVM:

| Chapter | Title | Name |
|---|---|---|
| C1, NW Boeing Field | Airfield Out and Back | `sample_out_and_back` |
| C1B, NW Lighthouse | Island Loop | `Island_Loop` |
| C2, Hollywood Studio | Studio Lot Dash | `Hollywood_Track_1` |
| C3, Hawai'ian Islands | Harbour Ring Run | `Harbour_Ring_Run` |
| C3, Hawai'ian Islands | Valley Run | `Valley_Run` |
| C4, Sky Haven | Crater Lakes | `Crater_Lakes` |
| C5, Manhattan | River Run | `River_Run` |

## Controls

| Input | What it does |
|---|---|
| Hold right mouse button and move | Look around |
| W, A, S, D, Q, E | Move the camera |
| Shift or Ctrl while moving | Change the camera's speed |
| Mouse wheel | Set the camera's base speed |
| Left click on a gate | Select it |
| Left click elsewhere | Place what **Left click places** is set to |
| Drag a gate's arrows | Move the selected gate |
| Drag a gate's ring | Turn the selected gate; hold Ctrl to snap to 15 degree steps |
| Hold H and move the mouse | Slide the selected gate over the ground under the cursor |
| Shift+F3 | Hide or show the editor and the course |

There is no collision in the editor, so the camera flies through anything. After typing in a name
or title field, press Enter to give the movement keys back to the camera.

## Building a course

A course has a start point, a set of gates, and a sequence that says which gate is flown next and
which way round.

**1. Name it.** The **course name** field is the file name, and the **title** field is what players
see. A blank title shows the name. **New** clears the editor and starts an empty course under the
typed name.

**2. Place the gates.** Choose **Pylon gate** or **Balloon ring** under **Left click places**, face
the way the racer should fly, and click the spot. Over the sea, the click lands on the water. A new
gate faces the way the camera faces, and it joins the sequence flown that way.

| Gate | Default size | Slider range |
|---|---|---|
| Pylon gate | 40 m wide, 30 m high | Width 5 to 200 m, height 5 to 120 m |
| Balloon ring | 40 m across, 50 m above the ground | Diameter 5 to 200 m, height above the ground 5 to 500 m |

A pylon gate always stands on the ground, and it follows the ground as you move it. A ring has an
extra arrow for its height. In the race, balloon rings bob, drift and roll on their own. The
**heading** slider turns either kind. **Nothing (click selects)** stops clicks from placing, which
helps when selecting gates in a crowded area.

**3. Set the start.** A new course starts at the camera. To change that, either press **Start here**
to put the start 2 m ahead of the camera, facing its way, or choose **Start point** under **Left
click places** and click the ground, which sets it 50 m above that spot. Races begin from a flying
start, so leave a clear run to the first gate.

**4. Order the sequence.** The **Sequence** panel lists every entry in flying order, and in the
world a numbered label sits over each gate with a line joining them in order.

- A new gate goes in after the selected row, or at the end if no row is selected. Clicking a gate
  in the world does not change that row.
- Clicking a row selects it and flies the camera to just behind that gate, looking along the
  approach.
- **Up** and **Down** move the selected row.
- **Flip** reverses the direction of the selected row. A gate only counts when it is flown the way
  its entry says.
- **Forward** and **Reverse**, under **Add the selected gate again**, fly the same gate a second
  time. That is how an out-and-back course reuses its gates.
- **Remove** takes out one row and keeps the gate. **Delete gate** removes the gate and every row
  that uses it.

**5. Save.** The button under the sliders writes the file, and the note below it shows the exact
path. In a downloaded build, that is `Courses\<chapter>\<name>.json` beside the executable. Saving
overwrites a file of the same name without asking, so change the name first to keep the old one.

Saving a shipped course this way does not change the shipped file. Your copy goes in the `Courses`
folder and takes its place. Delete your copy to get the original back.

## Flying your course

The editor has no test flight, so close it and start the course:

```
CSVM.exe -- --course=C1/my_course
```

You can also start CSVM normally. The course appears in the **Course** box in Instant Action's
Stunt Flying, and on a lobby's Mission Options for a Stunt Race. A course needs at least one entry
in its sequence to be flown.

Best times are kept per course and per plane. Moving a gate, changing the sequence, moving the
start or renaming the course starts a fresh set of best times. Changing only the title keeps them.

## Sharing a course

A course is one file. To give it to someone, send them the `.json` file. They put it in
`Courses\<chapter>\` beside their executable, using the same chapter folder.

In a network Stunt Race, only the host needs the file. The host picks it in the Course box and it
reaches every guest, who keeps a copy. A very large course is left out of the lobby's list, because
it no longer fits in a network message.

A file that cannot be read shows its reason on the editor's status line when you open it. Files are
plain JSON and can be edited by hand. `CSVM\data\courses` in the repository holds the shipped
courses as examples.
