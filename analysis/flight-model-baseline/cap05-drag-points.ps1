<#
.SYNOPSIS
    Evaluate the flight model's zero-thrust drag at CAP-05's four measured points, purely from the
    committed constants in FlightModel.cs.

.DESCRIPTION
    CAP-05's drag points are not produced by any --dump-flight row: they are raw video measurements
    of a zero-thrust deceleration (0.36 / 1.11 / 2.82 / 3.74 m/s^2 at x = V/fd_speed =
    0.25 / 0.35 / 0.46 / 0.50), and there is no scenario in the flight-envelope suite that isolates
    drag on its own. This script re-evaluates the model's closed-form drag directly.

    NOTE: it now measures the NEW model. Until PLAN-flight-model-rewrite B12 the formula was
    drag(x) = maxThrustAccel * x^DragExpLow, a power law those four points had been FITTED to.
    B12 replaced it with the original's parabolic polar in the delivered lift coefficient:

        q     = 0.5 * rho * V_fps^2                        lb/ft^2, dense band
        C_L   = min(n, clMax * q * S / W) * W / (q * S)     n = nom_gravity / 9.82, level flight
        C_D   = 0.73 * (0.12 + 0.8*C_L + 0.5*C_L^2)
        drag  = q * S * drag_factor * C_D * 9.82 / W        m/s^2

    so the four points are now an independent CHECK of an authored curve rather than a re-reading of
    a fit. Evaluated at level, unaccelerated flight (alpha = 0), which is the same condition the old
    script used and the condition under which the demanded load factor is exactly nom_gravity/9.82.

    This is pure arithmetic on committed constants: no Godot, no extracted data, no clock. Its
    output cannot vary between machines or runs (verification.md DET-9).

.EXAMPLE
    .\analysis\flight-model-baseline\cap05-drag-points.ps1
#>

# Bloodhawk-only: CAP-05 is Bloodhawk footage. Airframe values are vehicle.json's 'dynamics' block
# for pbloodhawk (extracted/zrdr/vehicle.zrd.json); the rest are FlightModel.cs constants.
$fdSpeed    = 135.0     # m/s, fd_speed
$vehWeight  = 1900.0    # veh_weight
$refArea    = 330.0     # ref_area
$dragFactor = 0.37      # drag_factor
$gravity    = 20.0      # player.json nom_gravity, m/s^2

$rho          = 2.2688e-3   # AirDensitySlugPerFt3, dense band
$feetPerMetre = 3.28084     # FeetPerMetre
$soundMs      = 1109.5 * 0.3048   # SpeedOfSoundFps * MetresPerFoot
$standardG    = 9.82        # StandardG
$clMaxStatic  = 0.75        # ClMaxStatic
$clMaxMach    = 0.15        # ClMaxMach
$polarScale   = 0.73        # DragPolarScale
$polarPara    = 0.12        # DragPolarParasite
$polarLin     = 0.8         # DragPolarLinear
$polarQuad    = 0.5         # DragPolarQuad

# x = V / fd_speed, measured drag accel in m/s^2 (CAP-05, analysis/video-flight-calibration/)
$points = @(
    @{ X = 0.25; Measured = 0.36 },
    @{ X = 0.35; Measured = 1.11 },
    @{ X = 0.46; Measured = 2.82 },
    @{ X = 0.50; Measured = 3.74 }
)

# Force invariant (dot-decimal) formatting regardless of the host's locale, so this file's
# committed output cannot mojibake into comma-decimal on a different machine (SHELL-4/SHELL-7).
$inv = [System.Globalization.CultureInfo]::InvariantCulture

Write-Output "x      C_L     model(m/s^2)  measured(m/s^2)  err"
foreach ($p in $points) {
    $v     = $p.X * $fdSpeed
    $q     = 0.5 * $rho * [math]::Pow($v * $feetPerMetre, 2)
    $qS    = $q * $refArea
    $clMax = [math]::Max(0.0, $clMaxStatic - $clMaxMach * ($v / $soundMs))
    # the demanded load factor at level flight, capped by the aerodynamic ceiling
    $n     = [math]::Min($gravity / $standardG, $clMax * $qS / $vehWeight)
    $cl    = $n * $vehWeight / $qS
    $cd    = $polarScale * ($polarPara + $polarLin * $cl + $polarQuad * $cl * $cl)
    $model = $qS * $dragFactor * $cd * $standardG / $vehWeight
    $err   = ($model - $p.Measured) / $p.Measured * 100
    $line  = [string]::Format($inv, "{0,-6:0.00} {1,6:0.0000} {2,12:0.0000}  {3,15:0.00}  {4:+0.0;-0.0}%", `
        [double]$p.X, [double]$cl, [double]$model, [double]$p.Measured, [double]$err)
    Write-Output $line
}
