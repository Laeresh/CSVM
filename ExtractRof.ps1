<#
.SYNOPSIS
    Extracts the Crimson Skies UI resource archives (GOSDATA\ASSETS\*.rof) and the
    UI string table (BINARIES\*.dll) into extracted\rof\.

.DESCRIPTION
    `.rof` is the game's UI resource container: a directory tree of zlib-deflated
    members holding the menu/hangar/paint-shop artwork, the GUI scripts, LAYOUT.CSV
    and the scrapbook. mech3ax does not touch it -- format decoded 2026-07-20, see
    docs\formats\rof.md.

    The *text* the UI displays is not in the .rof. It lives in a Win32 STRINGTABLE
    in BINARIES\langui.dll, which the archive's own SCRIPTS\RESOURCE.H maps to
    symbolic names (IDS_*). This script pulls both and joins them, so the aircraft
    names, descriptions, gun-mount names and engine names come out readable. See
    docs\formats\strings.md.

    Output layout under -Dest:
        ASSETS\...                 every member of crimson.rof, at its archive path
        _crimptch\ASSETS\...       every member of crimptch.rof (the patch overlay,
                                   which overrides the base archive at the same path)
        ui_strings.json            the joined string table
        <name>.png                 next to each custom .BM: its greyscale shading map
        <name>_mask.png            next to each custom .BM: the paint-region masks,
                                   R = paint slot 1, G = slot 2, B = slot 3

    Members are written verbatim; 588 of the 846 are already PNG/JPG/TGA/TIF and need
    no conversion. Only the 184 custom `.BM` textures are decoded, and only additively
    -- the original .BM is always written out too.

.PARAMETER Source
    The game's GOSDATA\ASSETS folder. Default: CrimsonSkiesGame\GOSDATA\ASSETS next
    to this script.

.PARAMETER Dest
    Output root. Default: extracted\rof next to this script.

.PARAMETER Raw
    Write archive members only: skip the .BM decoding and the string table.

.PARAMETER Force
    Re-extract even when the output is already newer than the source archive.

.EXAMPLE
    .\ExtractRof.ps1
    Extract everything into extracted\rof\.

.EXAMPLE
    .\ExtractRof.ps1 -Raw
    Just unpack the archives, no PNG decoding and no string table.
#>

