"""Grid detection from boundary-run statistics + robust soft-GCD lattice fit.

Idea: pixel art (even mushy/warped) is made of RUNS. Along each scanline the
distances between consecutive color-change boundaries are (approximately)
integer multiples of the cell size s. Collect multi-lag boundary distances
across all scanlines into a sub-pixel histogram and score candidate steps s
by a comb function  S(s) = mean_r cos(2*pi*r/s):  every distance that is a
multiple of s contributes +1, off-lattice distances cancel out. Completely
phase-free, so per-region phase shifts (sprite sheets) and jitter/warp are
tolerated.

Pipeline:
  1. median-filter, gradient along the scan axis, box-smooth PERPENDICULAR
     to it (real cell boundaries persist across >= a cell of scanlines;
     noise edges don't), non-max suppression, parabolic sub-pixel peaks.
  2. multi-lag distance pooling (lag 1..4): a spurious boundary splits a run
     into off-lattice halves at lag 1, but lag 2 jumps across it and lands
     back on the lattice -- robust to over-segmentation; missing boundaries
     just produce higher multiples of s, which the comb also rewards.
  3. comb scoring over s in [2.05, 26); divisor aliases (s/2, s/3 divide
     every multiple of s too) resolved by noise asymmetry (residual eps
     costs phase 2*pi*eps/s -- smaller s punished harder) plus an explicit
     "largest near-tied peak with fundamental support" rule.
  4. sub-pixel refinement: fine comb search + k-weighted least squares
     (the k=1 run mode is the most bias-prone under mush; long baselines
     average boundary noise out).
  5. LOCAL-STEP INTEGRATION: AI-generated grids drift in scale, so the
     dominant local pitch != W/cols. Re-estimate the step in overlapping
     2D tiles (fine comb near the global step) and integrate
     cols = W * mean(1/s_tile).  This is what rescues drifting "AI soup".
"""
import numpy as np
import cv2


S_MIN, S_MAX = 2.05, 26.0
RUN_MIN, RUN_MAX = 2.0, 64.0
BIN = 0.25       # run-length histogram bin width (px), selection stage
COHERENCE = 7    # perpendicular box-smooth of the gradient before NMS
THR_FRAC = 0.10  # edge threshold as a fraction of the p95 gradient
MAX_LAG = 4      # boundary-distance pooling depth
TILINGS = ((3, 3), (5, 5), (1, 8))  # (perp, scan) tile grids to pool


# ---------------------------------------------------------------- boundaries

def _prep(rgba):
    """Median-filtered float image, alpha folded in as an extra channel."""
    rgb = cv2.medianBlur(rgba[:, :, :3], 3).astype(np.float32)
    a = cv2.medianBlur(rgba[:, :, 3], 3).astype(np.float32)
    return np.dstack([rgb, a])


def _boundaries(img4, axis):
    """Sub-pixel color-boundary positions along `axis`.

    axis=1 -> boundaries along x within each row (for step_x).
    axis=0 -> boundaries along y within each column (for step_y).
    Returns (scanline_index, position) arrays, scanline-major order.
    """
    if axis == 0:
        img4 = np.transpose(img4, (1, 0, 2))
    # L1 color+alpha difference between neighbors along the scan axis
    d = np.abs(np.diff(img4, axis=1)).sum(axis=2)  # (H, W-1)
    if d.size == 0:
        return np.empty(0, np.int64), np.empty(0, np.float64)
    # coherence: true cell boundaries persist across the perpendicular axis
    # for at least a cell of scanlines; incoherent noise edges do not.
    d = cv2.boxFilter(d, -1, (1, COHERENCE), borderType=cv2.BORDER_REPLICATE)
    p95 = np.percentile(d[d > 0], 95) if (d > 0).any() else 0.0
    thr = max(20.0, THR_FRAC * p95)
    left = np.empty_like(d); left[:, 0] = 0; left[:, 1:] = d[:, :-1]
    right = np.empty_like(d); right[:, -1] = 0; right[:, :-1] = d[:, 1:]
    mask = (d > thr) & (d > left) & (d >= right)
    ys, xs = np.nonzero(mask)
    if xs.size < 4:
        return np.empty(0, np.int64), np.empty(0, np.float64)
    # parabolic sub-pixel refinement of the gradient peak
    dl = d[ys, np.maximum(xs - 1, 0)]
    dr = d[ys, np.minimum(xs + 1, d.shape[1] - 1)]
    dc = d[ys, xs]
    denom = dl - 2 * dc + dr
    safe = np.where(np.abs(denom) > 1e-6, denom, 1.0)
    off = np.where(np.abs(denom) > 1e-6, 0.5 * (dl - dr) / safe, 0.0)
    return ys, xs + np.clip(off, -0.5, 0.5)


