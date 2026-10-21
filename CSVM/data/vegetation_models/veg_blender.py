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
"""

import argparse
import json
import math
import os
import sys

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
    node = next(n for n in nodes if n["name"] == model)
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
    cam.data.ortho_scale = 2 * gap + max(w, model_obj.dimensions.x) + 4
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


def report(obj, glb, renders):
    d = obj.dimensions
    print("VEG model %s: %d triangles, %.2f x %.2f x %.2f m (w x d x h), glb %s (%d bytes)" % (
        obj.name, triangles(obj), d.x, d.y, d.z, glb, os.path.getsize(glb)))
    for r in renders:
        print("VEG render", r)