[CmdletBinding()]
param(
    [string] $Source,
    [string] $Dest,
    [switch] $Raw,
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
if (-not $Source) { $Source = Join-Path $RepoRoot "CrimsonSkiesGame\GOSDATA\ASSETS" }
if (-not $Dest)   { $Dest   = Join-Path $RepoRoot "extracted\rof" }

if (-not (Test-Path $Source)) {
    throw "Game ASSETS folder not found at $Source -- see PROJECT_CONTEXT.md for the install layout."
}

Add-Type -AssemblyName System.Drawing

# The decode itself lives in C# rather than PowerShell: unpacking 96 MB and
# interleaving ~3.1M mask pixels is the kind of tight loop PowerShell is slowest at.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

public static class CsRof
{
    // ---- container -------------------------------------------------------
    // Directory node: u32 entryCount, u32 poolLen, entryCount * 24-byte entries
    // [offset, sizeUncompressed, sizeCompressed, kind, nameLen, nameOffset],
    // then poolLen bytes of NUL-terminated names. kind: 0 stored, 1 dir, 2 deflate.

    const int KindStored = 0, KindDir = 1, KindDeflate = 2;

    public static int Files, Dirs, Images;

    public static void Extract(string rofPath, string destDir, bool decodeImages)
    {
        Files = Dirs = Images = 0;
        byte[] buf = File.ReadAllBytes(rofPath);
        ReadDir(buf, 0, destDir, decodeImages);
    }

    static uint U32(byte[] b, int o) { return BitConverter.ToUInt32(b, o); }

    static void ReadDir(byte[] buf, int offset, string destDir, bool decodeImages)
    {
        uint count = U32(buf, offset);
        uint poolLen = U32(buf, offset + 4);
        if (count > 100000 || poolLen > 10000000)
            throw new InvalidDataException("implausible .rof directory node at " + offset);

        int entBase = offset + 8;
        int poolBase = entBase + (int)count * 24;

        for (int i = 0; i < count; i++)
        {
            int e = entBase + i * 24;
            uint off = U32(buf, e);
            uint usize = U32(buf, e + 4);
            uint csize = U32(buf, e + 8);
            uint kind = U32(buf, e + 12);
            uint nlen = U32(buf, e + 16);
            uint noff = U32(buf, e + 20);

            int ns = poolBase + (int)noff, ne = ns;
            while (ne < ns + (int)nlen && buf[ne] != 0) ne++;
            string name = Encoding.ASCII.GetString(buf, ns, ne - ns);
            string target = Path.Combine(destDir, name);

            if (kind == KindDir)
            {
                Directory.CreateDirectory(target);
                Dirs++;
                ReadDir(buf, (int)off, target, decodeImages);
            }
            else if (kind == KindStored || kind == KindDeflate)
            {
                byte[] data = Inflate(buf, (int)off, (int)csize, (int)usize, kind == KindDeflate);
                Directory.CreateDirectory(destDir);
                File.WriteAllBytes(target, data);
                Files++;
                if (decodeImages && name.EndsWith(".BM", StringComparison.OrdinalIgnoreCase))
                    if (DecodeBm(data, target)) Images++;
            }
            else throw new InvalidDataException("unknown .rof entry kind " + kind + " for " + name);
        }
    }

    static byte[] Inflate(byte[] buf, int off, int csize, int usize, bool deflated)
    {
        byte[] outb = new byte[usize];
        if (!deflated) { Buffer.BlockCopy(buf, off, outb, 0, usize); return outb; }

        // Payloads are zlib streams; DeflateStream wants raw deflate, so skip the
        // 2-byte zlib header (the trailing adler32 is simply never read).
        using (var ms = new MemoryStream(buf, off + 2, csize - 2, false))
        using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < usize)
            {
                int n = ds.Read(outb, read, usize - read);
                if (n <= 0) break;
                read += n;
            }
            if (read != usize)
                throw new InvalidDataException("short inflate: got " + read + " of " + usize);
        }
        return outb;
    }

    // ---- .BM textures ----------------------------------------------------
    // u16 height, u16 width, then width*height*10 bytes of planes:
    //   [0 .. 3n)   24bpp RGB shading map
    //   [3n .. 6n)  three 8-bit paint-region weight masks (sum to 255 per pixel)
    //   [6n .. 10n) 32bpp pattern/decal overlay + alpha
    static bool DecodeBm(byte[] d, string bmPath)
    {
        if (d.Length < 4) return false;
        int h = BitConverter.ToUInt16(d, 0);
        int w = BitConverter.ToUInt16(d, 2);
        int n = w * h;
        if (w <= 0 || h <= 0 || d.Length < 4 + 6 * n) return false;

        string stem = bmPath.Substring(0, bmPath.Length - 3);
        // Shading map: source is RGB, GDI+ Format24bppRgb is BGR, so swap.
        SaveBgr(stem + ".png", w, h, delegate(int px, byte[] row, int ro) {
            int s = 4 + px * 3;
            row[ro] = d[s + 2]; row[ro + 1] = d[s + 1]; row[ro + 2] = d[s];
        });
        // Masks: slot1 -> R, slot2 -> G, slot3 -> B (written B,G,R for GDI+).
        SaveBgr(stem + "_mask.png", w, h, delegate(int px, byte[] row, int ro) {
            row[ro] = d[4 + 5 * n + px]; row[ro + 1] = d[4 + 4 * n + px]; row[ro + 2] = d[4 + 3 * n + px];
        });
        return true;
    }

    delegate void PixelWriter(int pixelIndex, byte[] row, int rowOffset);

    static void SaveBgr(string path, int w, int h, PixelWriter write)
    {
        using (var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
        {
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                                         ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[w * 3];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++) write(y * w + x, row, x * 3);
                    Marshal.Copy(row, 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), row.Length);
                }
            }
            finally { bmp.UnlockBits(bd); }
            bmp.Save(path, ImageFormat.Png);
        }
    }

    // ---- Win32 STRINGTABLE ----------------------------------------------
    // 16 strings per resource block; block id = (stringId >> 4) + 1, each slot a
    // u16 length followed by that many UTF-16LE code units (length 0 = unused).
    const int RtString = 6;

    public static SortedDictionary<int, string> ReadStrings(string dllPath)
    {
        var outMap = new SortedDictionary<int, string>();
        byte[] b = File.ReadAllBytes(dllPath);
        int pe = BitConverter.ToInt32(b, 0x3C);
        if (b[pe] != 'P' || b[pe + 1] != 'E') throw new InvalidDataException("not a PE: " + dllPath);

        int nsec = BitConverter.ToUInt16(b, pe + 6);
        int optSize = BitConverter.ToUInt16(b, pe + 20);
        int opt = pe + 24;
        int magic = BitConverter.ToUInt16(b, opt);
        int ddir = opt + (magic == 0x20B ? 112 : 96);
        uint rsrcRva = BitConverter.ToUInt32(b, ddir + 16);   // directory entry 2
        if (rsrcRva == 0) return outMap;

        var secs = new List<uint[]>();
        int sec = opt + optSize;
        for (int i = 0; i < nsec; i++)
        {
            int s = sec + i * 40;
            secs.Add(new uint[] { BitConverter.ToUInt32(b, s + 12),
                                  BitConverter.ToUInt32(b, s + 16),
                                  BitConverter.ToUInt32(b, s + 20) });
        }

        int rbase = (int)RvaToOff(rsrcRva, secs);
        foreach (uint[] t in Entries(b, rbase))
        {
            if (t[0] != RtString) continue;                      // level 1: type
            foreach (uint[] r in Entries(b, rbase + (int)(t[1] & 0x7FFFFFFF)))   // level 2: id
            {
                foreach (uint[] l in Entries(b, rbase + (int)(r[1] & 0x7FFFFFFF)))  // level 3: lang
                {
                    int leaf = rbase + (int)l[1];
                    int start = (int)RvaToOff(BitConverter.ToUInt32(b, leaf), secs);
                    int size = (int)BitConverter.ToUInt32(b, leaf + 4);
                    int o = start;
                    for (int i = 0; i < 16 && o + 2 <= start + size; i++)
                    {
                        int len = BitConverter.ToUInt16(b, o);
                        o += 2;
                        if (len > 0)
                        {
                            outMap[(((int)r[0] - 1) << 4) + i] = Encoding.Unicode.GetString(b, o, len * 2);
                            o += len * 2;
                        }
                    }
                }
            }
        }
        return outMap;
    }

    static IEnumerable<uint[]> Entries(byte[] b, int off)
    {
        int named = BitConverter.ToUInt16(b, off + 12);
        int ids = BitConverter.ToUInt16(b, off + 14);
        for (int i = 0; i < named + ids; i++)
        {
            int e = off + 16 + i * 8;
            yield return new uint[] { BitConverter.ToUInt32(b, e), BitConverter.ToUInt32(b, e + 4) };
        }
    }

    static uint RvaToOff(uint rva, List<uint[]> secs)
    {
        foreach (uint[] s in secs)
            if (rva >= s[0] && rva < s[0] + s[1]) return s[2] + (rva - s[0]);
        throw new InvalidDataException("RVA not mapped: " + rva);
    }
}
'@

