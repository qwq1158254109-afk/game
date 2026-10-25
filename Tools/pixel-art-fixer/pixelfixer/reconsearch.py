"""Grid detection by reconstruction search ("distillability").

The native resolution is the smallest grid whose box-downscale -> nearest-
upscale reconstruction still explains the image.  Key identity: the L2 error
of that round trip equals the total *within-cell variance* under the
(step, phase) grid, computable from cumulative sums of I and I^2 without
ever resizing:

    E(s, p) = sum(I^2) - sum_cells (sum_cell I)^2 / n_cell

Search runs per axis (cells = 1 row x band along the axis), so E_x measures
only horizontal information destruction.  The image is k-means quantized
first so AA/mush ramps snap to flat colors (otherwise their residual
variance swamps the curve).

Signals:
  * phase contrast  c(s) = (E(s, anti-phase) - E(s, best-phase)) / (sum):
    ~0 below the true step (any phase subdivides cells), peaked where the
    grid locks on.  Contrast also peaks at DIVISORS of the true step;
  * reconstruction error E(s): flat-ish below the true step, jumps above.

Final answer: error-gated harmonic promotion over refined candidates -
the largest step whose aligned reconstruction error stays near the small-
step floor and whose contrast remains significant.

Alignment matters: a 1% step error de-phases the grid across a wide image
and erases both signals.  Hence (a) tiles (row-blocks x column-segments)
each pick their own phase (absorbs sprite-sheet phase shifts + slow drift),
and (b) coarse peaks are refined on a dense local (s, phase) grid.
"""
import os
import sys

import numpy as np
import cv2

DEBUG = os.environ.get("RECON_DEBUG", "") != ""

S_MIN = 1.6
S_MAX = 24.0
S_RATIO = 1.025          # coarse geometric s grid spacing
MAX_ROWS = 360           # row subsampling cap
N_ROWBLOCKS = 5          # per-tile phase freedom (rows)
N_COLSEGS = 8            # max column segments (peak-locating tier only)
KMEANS_K = 14
C_FLOOR = 0.012          # contrast below this = "no grid signal"


# --------------------------------------------------------------- preprocess

def _quantize(rgb, k=KMEANS_K, sample=48000, seed=0):
    """k-means quantize premultiplied RGB; return centroid-color image."""
    h, w, _ = rgb.shape
    flat = rgb.reshape(-1, 3)
    n = flat.shape[0]
    rng = np.random.default_rng(seed)
    cv2.setRNGSeed(12345 + seed)   # cv2.kmeans uses OpenCV's global RNG;
    # without this, results depend on how many images ran earlier
    idx = rng.choice(n, min(sample, n), replace=False)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 25, 0.25)
    k = min(k, max(2, len(np.unique(np.round(flat[idx][::7] / 8), axis=0))))
    _, _, centers = cv2.kmeans(flat[idx].astype(np.float32), k, None, crit, 3,
                               cv2.KMEANS_PP_CENTERS)
    out = np.empty((n,), np.int32)
    c2 = (centers ** 2).sum(1)
    for i in range(0, n, 262144):
        blk = flat[i:i + 262144]
        out[i:i + 262144] = np.argmax(2.0 * (blk @ centers.T) - c2[None, :], 1)
    return centers[out].reshape(h, w, 3)


def _pca_channels(img3, alpha, n_comp=2):
    """Project HxWx3 onto its top PCA axes; append alpha if it varies."""
    h, w, _ = img3.shape
    flat = img3.reshape(-1, 3)
    mu = flat.mean(0)
    x = flat - mu
    cov = (x.T @ x) / max(x.shape[0], 1)
    evals, evecs = np.linalg.eigh(cov)
    order = np.argsort(evals)[::-1][:n_comp]
    comps = (x @ evecs[:, order]).reshape(h, w, -1).astype(np.float32)
    if alpha is not None and alpha.std() > 2.0:
        a = (alpha - alpha.mean())[..., None].astype(np.float32)
        comps = np.concatenate([comps, a], 2)
    return comps


