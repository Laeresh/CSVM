# The matrix both instruments run: label -> user args. Dot-sourced by capture.ps1 (which records
# what each line resolves to) and by compare.ps1 (which checks SessionSpec resolves it the same),
# so the two can never drift into testing different command lines.
#
# It is weighted toward the modes the pixel goldens do not reach at all (--anim-lab, --stunt,
# splitscreen, the menu), because those are the ones where a green RunTests.ps1 says nothing about
# whether a launch still resolves the way it used to. The label is what a diff names, so keep them
# stable.

# Grouped by what each group is here to pin down.
$matrix = [ordered]@{
    # No content arg: the launchscreen base every menu launch is patched on top of.
    "menu-bare"             = @()
    "menu-forced"           = @("--menu")
    "menu-screen"           = @("--menu=plane")
    "menu-over-content"     = @("--menu", "--chapter=C4", "--plane=player_fury")

    # Flight -- the default for any content arg.
    "fly-bare"              = @("--fly")
    "fly-plane"             = @("--plane=player_fury")
    "fly-chapter"           = @("--chapter=C4")
    "fly-mission-spawn"     = @("--fly", "--chapter=C2", "--mission=M01", "--spawn=3")
    "fly-pos-dir"           = @("--fly", "--pos=100,200,300", "--direction=0,0,-1")
    "fly-pos-lookat"        = @("--fly", "--pos=100,200,300", "--lookat=0,0,0")
    "fly-dir-no-pos"        = @("--fly", "--direction=0,0,-1")
    "fly-view"              = @("--fly", "--view=4")
    "fly-deprecated"        = @("--fly", "--campos=1,2,3", "--spawn-at=4,5,6", "--spawn-dir=0,0,-1")

    # The --det bundle: implied, opted out of, and overridden constituent by constituent.
    "det-explicit"          = @("--fly", "--det")
    "det-implied-shot"      = @("--fly", "--screenshot=.scratch/session-baseline.png")
    "det-opted-out"         = @("--fly", "--screenshot=.scratch/session-baseline.png", "--no-det")
    "det-overridden"        = @("--fly", "--screenshot=.scratch/session-baseline.png", "--seed=7", "--spawn=2", "--paint-seed=99", "--jitter=0.5")
    "det-burst"             = @("--fly", "--screenshot=.scratch/session-baseline.png", "--shots=4", "--frames=30")

    # Stunt -- no golden covers it.
    "stunt-bare"            = @("--stunt")
    "stunt-scenario"        = @("--stunt", "--scenario=zeppelin_run")
    "stunt-2p"              = @("--stunt", "--plane=player_bhawk,player_fury")

    # Splitscreen -- no golden covers it either, and the count arrives three different ways.
    "split-3p"              = @("--fly", "--players=3")
    "split-implied"         = @("--plane=player_bhawk,player_fury,player_kestrel")
    "split-clamped"         = @("--viewer", "--players=2")

    # The static inspection view and its labs.
    "viewer-bare"           = @("--viewer")
    "viewer-chapter"        = @("--viewer", "--chapter=C3")
    "viewer-damage"         = @("--viewer", "--damage")
    "viewer-beats-fly"      = @("--viewer", "--fly", "--plane=player_fury")
    "viewer-weaponlab"      = @("--weapon-lab=wep_06", "--weapon-mount=firepoint1")
    "viewer-node"           = @("--node=kkgate")

    # The spectator view.
    "freecam-bare"          = @("--freecam", "--chapter=C2")
    "freecam-collision"     = @("--freecam", "--chapter=C2", "--collision=show", "--debug-nodelab=deps")
    "freecam-beats-fly"     = @("--freecam", "--fly")
    "freecam-debug-damage"  = @("--freecam", "--debug-damage=kill")

    # The animation lab -- the most specific mode, and the one with the most field sites.
    "animlab-bare"          = @("--anim-lab", "--chapter=C2")
    "animlab-play"          = @("--play-anim=train")
    "animlab-beats-all"     = @("--anim-lab", "--fly", "--viewer", "--freecam")
    "animlab-node"          = @("--anim-lab", "--node=kkgate")

    # The synthetic stage, accepted and twice refused.
    "stage-empty"           = @("--stage=empty")
    "stage-empty-in-viewer" = @("--viewer", "--stage=empty")
    "stage-unknown"         = @("--stage=nosuch")

    # The probes that wear a mode as a disguise, coercing it at parse time.
    "probe-damage-test"     = @("--damage-test", "--damage-hd=25")
    "probe-effects-test"    = @("--effects-test")
    "probe-weapon-test"     = @("--weapon-test")
    "probe-run-tests"       = @("--run-tests=weapons")
    "probe-dump-flight"     = @("--dump-flight")

    # Modifiers that touch no mode.
    # --paint-color takes bytes, not floats, and repeats the last slot when given fewer than three.
    "paint-overrides"       = @("--fly", "--paint=none", "--paint-color=255,0,0/0,255,0/0,0,255", "--paint-decal=1,2,3")
    "paint-one-slot"        = @("--fly", "--paint-color=255,128,0")
    "tex-instruments"       = @("--freecam", "--tex-census", "--tex-override=foo", "--no-fog")
    "misc-modifiers"        = @("--fly", "--mute", "--no-vsync", "--perf", "--no-pads", "--infinite-ammo", "--fire", "--gun-select=1")
}
