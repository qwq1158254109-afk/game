"""Shift self-similarity grid detection prototype.

Core signal: for a pixel grid with cell size s, the dissimilarity curve
    d(t) = mean |F(x) - F(x+t)|      (per axis)
has local minima at t = k*s (the grid re-aligns with itself) and maxima
near half offsets. d(t) is phase-free, so sprite sheets with per-region
phase shifts still give a clean curve.

Empirical findings driving the design (see FINDINGS.md):
- raw RGB d(t) is a featureless monotone trend; gradient-family features
  (|grad|, blurred |grad|, |Laplacian|) carry the periodicity: cell
  boundaries form a spike train with period s that re-aligns under shift
  k*s regardless of cell colors.
- Content periodicity (sprite spacing, star fields) produces the
  *strongest* minima; the pixel grid is the *finest* consistent period.
  Selection must therefore prefer the smallest period whose evidence is
  consistent across many harmonics, not the largest score.
- Score = t-statistic of the comb contrasts c_k = d(half offsets) - d(k p)
  over k: true fundamentals are consistent over many k (high t), content
  periods have few harmonics, noise is inconsistent.
- Mush/drift destroy the global curve; per-tile curves (normalized, then
  score-aggregated across tiles) recover local structure.
"""
import numpy as np
import cv2
from PIL import Image

TMAX = 72
PMIN, PMAX = 1.7, 24.0
PSTEP = 0.02
KCAP = 24
PIX_BUDGET = 1_200_000   # stride the non-shift axis above this many pixels
TILE_TARGET = 144        # approximate tile size in px
QUALIFY_FRAC = 0.40      # peak must reach this fraction of the max score
QUALIFY_ABS = 4.0        # ... and this absolute t-statistic
VOTE_WIN = 0.15          # tile-vote search window, fraction of p*
VOTE_MIN = 1.5           # minimum tile score to cast a vote
DISP_UNIFORM = 0.015     # vote dispersion below which grid is uniform


# ---------------------------------------------------------------- features

