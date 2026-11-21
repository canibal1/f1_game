"""Track designer/validator. Mirrors the C# TrackSpline sampling (centripetal Catmull-Rom,
closed loop, resampled every 2 m) so the numbers printed here match the game."""
import math, sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

TRACKS = {}

def T(name, pts):
    TRACKS[name] = [(float(x), float(z)) for x, z in pts]

# Coordinates in metres (x right, z forward/up in top view). Index 0 sits in the middle of the
# main straight; driving direction is increasing index (counter-clockwise => pits on the outside).
T("anadolu", [
    (0, 0), (0, 200), (0, 380), (30, 470), (120, 500), (220, 470), (300, 520), (330, 620),
    (420, 680), (540, 640), (600, 520), (560, 400), (470, 330), (440, 220), (500, 110),
    (520, -20), (450, -140), (330, -170), (240, -120), (170, -220), (110, -380), (40, -430), (0, -360), (0, -200),
])
T("costa", [
    (0, 0), (0, 170), (0, 330), (30, 400), (100, 410), (150, 370), (210, 390), (230, 450),
    (300, 480), (380, 450), (400, 370), (360, 300), (380, 220), (450, 190), (470, 100),
    (420, 30), (330, 20), (290, -50), (230, -110), (160, -150), (110, -230), (60, -300), (10, -290), (0, -180),
])
T("fjord", [
    (0, 0), (0, 250), (0, 480), (-60, 620), (-200, 700), (-380, 680), (-520, 600), (-560, 460),
    (-480, 350), (-330, 330), (-260, 240), (-330, 120), (-480, 60), (-560, -60), (-520, -200),
    (-380, -280), (-220, -260), (-130, -360), (-40, -420), (0, -340), (0, -200),
])
T("oasis", [
    (0, 0), (0, 300), (0, 600), (20, 720), (110, 760), (200, 700), (260, 560), (380, 520),
    (480, 600), (580, 560), (600, 420), (520, 300), (540, 140), (620, 20), (600, -160),
    (480, -240), (340, -200), (250, -280), (120, -380), (30, -400), (0, -320), (0, -180),
])
T("sakura", [
    (0, 0), (0, 180), (0, 330), (40, 420), (140, 440), (200, 380), (180, 290), (240, 220),
    (330, 250), (380, 350), (470, 400), (560, 340), (560, 230), (480, 170), (500, 70),
    (580, 0), (560, -110), (440, -150), (330, -90), (250, -160), (180, -280), (90, -360), (20, -350), (0, -260), (0, -130),
])
T("tempo", [
    (0, 0), (0, 350), (0, 650), (40, 780), (160, 840), (300, 800), (360, 680), (360, 450),
    (380, 300), (360, 120), (360, -100), (330, -260), (220, -340), (100, -380), (20, -340), (0, -230), (0, -100),
])


def catmull_centripetal(pts, samples_per_seg=40):
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2, p3 = (np.array(pts[(i + k) % n]) for k in (-1, 0, 1, 2))
        def tj(ti, a, b):
            return ti + max(np.linalg.norm(b - a), 1e-4) ** 0.5
        t0 = 0.0; t1 = tj(t0, p0, p1); t2 = tj(t1, p1, p2); t3 = tj(t2, p2, p3)
        for s in range(samples_per_seg):
            t = t1 + (t2 - t1) * s / samples_per_seg
            a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1
            a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
            a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
            b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2
            b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3
            out.append((t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2)
    return np.array(out)


def resample(dense, ds=2.0):
    closed = np.vstack([dense, dense[:1]])
    seg = np.linalg.norm(np.diff(closed, axis=0), axis=1)
    cum = np.concatenate([[0], np.cumsum(seg)])
    L = cum[-1]
    n = int(round(L / ds))
    d = np.arange(n) * (L / n)
    x = np.interp(d, cum, closed[:, 0]); z = np.interp(d, cum, closed[:, 1])
    return np.stack([x, z], 1), L


def analyse(name, pts, W=14.0, ax=None):
    dense = catmull_centripetal(pts)
    P, L = resample(dense)
    n = len(P)
    # curvature radius using +-5 samples (10 m)
    k = 5
    R = np.full(n, 1e9)
    sgn = np.zeros(n)
    for i in range(n):
        a, b, c = P[(i - k) % n], P[i], P[(i + k) % n]
        ab, bc, ca = np.linalg.norm(b - a), np.linalg.norm(c - b), np.linalg.norm(a - c)
        cross = (b - a)[0] * (c - b)[1] - (b - a)[1] * (c - b)[0]
        area2 = abs(cross)
        if area2 > 1e-6:
            R[i] = ab * bc * ca / (2 * area2)
        sgn[i] = np.sign(cross)
    # clearance between non-neighbouring sections
    step = 4
    Q = P[::step]
    m = len(Q)
    minclear = 1e9; where = None
    for i in range(m):
        d = np.linalg.norm(Q - Q[i], axis=1)
        idx = np.arange(m)
        along = np.minimum(np.abs(idx - i), m - np.abs(idx - i)) * step * (L / n)
        mask = along > 220
        if mask.any():
            j = np.argmin(np.where(mask, d, 1e9))
            if d[j] < minclear:
                minclear = d[j]; where = (i * step, j * step)
    # straight length around index 0 (radius > 600 m)
    def straight_extent(direction):
        s = 0
        i = 0
        while R[i % n] > 400 and s < 2000:
            i += direction; s += L / n
        return s
    fwd, back = straight_extent(1), straight_extent(-1)
    # turn direction: signed area (>0 CCW)
    area = 0.5 * np.sum(P[:, 0] * np.roll(P[:, 1], -1) - np.roll(P[:, 0], -1) * P[:, 1])
    print(f"{name:8s} L={L:7.0f} m  minR={R.min():6.1f} m  clearance={minclear:6.1f} m at {where}  "
          f"straight(+{fwd:.0f}/-{back:.0f})  {'CCW' if area > 0 else 'CW'}  corners<60m={int((R < 60).sum() * L / n)} m")
    if ax is not None:
        ax.plot(P[:, 0], P[:, 1], 'k-', lw=1)
        tight = R < 40
        ax.plot(P[tight, 0], P[tight, 1], 'r.', ms=2)
        cp = np.array(pts)
        ax.plot(cp[:, 0], cp[:, 1], 'b.', ms=4)
        for i, (x, z) in enumerate(pts):
            ax.annotate(str(i), (x, z), fontsize=6, color='blue')
        ax.plot(P[0, 0], P[0, 1], 'go', ms=8)
        t = P[3] - P[0]; t /= np.linalg.norm(t); r = np.array([t[1], -t[0]])
        ax.arrow(P[0, 0], P[0, 1], t[0] * 60, t[1] * 60, head_width=15, color='g')
        ax.plot([P[0, 0] + r[0] * 17], [P[0, 1] + r[1] * 17], 'mx')
        ax.set_aspect('equal'); ax.set_title(f"{name} {L:.0f}m minR {R.min():.0f}")
        if where:
            a, b = P[where[0]], P[where[1]]
            ax.plot([a[0], b[0]], [a[1], b[1]], 'm--', lw=0.8)
    return L, R.min(), minclear


if __name__ == "__main__":
    fig, axes = plt.subplots(2, 3, figsize=(18, 12))
    for ax, (name, pts) in zip(axes.flat, TRACKS.items()):
        analyse(name, pts, ax=ax)
    plt.tight_layout(); plt.savefig(sys.argv[1] if len(sys.argv) > 1 else "tracks.png", dpi=70)