def _prep(rgba):
    a = rgba[..., 3].astype(np.float32)
    rgb = rgba[..., :3].astype(np.float32) * (a[..., None] / 255.0)
    q = _quantize(rgb)
    return _pca_channels(q, a)


# ------------------------------------------------- per-axis energy machinery

class AxisData:
    """Cumsum tables for one axis of a channel stack (rows subsampled).

    axis=0: analyze along the width (x); axis=1: transpose first (y).
    """

    def __init__(self, ch, axis):
        img = ch if axis == 0 else ch.transpose(1, 0, 2)
        H, W, C = img.shape
        if H > MAX_ROWS:
            rows = np.linspace(0, H - 1, MAX_ROWS).astype(int)
            img = img[rows]
            H = MAX_ROWS
        self.H, self.W, self.C = H, W, C
        # channels are zero-mean (PCA), so float32 cumsums stay accurate
        self.S = np.zeros((H, W + 1, C), np.float32)
        np.cumsum(img, 1, out=self.S[:, 1:])
        self.Q = np.zeros((H, W + 1, C), np.float32)
        np.cumsum(img * img, 1, out=self.Q[:, 1:])
        # row blocks: phase freedom PERPENDICULAR to the analysis axis
        # (handles sprite rows at different offsets) is always safe.
        # Phase freedom ALONG the axis (column segments) absorbs step error
        # and biases the error minimum, so it is used only to LOCATE peaks
        # on wide images (where genuine drift prevents a global lock) and
        # is followed by phase-drift regression to de-bias the step.
        self.nbr = max(1, min(N_ROWBLOCKS, H // 48))
        self.r_edges = np.linspace(0, H, self.nbr + 1).astype(int)
        self.max_nbc = max(1, min(N_COLSEGS, W // 96))
        self.seg = {}
        for nbc in range(1, self.max_nbc + 1):
            c_edges = np.linspace(0, W, nbc + 1).astype(int)
            qcol = np.diff(self.Q[:, c_edges, :].astype(np.float64),
                           axis=1).sum(2)
            q_t = np.add.reduceat(qcol, self.r_edges[:-1], axis=0)
            self.seg[nbc] = (c_edges, q_t)
        # normalizer: total variance about per-rowblock means
        c_edges, q_t = self.seg[1]
        scol = np.diff(self.S[:, c_edges, :].astype(np.float64), axis=1)
        en1 = ((scol ** 2).sum(2) / float(W))
        t_t = q_t - np.add.reduceat(en1, self.r_edges[:-1], axis=0)
        self.t_sum = float(np.maximum(t_t, 1e-9).sum())

    def nbc_for(self, s, min_cells=15.0):
        """Segments hold >= min_cells cells so phase freedom can't overfit."""
        return int(np.clip(self.W // (min_cells * s), 1, self.max_nbc))

    def energy_tiles_multi(self, s, phases, nbc):
        """Per-tile cell energies for several phases: (P, nbr, nbc).

        Cell boundaries are FRACTIONAL: band sums come from linearly
        interpolated cumsums (an exact fractional box-downscale under a
        piecewise-constant image model).  Integer-rounded boundaries would
        systematically mismatch the unknown rasterization of the original
        upscale and shift the error minimum away from the true step.
        """
        W = self.W
        c_edges = self.seg[nbc][0]
        J = int(np.ceil(W / s)) + 2
        k = np.arange(-1, J)
        B = np.asarray(phases, np.float64)[:, None] + s * k[None, :]
        B = np.clip(B, 0.0, float(W))                      # P x (J+1)
        P = B.shape[0]
        wds = np.diff(B, axis=1).astype(np.float32)        # P x J
        wds_safe = np.maximum(wds, 1e-3)
        i0 = np.clip(np.floor(B), 0, W - 1).astype(np.int64)
        f = (B - i0).astype(np.float32).reshape(-1)
        g0 = self.S[:, i0.reshape(-1), :]                  # H x P(J+1) x C
        g1 = self.S[:, i0.reshape(-1) + 1, :]
        g = g0 + (g1 - g0) * f[None, :, None]
        g = g.reshape(self.H, P, J + 1, self.C)
        bs = np.diff(g, axis=2)                            # H x P x J x C
        en = (bs * bs).sum(3) / wds_safe[None]             # H x P x J
        if nbc == 1:
            en_row = en.sum(2)                             # H x P
            out = np.add.reduceat(en_row.astype(np.float64),
                                  self.r_edges[:-1], axis=0).T
            return out[:, :, None]                         # P x nbr x 1
        out = np.empty((P, self.nbr, nbc))
        for i in range(P):
            centers = 0.5 * (B[i, 1:] + B[i, :-1])
            splits = np.searchsorted(centers, c_edges[1:-1])
            idx = np.concatenate([[0], splits])
            en_seg = np.add.reduceat(en[:, i, :], idx, axis=1)
            out[i] = np.add.reduceat(en_seg.astype(np.float64),
                                     self.r_edges[:-1], axis=0)
        return out

    def _n_phase(self, s, dense=False):
        """Phase samples ~0.5px apart (0.25px when dense), even count."""
        n = int(np.clip((4.0 if dense else 2.0) * s, 8, 40))
        return n & ~1

    def eval_s(self, s, dense=False, nbc=1):
        """(e_best, e_anti) at step s; per-(rowblock x segment) best phase.

        e_anti evaluates each tile at the anti-phase (best + s/2), the
        maximally-wrong phase.  Both normalized by total variance.
        """
        n = self._n_phase(s, dense)
        phases = np.arange(n) * (s / n)
        q_t = self.seg[nbc][1]
        ens = self.energy_tiles_multi(s, phases, nbc)      # n x nbr x nbc
        ib = ens.argmax(0)
        best = np.take_along_axis(ens, ib[None], 0)[0]
        ref = np.take_along_axis(ens, ((ib + n // 2) % n)[None], 0)[0]
        err_b = np.maximum(q_t - best, 0.0)
        err_r = np.maximum(q_t - ref, 0.0)
        return err_b.sum() / self.t_sum, err_r.sum() / self.t_sum

    def phase_regress(self, s0, n_iter=2):
        """De-bias a step estimate from per-segment phase drift.

        If the step is off by d, each segment's best phase advances
        linearly with position at slope d/s (clock recovery).  Fit the
        slope over unwrapped per-segment phases and correct s.
        """
        s = float(s0)
        for _ in range(n_iter):
            nbc = self.nbc_for(s)
            if nbc < 3:
                return s
            n = int(np.clip(4.0 * s, 16, 48)) & ~1
            phases = np.arange(n) * (s / n)
            ens = self.energy_tiles_multi(s, phases, nbc)  # n x nbr x nbc
            seg = ens.sum(1)                               # n x nbc
            ip = seg.argmax(0)
            phi = phases[ip]
            w = seg.max(0) - seg.mean(0)                   # segment contrast
            w = np.maximum(w, 1e-12)
            # unwrap (mod s) across segments
            un = np.empty(nbc)
            un[0] = phi[0]
            for j in range(1, nbc):
                d = (phi[j] - phi[j - 1] + 0.5 * s) % s - 0.5 * s
                un[j] = un[j - 1] + d
            c_edges = self.seg[nbc][0]
            xs = 0.5 * (c_edges[1:] + c_edges[:-1]).astype(np.float64)
            xm = np.average(xs, weights=w)
            pm = np.average(un, weights=w)
            denom = np.average((xs - xm) ** 2, weights=w)
            if denom <= 0:
                return s
            slope = np.average((xs - xm) * (un - pm), weights=w) / denom
            slope = float(np.clip(slope, -0.02, 0.02))
            s = s * (1.0 + slope)
        return s

    def best_global_phase(self, s):
        n = self._n_phase(s, dense=True)
        phases = np.arange(n) * (s / n)
        ens = self.energy_tiles_multi(s, phases, 1).sum((1, 2))
        return float(phases[int(np.argmax(ens))])


# ------------------------------------------------------------ search logic

def _s_grid(w):
    smax = min(S_MAX, w / 8.0)
    n = int(np.ceil(np.log(smax / S_MIN) / np.log(S_RATIO)))
    return S_MIN * (S_RATIO ** np.arange(n + 1))


def _coarse_curves(ad, s_list):
    eb = np.empty(len(s_list))
    er = np.empty(len(s_list))
    for i, s in enumerate(s_list):
        eb[i], er[i] = ad.eval_s(s)
    return eb, er


def _trend_fn(s_list, eb):
    """Running median of coarse error vs s = misaligned-baseline error."""
    ls = np.log(s_list)
    trend = np.empty_like(eb)
    for i in range(len(s_list)):
        m = np.abs(ls - ls[i]) <= 0.14
        trend[i] = np.median(eb[m])
    trend = np.maximum(trend, 1e-6)

    def fn(s):
        return float(np.interp(np.log(s), ls, trend))
    return fn


def _score(eb, er, trend_e):
    """Distillability score: anti-phase penalty x alignment advantage.

    (er - eb) is how much reconstruction degrades when the grid phase is
    maximally wrong (zero below the true step, large at it).  The trend
    factor measures how far the aligned error drops below the misaligned
    baseline; multiples of the true step have eb ~ trend and die here.
    """
    return max(er - eb, 0.0) * max(1.0 - eb / trend_e, 0.0)


def _peaks(s_list, score, k=6):
    cand = []
    n = len(s_list)
    for i in range(n):
        if score[i] >= score[max(0, i - 1)] and score[i] >= score[min(n - 1, i + 1)]:
            cand.append((score[i], s_list[i]))
    cand.sort(reverse=True)
    return [s for _, s in cand[:k]]


def _refine(ad, s0, smax, trend, span=0.035, seg=False):
    """Dense local search maximizing the score (locks grid alignment)."""
    best = (-1.0, s0, 1.0, 1.0)
    for rnd, (sp, npts) in enumerate([(span, 9), (0.008, 7)]):
        center = best[1] if rnd else s0
        for s in center * np.linspace(1 - sp, 1 + sp, npts):
            if s < 1.2 or s > smax * 1.02:
                continue
            nbc = ad.nbc_for(s) if seg else 1
            eb, er = ad.eval_s(s, dense=(rnd == 1), nbc=nbc)
            sc = _score(eb, er, trend(s))
            if sc > best[0]:
                best = (sc, s, eb, er)
    return best  # score, s, eb, er


def _build_table(ad, size):
    """Coarse scan + refined candidates incl. harmonics of the leaders."""
    s_list = _s_grid(size)
    eb, er = _coarse_curves(ad, s_list)
    trend = _trend_fn(s_list, eb)
    score = np.array([_score(b, r, trend(s))
                      for s, b, r in zip(s_list, eb, er)])
    smax = s_list[-1]
    table = {}

    def add(s0, span=0.035):
        for key in table:
            if abs(key - s0) / s0 < 0.03:
                return table[key]
        sc, s, b, r = _refine(ad, s0, smax, trend, span)
        for key in list(table):
            if abs(key - s) / s < 0.02:
                if sc > table[key][0]:
                    table[key] = (sc, s, b, r)
                return table[key]
        table[round(s, 2)] = (sc, s, b, r)
        return table[round(s, 2)]

    pk = _peaks(s_list, score, 5)
    sc_gate = 0.2 * max(score.max(), 1e-9)
    for i, s0 in enumerate(pk):
        if i == 0 or float(np.interp(s0, s_list, score)) >= sc_gate:
            add(s0)
    leaders = sorted(table.values(), key=lambda t: -t[0])[:2]
    for li, (sc0, s0, b0, r0) in enumerate(leaders):
        for m in ((2.0, 0.5, 3.0) if li == 0 else (2.0, 0.5)):
            st = s0 * m
            if S_MIN * 0.9 <= st <= smax:
                add(st, span=0.05)
    return table, trend


def _align(ad, s, smax):
    """Global-phase micro-refine: minimize aligned error, tight window."""
    best = (np.inf, s)
    for s2 in s * np.linspace(0.992, 1.008, 9):
        if s2 < 1.2 or s2 > smax * 1.02:
            continue
        eb, _ = ad.eval_s(s2, dense=True)
        if eb < best[0]:
            best = (eb, s2)
    return best[1], best[0]


def _detect_axis(ad, size):
    table, trend = _build_table(ad, size)
    items = sorted(table.values(), key=lambda t: -t[0])
    sc_ax, s_ax, eb_ax, er_ax = items[0]
    # candidate final steps: table winner, drift-regressed, micro-locked;
    # decided by global-phase reconstruction error
    s_reg = ad.phase_regress(s_ax)
    finals = []
    for s0 in {round(s_ax, 4), round(s_reg, 4)}:
        s_gl, e_gl = _align(ad, s0, size / 8.0)
        finals.append((e_gl, s_gl))
    e_ax2, s_fin = min(finals)
    if DEBUG:
        print(f"    debias: table={s_ax:.4f} regress={s_reg:.4f} "
              f"-> final={s_fin:.4f} (e={e_ax2:.4f})")
    s_ax = s_fin
    if DEBUG:
        print(f"    axis size={size}: chose s={s_ax:.3f} score={sc_ax:.4f}")
        for sc, s, b, r in sorted(table.values(), key=lambda t: t[1]):
            print(f"      cand s={s:7.3f} score={sc:.4f} eb={b:.4f} "
                  f"er={r:.4f} trend={trend(s):.4f}")
    cands = [(float(s), float(sc)) for sc, s, b, r in items][:8]
    return s_ax, sc_ax, eb_ax, cands, trend


# ---------------------------------------------------------------- detect

def detect(rgba):
    ch = _prep(rgba)
    H, W = rgba.shape[:2]
    adx = AxisData(ch, 0)
    ady = AxisData(ch, 1)
    sx, cx, ex, candx, trend_x = _detect_axis(adx, W)
    sy, cy, ey, candy, trend_y = _detect_axis(ady, H)

    # cross-axis rescue: a weak axis borrows the strong axis' step and
    # re-fits it locally (segment-phase freedom + drift regression); the
    # replacement must beat the axis' own step under the GLOBAL score,
    # which protects genuinely non-square grids from false adoption.
    def _gscore(ad, s, trend):
        eb, er = ad.eval_s(s, dense=True)
        return _score(eb, er, trend(s))

    def _rescue(ad, s_strong, smax, trend):
        _, s2, _, _ = _refine(ad, s_strong, smax, trend, span=0.05, seg=True)
        return ad.phase_regress(s2)

    smax_x = min(S_MAX, W / 8.0)
    smax_y = min(S_MAX, H / 8.0)
    if cx < 0.01 and cy > 2 * cx and abs(sx - sy) / max(sx, sy) > 0.04:
        s2 = _rescue(adx, sy, smax_x, trend_x)
        if _gscore(adx, s2, trend_x) > _gscore(adx, sx, trend_x):
            if DEBUG:
                print(f"    rescue x: {sx:.3f} -> {s2:.3f}")
            sx = s2
    elif cy < 0.01 and cx > 2 * cy and abs(sy - sx) / max(sx, sy) > 0.04:
        s2 = _rescue(ady, sx, smax_y, trend_y)
        if _gscore(ady, s2, trend_y) > _gscore(ady, sy, trend_y):
            if DEBUG:
                print(f"    rescue y: {sy:.3f} -> {s2:.3f}")
            sy = s2

    return {
        "step_x": float(sx), "step_y": float(sy),
        "cols": int(round(W / sx)), "rows": int(round(H / sy)),
        "phase_x": adx.best_global_phase(sx),
        "phase_y": ady.best_global_phase(sy),
        "conf_x": float(cx), "conf_y": float(cy),
        "candidates": sorted(candx + candy, key=lambda t: -t[1])[:8],
    }
