"""Renders the Enhanced Graphics cloud puff sprites, CSVM/data/cloud_puffs/*.png.bin.

Our own renders, not game data: each puff is a procedural volume (a few soft lobes summed and
broken up by fBM noise) in a cube domain, lit by one sun key from above and in front plus a neutral sky
fill, and rendered by Cycles on a transparent film. The result is an alpha mask (the
volume's coverage) with the puff's own shading in RGB, neutral grey, normalised so the lit top
reads 1.0. Two sets ship: veil_1 to veil_8 for the fvol deck cards (--set veil) and far_1 to far_6
for the placed cloud sprites (--set far). Mech3/CloudPuffs.cs loads each as one texture array under
Enhanced Graphics, tints it by the authored mask's own colour so a chapter's cloud keeps its
authored tint, and has each card pick its puff, tilt, mirror and size off a hash of its position.

The sprite's top is image +Y, which the card pose keeps at world up, so the key light is baked
from above. 512 x 512 is 8x the authored 64 x 64 masks, same square aspect.

Run headless from the repository root (never with a Blender window):

  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup
      --python CSVM/data/cloud_puffs/render_cloud_puffs.py -- [--out DIR] [--size N]
      [--samples N] [--set veil|far|far_soft|far_smooth|veil_pair|soft|billowy] [--only NAME[,NAME]]

The default --out is this script's own folder and the default --set is the shipped one. Each
puff is written as <name>.png.bin: PNG bytes under an extension Godot does not import (the export's data/*.bin filter packs it, see
PromptFontGlyphs.FontPath). The render is seeded, so a rerun reproduces the set up to the
denoiser's floating-point noise.
"""

import math
import os
import sys

import bpy
import numpy as np

# The candidate sets, one lighting setup and a list of puffs each; --set picks one, "veil" is
# shipped. Puff units are the domain's own, a cube from -1 to 1 filling the frame.
#   core: the radius of a central lump (0 for none); clusters: (x, z) centres the other lumps
#   gather round, lobes: lumps per cluster, spread: how far they reach, radius: their size range,
#   rise: their height range, stretch: (x, z) factors on their offsets, which flatten or stretch
#   the silhouette.
#   warp/warp_scale: how far a low-frequency noise pushes the lumps about; billow/billow_scale:
#   the fBM that eats into their outline; threshold/soft: where the summed field starts to hold
#   cloud and over how wide a ramp; density: the core's extinction per unit; base_z: the
#   flattened underside; window: (radius, gain) of the spherical fade to zero, so no puff
#   reaches the square's edge.
# sky: the fill's strength against the sun key; bounces: volume bounces, where fewer keep the
# underside grey and more wash the puff toward one white.
def _pair(scale, threshold, soft, density):
    core1, spread1, core2, spread2 = scale
    return {
        "puff_1": dict(seed=11, core=core1, lobes=8, spread=spread1, warp=0.45, warp_scale=1.4,
                       billow=1.1, billow_scale=3.2, threshold=threshold, soft=soft,
                       density=density, base_z=-0.62),
        "puff_2": dict(seed=23, core=core2, lobes=9, spread=spread2, warp=0.50, warp_scale=1.7,
                       billow=1.2, billow_scale=3.8, threshold=threshold, soft=soft,
                       density=density, base_z=-0.6),
    }


def _veil(seed, **shape):
    # The veil look: translucent core, light shading. Each entry overrides the silhouette.
    p = dict(seed=seed, core=0.0, clusters=[(0.0, 0.0)], lobes=8, spread=0.85, radius=(0.32, 0.5),
             rise=(-0.2, 0.45), stretch=(1.0, 1.0), warp=0.45, warp_scale=1.4, billow=1.1,
             billow_scale=3.2, threshold=0.02, soft=0.9, density=3.0, base_z=-0.62)
    p.update(shape)
    return p


