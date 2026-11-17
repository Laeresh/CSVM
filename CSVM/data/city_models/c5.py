"""Generates C5's city models: the facade atlas, its window ids texture, and per building kind a
model file with two to four near variants and a far box (city_blender.py holds the conventions).

Each HULLS entry holds measured numbers only: PROFILE is analysis/city-hulls/tiers.py's output
for the kind's main mesh, RGB its area-weighted mean unlit texture colour from census.py (the
texels at or below its UNLIT_LUMA, so the original's painted lit windows are left out; the
grade's target, since the engine lights the windows itself), FAMILY the facade family its
original texture belongs to (city_atlas.py), TURN the degrees the building stands turned in
its mesh frame. The kinds of cblock1, cblock2 and cblock3 are here; the other districts' hulls
join them.

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
# A shape placed 1,000 times or more in the city takes a third variant and one placed 2,000 times
# or more a fourth (the tuples' later entries, analysis/city-hulls/FINDINGS.md "Variants"): a
# set-back top floor, light wells cut into the long sides, or a crown stepped in twice.
WALKUP = (
    dict(parapet=1.1, kit=[("tank", (0.25, 0.7)), ("bulkhead", (0.8, 0.25)), ("vent",), ("vent",)]),
    dict(parapet=0.7, raised=1.0, kit=[("bulkhead", (0.2, 0.3)), ("vent",), ("vent",), ("vent",)]),
    dict(setback=1.5, parapet=0.9, ledge=0.5, kit=[("tank", (0.5, 0.5)), ("vent",), ("vent",)]),
    dict(notch=[("z-", 6.0, 2.5), ("z+", 6.0, 2.5)], parapet=1.2,
         kit=[("bulkhead", (0.2, 0.5)), ("tank", (0.8, 0.5)), ("vent",), ("vent",)]),
)
TENEMENT = (
    dict(parapet=1.0, kit=[("chimney", (0.05, 0.5)), ("chimney", (0.95, 0.5)), ("tank", (0.6, 0.65)), ("bulkhead", (0.2, 0.2))]),
    dict(parapet=0.6, raised=1.2, kit=[("chimney", (0.5, 0.05)), ("chimney", (0.5, 0.95)), ("chimney", (0.05, 0.3)),
                                       ("bulkhead", (0.75, 0.6)), ("vent",)]),
    dict(notch=[("z-", 8.0, 2.5)], parapet=0.8, kit=[("chimney", (0.05, 0.95)), ("chimney", (0.5, 0.95)),
                                                     ("chimney", (0.95, 0.95)), ("tank", (0.5, 0.35)), ("vent",)]),
)
TOWER = (
    dict(parapet=1.2, ledge=0.9, kit=[("machine", (0.5, 0.45)), ("tank", (0.15, 0.85)), ("vent",)], ledge_kit=[("vent",)]),
    dict(setback=2.0, parapet=0.8, ledge=1.0, kit=[("tank", (0.75, 0.3)), ("bulkhead", (0.2, 0.75)), ("vent",), ("vent",)]),
    dict(steps=[(8.0, 1.25), (4.0, 2.5)], parapet=0.7, ledge=0.6, kit=[("machine", (0.5, 0.5)), ("vent",)],
         ledge_kit=[("tank",)]),
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

def slab(mesh, x0, x1, z0, z1, y0, y1):
    """A box closed underneath too, for a part seen from below."""
    cb.box(mesh, x0, x1, z0, z1, y0, y1)
    pts = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)]
    mesh.face(pts, [(p[0] / cb.TW, p[2] / cb.TW) for p in pts], cb.ROOF, (0, -1, 0))


def frame_x(mesh, planes):
    """cb07b's open crown, measured: two 0.73 m walls along x on the z edges of the top tier from
    64 to 73.58 m, joined by two 0.19 m plates at 68.6 and 73.39 m."""
    for z0, z1 in ((-8.0, -7.24), (7.27, 8.0)):
        cb.box(mesh, -8.0, 8.0, z0, z1, 64.0, 73.58)
    for y in (68.6, 73.39):
        slab(mesh, -8.0, 8.0, -7.24, 7.27, y, y + 0.19)


def frame_z(mesh, planes):
    """The same crown with its walls on the x edges."""
    for x0, x1 in ((-8.0, -7.24), (7.27, 8.0)):
        cb.box(mesh, x0, x1, -8.0, 8.0, 64.0, 73.58)
    for y in (68.6, 73.39):
        slab(mesh, -7.24, 7.27, -8.0, 8.0, y, y + 0.19)


def crown_block(mesh, planes):
    """The far box's stand-in for cb07b's open crown: its outer box, closed."""
    cb.box(mesh, -8.0, 8.0, -8.0, 8.0, 64.0, 73.58)


def mast_cb09(mesh, planes):
    """A mast through cb09's pyramid apex at 108 m."""
    cb.box(mesh, -0.25, 0.25, -0.25, 0.25, 106.0, 111.5)


def finial_cb09(mesh, planes):
    cb.box(mesh, -0.4, 0.4, -0.4, 0.4, 105.5, 110.8)


