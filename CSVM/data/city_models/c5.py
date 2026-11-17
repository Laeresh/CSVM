"""Generates C5's city models: the facade atlas, its window ids texture, and per building kind a
model file with two near variants and a far box (city_blender.py holds the conventions).

Each HULLS entry holds measured numbers only: PROFILE is analysis/city-hulls/tiers.py's output
for the kind's main mesh, RGB its area-weighted mean texture colour from census.py (the
grade's target), FAMILY the facade family its original texture belongs to (city_atlas.py). The
cblock1 prototype's fourteen kinds are here; the other districts' hulls join them.

  .\\scripts\\MakeVegetationModel.ps1 -Folder city_models -Species c5 -NoRender
"""

import argparse
import os
import sys
import zlib

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402

import city_blender as cb  # noqa: E402

STONE, BRICK, WARM, STEEL = 0, 1, 2, 3


def sq(h):
    return [(-h, h, -h, h)]


APEX = [(0.0, 0.0, 0.0, 0.0)]

# The near variants. near0 keeps the hull's shape and dresses its roofs; near1 sets the top floor
# back or reshapes the crown, with other dressing. Kit entries: (kind, (x, z) spot share or none).
WALKUP = (
    dict(parapet=1.1, kit=[("tank", (0.25, 0.7)), ("bulkhead", (0.8, 0.25)), ("vent",), ("vent",)]),
    dict(parapet=0.7, raised=1.0, kit=[("bulkhead", (0.2, 0.3)), ("vent",), ("vent",), ("vent",)]),
)
TENEMENT = (
    dict(parapet=1.0, kit=[("chimney", (0.05, 0.5)), ("chimney", (0.95, 0.5)), ("tank", (0.6, 0.65)), ("bulkhead", (0.2, 0.2))]),
    dict(parapet=0.6, raised=1.2, kit=[("chimney", (0.5, 0.05)), ("chimney", (0.5, 0.95)), ("chimney", (0.05, 0.3)),
                                       ("bulkhead", (0.75, 0.6)), ("vent",)]),
)
TOWER = (
    dict(parapet=1.2, ledge=0.9, kit=[("machine", (0.5, 0.45)), ("tank", (0.15, 0.85)), ("vent",)], ledge_kit=[("vent",)]),
    dict(setback=2.0, parapet=0.8, ledge=1.0, kit=[("tank", (0.75, 0.3)), ("bulkhead", (0.2, 0.75)), ("vent",), ("vent",)]),
)


def chimneys_on_hip(mesh, planes):
    """Two chimneys through cb01d's hipped roof, 1.4 m above the slope where they stand."""
    for x, z in ((-8.0, 4.0), (8.0, -5.0)):
        cb.box(mesh, x - 0.4, x + 0.4, z - 0.6, z + 0.6, 20.0, 23.4)


def mast(mesh, planes):
    """A short mast on cb05a's crown apex."""
    cb.box(mesh, -0.2, 0.2, -0.2, 0.2, 58.0, 62.4)


def finial(mesh, planes):
    cb.box(mesh, -0.6, 0.6, -0.6, 0.6, 58.0, 60.2)


# cb04a's further mesh: a thin fin on each corner's diagonal over its upper slope, as (distance
# from the axis along the diagonal, y), measured; the decoration's motif.
FIN = ((12.25, 64.08), (11.67, 64.91), (10.95, 64.72), (7.82, 69.31), (7.82, 68.12))


def fins_cb04a(mesh, planes):
    """The four corner fins, each drawn from both sides."""
    for sx, sz in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        p = [(r * sx, y, r * sz) for r, y in FIN]
        side = cb.np.array((sx, 0.0, -sz))
        for a, b, c in ((0, 1, 2), (2, 1, 3), (2, 3, 4)):
            tri = [p[a], p[b], p[c]]
            uvs = [(q[0] / cb.TW, q[1] / cb.TW) for q in tri]
            mesh.face(tri, uvs, cb.ROOF, side)
            mesh.face(tri, uvs, cb.ROOF, -side)


HIP = (
    dict(extras=[chimneys_on_hip]),
    dict(crown=[(23.0, sq(4.0))], parapet=0.4, kit=[("bulkhead", (0.5, 0.5))]),
)
DECO_CROWN = (
    dict(extras=[mast]),
    dict(crown=[(56.0, sq(7.5)), (57.0, sq(7.5)), (57.0, sq(3.6)), (58.0, sq(3.6))], parapet=0.5, ledge=0.5,
         extras=[finial]),
)

P_CB00 = [(0, [(-20, 20, -12, 12)]), (16, [(-20, 20, -12, 12)])]
P_CB01 = [(0, sq(16)), (24, sq(16))]
P_CB03 = [
    (0, sq(16)), (36, sq(16)),
    (36, [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12), (4, 16, -16, -12), (4, 16, 12, 16)]),
    (60, [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12), (4, 16, -16, -12), (4, 16, 12, 16)]),
    (60, sq(12)), (64, sq(12)),
]

