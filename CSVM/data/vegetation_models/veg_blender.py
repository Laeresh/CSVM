"""Shared helpers for the vegetation model generators in this folder (one script per species).

The models are the remake's own art, built from geometry and a procedural texture in the script:
nothing here reads the original's texture into a model. The original card is read only for the
comparison render, which goes to a scratch folder and is never shipped.

Conventions every generator keeps: metres, the pivot at the trunk base on the ground, Blender Z
up (the glTF exporter writes Y up, as Godot reads it), one mesh with one alpha-masked material
and one texture atlas, so an instanced draw takes one surface. The model is written as
<chapter>/<model>.glb.bin: GLB bytes under an extension Godot does not import, packed by the
export's data/*.bin filter, as the cloud puffs ship.

Run through MakeVegetationModel.ps1 at the repository root, or headless by hand:

  blender -b --factory-startup --python CSVM/data/vegetation_models/<species>.py --
      [--chapter C1] [--out DIR] [--render DIR] [--data-root DIR]

--render writes the comparison renders there (needs <data-root>/extracted for the card).

clump_render writes the species' clump tiles beside its model: <chapter>/<model>.clump.png.bin (a strip
of tiles, alpha cut at 0.5), <model>.clump.side.png.bin (each tree's sun-side share, red on its right
half, green on its left) and <model>.clump.json (a tile's width in metres, each tile's tree count).
CanopyClumps draws them in its atlas in place of tiles composed from the cards (docs/formats/model-packs.md).
"""

import argparse
import json
import math
import os
import struct
import sys
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))


def parse_args(chapters):
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--chapter", default=chapters[0], choices=chapters)
    p.add_argument("--out", default=HERE, help="folder the <chapter>/<model>.glb.bin goes under")
    p.add_argument("--render", default=None, help="folder for the comparison renders")
    p.add_argument("--data-root", default=os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(os.path.dirname(HERE)))
    return p.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_image(name, rgba):
    """A packed PNG image from an (H, W, 4) array of sRGB-encoded 0..1 values, row 0 at the bottom."""
    h, w, _ = rgba.shape
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    img.file_format = "PNG"
    img.pack()
    return img


def atlas_material(name, img):
    """Principled BSDF on the atlas, alpha through a Round node: the glTF exporter writes MASK, cutoff 0.5."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = False
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.9
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    rnd = nt.nodes.new("ShaderNodeMath")
    rnd.operation = "ROUND"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], rnd.inputs[0])
    nt.links.new(rnd.outputs[0], bsdf.inputs["Alpha"])
    return mat


def mesh_object(name, bm, normals, mat):
    """Turns a bmesh into an object with per-vertex custom normals (None keeps the geometric ones)."""
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if normals is not None:
        me.normals_split_custom_set_from_vertices([tuple(n) for n in normals])
    me.materials.append(mat)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def export_glb(obj, out_dir, chapter, model):
    """Writes <out_dir>/<chapter>/<model>.glb.bin and returns its path."""
    folder = os.path.join(out_dir, chapter)
    os.makedirs(folder, exist_ok=True)
    tmp = os.path.join(folder, model + ".glb")
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=tmp, export_format="GLB", use_selection=True, export_yup=True, export_apply=True,
        export_normals=True, export_texcoords=True, export_materials="EXPORT",
        export_vertex_color="NONE", export_animations=False, export_extras=False)
    dst = tmp + ".bin"
    os.replace(tmp, dst)
    return dst


def read_card(data_root, chapter, model):
    """The card quad's width and height (m) and its texture's PNG, from the chapter's extracted gamez."""
    gz = os.path.join(data_root, "extracted", chapter, "gamez")
    load = lambda f: json.load(open(os.path.join(gz, f), encoding="utf-8"))
    nodes, models, mats, texs = load("nodes.json"), load("models.json"), load("materials.json"), load("textures.json")
    # A placed copy carries the name without a model (C3's palmtree2): take the first that resolves one.
    node = next(n for n in nodes if n["name"] == model
                and (n["model_index"] >= 0 or any(nodes[c]["model_index"] >= 0 for c in n["child_indices"])))
    mi = node["model_index"]
    if mi < 0:
        mi = next(nodes[c]["model_index"] for c in node["child_indices"] if nodes[c]["model_index"] >= 0)
    m = models[mi]
    xs = [v["x"] for v in m["vertices"]]
    ys = [v["y"] for v in m["vertices"]]
    mat = mats[m["polygons"][0]["materials"][0]["material_index"]]
    tex = texs[mat["Textured"]["texture_index"]]["name"]
    png = os.path.join(data_root, "extracted", chapter, "texture", os.path.splitext(tex)[0] + ".png")
    return max(xs) - min(xs), max(ys) - min(ys), png