VEIL_POOL = {
    # A rounded heap, the pair's first puff.
    "veil_1": _veil(11, core=0.9),
    # A long, flat bank.
    "veil_2": _veil(31, core=0.5, lobes=12, spread=1.0, radius=(0.3, 0.45), rise=(-0.1, 0.12),
                    stretch=(1.25, 0.5), base_z=-0.3),
    # Many knots, strongly broken up.
    "veil_3": _veil(47, core=0.6, lobes=14, spread=0.85, radius=(0.25, 0.4), billow=1.5,
                    billow_scale=4.5, union="sum", threshold=0.15),
    # Two separate heaps with a thin gap.
    "veil_4": _veil(59, clusters=[(-0.5, -0.05), (0.5, 0.12)], lobes=7, spread=0.26,
                    radius=(0.3, 0.42), union="sum", threshold=0.12, soft=0.9, warp=0.6,
                    billow=1.4),
    # Three smaller heaps along an arc.
    "veil_5": _veil(71, clusters=[(-0.6, -0.15), (0.0, 0.22), (0.6, -0.1)], lobes=6,
                    spread=0.2, radius=(0.26, 0.36), union="sum", threshold=0.12, soft=0.9,
                    warp=0.6, billow=1.4),
    # A thin, wispy-edged sheet.
    "veil_6": _veil(83, core=0.75, lobes=9, spread=0.9, radius=(0.35, 0.5), billow=1.7,
                    billow_scale=5.5, warp=0.7, density=1.8, soft=1.2),
    # A lopsided tower, its mass to one side and climbing.
    "veil_7": _veil(97, clusters=[(-0.3, -0.1), (-0.2, 0.35), (0.4, -0.3)], lobes=5,
                    spread=0.28, radius=(0.26, 0.38), base_z=-0.7, union="sum", threshold=0.15,
                    warp=0.6, billow=1.4),
    # A wide heap with a ragged top.
    "veil_8": _veil(23, core=0.88, lobes=9, spread=0.9, warp=0.5, warp_scale=1.7, billow=1.2,
                    billow_scale=3.8, base_z=-0.6),
}

def _far(seed, **shape):
    # The far look: the veil's light shading on fuller, denser heaps, for the large placed cloud
    # clusters, whose authored masks are denser than the deck reads.
    p = _veil(seed, core=1.1, lobes=9, spread=1.0, radius=(0.4, 0.6), rise=(-0.3, 0.5),
              threshold=0.0, soft=1.1, density=5.0, window=(0.97, 2.5), base_z=-0.85)
    p.update(shape)
    return p


FAR_SMOOTH_POOL = {
    # A big rounded heap.
    "far_1": _far(101),
    # A wide heap.
    "far_2": _far(113, stretch=(1.2, 0.8), base_z=-0.8),
    # A towering heap.
    "far_3": _far(127, core=0.8, rise=(-0.1, 0.7), stretch=(0.8, 1.2), base_z=-0.9),
    # Two heaps grown together.
    "far_4": _far(131, core=0.0, clusters=[(-0.35, -0.05), (0.38, 0.1)], lobes=6, spread=0.35,
                  radius=(0.35, 0.5), union="sum", threshold=0.15),
    # A flat-bottomed cumulus.
    "far_5": _far(137, core=0.85, base_z=-0.7, rise=(-0.05, 0.5)),
    # A ragged heap.
    "far_6": _far(149, core=0.85, billow=1.5, billow_scale=4.5, warp=0.6),
}


def _mass(seed, soft_look, **shape):
    # The placed clusters' mass: many lumps summed into one cauliflower heap that fills the card,
    # broken up by a coarse billow and a fine detail noise. soft_look trades the crisp edge for the
    # authored masks' soft, shapeless fringe.
    p = _veil(seed, core=1.0, lobes=14, spread=1.1, radius=(0.35, 0.55), rise=(-0.35, 0.6),
              union="sum", warp=0.55, billow=1.3, billow_scale=3.5, detail=0.45,
              detail_scale=9.0, window=(0.98, 2.5), base_z=-0.9)
    p.update(dict(threshold=0.0, soft=1.3, density=5.5, detail=0.35) if soft_look
             else dict(threshold=0.05, soft=0.9, density=6.0))
    p.update(shape)
    return p


