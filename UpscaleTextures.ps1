<#
.SYNOPSIS
    4x-upscales an extracted texture folder with Real-ESRGAN into a sibling
    "_x4" folder (e.g. extracted\C1\rtexture15 -> extracted\C1\rtexture15_x4).

.DESCRIPTION
    Runs the upscayl-ncnn engine (a maintained fork of realesrgan-ncnn-vulkan;
    tools\upscayl-ncnn\, not committed -- see .PARAMETER Exe for where to get it)
    with the realesrgan-x4plus model over every PNG in the source folder. Runs
    entirely locally on any Vulkan-capable GPU; the full 881-file C1 rtexture15
    set takes well under a minute on an RTX 5080. Alpha channels survive,
    including decals whose art lives under fully transparent pixels.

    ⚠ Textures with a dimension under 16 px (the 8x8 hatch patterns, the 32x8
    crane chain strips -- 9 files in C1) CRASH THE NET: the engine emits garbage
    for them AND for every image processed after them in the same run (diagnosed
    2026-08-06: a batch was clean until crane1.png, garbage after). They are
    therefore pulled out of the batch and processed one file per engine run,
    tile-padded to 32 px (they are repeating patterns; tiling is the natural
    continuation), then cropped back to size*4.

    Every output is verified against its source (mean color of a 16x16
    thumbnail); failures are retried in a solo engine run, and anything still
    failing falls back to a plain bicubic 4x so the folder is never left with
    garbage. Fallbacks are listed loudly -- an empty report means every file is
    genuine Real-ESRGAN output.

    Output is game-derived and lands under extracted\ (gitignored). The engine
    prefers a "<tier>_x4" sibling of the chapter's winning texture tier when the
    folder exists (SessionPaths.ChapterTextures) -- so after running this script,
    the next session loads the upscaled set automatically; delete or rename the
    _x4 folder to fall back to the originals.

    In a git worktree, tools\ and extracted\ live in the primary tree: set
    CSVM_DATA_ROOT (the same env var RunDev.ps1 and the engine read) and both
    defaults follow it.

.PARAMETER Source
    Folder of PNGs to upscale. Default: extracted\C1\rtexture15 next to this
    script, falling back to CSVM_DATA_ROOT when absent here.

.PARAMETER Dest
    Output folder. Default: "<Source>_x4".

.PARAMETER Model
    Real-ESRGAN model name (a .param/.bin pair in tools\realesrgan\models).
    Default: realesrgan-x4plus -- best on this asset set; the bundled anime
    variants repaint the photographic terrain into mush.

.PARAMETER Exe
    Path to upscayl-bin.exe. Default: tools\upscayl-ncnn\upscayl-bin-*\ next to
    this script, falling back to CSVM_DATA_ROOT. Portable AGPL build, no install:
    https://github.com/upscayl/upscayl-ncnn/releases (upscayl-bin-<date>-windows.zip)
    -- unzip into tools\upscayl-ncnn\. Do NOT use the older 2022
    realesrgan-ncnn-vulkan build here: on an RTX 5080 it garbles bright textures
    even in solo runs (the model files are still taken from its models\ folder,
    which IS still needed -- both live in tools\).

.PARAMETER ModelDir
    Folder holding the model .param/.bin pairs. Default: tools\realesrgan\models
    (the 2022 portable build ships them; the upscayl zip does not), falling back
    to CSVM_DATA_ROOT.

.EXAMPLE
    .\UpscaleTextures.ps1
    Upscale C1's full-quality tier into extracted\C1\rtexture15_x4.

.EXAMPLE
    .\UpscaleTextures.ps1 -Source extracted\C2\rtexture14
    Same for C2's full-quality tier.
#>