def _card_object(w, h, png):
    """The original card at its real size, unlit (as the game draws it) and blended on its alpha."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in ((-w / 2, 0, 0), (w / 2, 0, 0), (w / 2, 0, h), (-w / 2, 0, h))]
    f = bm.faces.new(vs)
    uv = bm.loops.layers.uv.new("UVMap")
    for loop, c in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop[uv].uv = c
    mat = bpy.data.materials.new("card")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(png)
    tex.interpolation = "Linear"
    emit = nt.nodes.new("ShaderNodeEmission")
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
    nt.links.new(tex.outputs["Alpha"], mix.inputs[0])
    nt.links.new(clear.outputs[0], mix.inputs[1])
    nt.links.new(emit.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    obj = mesh_object("card", bm, None, mat)
    obj.visible_shadow = False
    return obj


def compare_render(model_obj, card, out_dir, stem, atlas, views=(("side", 4.0), ("above", 35.0))):
    """Renders the model left and the card right, same camera and sun, one PNG per view elevation.

    Also writes the model's atlas, so the texture can be judged on its own.
    """
    w, h, png = card
    scene = bpy.context.scene
    os.makedirs(out_dir, exist_ok=True)
    atlas.save_render(os.path.join(out_dir, stem + "_atlas.png"))
    gap = max(w, model_obj.dimensions.x) / 2 + 1.0
    model_obj.location.x = -gap
    _card_object(w, h, png).location.x = gap

    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=60)
    gmat = bpy.data.materials.new("ground")
    gmat.use_nodes = True
    next(n for n in gmat.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.09, 0.1, 0.07, 1)
    mesh_object("ground", bm, None, gmat)

    world = bpy.data.worlds.new("sky")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.72, 1)
    bg.inputs["Strength"].default_value = 0.45
    scene.world = world

    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(2)
    sun.rotation_euler = (math.radians(45), 0, math.radians(-30))
    scene.collection.objects.link(sun)

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    # Wide enough for both, and tall enough for the taller (the frame is 1.4 times wider than high).
    cam.data.ortho_scale = max(2 * gap + max(w, model_obj.dimensions.x) + 4, 1.4 * (max(h, model_obj.dimensions.z) + 3))
    scene.collection.objects.link(cam)
    scene.camera = cam

    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 64
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1400, 1000
    target = Vector((0, 0, h * 0.5))
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for name, elev in views:
        e = math.radians(elev)
        cam.location = target + Vector((0, -100 * math.cos(e), 100 * math.sin(e)))
        cam.rotation_euler = (math.pi / 2 - e, 0, 0)
        scene.render.filepath = os.path.join(out_dir, "%s_%s.png" % (stem, name))
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
    return paths


# The tile convention CanopyClumps composes by: 24 m wide at 10 px a metre, the trees spread across it in
# even strata at 0.85 to 1.1 of the card, a tree up to 16 m deeper raised up to 8 m (a row seen from about
# 30 degrees above). Rendered at CLUMP_SS times that per axis and averaged down.
CLUMP_WIDTH_M = 24.0
CLUMP_PX_PER_M = 10
CLUMP_RISE_M = 8.0
CLUMP_SS = 4


def write_png(path, rgb):
    """An 8-bit PNG from a (H, W, 3 or 4) uint8 array, row 0 at the top. No time or text chunk, so a
    rerun writes the same bytes."""
    h, w, c = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    head = struct.pack(">IIBBBBB", w, h, 8, {3: 2, 4: 6}[c], 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", head) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def _side_material(atlas):
    """Emits each tree's sun-side share: its offset from its own trunk over a sixth of its width (the
    object's side_w), red to the right, green to the left, each clamped to 0..1, cut by the atlas alpha."""
    mat = bpy.data.materials.new("clump_side")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    node = nt.nodes.new
    tex = node("ShaderNodeTexImage")
    tex.image = atlas
    rnd = node("ShaderNodeMath")
    rnd.operation = "ROUND"
    geo, info, width = node("ShaderNodeNewGeometry"), node("ShaderNodeObjectInfo"), node("ShaderNodeAttribute")
    width.attribute_type = "OBJECT"
    width.attribute_name = "side_w"
    p, o = node("ShaderNodeSeparateXYZ"), node("ShaderNodeSeparateXYZ")
    sub, div, neg = node("ShaderNodeMath"), node("ShaderNodeMath"), node("ShaderNodeMath")
    sub.operation, div.operation, neg.operation = "SUBTRACT", "DIVIDE", "MULTIPLY"
    neg.inputs[1].default_value = -1.0
    right, left = node("ShaderNodeMath"), node("ShaderNodeMath")
    for m in (right, left):
        m.operation = "ADD"
        m.use_clamp = True
        m.inputs[1].default_value = 0.0
    rgb = node("ShaderNodeCombineColor")
    rgb.inputs[2].default_value = 0.0
    emit, clear, mix = node("ShaderNodeEmission"), node("ShaderNodeBsdfTransparent"), node("ShaderNodeMixShader")
    link = nt.links.new
    link(geo.outputs["Position"], p.inputs[0])
    link(info.outputs["Location"], o.inputs[0])
    link(p.outputs[0], sub.inputs[0])
    link(o.outputs[0], sub.inputs[1])
    link(sub.outputs[0], div.inputs[0])
    link(width.outputs["Fac"], div.inputs[1])
    link(div.outputs[0], right.inputs[0])
    link(div.outputs[0], neg.inputs[0])
    link(neg.outputs[0], left.inputs[0])
    link(right.outputs[0], rgb.inputs[0])
    link(left.outputs[0], rgb.inputs[1])
    link(rgb.outputs[0], emit.inputs["Color"])
    link(tex.outputs["Alpha"], rnd.inputs[0])
    link(rnd.outputs[0], mix.inputs[0])
    link(clear.outputs[0], mix.inputs[1])
    link(emit.outputs[0], mix.inputs[2])
    link(mix.outputs[0], out.inputs["Surface"])
    return mat


