"""Render a mesh pack (.bytes) from a few angles to check orientation/scale."""
import struct, sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection


def quat_mat(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


def read(path, scale=(1, 1, 1)):
    b = open(path, "rb").read()
    assert b[:4] == b"MPK1"
    o = 4
    def i32():
        nonlocal o
        v = struct.unpack_from("<i", b, o)[0]; o += 4; return v
    def f32(n):
        nonlocal o
        v = struct.unpack_from("<%df" % n, b, o); o += 4 * n; return v
    tris = []
    for _ in range(i32()):
        n = i32(); name = b[o:o + n].decode(); o += n
        t = np.array(f32(3)); r = f32(4); s = np.array(f32(3))
        nv = i32()
        v = np.array(f32(nv * 3)).reshape(-1, 3)
        _ = f32(nv * 3)
        R = quat_mat(r)
        wv = (R @ (v * s).T).T + t
        wv = wv * np.array(scale)
        for _g in range(i32()):
            c = struct.unpack_from("<4B", b, o); o += 4
            ni = i32()
            idx = struct.unpack_from("<%di" % ni, b, o); o += 4 * ni
            for k in range(0, ni, 3):
                tris.append((wv[[idx[k], idx[k + 1], idx[k + 2]]], np.array(c[:3]) / 255.0))
    return tris


def render(path, out, scale=(1, 1, 1)):
    tris = read(path, scale)
    fig = plt.figure(figsize=(15, 5))
    views = [(20, -60, "3/4"), (0, 0, "side (+X)"), (90, -90, "top")]
    for i, (el, az, title) in enumerate(views):
        ax = fig.add_subplot(1, 3, i + 1, projection="3d")
        # plot with Unity (x, z, y) -> matplotlib (x, y, z)
        polys = [t[0][:, [0, 2, 1]] for t in tris]
        cols = [t[1] for t in tris]
        pc = Poly3DCollection(polys, facecolors=cols, edgecolors="none")
        ax.add_collection3d(pc)
        allv = np.concatenate(polys)
        mn, mx = allv.min(0), allv.max(0)
        c = (mn + mx) / 2; r = (mx - mn).max() / 2
        ax.set_xlim(c[0] - r, c[0] + r); ax.set_ylim(c[1] - r, c[1] + r); ax.set_zlim(c[2] - r, c[2] + r)
        ax.set_xlabel("x"); ax.set_ylabel("z (fwd)"); ax.set_zlabel("y")
        ax.view_init(el, az); ax.set_title(title)
    print("bounds min", allv.min(0), "max", allv.max(0))
    plt.tight_layout(); plt.savefig(out, dpi=80)


if __name__ == "__main__":
    sc = tuple(float(x) for x in sys.argv[3].split(",")) if len(sys.argv) > 3 else (1, 1, 1)
    render(sys.argv[1], sys.argv[2], sc)