# Family by the original's facade texture: smbldg1 and smbldg2 are grey stone walk-ups, smbldg3
# and smbldg4 brick tenements, bldg1 and bldg4 warm office towers, bldg2 and bldg3 dark ones.
HULLS = {
    "cb00a": dict(family=STONE, rgb=(49, 50, 49), profile=P_CB00, near=WALKUP),      # smbldg1
    "cb00b": dict(family=STONE, rgb=(43, 44, 36), profile=P_CB00, near=WALKUP),      # smbldg2
    "cb01a": dict(family=STONE, rgb=(51, 52, 51), profile=P_CB01, near=WALKUP),      # smbldg1
    "cb01b": dict(family=STONE, rgb=(43, 44, 35), profile=P_CB01, near=WALKUP),      # smbldg2
    "cb01c": dict(family=BRICK, rgb=(44, 41, 37), profile=P_CB01, near=TENEMENT),    # smbldg3
    "cb01d": dict(family=BRICK, rgb=(46, 38, 29), near=HIP,                          # smbldg4
                  profile=[(0, sq(16)), (20, sq(16)), (24, APEX)]),
    "cb02a": dict(family=STEEL, rgb=(50, 51, 55), near=TOWER, profile=[              # bldg3
        (0, sq(15.89)), (35, sq(15.89)),
        (35, [(-15.89, 15.89, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89)]),
        (40, [(-15.89, 15.89, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89)]),
        (40, [(-15.89, 5.3, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89), (5.3, 15.89, -5.3, 5.3)]),
        (44.99, [(-15.89, 5.3, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89), (5.3, 15.89, -5.3, 5.3)]),
        (44.99, [(-15.89, 5.3, -15.89, 5.3)]), (65.18, [(-15.89, 5.3, -15.89, 5.3)])]),
    "cb02b": dict(family=WARM, rgb=(65, 62, 54), near=TOWER, profile=[               # bldg4
        (0, sq(16)), (36, sq(16)),
        (36, [(-16, 16, -16, 4), (-4, 16, 4, 16)]), (40, [(-16, 16, -16, 4), (-4, 16, 4, 16)]),
        (40, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]), (44, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]),
        (44, [(-16, 4, -16, 4)]), (64, [(-16, 4, -16, 4)])]),
    "cb03a": dict(family=WARM, rgb=(72, 65, 57), profile=P_CB03, near=TOWER),        # bldg1
    "cb03b": dict(family=STEEL, rgb=(43, 44, 37), profile=P_CB03, near=TOWER),       # bldg2
    "cb04a": dict(family=STEEL, rgb=(43, 44, 37), near=(                             # bldg2, clock, motif
        dict(TOWER[0], extras=[fins_cb04a]), dict(TOWER[1], extras=[fins_cb04a])),
        profile=[(0, sq(16)), (32, sq(16)), (36, sq(12)), (63.53, sq(12)), (68, sq(8)), (80, sq(8))]),
    "cb04b": dict(family=STEEL, rgb=(54, 52, 54), near=TOWER,                        # bldg3
                  profile=[(0, sq(16)), (16, sq(16)), (20, sq(12)), (60, sq(12)), (64, sq(8)), (80, sq(8))]),
    "cb05a": dict(family=WARM, rgb=(68, 68, 60), near=DECO_CROWN,                    # bldg4, teal crown
                  profile=[(0, sq(11.86)), (56, sq(11.86)), (58.93, APEX)]),
    "cb05b": dict(family=WARM, rgb=(73, 65, 57), near=TOWER,                         # bldg1
                  profile=[(0, sq(12)), (64, sq(12)), (64, sq(8)), (92, sq(8))]),
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out", default=os.path.dirname(os.path.abspath(__file__)))
    p.add_argument("--only", nargs="*", help="kinds to write (the atlas is always written)")
    # Accepted for the runner's sake: the city's comparison renders are analysis/city-hulls/preview.py's.
    p.add_argument("--render")
    p.add_argument("--data-root")
    p.add_argument("--chapter", default="C5", choices=["C5"])
    return p.parse_args(argv)


def main():
    args = parse_args()
    cb.vb.reset_scene()
    means = cb.write_atlas(args.out, args.chapter)
    for model, h in HULLS.items():
        if args.only and model not in args.only:
            continue
        far = cb.far_model(h["profile"])
        nears = [cb.near_model(h["profile"], n, np.random.default_rng([zlib.crc32(model.encode()), i]))
                 for i, n in enumerate(h["near"])]
        factor = cb.grade(far, h["family"], h["rgb"], means)
        mat = cb.grade_material(model, factor)
        objs = [cb.to_object("near%d" % i, m, h["family"], mat) for i, m in enumerate(nears)]
        objs.append(cb.to_object("far", far, h["family"], mat))
        path = cb.export(objs, args.out, args.chapter, model)
        print("CITY %s: near0 %d, near1 %d, far %d triangles; family %d; grade %.4f %.4f %.4f; %s (%d bytes)" % (
            model, nears[0].triangles(), nears[1].triangles(), far.triangles(), h["family"], *factor, path,
            os.path.getsize(path)))
        for o in objs:
            me = o.data
            cb.bpy.data.objects.remove(o)
            cb.bpy.data.meshes.remove(me)
        cb.bpy.data.materials.remove(mat)


main()