def _mass_pool(soft_look):
    return {
        # One broad heap.
        "far_1": _mass(201, soft_look),
        # A wide heap.
        "far_2": _mass(211, soft_look, stretch=(1.25, 0.85)),
        # A towering heap.
        "far_3": _mass(223, soft_look, rise=(-0.2, 0.8), stretch=(0.85, 1.15)),
        # Two heaps grown together.
        "far_4": _mass(227, soft_look, core=0.6, clusters=[(-0.4, 0.0), (0.42, 0.08)], lobes=8,
                       spread=0.45),
        # Three heaps along a ridge.
        "far_5": _mass(229, soft_look, core=0.5, clusters=[(-0.5, 0.0), (0.0, 0.25), (0.5, -0.05)],
                       lobes=6, spread=0.35),
        # A ragged heap.
        "far_6": _mass(233, soft_look, billow=1.7, detail=0.6, warp=0.7),
    }

SETS = {
    # A: opaque cores, strong baked shading.
    "billowy": dict(sky=0.2, bounces=2, puffs=_pair((0.72, 0.65, 0.70, 0.70), 0.05, 0.55, 9.0)),
    # B: opaque cores, light baked shading.
    "soft": dict(sky=0.5, bounces=6, puffs=_pair((0.8, 0.75, 0.78, 0.8), 0.0, 0.7, 4.5)),
    # C as a pair: translucent cores, light shading, two puffs.
    "veil_pair": dict(sky=0.8, bounces=6, puffs=_pair((0.9, 0.85, 0.88, 0.9), 0.02, 0.9, 3.0)),
    # C as shipped: the same look over eight silhouettes, one per card by a hash of its position.
    "veil": dict(sky=0.8, bounces=6, puffs=VEIL_POOL),
    # The placed cloud clusters: the same lighting over six detailed heaps (shipped), a softer
    # candidate with the authored masks' fringe, and the earlier smooth set.
    "far": dict(sky=0.8, bounces=6, puffs=_mass_pool(soft_look=False)),
    "far_soft": dict(sky=0.8, bounces=6, puffs=_mass_pool(soft_look=True)),
    "far_smooth": dict(sky=0.8, bounces=6, puffs=FAR_SMOOTH_POOL),
}

ANISOTROPY = 0.2
# Where the sun key comes from, as (elevation above the image plane's horizon, turn toward the
# camera), degrees. From above and a little in front, so the top and the near face are lit and
# the underside falls into shadow.
SUN_ELEVATION = 55.0
SUN_TOWARD_CAMERA = 35.0
SUN_STRENGTH = 4.0

# Straight-alpha luminance percentile, over texels at least this opaque, that is mapped to 1.0.
NORMALISE_PERCENTILE = 99.0
NORMALISE_MIN_ALPHA = 0.3


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"out": os.path.dirname(os.path.abspath(__file__)), "size": 512, "samples": 2048, "only": None, "set": "veil"}
    i = 0
    while i < len(argv):
        key = argv[i].lstrip("-")
        opts[key] = argv[i + 1]
        i += 2
    opts["out"] = os.path.abspath(opts["out"])
    opts["size"] = int(opts["size"])
    opts["samples"] = int(opts["samples"])
    opts["only"] = set(opts["only"].split(",")) if opts["only"] else None
    return opts


def set_enum(owner, prop, wanted):
    # The engine and the colour management enums are dynamic and under-reported by RNA, so an
    # identifier is tried rather than looked up; the TypeError lists the accepted ones.
    for value in wanted:
        try:
            setattr(owner, prop, value)
            return value
        except TypeError:
            continue
    raise RuntimeError(f"none of {wanted} accepted for {prop}")