def _render_exr(path):
    """Renders the scene to a float EXR and returns its premultiplied RGBA, row 0 at the bottom."""
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)


def _down(px):
    h, w, c = px.shape
    return px.reshape(h // CLUMP_SS, CLUMP_SS, w // CLUMP_SS, CLUMP_SS, c).mean(axis=(1, 3))


def _srgb(lin):
    lin = np.clip(lin, 0.0, 1.0)
    return np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)


def clump_render(obj, card_w, card_h, out_dir, chapter, model, trees, seed, grade, scratch=None):
    """Renders the clump tiles of obj's species, one tile of trees[i] trees each, side by side, the colour
    scaled by grade (linear, per channel).

    Orthographic from the side, the sky and a sun from above the camera lighting it evenly left and right
    (the game adds the sun side from the side map). Writes the three files beside the model and returns
    their paths; with scratch, also a preview of the strip on grey there.
    """
    scene = bpy.context.scene
    tile_w, tile_h = int(CLUMP_WIDTH_M * CLUMP_PX_PER_M), math.ceil((CLUMP_RISE_M + card_h * 1.1) * CLUMP_PX_PER_M)
    folder = os.path.join(out_dir, chapter)
    os.makedirs(folder, exist_ok=True)
    tmp = os.path.join(folder, model + ".clump.tmp.exr")
    made = []

    world = bpy.data.worlds.new("clump_sky")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.72, 1)
    bg.inputs["Strength"].default_value = 0.6
    scene.world = world
    sun = bpy.data.objects.new("clump_sun", bpy.data.lights.new("clump_sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(40), 0, 0)
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("clump_cam", bpy.data.cameras.new("clump_cam"))
    cam.data.type = "ORTHO"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.ortho_scale = tile_h / CLUMP_PX_PER_M
    cam.data.clip_end = 1000
    cam.location = (0, -300, tile_h / CLUMP_PX_PER_M / 2)
    cam.rotation_euler = (math.pi / 2, 0, 0)
    scene.collection.objects.link(cam)
    made += [sun, cam]
    scene.camera = cam

    # EEVEE, as the comparison render: Cycles shades the crown's bent normals about 8 times darker.
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 16
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = tile_w * CLUMP_SS, tile_h * CLUMP_SS
    scene.render.resolution_percentage = 100
    fmt = scene.render.image_settings
    fmt.file_format, fmt.color_depth, fmt.color_mode = "OPEN_EXR", "32", "RGBA"

    rng = np.random.default_rng(seed)
    colour_mat = obj.data.materials[0]
    side_mat = _side_material(next(n for n in colour_mat.node_tree.nodes if n.type == "TEX_IMAGE").image)
    obj.hide_render = True
    colour, side = [], []
    for t, n in enumerate(trees):
        group = []
        for i in range(n):
            depth, scale, jitter, yaw = rng.uniform(), 0.85 + 0.25 * rng.uniform(), rng.uniform(), rng.uniform(0, 2 * math.pi)
            half = min(CLUMP_WIDTH_M / 2, card_w * scale / 2)
            o = bpy.data.objects.new("clump_%d_%d" % (t, i), obj.data)
            o.location = (half + (CLUMP_WIDTH_M - 2 * half) * (i + jitter) / n - CLUMP_WIDTH_M / 2, 16 * depth, CLUMP_RISE_M * depth)
            o.rotation_euler = (0, 0, yaw)
            o.scale = (scale, scale, scale)
            o["side_w"] = card_w * scale / 6
            scene.collection.objects.link(o)
            group.append(o)
        obj.data.materials[0] = colour_mat
        colour.append(_down(_render_exr(tmp)))
        obj.data.materials[0] = side_mat
        side.append(_down(_render_exr(tmp)))
        for o in group:
            bpy.data.objects.remove(o)
    obj.data.materials[0] = colour_mat
    obj.hide_render = False
    os.remove(tmp)
    for o in made:
        bpy.data.objects.remove(o)
    scene.render.film_transparent = False
    fmt.file_format, fmt.color_depth = "PNG", "8"

    # Premultiplied: a texel's colour is its covered mean. Clear texels are written black; the game bleeds them.
    c, s = np.concatenate(colour, axis=1)[::-1], np.concatenate(side, axis=1)[::-1]
    a = c[..., 3:4]
    solid = a >= 0.5
    cover = np.maximum(a, 1e-6)
    tile = np.zeros(c.shape, np.uint8)
    tile[..., :3] = np.where(solid, np.round(_srgb(c[..., :3] / cover * np.array(grade, np.float32)) * 255), 0)
    tile[..., 3:] = np.where(solid, 255, 0)
    sides = np.zeros(c.shape[:2] + (3,), np.uint8)
    sides[..., :2] = np.where(solid, np.round(np.clip(s[..., :2] / cover, 0, 1) * 255), 0)

    stem = os.path.join(folder, model + ".clump")
    write_png(stem + ".png.bin", tile)
    write_png(stem + ".side.png.bin", sides)
    with open(stem + ".json", "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps({"width_m": CLUMP_WIDTH_M, "trees": list(trees)}, indent=2) + "\n")
    if scratch:
        os.makedirs(scratch, exist_ok=True)
        grey = np.full(tile.shape[:2] + (3,), 128, np.float32)
        k = tile[..., 3:] / 255.0
        write_png(os.path.join(scratch, "%s_%s_clump.png" % (chapter, model)), np.round(tile[..., :3] * k + grey * (1 - k)).astype(np.uint8))
        write_png(os.path.join(scratch, "%s_%s_clump_side.png" % (chapter, model)), sides)
        mean = tile[..., :3][tile[..., 3] > 0].mean(axis=0)
        print("VEG clump mean sRGB %.1f %.1f %.1f over %d solid texels" % (*mean, int((tile[..., 3] > 0).sum())))
    return [stem + ".png.bin", stem + ".side.png.bin", stem + ".json"]


def report(obj, glb, renders):
    d = obj.dimensions
    print("VEG model %s: %d triangles, %.2f x %.2f x %.2f m (w x d x h), glb %s (%d bytes)" % (
        obj.name, triangles(obj), d.x, d.y, d.z, glb, os.path.getsize(glb)))
    for r in renders:
        print("VEG render", r)
