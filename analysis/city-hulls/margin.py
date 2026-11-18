"""Checks each city model (CSVM/data/city_models/<chapter>/<model>.glb.bin) against its original
hull: per mesh node (near0, near1 and so on, far), the largest inward deviation, the largest height above
the hull and the largest sideways overhang, in metres, against the plan's margin (about 3 m in,
about 4 m above, nothing sideways).

Both shapes are height fields on a GRID-metre grid: a column's top is the highest surface over
it, and the column is taken as solid below that. The city's buildings are stacked prisms with
dressing standing on roofs, so this holds for them; an overhang (cb04a's cornice ring, 0.25 m
past its tier) reads as solid down to the roof below it, which only makes the check more lenient
by that overhang. Inward: a column of the original missing in the model, at a height y, is as
deep as the nearer of the original's surface above it and the original's outline at y. A det dressing model is thin pieces, not a
hull, so it is checked by its bounding box instead: within the original's grown by DET_TOL. Above:
over the original's footprint, the model's top minus the original's. Sideways: a model column
outside the original's footprint, its distance to that footprint. The original is every mesh of
the decoration's chain (census.py's geometry). Prints numbers only.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/city-hulls/margin.py [cb00a cb03a ...]
"""
import json
import os
import struct
import sys

sys.dont_write_bytecode = True

import numpy as np
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census as C  # noqa: E402

MODELS = os.path.join(C.REPO, "CSVM", "data", "city_models")
GRID = 0.25
PAD = 8.0
LIMITS = {"in": 3.0, "above": 4.0, "side": 0.3}
# A det dressing model is measured by its box: at most this far past the original's, in metres.
DET_TOL = 0.05


def read_glb(path):
    """{node name: (positions (n, 3), uv0 (n, 2), uv1 (n, 2), triangles (m, 3))} and the
    material's baseColorFactor."""
    data = open(path, "rb").read()
    jlen = struct.unpack("<I", data[12:16])[0]
    j = json.loads(data[20:20 + jlen])
    bin_ = data[20 + jlen + 8:]
    comps = {5126: np.float32, 5123: np.uint16, 5125: np.uint32, 5121: np.uint8}
    width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}

    def acc(i):
        a = j["accessors"][i]
        bv = j["bufferViews"][a["bufferView"]]
        off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        dt = comps[a["componentType"]]
        n = a["count"] * width[a["type"]]
        return np.frombuffer(bin_, dt, n, off).reshape(a["count"], -1) if width[a["type"]] > 1 \
            else np.frombuffer(bin_, dt, n, off)

    out = {}
    for node in j["nodes"]:
        prim = j["meshes"][node["mesh"]]["primitives"][0]
        at = prim["attributes"]
        out[node["name"]] = (acc(at["POSITION"]).astype(float), acc(at["TEXCOORD_0"]).astype(float),
                             acc(at["TEXCOORD_1"]).astype(float), acc(prim["indices"]).reshape(-1, 3))
    factor = j["materials"][0]["pbrMetallicRoughness"]["baseColorFactor"][:3]
    return out, factor


def heightfield(tris, x0, z0, nx, nz):
    """The highest surface over each GRID cell centre, 0 where there is none."""
    H = np.zeros((nx, nz))
    for t in tris:
        P = np.asarray(t, float)
        xa, za = (P[:, 0] - x0) / GRID - 0.5, (P[:, 2] - z0) / GRID - 0.5
        d = (za[1] - za[2]) * (xa[0] - xa[2]) + (xa[2] - xa[1]) * (za[0] - za[2])
        if abs(d) < 1e-9:
            continue  # a wall: no area seen from above
        i0, i1 = max(0, int(np.floor(xa.min()))), min(nx - 1, int(np.ceil(xa.max())))
        k0, k1 = max(0, int(np.floor(za.min()))), min(nz - 1, int(np.ceil(za.max())))
        if i1 < i0 or k1 < k0:
            continue
        gx, gz = np.meshgrid(np.arange(i0, i1 + 1), np.arange(k0, k1 + 1), indexing="ij")
        w0 = ((za[1] - za[2]) * (gx - xa[2]) + (xa[2] - xa[1]) * (gz - za[2])) / d
        w1 = ((za[2] - za[0]) * (gx - xa[2]) + (xa[0] - xa[2]) * (gz - za[2])) / d
        w2 = 1 - w0 - w1
        m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        y = w0 * P[0, 1] + w1 * P[1, 1] + w2 * P[2, 1]
        sub = H[i0:i1 + 1, k0:k1 + 1]
        sub[m] = np.maximum(sub[m], y[m])
    return H


def deviations(Ho, Hn):
    above = float(np.max(np.where(Ho > 0, Hn - Ho, 0.0), initial=0.0))
    foot = Ho > 0
    side_d = ndimage.distance_transform_edt(~foot, sampling=GRID)
    side = float(np.max(np.where((Hn > 0) & ~foot, side_d, 0.0), initial=0.0))
    inward = 0.0
    for y in np.arange(GRID / 2, Ho.max(), GRID):
        O, N = Ho > y, Hn > y
        miss = O & ~N
        if not miss.any():
            continue
        horiz = ndimage.distance_transform_edt(O, sampling=GRID)
        depth = np.minimum(horiz, Ho - y)
        inward = max(inward, float(depth[miss].max()))
    return inward, above, side


def main():
    ch = C.Chapter()
    names = sys.argv[1:] or sorted(f[:-8] for f in os.listdir(os.path.join(MODELS, "C5")) if f.endswith(".glb.bin"))
    print("%-6s %-6s %6s %8s %8s %8s  %s" % ("model", "node", "tris", "in (m)", "above", "side", "inside the margin"))
    for m in names:
        node = next(i for i in ch.by_name[m + ".flt"] if ch.parts(i))
        orig = [t[0] for t in ch.geometry(node)]
        meshes, _ = read_glb(os.path.join(MODELS, "C5", m + ".glb.bin"))
        if "det" in m:
            # Dressing is thin pieces, not a hull: its box must stay inside the original's grown by DET_TOL.
            olo, ohi = np.min(np.reshape(orig, (-1, 3)), 0), np.max(np.reshape(orig, (-1, 3)), 0)
            for name in sorted(meshes, key=lambda n: (n == "far", n)):
                pos, _, _, tri = meshes[name]
                past = float(max(0.0, *(olo - pos.min(0)), *(pos.max(0) - ohi)))
                print("%-6s %-6s %6d   box: %.3f m past the original's box  %s" % (
                    m, name, len(tri), past, "yes" if past <= DET_TOL else "NO"))
            continue
        allp = np.concatenate([np.asarray(orig).reshape(-1, 3)] + [p for p, _, _, _ in meshes.values()])
        x0, z0 = allp[:, 0].min() - PAD, allp[:, 2].min() - PAD
        nx = int(np.ceil((allp[:, 0].max() + PAD - x0) / GRID))
        nz = int(np.ceil((allp[:, 2].max() + PAD - z0) / GRID))
        Ho = heightfield(orig, x0, z0, nx, nz)
        for name in sorted(meshes, key=lambda n: (n == "far", n)):
            pos, _, _, tri = meshes[name]
            Hn = heightfield(pos[tri], x0, z0, nx, nz)
            i, a, s = deviations(Ho, Hn)
            ok = i <= LIMITS["in"] and a <= LIMITS["above"] and s <= LIMITS["side"]
            print("%-6s %-6s %6d %8.2f %8.2f %8.2f  %s" % (m, name, len(tri), i, a, s, "yes" if ok else "NO"))


if __name__ == "__main__":
    main()
