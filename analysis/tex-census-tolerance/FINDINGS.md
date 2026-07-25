# How the texture census classifies a pixel, and why those constants

The `--tex-census` instrument gives every texture a unique flat colour so "is this surface actually
drawing?" becomes a pixel count. Counting them back out of a rendered frame is the hard half: the
world shader multiplies the flat by a baked per-vertex colour, so a census pixel is almost never the
colour the palette assigned it. These are the scripts that chose the classifier, kept here because
`docs/cli.md` and `docs/HISTORY.md` cite their numbers and `.scratch/` is swept.

Ground truth throughout is `--tex-override` on one texture, which paints exactly one surface: the
C1/M04 moored zeppelin at `--pos=-5000,240,-5165 --lookat=-5248,200,-5165`, **113,947 px**.

## The scripts

- `sweep.py` — sweeps classifier tolerance and separation against the override ground truth plus a
  60-name control set of C4/C5-only textures, reporting recall and false credit per setting. Also
  re-implements the engine's colour hash independently, which is what proved the map agrees 397/397.
- `count.py` — counts census colours in one shot; the reference for the engine-side counter.
- `chroma.py` — measures the chromaticity spread of a known surface, the evidence that the residual
  is not a scalar dim.

## What they established

**The distortion is chromatic, not a dim.** The shader multiplies per channel, so half the zeppelin
hull reads `184,0,196` where a scalar dim of the assigned flat would give `204,0,204`. Chromaticity
shift measured p50 **0.088**, p75 **0.132**, worst **0.124** across the hull. A brightness-tolerant
classifier that assumed a scalar factor was therefore never going to work.

**Classification is chromaticity (linear colour ÷ brightest channel), tolerance 0.045, absolute
separation 0.03.** Swept against ground truth: **75.4 % recall at a worst-case 575 px false credit**
over the 60-name control set.

**Ratio margins were measured and rejected.** Margin 1.6 gave **5,061 px** false credit; margin 2.5
cut recall to **47 %**. The reason is structural: a pixel sitting exactly on a flat has a winning
distance near zero, so *any* ratio test passes trivially. Adding a ratio margin on top of the
absolute gap changed nothing. Absolute separation is the only one that discriminates here.

**Fog is the dominant distortion and is switched off rather than tolerated.** Same pose, same build:
**374,491 px classified confidently with `--no-fog` against 129,210 with fog on**; unmatched pixels
19.4 % → 58.8 %. The control that shows this check could fail is starker — the same zeppelin at
2.4 km reads **0 px with fog on and 1,063 px with `--no-fog`**. Fog alone erased the entire subject.

**The palette had to be drawn in linear space.** The first version drew its two free channels
uniformly in sRGB bytes, which bunches them into the corners once gamma is undone. Redrawing
uniformly in linear and converting to sRGB took the same texture from **55,950 → 88,301 px**.

## The limit, stated plainly

At ~400 textures in a frame, a per-texture census read back from one shot is **not** exact. `px` is a
lower bound and `px + contested` an upper one; below roughly **1,000 px** treat a texture as "not
shown" rather than "absent", and settle the question with `--tex-override`, which is exact because it
paints one surface. Colour collisions are real and deliberate: 8 bits a channel leave about 200k
reachable colours, so 4 of C1's 882 names collide. Each collision is warned by name — nudging them
apart would make a texture's colour depend on load order, which would break the map's stability
across runs.
