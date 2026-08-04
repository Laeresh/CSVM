# Pixel delta between two directories of same-named PNG captures.
#
# The golden manifest asks a landed visual change to NAME the shots it moved and say by how much
# ("0.19 % of pixels, max delta 107, below row 363"). This produces those figures, so a golden
# move is argued from a measurement rather than from a changed hash.
#
#   .\analysis\bl-135-callsequence-lag\compare-shots.ps1 -A .scratch\bl-135\goldens-baseline `
#                                                        -B .scratch\bl-135\goldens-drained

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$A,
    [Parameter(Mandatory)][string]$B
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Pixels($path) {
    $bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $path))
    try {
        $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                              [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($data.Stride * $bmp.Height)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
            return [pscustomobject]@{ w = $bmp.Width; h = $bmp.Height; stride = $data.Stride; bytes = $bytes }
        }
        finally { $bmp.UnlockBits($data) }
    }
    finally { $bmp.Dispose() }
}

foreach ($f in Get-ChildItem $A -Filter *.png | Sort-Object Name) {
    $other = Join-Path $B $f.Name
    if (-not (Test-Path $other)) { continue }
    $p = Pixels $f.FullName
    $q = Pixels $other
    if ($p.w -ne $q.w -or $p.h -ne $q.h) { Write-Host "  $($f.Name): SIZE differs"; continue }

    $diff = 0; $max = 0; $minRow = [int]::MaxValue; $maxRow = -1
    for ($y = 0; $y -lt $p.h; $y++) {
        $row = $y * $p.stride
        for ($x = 0; $x -lt $p.w; $x++) {
            $i = $row + ($x * 4)
            $d = 0
            foreach ($c in 0, 1, 2) {
                $e = [Math]::Abs($p.bytes[$i + $c] - $q.bytes[$i + $c])
                if ($e -gt $d) { $d = $e }
            }
            if ($d -eq 0) { continue }
            $diff++
            if ($d -gt $max) { $max = $d }
            if ($y -lt $minRow) { $minRow = $y }
            if ($y -gt $maxRow) { $maxRow = $y }
        }
    }
    $pct = if ($p.w * $p.h) { 100.0 * $diff / ($p.w * $p.h) } else { 0 }
    if ($diff -eq 0) {
        Write-Host ("  {0,-22} identical" -f $f.Name)
    }
    else {
        Write-Host ("  {0,-22} {1,8} px ({2:0.000} %), max channel delta {3}, rows {4}-{5}" -f `
            $f.Name, $diff, $pct, $max, $minRow, $maxRow)
    }
}