FRAME = (
    dict(parapet=0.6, ledge=0.9, kit=[("vent",), ("vent",)], ledge_kit=[("vent",)], extras=[frame_x]),
    dict(parapet=0.6, ledge=0.5, kit=[("tank", (0.5, 0.5))], extras=[frame_z]),
)
# cb09's pyramid: a mast on it, or a crown stepped in three tiers under its slope.
SPIRE = (
    dict(extras=[mast_cb09]),
    dict(crown=[(99, sq(8)), (99, sq(6)), (102, sq(6)), (102, sq(3.5)), (105.5, sq(3.5))], parapet=0.5, ledge=0.4,
         extras=[finial_cb09]),
)
# cb10a's shed top rising to 108 m on its +z edge: kept, or built as four floors stepping up under it.
SHED = (
    TOWER[0],
    dict(crown=[(95, sq(8)), (95, [(-8, 8, -4, 8)]), (99, [(-8, 8, -4, 8)]), (99, [(-8, 8, 0, 8)]),
                (103, [(-8, 8, 0, 8)]), (103, [(-8, 8, 4, 8)]), (107, [(-8, 8, 4, 8)])],
         parapet=0.5, ledge=0.5, kit=[("vent",)], ledge_kit=[("vent",)]),
)

P_CB00 = [(0, [(-20, 20, -12, 12)]), (16, [(-20, 20, -12, 12)])]
P_CB01 = [(0, sq(16)), (24, sq(16))]
P_CB03 = [
    (0, sq(16)), (36, sq(16)),
    (36, [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12), (4, 16, -16, -12), (4, 16, 12, 16)]),
    (60, [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12), (4, 16, -16, -12), (4, 16, 12, 16)]),
    (60, sq(12)), (64, sq(12)),
]
P_CB07 = [(0, sq(16)), (36, sq(16)), (36, sq(12)), (56, sq(12)), (56, sq(8)), (80, sq(8))]
# The two chamfers rise 3.6 m from tier to tier; tiers.py's 12.01 and 8.01 are its extrapolation.
P_CB09 = [(0, sq(16)), (36, sq(16)), (39.6, sq(12)), (84, sq(12)), (87.6, sq(8)), (96, sq(8)), (108, APEX)]
P_CB11 = [(0, sq(16)), (32, sq(16))]