# ---------------------------------------------------------------------------

$rofs = @(
    @{ Name = "crimson.rof";  Out = $Dest },
    @{ Name = "crimptch.rof"; Out = (Join-Path $Dest "_crimptch") }
)

Write-Host "Extracting Crimson Skies UI resources" -ForegroundColor Cyan
Write-Host "  from: $Source"
Write-Host "  to:   $Dest"
if ($Raw) { Write-Host "  (raw: no .BM decoding, no string table)" }
Write-Host ""

$totalFiles = 0; $totalImages = 0; $skipped = 0

foreach ($r in $rofs) {
    $rofPath = Join-Path $Source $r.Name
    if (-not (Test-Path $rofPath)) {
        Write-Host "  SKIP $($r.Name) (not present)" -ForegroundColor DarkGray
        continue
    }

    $marker = Join-Path $r.Out "ASSETS"
    $upToDate = (Test-Path $marker) -and
                ((Get-Item $marker).LastWriteTime -ge (Get-Item $rofPath).LastWriteTime)
    if ($upToDate -and -not $Force) {
        Write-Host "  ok   $($r.Name) (up to date)" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    Write-Host "  ->   $($r.Name)" -ForegroundColor Green
    New-Item -ItemType Directory -Path $r.Out -Force | Out-Null
    [CsRof]::Extract($rofPath, $r.Out, (-not $Raw))
    Write-Host "       $([CsRof]::Files) files, $([CsRof]::Dirs) dirs, $([CsRof]::Images) .BM decoded"
    $totalFiles += [CsRof]::Files
    $totalImages += [CsRof]::Images
}

# ---- string table ---------------------------------------------------------

if (-not $Raw) {
    $binaries = Join-Path $Source "BINARIES"
    $resourceH = Join-Path $Dest "ASSETS\SCRIPTS\RESOURCE.H"

    # RESOURCE.H names the IDs; it ships inside the archive we just unpacked.
    $symbols = @{}
    if (Test-Path $resourceH) {
        foreach ($line in (Get-Content -LiteralPath $resourceH)) {
            if ($line -match '^\s*#define\s+(IDS_\w+)\s+(\d+)') {
                if (-not $symbols.ContainsKey([int]$Matches[2])) {
                    $symbols[[int]$Matches[2]] = $Matches[1]
                }
            }
        }
    }

    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($dll in @("langui.dll", "language.dll")) {
        $dllPath = Join-Path $binaries $dll
        if (-not (Test-Path $dllPath)) {
            Write-Host "  SKIP $dll (not present)" -ForegroundColor Yellow
            continue
        }
        $table = [CsRof]::ReadStrings($dllPath)
        Write-Host "  ->   $dll ($($table.Count) strings)" -ForegroundColor Green
        foreach ($kv in $table.GetEnumerator()) {
            # Strings carry a leading [FONTID] tag naming the font to render them in.
            $font = $null; $text = $kv.Value
            if ($text -match '^\[(\w+)\](.*)$') { $font = $Matches[1]; $text = $Matches[2] }
            $sym = $null
            if ($symbols.ContainsKey($kv.Key)) { $sym = $symbols[$kv.Key] }
            $rows.Add([pscustomobject]@{
                id     = $kv.Key
                symbol = $sym
                font   = $font
                text   = $text
                dll    = [System.IO.Path]::GetFileNameWithoutExtension($dll)
            })
        }
    }

    if ($rows.Count -gt 0) {
        $jsonPath = Join-Path $Dest "ui_strings.json"
        $rows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
        Write-Host "       ui_strings.json ($($rows.Count) rows)"
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
Write-Host "  files extracted: $totalFiles"
if ((-not $Raw) -and $totalImages -gt 0) {
    Write-Host "  .BM decoded:     $totalImages (each -> .png + _mask.png)"
}
if ($skipped -gt 0) { Write-Host "  up to date:      $skipped archive(s); pass -Force to redo" }