def _luma(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def _quant_labels(rgba, colors=16):
    im = Image.fromarray(rgba, "RGBA").convert("RGB")
    q = im.quantize(colors=colors, method=Image.MEDIANCUT,
                    dither=Image.Dither.NONE)
    return np.asarray(q, dtype=np.int16)


def _gradmag(y):
    gx = np.abs(np.diff(y, axis=1, prepend=y[:, :1]))
    gy = np.abs(np.diff(y, axis=0, prepend=y[:1]))
    return gx + gy


def build_features(rgba):
    rgb = rgba[..., :3].astype(np.float32)
    alpha = rgba[..., 3].astype(np.float32) / 255.0
    y = _luma(rgb)
    yb = cv2.GaussianBlur(y, (0, 0), 1.0)
    feats = {
        "grad0": (_gradmag(y)[..., None], False),
        "gradb": (_gradmag(yb)[..., None], False),
        "lapb": (np.abs(cv2.Laplacian(yb, cv2.CV_32F))[..., None], False),
        "quant": (_quant_labels(rgba)[..., None].astype(np.float32), True),
    }
    return feats, alpha, feats["grad0"][0][..., 0]


# ------------------------------------------------------------ tiled d(t)

def _dcurves_tiled(F, wt, axis, tmax, ny, nx):
    """Per-tile numerator/denominator curves.

    Returns (num[ny, nx, T], den[ny, nx, T]); d = num/den. Keeping both
    lets callers regroup tiles exactly (coarse tiles, global) for free.
    """
    arr, is_label = F
    if axis == 0:
        arr = np.swapaxes(arr, 0, 1)
        wt = wt.T
    h, w = arr.shape[:2]
    stride = max(1, int(np.ceil(h * w / PIX_BUDGET)))
    arr = arr[::stride]
    wt = wt[::stride]
    h = arr.shape[0]
    T = max(int(min(tmax, w // 3)), 1)
    rs = np.unique(np.linspace(0, h, ny + 1)[:-1].astype(int))
    cs0 = np.unique(np.linspace(0, w, nx + 1)[:-1].astype(int))
    ny, nx = len(rs), len(cs0)
    num = np.zeros((ny, nx, T))
    den = np.zeros((ny, nx, T))
    single = arr.shape[-1] == 1
    for t in range(1, T + 1):
        A, B = arr[:, t:], arr[:, :-t]
        wp = wt[:, t:] * wt[:, :-t]
        if is_label:
            diff = (A[..., 0] != B[..., 0]).astype(np.float32)
        elif single:
            d = A[..., 0] - B[..., 0]
            diff = np.abs(d, out=d)
        else:
            diff = np.abs(A - B).mean(-1)
        cs = np.minimum(cs0, max(w - t - 1, 0))
        if len(np.unique(cs)) != nx:  # degenerate; collapse columns
            cs = np.array([0])
        nu = np.add.reduceat(np.add.reduceat(diff * wp, rs, axis=0),
                             cs, axis=1)
        de = np.add.reduceat(np.add.reduceat(wp, rs, axis=0), cs, axis=1)
        if nu.shape != (ny, nx):
            num[:, :, t - 1] += nu.sum() / (ny * nx)
            den[:, :, t - 1] += de.sum() / (ny * nx)
        else:
            num[:, :, t - 1] = nu
            den[:, :, t - 1] = de
    return num, den


def _group(num, den, gy, gx):
    """Regroup tile num/den grids into a gy x gx grid of d(t) curves."""
    ny, nx, T = num.shape
    ys = np.linspace(0, ny, gy + 1).astype(int)
    xs = np.linspace(0, nx, gx + 1).astype(int)
    out = []
    for a in range(gy):
        for b in range(gx):
            n = num[ys[a]:ys[a + 1], xs[b]:xs[b + 1]].sum(axis=(0, 1))
            d = den[ys[a]:ys[a + 1], xs[b]:xs[b + 1]].sum(axis=(0, 1))
            out.append(n / np.maximum(d, 1e-9))
    return np.array(out)


# ---------------------------------------------------------------- scoring

def _comb_tstat(dm, p_grid, kcap=KCAP):
    """t-statistic comb score for one normalized curve dm(t), t=1..T.

    c_k = mean(halves) - d(k p), detrended by a box mean of width p; the
    score is mean(c)*sqrt(K) / (std(c) + noise floor): consistency over
    many harmonics wins over one deep content-period minimum.
    """
    T = len(dm)
    S = np.full(len(p_grid), -1e9)
    if T < 8:
        return S
    ts = np.arange(1, T + 1, dtype=np.float64)
    F = np.concatenate([[0.0], np.cumsum((dm[1:] + dm[:-1]) * 0.5)])
    noise = np.median(np.abs(np.diff(dm, 2))) + 1e-9

    def interp(q):
        return np.interp(q, ts, dm)

    def box(q, p):
        lo = np.maximum(q - p[..., None] / 2, 1.0)
        hi = np.minimum(q + p[..., None] / 2, float(T))
        return ((np.interp(hi, ts, F) - np.interp(lo, ts, F))
                / np.maximum(hi - lo, 1e-9))

    Ks = np.minimum(np.floor(T / p_grid - 0.5).astype(int), kcap)
    for K in np.unique(Ks):
        if K < 2:
            continue
        sel = np.where(Ks == K)[0]
        ps = p_grid[sel]
        ks = np.arange(1, K + 1, dtype=np.float64)
        qm = ps[:, None] * ks
        rm = interp(qm) - box(qm, ps)
        rh1 = interp(qm - ps[:, None] / 2) - box(qm - ps[:, None] / 2, ps)
        rh2 = interp(qm + ps[:, None] / 2) - box(qm + ps[:, None] / 2, ps)
        c = 0.5 * (rh1 + rh2) - rm
        mean_c = c.mean(axis=1)
        std_c = c.std(axis=1)
        # penalize few-harmonic (large p) scores: content periods live there
        kpen = min(1.0, (K - 1) / 3.0)
        S[sel] = kpen * mean_c * np.sqrt(K) / (std_c + 0.5 * noise + 1e-9)
    return S


def _local_maxima(z):
    idx = np.where((z[1:-1] >= z[:-2]) & (z[1:-1] > z[2:]))[0] + 1
    return idx[np.argsort(z[idx])[::-1]]


def _parabolic(x, y, i):
    if 0 < i < len(x) - 1:
        denom = y[i - 1] - 2 * y[i] + y[i + 1]
        if abs(denom) > 1e-12:
            off = float(np.clip(0.5 * (y[i - 1] - y[i + 1]) / denom, -1, 1))
            return x[i] + off * (x[1] - x[0])
    return x[i]


# ------------------------------------------------------- harmonic refine

def _refine_step(R, s0, kdecay=0.85):
    """Sub-pixel step: positions of minima of R(t) near k*s0, LS fit."""
    T = len(R)
    ts = np.arange(1, T + 1, dtype=np.float64)
    pairs = []
    K = int(min(KCAP, (T - 1) / s0))
    for k in range(1, K + 1):
        t0 = k * s0
        half = max(1.5, 0.3 * s0)
        lo = max(int(np.floor(t0 - half)), 1)
        hi = min(int(np.ceil(t0 + half)), T)
        if hi - lo < 2:
            continue
        seg = R[lo - 1:hi]
        j = int(np.argmin(seg))
        tk = _parabolic(ts[lo - 1:hi], seg, j)
        pairs.append([k, tk, kdecay ** (k - 1)])
    if not pairs:
        return s0
    pairs = np.array(pairs)
    for _ in range(2):
        ks, tk, w = pairs.T
        s = float((w * ks * tk).sum() / (w * ks * ks).sum())
        keep = np.abs(tk - ks * s) <= max(0.2 * s0, 1.0)
        if keep.all() or keep.sum() < 1:
            break
        pairs = pairs[keep]
    return s if 0.6 * s0 < s < 1.4 * s0 else s0


# ---------------------------------------------------------------- phase

def _phase(grad, step, axis):
    prof = grad.sum(axis=0) if axis == 1 else grad.sum(axis=1)
    n = len(prof)
    xs = np.arange(n, dtype=np.float64)
    best, bphi = -1.0, 0.0
    for phi in np.arange(0.0, step, 0.1):
        qs = np.arange(phi, n - 1, step)
        if len(qs) < 2:
            continue
        v = float(np.interp(qs, xs, prof).mean())
        if v > best:
            best, bphi = v, phi
    return bphi


# ---------------------------------------------------------------- detect

# quant ablated to 0: identical accuracy on the bench, ~10% faster without
# it (kept in the code for future JPEG-heavy sets; set > 0 to re-enable)
FEAT_WEIGHTS = {"grad0": 1.0, "gradb": 1.0, "lapb": 1.0, "quant": 0.0}


def _residual_curve(d):
    """Mean-normalized, box(9)-detrended residual for refinement."""
    if d.mean() < 1e-9:
        return np.zeros_like(d)
    dm = d / d.mean()
    k = np.ones(9) / 9.0
    pad = np.pad(dm, 4, mode="edge")
    return dm - np.convolve(pad, k, mode="valid")


def _score_curves(d_curves, p_grid, fw, kcap=KCAP):
    """Clipped comb t-stat score for each curve, scaled by feature weight."""
    out = np.zeros((len(d_curves), len(p_grid)))
    for i, d in enumerate(d_curves):
        m = d.mean()
        if m > 1e-9:
            out[i] = fw * np.clip(_comb_tstat(d / m, p_grid, kcap),
                                  -10.0, 30.0)
    return out


def _detect_axis(feats, wt, axis, size):
    p_grid = np.arange(PMIN, min(PMAX, size / 4), PSTEP)
    h, w = wt.shape
    other, shift = (h, w) if axis == 1 else (w, h)
    ny = int(np.clip(round(other / 128.0), 1, 8))       # non-shift axis
    nx = int(np.clip(round(shift / TILE_TARGET), 1, 6))  # shift axis
    # selection bands: full width along the shift axis (best SNR for
    # locating the right period region), a few bands across
    gy, gx = min(ny, 4), 1
    S_fine = S_coarse = S_glob = None
    w_fine = w_coarse = None
    agg_res = None
    for name, F in feats.items():
        if FEAT_WEIGHTS.get(name, 0.0) <= 0.0:
            continue
        num, den = _dcurves_tiled(F, wt, axis, TMAX, ny, nx)
        d_fine = _group(num, den, num.shape[0], num.shape[1])
        d_coarse = _group(num, den, gy, gx)
        d_glob = _group(num, den, 1, 1)
        fw = FEAT_WEIGHTS[name]
        if S_fine is None:
            S_fine = np.zeros((len(d_fine), len(p_grid)))
            S_coarse = np.zeros((len(d_coarse), len(p_grid)))
            S_glob = np.zeros(len(p_grid))
            agg_res = np.zeros(d_glob.shape[1])
        S_fine += _score_curves(d_fine, p_grid, fw, kcap=6)
        S_coarse += _score_curves(d_coarse, p_grid, fw)
        S_glob += _score_curves(d_glob, p_grid, fw)[0]
        agg_res += fw * _residual_curve(d_glob[0])
        if name == "grad0":
            w_fine = d_fine.mean(axis=1)
            w_coarse = d_coarse.mean(axis=1)
    if S_fine is None or w_coarse is None or w_coarse.sum() <= 1e-9:
        return 8.0, [(8.0, 0.0)], 0.0
    wc = w_coarse / w_coarse.sum()
    Stot = ((wc[:, None] * S_coarse).sum(axis=0) + 0.5 * S_glob) / 1.5
    peaks = _local_maxima(Stot)
    if len(peaks) == 0 or Stot[peaks[0]] <= 0:
        return 8.0, [(8.0, 0.0)], 0.0
    smax = Stot[peaks[0]]
    floor = max(QUALIFY_FRAC * smax, QUALIFY_ABS)
    qual = [i for i in peaks if Stot[i] >= floor]
    best_i = min(qual, key=lambda i: p_grid[i]) if qual else peaks[0]
    # explicit divisor walk (peak list can miss a shallow fundamental)
    changed = True
    while changed:
        changed = False
        for m in (6, 5, 4, 3, 2):
            pf = p_grid[best_i] / m
            if pf < p_grid[0]:
                continue
            j0 = int(round((pf - p_grid[0]) / PSTEP))
            rad = max(int(round(0.05 * pf / PSTEP)), 4)
            lo, hi = max(j0 - rad, 0), min(j0 + rad + 1, len(p_grid))
            if hi > lo:
                j = lo + int(np.argmax(Stot[lo:hi]))
                if Stot[j] >= max(0.45 * Stot[best_i], 0.8 * QUALIFY_ABS):
                    best_i = j
                    changed = True
                    break
    p_star = p_grid[best_i]

    # per-fine-tile vote around p_star (handles drift: local periods vary;
    # cols = sum(width_i / s_i), so 1/s averages linearly -> harmonic mean)
    def tile_votes(center):
        win = max(VOTE_WIN * center, 6 * PSTEP)
        lo = max(int(round((center - win - p_grid[0]) / PSTEP)), 0)
        hi = min(int(round((center + win - p_grid[0]) / PSTEP)) + 1,
                 len(p_grid))
        vs, ws = [], []
        for i in range(S_fine.shape[0]):
            if w_fine[i] <= 1e-9:
                continue
            seg = S_fine[i, lo:hi]
            if len(seg) < 3:
                continue
            j = int(np.argmax(seg))
            if seg[j] < VOTE_MIN:
                continue
            vs.append(_parabolic(p_grid[lo:hi], seg, j))
            ws.append(min(float(seg[j]), 8.0))
        return np.asarray(vs), np.asarray(ws)

    def wmedian(v, w):
        o = np.argsort(v)
        cw = np.cumsum(w[o])
        return float(v[o][np.searchsorted(cw, 0.5 * cw[-1])])

    votes, vw = tile_votes(p_star)
    if len(votes):
        med = wmedian(votes, vw)
        votes, vw = tile_votes(med)  # re-center once
    if len(votes):
        med = wmedian(votes, vw)
        disp = wmedian(np.abs(votes - med), vw) / max(med, 1e-9)
        if disp < DISP_UNIFORM:
            # uniform grid: median vote + sub-pixel harmonic LS
            s0 = med
            s_ls = _refine_step(agg_res, s0)
            if abs(s_ls - s0) <= max(0.03 * s0, 0.04):
                s0 = s_ls
        else:
            # drifting grid: harmonic mean of local periods
            s0 = float(vw.sum() / (vw / votes).sum())
    else:
        s0 = _parabolic(p_grid, Stot, best_i)
        s_ls = _refine_step(agg_res, s0)
        if abs(s_ls - s0) <= max(0.03 * s0, 0.04):
            s0 = s_ls
    cands = [(float(_parabolic(p_grid, Stot, i)), float(Stot[i]))
             for i in peaks[:8]]
    cands.insert(0, (float(s0), float(Stot[best_i])))
    return s0, cands, float(Stot[best_i])


def detect(rgba):
    h, w = rgba.shape[:2]
    feats, alpha, grad = build_features(rgba)
    wt = np.maximum(alpha, 0.05).astype(np.float32)
    sx, cand_x, confx = _detect_axis(feats, wt, 1, w)
    sy, cand_y, confy = _detect_axis(feats, wt, 0, h)
    cols = max(1, int(round(w / sx)))
    rows = max(1, int(round(h / sy)))
    px = _phase(grad, sx, 1)
    py = _phase(grad, sy, 0)
    cands = sorted(cand_x + cand_y, key=lambda c: -c[1])
    return dict(step_x=float(sx), step_y=float(sy), cols=cols, rows=rows,
                phase_x=float(px), phase_y=float(py),
                conf=float(min(confx, confy)), candidates=cands)
