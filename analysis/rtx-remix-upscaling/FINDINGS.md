# Can NVIDIA RTX Remix's AI texture tools upscale our `rtexture` textures?

**Researched 2026-08-06** against NVIDIA's own docs and GitHub repos (primary sources; doc
dates noted per citation — Remix moves fast, re-check before acting on this in 2027).

**Framing correction first:** the question arrived as "the rtexture15 textures used by
MechWarrior 3", but this repo's source game is **Crimson Skies (2000, Zipper Interactive,
Microsoft)** — the `Mech3` namespace and the mech3ax extractor are MechWarrior 3 heritage
(same Zipper ZBD engine family; the fork's `unzbd cs` modes handle CS). "rtexture15" is not a
format: `N` in `rtextureN.zbd` is a video-memory size budget in MB, and C1's `rtexture15` is
that chapter's full-quality tier (881 textures, ~14.85 MB) — see
[`analysis/rtexture-tiers/FINDINGS.md`](../rtexture-tiers/FINDINGS.md). Every conclusion below
holds for either title, because both predate DirectX 8 (MW3 is a 1999 DX6 game, Crimson Skies
a 2000 DX7-era game — era claim inferred from release dates and shipped DX redistributables,
not from a primary spec).

## TL;DR verdict

- **The RTX Remix runtime (capture-and-replace in game) path: NO.** Remix replaces
  `d3d9.dll` only; NVIDIA says outright it "cannot interact with OpenGL or DirectX 7, 8,
  etc." by itself, wrappers exist only for DX8/early-OpenGL → fixed-function DX9, and NVIDIA
  is "not currently aware of any wrapper libraries for DirectX 7 to fixed function DX9" —
  which closes the door on DX6/DX7 titles. It is also pointless for CSVM: our renderer is
  Godot 4 (Vulkan/D3D12), un-hookable by design, and we already extract every texture
  losslessly, so a capture would add nothing.
- **The Toolkit's AI texture tools standalone on exported files: YES, technically.** The
  classic "AI Texture Tools" tab takes ordinary image files (PNG among them), 4x-upscales and
  generates albedo/normal/roughness, outputs DDS; the 2026-era toolkit routes the same job
  through bundled ComfyUI workflows with batch support. Requires an NVIDIA RTX GPU,
  8–12 GB VRAM.
- **But we probably shouldn't use Remix for it.** The toolkit is built around a
  capture→USD-mod→runtime pipeline we'd use none of; outputs are DDS (our loader reads PNG);
  and the equally-capable community model set **PBRify_Remix (CC0, chaiNNer, folder-in →
  PNG-out batch)** does the same job with less machinery and cleaner provenance.
- **Whatever tool is used, the outputs are derivatives of Microsoft-owned textures and can
  never be committed or shipped** — the repo's XWVM legal model ("no game assets in version
  control — ever", `PROJECT_CONTEXT.md`) decides this before NVIDIA's license even gets a
  vote. The only shippable shape is a script the player runs locally over their own
  extraction, like `ExtractAssets.ps1` today.

## What the textures actually are (repo facts, all verifiable locally)

- On disk inside `rtextureN.zbd` / `texture.zbd`: **16-bit RGB565 texels, optionally with a
  separate alpha plane, or 8-bit paletted with RGB565 palettes** — see the fork's decoder
  (`tools/mech3ax/crates/image/src/read.rs`: `rgb565to888`, `rgb565to888a`, `pal8to888`,
  `simple_alpha`).
- `ExtractAssets.ps1` runs `unzbd cs textures` → **PNG per texture** (RGB8/RGBA8), round-trip
  byte-identical (`docs/formats/extraction.md`). So "can we export to PNG" is already done:
  `extracted/C1/rtexture15/` holds 882 loose PNGs today.
- Resolutions (C1 top tier, measured for this note): 64×64 is the mode (256 files), range
  8×8 → 256×256, only 20 files at 256×256. All far below the 512×512 input ceiling the AI
  tools recommend, so size is no obstacle; 4x output lands at 32×32 → 1024×1024.
- The engine consumes those PNGs via `CSVM/src/Mech3/TextureArchive.cs` (zip or loose dir;
  loose dir preferred). Note its authored-mip logic: `_1`/`_2` siblings are installed only
  when *exactly* half/quarter the base's size, so an upscaled base texture would simply get
  box-filtered mips (siblings refused, logged) — no crash, but the artists' hand-tuned mips
  would stop applying. ~50–90 textures per chapter also *are* those mip siblings and should
  be excluded from (or regenerated after) any upscale batch.

## Path (a): the RTX Remix runtime — dead end

Documented, from NVIDIA:

- "RTX Remix functions as a replacement for DirectX 9 and doesn't directly support OpenGL or
  older DirectX versions"; it targets **DX8/DX9 fixed-function** games, with DX9.0c
  shader-based games "probably won't work".
  ([Game Compatibility, docs.omniverse.nvidia.com, page dated 2026-08-04](https://docs.omniverse.nvidia.com/kit/docs/rtx_remix/latest/docs/introduction/intro-compatibility.html))
- "Remix functions as a DirectX 9 replacer, and by itself cannot interact with OpenGL or
  DirectX 7, 8, etc." Wrappers "translate from early OpenGL or DirectX 8 to fixed function
  DirectX 9" and have worked, but: **"We are not currently aware of any wrapper libraries for
  DirectX 7 to fixed function DirectX 9."**
  ([rtx-remix wiki: Compatibility, last edited 2025-10-08](https://github.com/NVIDIAGameWorks/rtx-remix/wiki/Compatibility))
- dgVoodoo2-style wrappers don't help: dgVoodoo2 translates old APIs to **D3D11/12**, which
  Remix cannot hook — Remix needs the game speaking fixed-function D3D9 (inferred from the
  above two pages; NVIDIA's docs point to a community Discord thread for the wrapper list
  rather than naming any wrapper).

So the original DX6/DX7 exe cannot be captured by Remix today, and CSVM's own Godot renderer
never could be (Remix hooks `d3d9.dll`; Godot 4 renders via Vulkan/D3D12). Since mech3ax
already exports every texture losslessly, the runtime/capture path offers nothing we lack —
its one unique payoff (in-game path-traced relighting of the *original* exe) is unreachable
for this game generation.

## Path (b): the Toolkit's AI texture tools, standalone on files — works, with caveats

Two generations of the feature exist; cite the one you mean:

**Classic built-in "AI Texture Tools"** (docs versions 1.2.4 → 2024.4.x; the 1.2.4 page was
last updated 2026-01-24):

- Two functions: **4x upscale** and **PBR generation** — "albedo, normal map, roughness map"
  from an input image.
  ([Using AI Texture Tools, 1.2.4](https://docs.omniverse.nvidia.com/kit/docs/rtx_remix/1.2.4/docs/howto/learning-aitexturetools.html))
- UI: an `AI Tools` tab whose "interface is similar to the Material Ingestion tab" — i.e. you
  feed it texture *files*, no game capture required (documented interface; "no capture
  needed" is my inference from the file-based ingestion UI, and the ComfyUI generation below
  makes it explicit).
- Modes: **Speed** (input resized to 256×256, output always 1024×1024, single pass) vs
  **Quality** (original resolution in, 4x out, tiled in 256×256 sections; "input textures
  with a maximum resolution of 512x512 is recommended to prevent VRAM overflow"). Our
  8×8–256×256 set fits either mode trivially.
- Ingestion-side formats: inputs "`.bmp, .dds, .gif, .hdr, .pgm, .jpg, .pic, .png, .ppm`";
  texture output is "`.dds (DirectDraw Surface)`" (BC-compressed); "Advanced users can
  leverage the CLI tool for batch ingestion of assets."
  ([Ingesting Assets, latest, dated 2026-08-04](https://docs.omniverse.nvidia.com/kit/docs/rtx_remix/latest/docs/howto/learning-ingestion.html))

**Current (2026) toolkit "AI Tools"** — rebuilt on ComfyUI:

- "Workflows can perform any task that ComfyUI supports: PBR texture generation, upscaling,
  style transfer, mesh processing, and more." Jobs queue in SQLite, batch-deduplicate, and
  "several template workflows download large AI models from HuggingFace on first run."
  Hardware: "NVIDIA GPU with at least 8 GB VRAM (12 GB+ recommended)".
  ([toolkit-remix `docs/howto/learning-aitools.md`, main branch, fetched 2026-08-06](https://github.com/NVIDIAGameWorks/toolkit-remix/blob/main/docs/howto/learning-aitools.md))
- NVIDIA's own ComfyUI node pack: "RTX Remix Save Texture node supports batch processing
  (multiple textures in one execution)"; REST-API nodes give "full programmatic control over
  the RTX Remix Toolkit". Apache-2.0.
  ([NVIDIAGameWorks/ComfyUI-RTX-Remix](https://github.com/NVIDIAGameWorks/ComfyUI-RTX-Remix))
- NVIDIA's framing of the batch story: modders can "export all game textures captured in RTX
  Remix to ComfyUI and enhance them in one big batch before automatically bringing them
  back"; the community model **PBRFusion 3** is named as the workhorse.
  ([NVIDIA blog, 2025-03-13](https://blogs.nvidia.com/blog/rtx-ai-garage-rtx-remix))

**How our textures would flow through:** `extracted/<chapter>/rtexture<N>/*.png` → AI
tools/ComfyUI (batch) → 4x albedo + normal + roughness. Frictions: the toolkit path emits
**DDS** (BC-compressed) organized for a Remix USD mod project, while `TextureArchive.cs`
enumerates **`*.png` only** — so a conversion step (or a loader change) is needed either way;
the PBR maps are useless until CSVM's materials grow normal/roughness inputs (today the
loader builds plain albedo `ImageTexture`s); and hundreds of tiny non-square gauge/decal
textures are exactly the content AI PBR-ification handles worst (flat-colored HUD art,
text, dial faces — inference from the content, not documented).

## Hardware and licensing constraints

- **RTX GPU mandatory** for the toolkit/runtime: "To use RTX Remix and its mods effectively,
  you'll need an RTX-powered PC"; recommended RTX 4070 / 12 GB VRAM, i7-13700K/R7-7700X,
  32 GB RAM, Win10 minimum.
  ([Requirements, latest, dated 2026-08-04](https://docs.omniverse.nvidia.com/kit/docs/rtx_remix/latest/docs/introduction/intro-requirements.html))
- **Code licenses:** the combined `rtx-remix` repo is **MIT** ([LICENSE.txt](https://github.com/NVIDIAGameWorks/rtx-remix/blob/main/LICENSE.txt));
  the toolkit and the ComfyUI node pack are **Apache-2.0**
  ([toolkit-remix](https://github.com/NVIDIAGameWorks/toolkit-remix)). Neither license claims
  ownership of tool *output*, and no NVIDIA doc I found asserts any rights over generated
  textures — but note the 2026 AI Tools pull their models from **HuggingFace at first run**,
  so each model's own license governs its outputs case-by-case (documented model-download
  mechanism; the per-model-license consequence is inference). No explicit "outputs are yours"
  statement exists in the Remix docs — treated as an open question below.
- **The decisive constraint is ours, not NVIDIA's:** an AI-upscaled derivative of a
  Microsoft-owned texture is still a derivative of a Microsoft-owned texture. The repo's
  standing rule ("no game assets in version control — ever"; XWVM model, importer reads the
  player's own install — `PROJECT_CONTEXT.md`) forbids committing or publishing such outputs
  regardless of what the upscaler's license says. The only shape that fits the project is a
  **player-run local pipeline** (a script beside `ExtractAssets.ps1` that reads
  `extracted/` and writes an `extracted/.../upscaled/` sibling), plus committed *code* only.

## Alternatives (context, one paragraph)

For a headless, folder-in/folder-out batch over 800+ small PNGs, the ESRGAN family fits
better than the Remix toolkit: **chaiNNer** (node-based batch image tool) running
**PBRify_Remix** — a community model suite that both 4x-upscales and generates
normal/roughness/height, ships a preconfigured chain file, labels outputs by source name,
and is **CC0-licensed, trained exclusively on CC0 ambientCG content**
([Kim2091/PBRify_Remix](https://github.com/Kim2091/PBRify_Remix)) — arguably *cleaner*
provenance than NVIDIA's own models; the same models run in ComfyUI via NVIDIA's node pack.
Plain **Real-ESRGAN** ([xinntao/Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN), BSD-3)
or the GUI wrapper **Upscayl** ([upscayl/upscayl](https://github.com/upscayl/upscayl),
AGPL-3.0) cover upscale-only, run on non-NVIDIA GPUs (NCNN/Vulkan), and need no RTX card —
relevant since CSVM's players aren't guaranteed NVIDIA hardware.

## Open questions

1. **NVIDIA model-output terms:** no Remix doc states who owns AI Texture Tool output; the
   HuggingFace-downloaded models each carry their own license. Needs checking per template
   workflow if the Remix route is ever chosen (moot if PBRify/CC0 is used).
2. **Would upscales even read as an improvement here?** The originals are deliberately
   low-res, palette-tight art; a playtest A/B (one chapter, upscale-only, no PBR) should
   precede any material-system work.
3. **Engine integration:** `TextureArchive.cs` reads PNG only, installs authored `_1`/`_2`
   mips only at exact half/quarter sizes, and `PlanePainter`/decal indexing assume original
   texel layouts — an upscaled-tree loader mode (and exclusion of `_N` mip siblings from the
   batch) is a small but real work item.
4. **Non-square/tiny inputs:** the AI tools' documented guidance assumes square ≥256 inputs
   ("start with … 512x512" for best results); how 8×8–64×32 gauge art survives Speed mode's
   forced 256×256 resize is undocumented — would need an empirical check.