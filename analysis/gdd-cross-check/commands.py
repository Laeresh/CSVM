"""Q: which player-facing commands did the RETAIL game ship?

messages.json's MSG_CMD_*/MSG_CAM*/MSG_PADLOCK_* block is the controls-configuration screen's
label set — what the shipped binary let a player bind. It is better evidence than any asset-name
sweep (an absent filename proves nothing) or than the pre-release design document (whose own
keyboard table contradicts itself).

Cited by docs/formats/strings.md ("The bindable-command table").
"""
import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zrdrlib import load

entries = sorted(load("messages.json")["entries"], key=lambda e: e["id"])
print(f"messages.json entries: {len(entries)}")

CMD = re.compile(r"^MSG_(CMD_|CAM2_|CAM_ZOOM|PADLOCK_|LOOK_|NOSE_|ROLL_|RUDDER_|LEVEL_TOG"
                 r"|INC_THROTTLE|DEC_THROTTLE|THROTTLE_\d|MISSILE_NEXT|FIRE_MISSILE"
                 r"|JETTISON_|CHAT_|WINGMAN_(ENGAGE|FIRE|BKT_))")
# MSG_WINGMAN_SHOT_DOWN is a notification and MSG_DLG_CONTROLS a dialog title — both look like
# commands by name. Excluded above / by the id gate below; recount if either rule is relaxed.
HEAD = re.compile(r"^MSG_\w+_CONTROLS$")
DEV = re.compile(r"^MSG_(KEYA|KEYB|JOYBTN|MOUSEBTN|JBTN_|MBTN_)")

print("\n--- category headings (the 3005-3011 block) ---")
heads = [e for e in entries if HEAD.match(e["key"]) and 3000 <= e["id"] < 3100]
for e in heads:
    print(f"  {e['id']:>6}  {e['key']:<26} {e['value']!r}")
print(f"  ({len(heads)} headings)")

cmds = [e for e in entries if CMD.match(e["key"])]
print(f"\n--- bindable commands: {len(cmds)} ---")
for e in cmds:
    print(f"  {e['id']:>6}  {e['key']:<32} {e['value']!r}")

print("\n--- binding device vocabulary ---")
for e in entries:
    if DEV.match(e["key"]):
        print(f"  {e['id']:>6}  {e['key']:<32} {e['value']!r}")

print("\n--- the claims this settles ---")
by = {e["key"]: e["value"] for e in entries}
for k in ("MSG_CAM2_TOG", "MSG_CMD_PADLOCK_SNAP", "MSG_CMD_PADLOCK_STICK", "MSG_CMD_PADLOCK_WATCH",
          "MSG_CMD_PAUSE_GAME", "MSG_CMD_LAUNCH_AUTO_LAND", "MSG_CMD_BAIL_OUT", "MSG_CMD_NITROUS",
          "MSG_CMD_CANNON_NEXT", "MSG_CMD_CANNON_PREV", "MSG_KEYA", "MSG_KEYB", "MSG_HUD_HEALTH"):
    print(f"  {k:<28} {by.get(k, '*** ABSENT ***')!r}")
tg = sorted(k for k in by if k.startswith("MSG_CMD_TARGET_"))
print(f"  targeting suite ({len(tg)}): {tg}")
