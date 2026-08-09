<#
.SYNOPSIS
    PLAN-flight-model-rewrite A2 baseline: evaluate the current model's zero-thrust drag formula
    at CAP-05's four measured points, purely from the committed constants in FlightModel.cs.

.DESCRIPTION
    CAP-05's drag points are not produced by any --dump-flight row: they are the raw video
    measurements that ThrustConst/DragExpLow/DragExpHigh were fitted against (see
    src/Flight/FlightModel.cs, the "CAP-05's zero-thrust clip" comment block). There is no
    scenario in the flight-envelope suite that isolates zero-thrust drag on its own, so this
    script re-evaluates the closed-form drag(x) = maxThrustAccel * x^DragExpLow directly, with
    alpha = 0 (level, unaccelerated flight, so the induced term is exactly zero) -- the same
    formula FlightModel.Step applies at src/Flight/FlightModel.cs's dragAccel line.

    This is pure arithmetic on committed constants: no Godot, no extracted data, no clock. Its
    output cannot vary between machines or runs (verification.md DET-9).

.EXAMPLE
    .\analysis\flight-model-baseline\cap05-drag-points.ps1
#>

# Bloodhawk-only: CAP-05 is Bloodhawk footage, and MaxThrustAccel/DragExpLow are pinned jointly
# against it (FlightModel.cs, ThrustConst's doc comment).
$maxThrustAccel = 34.92   # m/s^2, printed by --dump-flight's header for player_bhawk
$dragExpLow = 3.278       # FlightModel.cs private const DragExpLow

# x = V / fd_speed, measured drag accel in m/s^2 (FlightModel.cs's DragExpLow comment)
$points = @(
    @{ X = 0.25; Measured = 0.36 },
    @{ X = 0.35; Measured = 1.11 },
    @{ X = 0.46; Measured = 2.82 },
    @{ X = 0.50; Measured = 3.74 }
)

# Force invariant (dot-decimal) formatting regardless of the host's locale, so this file's
# committed output cannot mojibake into comma-decimal on a different machine (SHELL-4/SHELL-7).
$inv = [System.Globalization.CultureInfo]::InvariantCulture

Write-Output "x      model(m/s^2)  measured(m/s^2)  err"
foreach ($p in $points) {
    $model = $maxThrustAccel * [math]::Pow($p.X, $dragExpLow)
    $err = ($model - $p.Measured) / $p.Measured * 100
    $line = [string]::Format($inv, "{0,-6:0.00} {1,13:0.0000}  {2,15:0.00}  {3:+0.0;-0.0}%", `
        [double]$p.X, [double]$model, [double]$p.Measured, [double]$err)
    Write-Output $line
}