def setup_scene(size, samples, lighting):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    set_enum(scene.render, "engine", ["CYCLES"])
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 16
    scene.cycles.volume_bounces = lighting["bounces"]
    scene.cycles.transparent_max_bounces = 16
    scene.cycles.seed = 1
    try_gpu(scene)
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    set_enum(scene.view_settings, "view_transform", ["Standard"])
    set_enum(scene.view_settings, "look", ["None"])
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_depth = "32"

    world = bpy.data.worlds.new("sky")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    bg.inputs["Strength"].default_value = lighting["sky"]
    scene.world = world

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.0
    cam = bpy.data.objects.new("cam", cam_data)
    cam.location = (0.0, -10.0, 0.0)
    cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    sun_data = bpy.data.lights.new("sun", "SUN")
    sun_data.energy = SUN_STRENGTH
    sun_data.angle = math.radians(2.0)
    sun = bpy.data.objects.new("sun", sun_data)
    el = math.radians(SUN_ELEVATION)
    az = math.radians(SUN_TOWARD_CAMERA)
    # Direction from the puff toward the sun: up by the elevation, toward the camera (-Y) by the
    # turn, and a little from the left so the shading is not symmetric.
    to_sun = np.array([-0.25, -math.sin(az) * math.cos(el), math.sin(el)])
    to_sun /= np.linalg.norm(to_sun)
    from mathutils import Vector
    sun.rotation_euler = Vector(tuple(-to_sun)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)
    return scene


def try_gpu(scene):
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
        try:
            prefs.compute_device_type = kind
        except TypeError:
            continue
        prefs.get_devices()
        gpus = [d for d in prefs.devices if d.type == kind]
        if gpus:
            for d in prefs.devices:
                d.use = d.type == kind
            scene.cycles.device = "GPU"
            print(f"cloud puffs: rendering on {kind} {[d.name for d in gpus]}")
            return
    print("cloud puffs: rendering on the CPU")


def lobes(p):
    # A wide core plus seeded lobes around and above it, recentred on their volume-weighted mean
    # so the puff sits in the middle of the frame.
    rng = np.random.default_rng(p["seed"])
    out = [(np.array([0.0, 0.0, 0.0]), p["core"])] if p["core"] > 0.0 else []
    sx, sz = p.get("stretch", (1.0, 1.0))
    rmin, rmax = p.get("radius", (0.32, 0.5))
    zlo, zhi = p.get("rise", (-0.2, 0.45))
    for cx, cz in p.get("clusters", [(0.0, 0.0)]):
        for _ in range(p["lobes"]):
            angle = rng.uniform(0.0, 2.0 * math.pi)
            reach = rng.uniform(0.35, 1.0) * p["spread"]
            centre = np.array([cx + math.cos(angle) * reach * sx, math.sin(angle) * reach * 0.5,
                               cz + rng.uniform(zlo, zhi) * sz])
            out.append((centre, rng.uniform(rmin, rmax)))
    weights = np.array([r ** 3 for _, r in out])
    mean = sum(c * w for (c, _), w in zip(out, weights)) / weights.sum()
    # The spherical window below fades everything to zero inside the unit circle, so a card
    # turned about its own centre never clips the puff against the square's edge.
    return [(tuple(c - mean), r) for c, r in out]


def link(tree, a, b):
    tree.links.new(a, b)


