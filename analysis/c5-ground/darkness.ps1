<#
PLAN-c5-ground A3: the rendered luma of C5's block floor, walls, roofs and streets at fixed views, and the
transfer from ground texture luma to rendered luma, so B12 can pick the floor's texture level.

Every case is one --freecam --det --fov=60 capture at a view below (the poses of analysis/veg-budget/measure.ps1),
launched through RunProbe.ps1 on the hidden desktop. Per view:

  E0     Enhanced, the city on (a --det Enhanced launch draws it at near). The subject.
  Ennn   E0 with every cblock texture flattened to grey nnn (--tex-override): the ground's transfer curve, and,
         differenced, the ground mask (SHOT-38: the pixels the ground touches change between two colours).
  EN128  E128 with --no-clutter: pixels that differ from E128 and are not ground are buildings.
  EL0    E0 with the sun off (--debug-lighting=sun_energy:0).
  EL1    E0 with a strong sun at the zenith: a roof gains light over EL0, a wall gains none.
  F0, F016, F128, FN128   the same under Faithful (no city models: the original buildings).

darkness_measure.py classifies the pixels and writes the table and one montage per view. Captures hold game
pixels: -Out must be outside the repo.

    $env:CSVM_DATA_ROOT = "Z:\CSVM"
    & Z:\CSVM\.claude\worktrees\c5g-a3\analysis\c5-ground\darkness.ps1 -Out <scratch>\a3
    & ...\darkness.ps1 -Out <scratch>\a3 -SummaryOnly
#>
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string[]]$Views = @("cb7", "cb1", "alt", "street"),
    [string[]]$Cases = @("E0", "E016", "E024", "E032", "E048", "E064", "E128", "EN128", "EL0", "EL1", "F0", "F016", "F128", "FN128"),
    [int]$Frames = 300,
    [string]$Resolution = "1920x1080",
    # The sun offset that stands C5's Enhanced moon at the zenith: 90 degrees less its elevation (darkness_measure.py
    # checks it, since a wall then gains no light).
    [double]$SunPitch = 60,
    [switch]$SummaryOnly
)

$ErrorActionPreference = "Continue"  # native stderr lines must not abort the run (PS 5.1)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$probe = Join-Path $repoRoot "scripts\RunProbe.ps1"
if ((Resolve-Path -LiteralPath (Split-Path $Out -Parent)).Path.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "-Out must be outside the repo: the captures hold game pixels"
}
New-Item -ItemType Directory -Force -Path $Out | Out-Null

# measure.ps1's C5 views; ground is y=5.
$poses = @{
    "cb7"    = @("--pos=-3328,125,-3840", "--lookat=-3328,5,-3440")
    "cb1"    = @("--pos=-7124,165,-11732", "--lookat=-7424,5,-12032")
    "alt"    = @("--pos=-5000,450,-7000", "--lookat=-9000,250,-11000")
    "street" = @("--pos=-7306,50,-11700", "--lookat=-7306,40,-12600")
    "cb4"    = @("--pos=-4600,60,-6300", "--lookat=-4600,15,-5600")
}

function Get-Grey([int]$g) {
    $hex = "#{0:x2}{0:x2}{0:x2}" -f $g
    return @(1..7 | ForEach-Object { "--tex-override=cblock$_=$hex" })
}

function Get-CaseArgs([string]$case) {
    $look = if ($case.StartsWith("F")) { "--graphics=original" } else { "--graphics=enhanced" }
    $a = @($look)
    if ($case -match '^[EF]N?(\d{3})$') { $a += Get-Grey ([int]$matches[1]) }
    if ($case -match '^[EF]N') { $a += "--no-clutter" }
    if ($case -eq "EL0") { $a += "--debug-lighting=sun_energy:0" }
    if ($case -eq "EL1") { $a += "--debug-lighting=sun_energy:4,sun_pitch:$SunPitch" }
    return $a
}

if (-not $SummaryOnly) {
    foreach ($view in $Views) {
        foreach ($case in $Cases) {
            $base = Join-Path $Out "$view-$case"
            if (Test-Path "$base.png") { continue }
            $godotArgs = @("--freecam", "--chapter=C5") + $poses[$view] + (Get-CaseArgs $case) +
                @("--fov=60", "--det", "--mute", "--frames=$Frames", "--screenshot=$base.png")
            & $probe -TimeoutSec 600 -Resolution $Resolution -EngineArgs @("--log-file", "$base.log") @godotArgs | Out-Null
            Write-Host "$view $case exit $LASTEXITCODE"
        }
    }
}

$poseText = ($Views | ForEach-Object { "$_=" + (($poses[$_] -join ";") -replace "--(pos|lookat)=", "") }) -join " "
python -I (Join-Path $PSScriptRoot "darkness_measure.py") $Out $Resolution $poseText