# Family by the original's facade texture: smbldg1 and smbldg2 are grey stone walk-ups, smbldg3
# and smbldg4 brick tenements, bldg1 and bldg4 warm office towers, bldg2 and bldg3 dark ones.
HULLS = {
    "cb00a": dict(family=STONE, rgb=(35, 36, 35), profile=P_CB00, near=WALKUP),      # smbldg1
    "cb00b": dict(family=STONE, rgb=(33, 34, 27), profile=P_CB00, near=WALKUP),      # smbldg2
    "cb01a": dict(family=STONE, rgb=(34, 35, 34), profile=P_CB01, near=WALKUP[:3]),  # smbldg1
    "cb01b": dict(family=STONE, rgb=(31, 33, 24), profile=P_CB01, near=WALKUP[:3]),  # smbldg2
    "cb01c": dict(family=BRICK, rgb=(35, 31, 27), profile=P_CB01, near=TENEMENT[:2]),  # smbldg3
    "cb01d": dict(family=BRICK, rgb=(37, 28, 18), near=HIP,                          # smbldg4
                  profile=[(0, sq(16)), (20, sq(16)), (24, APEX)]),
    "cb02a": dict(family=STEEL, rgb=(31, 32, 42), near=TOWER, profile=[              # bldg3
        (0, sq(15.89)), (35, sq(15.89)),
        (35, [(-15.89, 15.89, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89)]),
        (40, [(-15.89, 15.89, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89)]),
        (40, [(-15.89, 5.3, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89), (5.3, 15.89, -5.3, 5.3)]),
        (44.99, [(-15.89, 5.3, -15.89, 5.3), (-5.3, 15.89, 5.3, 15.89), (5.3, 15.89, -5.3, 5.3)]),
        (44.99, [(-15.89, 5.3, -15.89, 5.3)]), (65.18, [(-15.89, 5.3, -15.89, 5.3)])]),
    "cb02b": dict(family=WARM, rgb=(36, 32, 27), near=TOWER, profile=[               # bldg4
        (0, sq(16)), (36, sq(16)),
        (36, [(-16, 16, -16, 4), (-4, 16, 4, 16)]), (40, [(-16, 16, -16, 4), (-4, 16, 4, 16)]),
        (40, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]), (44, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]),
        (44, [(-16, 4, -16, 4)]), (64, [(-16, 4, -16, 4)])]),
    "cb03a": dict(family=WARM, rgb=(42, 33, 28), profile=P_CB03, near=TOWER[:2]),    # bldg1
    "cb03b": dict(family=STEEL, rgb=(33, 34, 28), profile=P_CB03, near=TOWER[:2]),   # bldg2
    "cb04a": dict(family=STEEL, rgb=(33, 35, 29), near=(                             # bldg2, clock, motif
        dict(TOWER[0], extras=[fins_cb04a]), dict(TOWER[1], extras=[fins_cb04a])),
        profile=[(0, sq(16)), (32, sq(16)), (36, sq(12)), (63.53, sq(12)), (68, sq(8)), (80, sq(8))]),
    "cb04b": dict(family=STEEL, rgb=(36, 34, 41), near=TOWER[:2],                    # bldg3
                  profile=[(0, sq(16)), (16, sq(16)), (20, sq(12)), (60, sq(12)), (64, sq(8)), (80, sq(8))]),
    "cb05a": dict(family=WARM, rgb=(37, 34, 30), near=DECO_CROWN,                    # bldg4, teal crown
                  profile=[(0, sq(11.86)), (56, sq(11.86)), (58.93, APEX)]),
    "cb05b": dict(family=WARM, rgb=(42, 33, 28), near=TOWER[:2],                     # bldg1
                  profile=[(0, sq(12)), (64, sq(12)), (64, sq(8)), (92, sq(8))]),
    # cblock2 and cblock3 (cb06 and cb11 also stand in cblock5 and cblock6). Each stands turned
    # 45 degrees in its mesh frame; its profile is measured turned back (tiers.py --turn=45).
    "cb06a": dict(family=BRICK, rgb=(37, 30, 24), profile=P_CB00, near=TENEMENT, turn=45),   # smbldg5
    "cb06b": dict(family=STONE, rgb=(35, 36, 35), profile=P_CB00, near=WALKUP[:3], turn=45),  # smbldg1
    "cb06c": dict(family=STONE, rgb=(33, 34, 27), profile=P_CB00, near=WALKUP[:3], turn=45),  # smbldg2
    "cb07a": dict(family=WARM, rgb=(42, 33, 28), profile=P_CB07, near=TOWER[:2], turn=45),    # bldg1
    "cb07b": dict(family=WARM, rgb=(36, 33, 28), near=FRAME, turn=45,                         # bldg4, open crown
                  profile=P_CB07[:-1] + [(64, sq(8))], far_extras=[crown_block]),
    "cb08a": dict(family=WARM, rgb=(42, 33, 28), profile=P_CB03, near=TOWER[:2], turn=45),    # bldg1
    "cb08b": dict(family=STEEL, rgb=(33, 34, 28), profile=P_CB03, near=TOWER[:2], turn=45),   # bldg2
    "cb09a": dict(family=STEEL, rgb=(33, 35, 29), profile=P_CB09, near=SPIRE, turn=45),       # bldg2
    "cb09b": dict(family=WARM, rgb=(41, 33, 28), profile=P_CB09, near=SPIRE, turn=45),        # bldg1
    "cb10a": dict(family=STEEL, rgb=(31, 32, 42), near=SHED, turn=45, profile=[               # bldg3
        (0, sq(16)), (4, sq(16)), (4, sq(12)), (84, sq(12)), (84, sq(8)), (92, sq(8)), (108, [(-8, 8, 8, 8)])]),
    "cb10b": dict(family=STEEL, rgb=(31, 32, 42), near=TOWER[:2], turn=45, profile=[          # bldg3
        (0, sq(16)), (4, sq(16)), (4, sq(12)), (84, sq(12)), (84, sq(8)), (108, sq(8))]),
    "cb11a": dict(family=STONE, rgb=(33, 34, 33), profile=P_CB11, near=WALKUP[:3], turn=45),  # smbldg1
    "cb11b": dict(family=STONE, rgb=(31, 32, 23), profile=P_CB11, near=WALKUP[:3], turn=45),  # smbldg2
    "cb11c": dict(family=BRICK, rgb=(34, 31, 26), profile=P_CB11, near=TENEMENT, turn=45),    # smbldg3
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
        for extra in h.get("far_extras", ()):
            extra(far, None)
        nears = [cb.near_model(h["profile"], n, np.random.default_rng([zlib.crc32(model.encode()), i]))
                 for i, n in enumerate(h["near"])]
        if h.get("turn"):
            for m in nears + [far]:
                cb.turn(m, h["turn"])
        factor = cb.grade(far, h["family"], h["rgb"], means)
        mat = cb.grade_material(model, factor)
        objs = [cb.to_object("near%d" % i, m, h["family"], mat) for i, m in enumerate(nears)]
        objs.append(cb.to_object("far", far, h["family"], mat))
        path = cb.export(objs, args.out, args.chapter, model)
        print("CITY %s: %s, far %d triangles; family %d; grade %.4f %.4f %.4f; %s (%d bytes)" % (
            model, ", ".join("near%d %d" % (i, m.triangles()) for i, m in enumerate(nears)), far.triangles(),
            h["family"], *factor, path, os.path.getsize(path)))
        for o in objs:
            me = o.data
            cb.bpy.data.objects.remove(o)
            cb.bpy.data.meshes.remove(me)
        cb.bpy.data.materials.remove(mat)


main()