def _lag_diffs(ys, pos, max_lag=MAX_LAG):
    """Pooled pos[i+lag]-pos[i], lag=1..max_lag, within each scanline."""
    out = []
    for lag in range(1, max_lag + 1):
        if pos.size <= lag:
            break
        d_ = pos[lag:] - pos[:-lag]
        d_ = d_[ys[lag:] == ys[:-lag]]
        out.append(d_[(d_ >= RUN_MIN) & (d_ <= RUN_MAX)])
    if not out:
        return np.empty(0, np.float32)
    return np.concatenate(out).astype(np.float32)


# ---------------------------------------------------------------- soft GCD

def _hist(runs, bin_w):
    nb = int(RUN_MAX / bin_w) + 1
    hist, edges = np.histogram(runs, bins=nb, range=(0, RUN_MAX))
    centers = 0.5 * (edges[:-1] + edges[1:])
    keep = hist > 0
    return hist[keep].astype(np.float64), centers[keep]


def _comb_score(runs, s_grid, bin_w=BIN, weights=None):
    """S(s) = weighted mean over distances of cos(2*pi*r/s)."""
    if runs.size == 0:
        return np.zeros_like(s_grid), 0.0
    hist, centers = _hist(runs, bin_w)
    total = hist.sum()
    w = hist * weights(centers) if weights else hist
    S = (w[:, None] * np.cos(2 * np.pi * centers[:, None] / s_grid[None, :])
         ).sum(0) / w.sum()
    return S, total


def _pick_step(runs, s_grid):
    """Best step from the comb score, with largest-near-tie divisor logic."""
    S, total = _comb_score(runs, s_grid)
    if total < 50:
        return None, 0.0, []
    loc = np.zeros(S.shape, bool)
    loc[1:-1] = (S[1:-1] > S[:-2]) & (S[1:-1] >= S[2:])
    idx = np.nonzero(loc)[0]
    if idx.size == 0:
        return None, 0.0, []
    order = idx[np.argsort(S[idx])[::-1]]
    smax = S[order[0]]
    if smax <= 0:
        return None, 0.0, []
    cands = [(float(s_grid[i]), float(S[i])) for i in order[:12]]

    def fund(s):  # mass of distances near 1*s (fundamental support)
        m = np.abs(runs - s) < max(0.6, 0.18 * s)
        return m.sum() / total

    # among near-tied peaks prefer the LARGEST s with fundamental support --
    # kills the s/2, s/3 divisor aliases on clean lattices. The ratio can be
    # generous because larger FALSE steps are anti-phase for odd multiples
    # of the true step (cos(pi*odd) = -1) and score far below the true peak.
    tied = [(float(s_grid[i]), float(S[i])) for i in order
            if S[i] >= 0.70 * smax]
    tied.sort(key=lambda t: -t[0])
    best_s, best_v = float(s_grid[order[0]]), float(smax)
    for s, v in tied:
        if fund(s) >= 0.04:
            best_s, best_v = s, v
            break
    return best_s, best_v, cands


def _refine(runs, s):
    """Sub-pixel refinement: fine comb (k-weighted) + k-weighted LS polish."""
    if runs.size == 0:
        return float(s)
    hist, centers = _hist(runs, 0.05)
    fine = np.arange(0.94 * s, 1.06 * s, 0.002)
    wk = centers / s  # weight by multiple k: favors long baselines
    Sf = ((hist * wk)[:, None] *
          np.cos(2 * np.pi * centers[:, None] / fine[None, :])).sum(0)
    s = float(fine[np.argmax(Sf)])
    for _ in range(2):
        k = np.round(runs / s)
        ok = k >= 1
        res = np.abs(runs - k * s)
        w = np.clip(1.0 - res / (0.30 * s), 0, 1) * ok * k
        den = (w * k * k).sum()
        if den <= 0:
            break
        s = float((w * k * runs).sum() / den)
    return s


# ------------------------------------------------------ local-step integration