def math_node(tree, op, a=None, b=None, clamp=False):
    n = tree.nodes.new("ShaderNodeMath")
    n.operation = op
    n.use_clamp = clamp
    for idx, v in ((0, a), (1, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[idx].default_value = v
        else:
            link(tree, v, n.inputs[idx])
    return n.outputs[0]


def noise_node(tree, vector, scale, detail, roughness, w):
    n = tree.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = roughness
    n.inputs["W"].default_value = w
    link(tree, vector, n.inputs["Vector"])
    return n


def puff_material(name, p):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    for n in list(tree.nodes):
        tree.nodes.remove(n)
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    coord = tree.nodes.new("ShaderNodeTexCoord").outputs["Object"]

    # Domain warp: the lobes are measured in coordinates pushed about by a low-frequency vector
    # noise, which turns their spheres into lumpy, uneven masses.
    warp_noise = noise_node(tree, coord, p["warp_scale"], 3.0, 0.5, float(p["seed"]))
    offset = tree.nodes.new("ShaderNodeVectorMath")
    offset.operation = "SUBTRACT"
    link(tree, warp_noise.outputs["Color"], offset.inputs[0])
    offset.inputs[1].default_value = (0.5, 0.5, 0.5)
    scaled = tree.nodes.new("ShaderNodeVectorMath")
    scaled.operation = "SCALE"
    link(tree, offset.outputs[0], scaled.inputs[0])
    scaled.inputs["Scale"].default_value = p["warp"]
    warped = tree.nodes.new("ShaderNodeVectorMath")
    warped.operation = "ADD"
    link(tree, coord, warped.inputs[0])
    link(tree, scaled.outputs[0], warped.inputs[1])
    q = warped.outputs[0]

    field = None
    for (c, r) in lobes(p):
        sub = tree.nodes.new("ShaderNodeVectorMath")
        sub.operation = "SUBTRACT"
        link(tree, q, sub.inputs[0])
        sub.inputs[1].default_value = c
        length = tree.nodes.new("ShaderNodeVectorMath")
        length.operation = "LENGTH"
        link(tree, sub.outputs[0], length.inputs[0])
        d = math_node(tree, "DIVIDE", length.outputs["Value"], r)
        lobe = math_node(tree, "SUBTRACT", 1.0, d, clamp=True)
        field = lobe if field is None else math_node(tree, "ADD" if p.get("union") == "sum" else "MAXIMUM", field, lobe)

    # Billows: fBM noise on the warped coordinates, centred on zero, eats into the falloff.
    billow = noise_node(tree, q, p["billow_scale"], 8.0, 0.6, float(p["seed"]) + 7.0)
    centred = math_node(tree, "SUBTRACT", billow.outputs["Fac"], 0.5)
    field = math_node(tree, "ADD", field, math_node(tree, "MULTIPLY", centred, p["billow"]))
    # Optional fine surface detail: a second, higher-frequency fBM that breaks the edges into
    # small curls and gives the lit surface its own texture.
    if p.get("detail", 0.0) > 0.0:
        fine = noise_node(tree, q, p["detail_scale"], 6.0, 0.7, float(p["seed"]) + 13.0)
        fine_c = math_node(tree, "SUBTRACT", fine.outputs["Fac"], 0.5)
        field = math_node(tree, "ADD", field, math_node(tree, "MULTIPLY", fine_c, p["detail"]))

    # A flattened base, as a cumulus has, and a spherical window so nothing reaches the cube.
    sep = tree.nodes.new("ShaderNodeSeparateXYZ")
    link(tree, coord, sep.inputs[0])
    base = math_node(tree, "MULTIPLY", math_node(tree, "SUBTRACT", sep.outputs["Z"], p["base_z"]), 4.0, clamp=True)
    radius = tree.nodes.new("ShaderNodeVectorMath")
    radius.operation = "LENGTH"
    link(tree, coord, radius.inputs[0])
    edge, gain = p.get("window", (1.0, 6.0))
    window = math_node(tree, "MULTIPLY", math_node(tree, "SUBTRACT", edge, radius.outputs["Value"]), gain, clamp=True)
    field = math_node(tree, "MULTIPLY", math_node(tree, "MULTIPLY", field, base), window)

    # Soft threshold, then the density: optically thick in the core, thin at the fringe.
    ramp = tree.nodes.new("ShaderNodeMapRange")
    ramp.clamp = True
    ramp.inputs["From Min"].default_value = p["threshold"]
    ramp.inputs["From Max"].default_value = p["threshold"] + p["soft"]
    ramp.inputs["To Min"].default_value = 0.0
    ramp.inputs["To Max"].default_value = p["density"]
    link(tree, field, ramp.inputs["Value"])

    vol = tree.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    vol.inputs["Anisotropy"].default_value = ANISOTROPY
    link(tree, ramp.outputs["Result"], vol.inputs["Density"])
    link(tree, vol.outputs[0], out.inputs["Volume"])
    return mat


def render_puff(scene, name, params, raw_path):
    for obj in [o for o in scene.objects if o.type == "MESH"]:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.mesh.primitive_cube_add(size=2.0, location=(0.0, 0.0, 0.0))
    domain = bpy.context.active_object
    domain.name = name
    domain.data.materials.append(puff_material(name, params))
    scene.render.filepath = raw_path
    bpy.ops.render.render(write_still=True)


def finish(raw_path, final_path):
    # EXR is linear and premultiplied: un-premultiply, normalise the lit top to 1.0, encode sRGB.
    img = bpy.data.images.load(raw_path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float64).reshape(h, w, 4)
    bpy.data.images.remove(img)
    alpha = np.clip(px[..., 3], 0.0, 1.0)
    rgb = px[..., :3] / np.maximum(alpha, 1e-6)[..., None]
    lum = rgb.mean(axis=2)
    sel = lum[alpha >= NORMALISE_MIN_ALPHA]
    peak = np.percentile(sel, NORMALISE_PERCENTILE) if sel.size else 1.0
    rgb = np.clip(rgb / peak, 0.0, 1.0)

    # Texels with next to no coverage take the coverage-weighted colour of their neighbourhood,
    # so the mip chain does not pull a dark fringe in from their undefined RGB.
    fill = blur_weighted(rgb, alpha, 24)
    thin = np.clip(alpha / 0.05, 0.0, 1.0)[..., None]
    rgb = (rgb * thin) + (fill * (1.0 - thin))

    srgb = np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(rgb, 1.0 / 2.4) - 0.055)
    outpx = np.concatenate([srgb, alpha[..., None]], axis=2)
    outpx = np.round(outpx * 255.0) / 255.0

    out = bpy.data.images.new(os.path.basename(final_path), w, h, alpha=True)
    out.alpha_mode = "STRAIGHT"
    out.pixels = outpx.reshape(-1).tolist()
    out.filepath_raw = final_path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    print(f"cloud puffs: {final_path} peak {peak:.3f} mean alpha {alpha.mean():.3f}")


def blur_weighted(rgb, alpha, radius):
    num = rgb * alpha[..., None]
    den = alpha.copy()
    for _ in range(3):
        num = box(num, radius)
        den = box(den, radius)
    return num / np.maximum(den, 1e-6)[..., None]


def box(a, r):
    # Separable box blur with edge clamping, on the two image axes.
    for axis in (0, 1):
        pad = [(0, 0)] * a.ndim
        pad[axis] = (r + 1, r)
        c = np.cumsum(np.pad(a, pad, mode="edge"), axis=axis)
        n = a.shape[axis]
        hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
        lo = np.take(c, np.arange(0, n), axis=axis)
        a = (hi - lo) / (2 * r + 1)
    return a


def main():
    opts = parse_args()
    os.makedirs(opts["out"], exist_ok=True)
    chosen = SETS[opts["set"]]
    scene = setup_scene(opts["size"], opts["samples"], chosen)
    for name, params in chosen["puffs"].items():
        if opts["only"] and name not in opts["only"]:
            continue
        raw = os.path.join(bpy.app.tempdir or opts["out"], f"{name}_raw.exr")
        final = os.path.join(opts["out"], f"{name}.png.bin")
        render_puff(scene, name, params, raw)
        finish(raw, final)
        os.remove(raw)


main()