[CmdletBinding()]
param(
    [string] $Source,
    [string] $Dest,
    [string] $Model = "realesrgan-x4plus",
    [string] $Exe,
    [string] $ModelDir
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$RepoRoot = $PSScriptRoot

# Resolve a repo-relative path here first, then under CSVM_DATA_ROOT (worktrees
# have no tools\ or extracted\ -- the primary tree does).
function Resolve-TreePath([string] $rel) {
    $here = Join-Path $RepoRoot $rel
    if (Test-Path $here) { return $here }
    if ($env:CSVM_DATA_ROOT) {
        $there = Join-Path $env:CSVM_DATA_ROOT $rel
        if (Test-Path $there) { return $there }
    }
    return $here   # the caller's error message names this path
}

if (-not $Source) { $Source = Resolve-TreePath "extracted\C1\rtexture15" }
if (-not $ModelDir) { $ModelDir = Resolve-TreePath "tools\realesrgan\models" }
if (-not $Exe) {
    $exeDir = Resolve-TreePath "tools\upscayl-ncnn"
    $found = @(Get-ChildItem $exeDir -Recurse -Filter "upscayl-bin.exe" -ErrorAction SilentlyContinue)
    $Exe = if ($found.Count -gt 0) { $found[0].FullName } else { Join-Path $exeDir "upscayl-bin.exe" }
}

if (-not (Test-Path $Exe)) {
    throw "upscayl-bin not found at $Exe -- download the portable engine (see the .PARAMETER Exe help in this script) and unzip it into tools\upscayl-ncnn\."
}
if (-not (Test-Path (Join-Path $ModelDir "$Model.param"))) {
    throw "model $Model not found in $ModelDir -- the models ship with the 2022 realesrgan-ncnn-vulkan zip (see docs in this script's header)."
}
if (-not (Test-Path $Source)) {
    throw "Source folder not found at $Source -- run ExtractAssets.ps1 -Unzip first (the loose-PNG folder is what this script consumes)."
}
$Source = (Resolve-Path $Source).Path
if (-not $Dest) { $Dest = "${Source}_x4" }

$inputs = @(Get-ChildItem -Path $Source -File -Filter *.png)
if ($inputs.Count -eq 0) { throw "No PNGs in $Source" }
New-Item -ItemType Directory -Force -Path $Dest | Out-Null

# ---- helpers --------------------------------------------------------------

# Mean B,G,R,A of the image scaled to a 16x16 thumbnail -- coarse, but garbage
# output (noise, black frames, scanline trash) moves the means by far more than
# a faithful 4x upscale ever does.
function Get-ThumbStats([string] $path) {
    $img = [System.Drawing.Image]::FromFile($path)
    try {
        $bmp = New-Object System.Drawing.Bitmap(16, 16, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBilinear
        $g.DrawImage($img, 0, 0, 16, 16)
        $g.Dispose()
        $rect = New-Object System.Drawing.Rectangle(0, 0, 16, 16)
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, $bmp.PixelFormat)
        $bytes = New-Object byte[] (16 * 16 * 4)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        $bmp.UnlockBits($data)
        $bmp.Dispose()
        $sums = @(0.0, 0.0, 0.0, 0.0)
        for ($i = 0; $i -lt $bytes.Length; $i += 4) {
            $sums[0] += $bytes[$i]; $sums[1] += $bytes[$i + 1]; $sums[2] += $bytes[$i + 2]; $sums[3] += $bytes[$i + 3]
        }
        return $sums | ForEach-Object { $_ / 256.0 }
    }
    finally { $img.Dispose() }
}

# err = mean |color delta|, aErr = |alpha delta| between source and its upscale.
function Get-UpscaleError([string] $srcPath, [string] $outPath) {
    $o = Get-ThumbStats $srcPath
    $u = Get-ThumbStats $outPath
    $err = ([math]::Abs($o[0] - $u[0]) + [math]::Abs($o[1] - $u[1]) + [math]::Abs($o[2] - $u[2])) / 3
    return @{ Err = $err; AErr = [math]::Abs($o[3] - $u[3]) }
}

function Invoke-Engine([string] $inPath, [string] $outPath) {
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $Exe -i $inPath -o $outPath -m $ModelDir -n $Model -s 4 -f png 2>&1 | Out-Null
    $exit = $LASTEXITCODE
    $ErrorActionPreference = $prevEap
    if ($exit -ne 0) { throw "upscayl-bin failed (exit $exit) on $inPath" }
}

# Tile-pad a small texture up to 32 px per side (repeat, matching how these
# pattern strips tile in the world), for cropping back after the upscale.
function New-TilePadded([string] $inPath, [string] $outPath) {
    $img = [System.Drawing.Image]::FromFile($inPath)
    $w = [math]::Max($img.Width, 32); $h = [math]::Max($img.Height, 32)
    $canvas = New-Object System.Drawing.Bitmap($w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $brush = New-Object System.Drawing.TextureBrush($img)
    $brush.WrapMode = [System.Drawing.Drawing2D.WrapMode]::Tile
    $g.FillRectangle($brush, 0, 0, $w, $h)
    $g.Dispose(); $brush.Dispose(); $img.Dispose()
    $canvas.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}

function Copy-CroppedTo([string] $bigPath, [string] $outPath, [int] $w, [int] $h) {
    $big = [System.Drawing.Image]::FromFile($bigPath)
    $crop = New-Object System.Drawing.Bitmap($w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $dstR = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $g.DrawImage($big, $dstR, $dstR, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose(); $big.Dispose()
    $crop.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $crop.Dispose()
}

# Last resort: plain bicubic 4x. Never garbage, just soft -- and loudly reported.
function New-Bicubic4x([string] $inPath, [string] $outPath) {
    $img = [System.Drawing.Image]::FromFile($inPath)
    $bmp = New-Object System.Drawing.Bitmap(($img.Width * 4), ($img.Height * 4), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($img, 0, 0, $bmp.Width, $bmp.Height)
    $g.Dispose(); $img.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# ---- split: batchable vs net-crashing tiny strips -------------------------

$tiny = @(); $normal = @()
foreach ($f in $inputs) {
    $img = [System.Drawing.Image]::FromFile($f.FullName)
    $isTiny = ([math]::Min($img.Width, $img.Height) -lt 16)
    $dims = @($img.Width, $img.Height)
    $img.Dispose()
    if ($isTiny) { $tiny += ,@($f, $dims) } else { $normal += $f }
}

Write-Host "Upscaling $($inputs.Count) PNG(s) 4x with $Model ($($tiny.Count) tiny strip(s) via tile-pad)" -ForegroundColor Cyan
Write-Host "  from: $Source"
Write-Host "  to:   $Dest"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# ---- pass 1: the big batch ------------------------------------------------

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("csvm-upscale-" + [System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Force "$staging\in" | Out-Null
foreach ($f in $normal) { Copy-Item $f.FullName "$staging\in\" }
try {
    Invoke-Engine "$staging\in" $Dest

    # ---- pass 2: tiny strips, one engine run each, padded ----------------
    foreach ($t in $tiny) {
        $f = $t[0]; $w = $t[1][0]; $h = $t[1][1]
        $padded = Join-Path $staging ($f.BaseName + "-pad.png")
        $upped = Join-Path $staging ($f.BaseName + "-pad_x4.png")
        New-TilePadded $f.FullName $padded
        Invoke-Engine $padded $upped
        Copy-CroppedTo $upped (Join-Path $Dest $f.Name) ($w * 4) ($h * 4)
    }

    # ---- pass 3: verify everything, retry failures solo ------------------
    $fallbacks = @(); $retried = 0
    foreach ($f in $inputs) {
        $outPath = Join-Path $Dest $f.Name
        if (-not (Test-Path $outPath)) { throw "engine produced no output for $($f.Name)" }
        $e = Get-UpscaleError $f.FullName $outPath
        if ($e.Err -le 25 -and $e.AErr -le 25) { continue }

        # Suspect: re-run alone (the known-good mode), re-check.
        $retried++
        Invoke-Engine $f.FullName $outPath
        $e = Get-UpscaleError $f.FullName $outPath
        if ($e.Err -le 25 -and $e.AErr -le 25) { continue }

        New-Bicubic4x $f.FullName $outPath
        $fallbacks += $f.Name
    }
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}
$sw.Stop()

$outputs = @(Get-ChildItem -Path $Dest -File -Filter *.png)
$mb = [math]::Round(($outputs | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Done: $($outputs.Count) file(s), $mb MB, $([int]$sw.Elapsed.TotalSeconds)s ($retried retried solo)" -ForegroundColor Cyan
if ($fallbacks.Count -gt 0) {
    Write-Host "  $($fallbacks.Count) file(s) fell back to plain bicubic 4x (Real-ESRGAN kept producing garbage):" -ForegroundColor Yellow
    $fallbacks | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
}
if ($outputs.Count -ne $inputs.Count) {
    Write-Host "  WARNING: input/output count mismatch ($($inputs.Count) in, $($outputs.Count) out)" -ForegroundColor Yellow
    exit 1
}