def _tile_peak(diffs, s0):
    """Fine comb peak near s0 for one tile; None if unreliable."""
    if diffs.size < 350:
        return None
    fine = np.arange(0.87 * s0, 1.13 * s0, 0.005)
    hist, centers = _hist(diffs, 0.05)
    S = (hist[:, None] * np.cos(2 * np.pi * centers[:, None] / fine[None, :])
         ).sum(0) / hist.sum()
    pk = int(np.argmax(S))
    if pk == 0 or pk == len(fine) - 1 or S[pk] < 0.12:
        return None  # peak at window edge or too weak -> distrust
    return float(fine[pk])


def _integrate_step(ys, pos, n_perp, n_scan, s0):
    """cols = W * mean(1/s_local): pooled over several tile grids.

    AI pseudo-grids drift in scale; the global comb finds the DOMINANT local
    pitch, which can differ from W/cols by several percent. Estimating the
    step per tile and averaging 1/s recovers the global cell count.
    """
    inv = []
    for tp, tsc in TILINGS:
        ye = np.linspace(0, n_perp, tp + 1)
        xe = np.linspace(0, n_scan, tsc + 1)
        for i in range(tp):
            rows_m = (ys >= ye[i]) & (ys < ye[i + 1])
            for j in range(tsc):
                m = rows_m & (pos >= xe[j]) & (pos < xe[j + 1])
                s_i = _tile_peak(_lag_diffs(ys[m], pos[m]), s0)
                if s_i is not None:
                    inv.append(1.0 / s_i)
    if not inv:
        return s0
    return 1.0 / float(np.mean(inv))


# ---------------------------------------------------------------- detect

def detect(rgba):
    h, w = rgba.shape[:2]
    img4 = _prep(rgba)
    s_grid = np.arange(S_MIN, S_MAX, 0.01)

    axes = {}
    for axis, name in ((1, "x"), (0, "y")):
        ys, pos = _boundaries(img4, axis)
        runs = _lag_diffs(ys, pos)
        s, v, cands = _pick_step(runs, s_grid)
        if s is not None:
            s = _refine(runs, s)
        axes[name] = dict(s=s, v=v, cands=cands, nruns=runs.size, runs=runs,
                          ys=ys, pos=pos)

    sx, sy = axes["x"]["s"], axes["y"]["s"]
    vx, vy = axes["x"]["v"], axes["y"]["v"]
    if sx is None and sy is None:
        return dict(step_x=8.0, step_y=8.0, cols=round(w / 8), rows=round(h / 8),
                    phase_x=0.0, phase_y=0.0, candidates=[])
    # cross-axis reconciliation: a weak axis borrows the strong axis's step
    if sx is None or (sy is not None and vx < 0.5 * vy and
                      abs(sx - sy) > 0.15 * sy):
        sx2 = _refine(axes["x"]["runs"], sy) if axes["x"]["runs"].size else sy
        if abs(sx2 - sy) < 0.15 * sy:
            sx = sx2
    if sy is None or (sx is not None and vy < 0.5 * vx and
                      abs(sy - sx) > 0.15 * sx):
        sy2 = _refine(axes["y"]["runs"], sx) if axes["y"]["runs"].size else sx
        if abs(sy2 - sx) < 0.15 * sx:
            sy = sy2
    if sx is None:
        sx = sy
    if sy is None:
        sy = sx

    # local-step integration (drift-aware effective step)
    sx = _integrate_step(axes["x"]["ys"], axes["x"]["pos"], h, w, sx)
    sy = _integrate_step(axes["y"]["ys"], axes["y"]["pos"], w, h, sy)

    # square-cell reconciliation: AI pseudo-pixels are near-square; when the
    # two axes land within ~8.5% of each other the residual disagreement is
    # mostly noise, so pool them (harmonic mean preserves cell counts).
    rel = abs(sx - sy) / (0.5 * (sx + sy))
    if rel < 0.085 or (rel < 0.15 and max(vx, vy) < 0.15):
        sx = sy = 2.0 / (1.0 / sx + 1.0 / sy)

    cands = sorted(axes["x"]["cands"] + axes["y"]["cands"],
                   key=lambda t: -t[1])
    return dict(step_x=float(sx), step_y=float(sy),
                cols=int(round(w / sx)), rows=int(round(h / sy)),
                phase_x=0.0, phase_y=0.0,
                score_x=float(vx), score_y=float(vy),
                nruns_x=int(axes["x"]["nruns"]), nruns_y=int(axes["y"]["nruns"]),
                candidates=cands)
