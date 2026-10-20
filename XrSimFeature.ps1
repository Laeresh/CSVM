# The one per-launch environment switch the game launchers share: the Godot feature tag the
# --xr-sim stand-in headset needs at engine start. Dot-sourced by RunTests.ps1, RunProbe.ps1 and
# RunGame.ps1; see docs/tooling.md.

<#
.SYNOPSIS
Runs $Launch with Godot's custom feature tag csvm_xr_sim in the environment when the CSVM arguments
(those after "--") carry --xr-sim, and puts the variable back afterwards.

.DESCRIPTION
The --xr-sim stand-in headset renders in stereo, which needs Godot's multiview shader variants, and
Godot compiles those only at engine start. project.godot turns them on under the csvm_xr_sim feature
tag, and the editor binary reads custom tags from GODOT_EDITOR_CUSTOM_FEATURES. A process inherits
the variable at creation, so it is set around the start alone: concurrent launches without the flag
(the other golden workers) and every later launch from the same shell never see it.
#>
function Invoke-WithXrSimFeature {
    param(
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [Parameter(Mandatory=$true)][scriptblock]$Launch
    )
    $xrSimSep = [Array]::IndexOf($Arguments, "--")
    $xrSimUserArgs = if ($xrSimSep -ge 0) { @($Arguments | Select-Object -Skip ($xrSimSep + 1)) } else { @() }
    $xrSimWanted = @($xrSimUserArgs | Where-Object { $_ -eq "--xr-sim" -or $_ -like "--xr-sim=*" }).Count -gt 0
    if (-not $xrSimWanted) {
        return & $Launch
    }
    $xrSimVar = "GODOT_EDITOR_CUSTOM_FEATURES"
    $xrSimBefore = [Environment]::GetEnvironmentVariable($xrSimVar, "Process")
    $xrSimTags = if ($xrSimBefore) { "$xrSimBefore,csvm_xr_sim" } else { "csvm_xr_sim" }
    [Environment]::SetEnvironmentVariable($xrSimVar, $xrSimTags, "Process")
    try {
        return & $Launch
    } finally {
        [Environment]::SetEnvironmentVariable($xrSimVar, $xrSimBefore, "Process")
    }
}
