"""Grid detection via banded autocorrelation + cepstrum of derivative profiles.

Family: translation-invariant period estimators (ACF / cepstrum).

Pipeline per axis:
1. Feature maps: |2nd difference| of gray (impulses at knots even for
   bilinear/bicubic ramps) and |1st difference| of median+quantized gray.
2. Project each map into row-band profiles (sum over `band` lines) - the
   lattice boundary positions are shared across lines inside a band, so
   projection amplifies them over per-cell random texture.
3. ACF of each band profile (FFT, zero-padded, unbiased), averaged over
   bands -> translation-invariant across bands, robust to per-region phase.
4. Comb-minus-anticomb score over a fine fractional step grid, plus an
   averaged-cepstrum vote; subharmonic suppression at selection time.
"""
import numpy as np

try:
    import cv2
except Exception:  # pragma: no cover
    cv2 = None

MIN_STEP = 2.0
MAX_STEP = 24.0
STEP_GRID = 0.02
BAND = 24


# ---------------------------------------------------------------- features
def to_gray(rgba):
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3:4].astype(np.float32) / 255.0
    g = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
    return g * a[..., 0] + 127.5 * (1.0 - a[..., 0])


def d1_along(g, axis):
    d = np.abs(np.diff(g, axis=axis))
    pad = [(0, 0), (0, 0)]
    pad[axis] = (0, 1)
    return np.pad(d, pad)


def d2_along(g, axis):
    d = np.abs(np.diff(g, n=2, axis=axis))
    pad = [(0, 0), (0, 0)]
    pad[axis] = (1, 1)
    return np.pad(d, pad)


def median_quant(g):
    if cv2 is not None:
        m = cv2.medianBlur(g.astype(np.uint8), 3).astype(np.float32)
    else:
        m = g
    return np.round(m / 12.0) * 12.0


# ---------------------------------------------------------------- ACF/cepstrum
def band_profiles(feat, axis, band=BAND):
    """Sum feature over bands of `band` lines -> (n_bands, extent)."""
    x = feat if axis == 1 else feat.T
    nb = max(1, x.shape[0] // band)
    x = x[: nb * band]
    return x.reshape(nb, -1, x.shape[1]).sum(1)


def band_acf(prof):
    """Mean unbiased ACF over band profiles, acf[0] == 1."""
    x = prof - prof.mean(axis=1, keepdims=True)
    n = x.shape[1]
    nfft = 1 << int(np.ceil(np.log2(2 * n)))
    F = np.fft.rfft(x, nfft, axis=1)
    p = (F * np.conj(F)).real
    # normalise each band by its own power so one busy band can't dominate
    p /= np.clip(p.sum(axis=1, keepdims=True), 1e-12, None)
    ac = np.fft.irfft(p, nfft, axis=1)[:, :n].sum(0)
    lags = np.arange(n, dtype=np.float64)
    ac = ac * (n / np.clip(n - lags, 1, None))
    ac /= max(ac[0], 1e-12)
    return ac


def band_cepstrum(prof):
    """Cepstrum of the mean log power spectrum over band profiles."""
    x = prof - prof.mean(axis=1, keepdims=True)
    n = x.shape[1]
    nfft = 1 << int(np.ceil(np.log2(2 * n)))
    F = np.fft.rfft(x, nfft, axis=1)
    logp = np.log((F * np.conj(F)).real.mean(0) + 1e-6)
    logp -= logp.mean()
    c = np.fft.irfft(logp, nfft)[: n // 2]
    c = (c - np.median(c)) / (c.std() + 1e-12)
    return c


def _interp(arr, pos):
    i = np.clip(pos.astype(int), 0, len(arr) - 2)
    f = pos - i
    return arr[i] * (1.0 - f) + arr[i + 1] * f


def comb_score(ac, s, k_max=24, k0=12.0):
    """Comb minus anti-comb on the ACF at multiples of s."""
    n = len(ac)
    K = int(min((n - 2) / s, k_max))
    if K < 2:
        return -1.0
    k = np.arange(1, K + 1, dtype=np.float64)
    w = np.exp(-(k - 1) / k0)
    pk = _interp(ac, k * s)
    tr = 0.5 * (_interp(ac, (k - 0.5) * s) + _interp(ac, (k + 0.5) * s))
    return float(np.sum(w * (pk - tr)) / np.sum(w))


def comb_score_vec(ac, steps, k_max=24, k0=12.0):
    """Vectorized comb_score over an array of steps (identical values).

    The per-step python loop was ~27k calls per image; one interp over a
    (n_steps, K) position matrix replaces it."""
    n = len(ac)
    steps = np.asarray(steps, dtype=np.float64)
    out = np.full(len(steps), -1.0)
    Ks = np.minimum(((n - 2) / steps).astype(int), k_max)
    kk = np.arange(1, k_max + 1, dtype=np.float64)

    def interp_mat(pos):
        i = np.clip(pos.astype(int), 0, n - 2)
        f = pos - i
        return ac[i] * (1.0 - f) + ac[i + 1] * f

    pos = steps[:, None] * kk[None, :]
    pk = interp_mat(pos)
    tr = 0.5 * (interp_mat(pos - 0.5 * steps[:, None])
                + interp_mat(pos + 0.5 * steps[:, None]))
    w = np.exp(-(kk - 1) / k0)
    valid = kk[None, :] <= Ks[:, None]
    wv = w[None, :] * valid
    wsum = wv.sum(axis=1)
    ok = Ks >= 2
    with np.errstate(invalid="ignore"):
        vals = (wv * (pk - tr)).sum(axis=1) / np.maximum(wsum, 1e-12)
    out[ok] = vals[ok]
    return out



def plain_comb(ac, s, k_max=16, k0=8.0):
    """Comb sum only (no anti-comb): 'is there ACF mass at multiples of s'."""
    n = len(ac)
    K = int(min((n - 2) / s, k_max))
    if K < 1:
        return 0.0
    k = np.arange(1, K + 1, dtype=np.float64)
    w = np.exp(-(k - 1) / k0)
    return float(np.sum(w * _interp(ac, k * s)) / np.sum(w))


def train_quality(ac, s0, k_max=20):
    """How well an actual ACF peak train fits multiples of s0.

    Returns (coverage-weighted inlier mass) * exp(-4*rms residual / s0).
    Distinguishes the true fundamental from rational aliases (e.g. 25/9 of
    the true step under a JPEG 8px lattice) that score similar comb values.
    """
    n = len(ac)
    K = int(min((n - 3) / s0, k_max))
    if K < 2:
        return 0.0
    res, mass, tot = [], 0.0, 0
    for k in range(1, K + 1):
        c0 = k * s0
        r = max(2, int(0.35 * s0))
        lo = max(1, int(c0 - r)); hi = min(n - 2, int(np.ceil(c0 + r)))
        if hi <= lo:
            continue
        tot += 1
        seg = ac[lo:hi + 1]
        i = int(np.argmax(seg)) + lo
        if i <= lo or i >= hi or ac[i] <= 0:
            continue
        d = (ac[i - 1] - ac[i + 1]) / (2 * (ac[i - 1] - 2 * ac[i] + ac[i + 1]) + 1e-12)
        p = i + float(np.clip(d, -1, 1))
        res.append((p - c0) / s0)
        mass += ac[i]
    if not res or tot == 0:
        return 0.0
    rms = float(np.sqrt(np.mean(np.square(res))))
    return (mass / tot) * np.exp(-4.0 * rms)


def refine_step_acf(ac, s0, k_max=24):
    """Refine step by fitting a line through ACF peak-train positions.

    Finds the local ACF maximum near each multiple k*s0 (parabolic subpixel),
    then robust-fits peak_pos ~ k * s through the origin.
    """
    n = len(ac)
    K = int(min((n - 3) / s0, k_max))
    if K < 2:
        return s0
    pos, ks, wts = [], [], []
    for k in range(1, K + 1):
        c0 = k * s0
        r = max(2, int(0.3 * s0))
        lo = max(1, int(c0 - r))
        hi = min(n - 2, int(np.ceil(c0 + r)))
        if hi <= lo:
            continue
        seg = ac[lo:hi + 1]
        i = int(np.argmax(seg)) + lo
        if i <= lo or i >= hi:      # peak on window edge: unreliable
            continue
        d = (ac[i - 1] - ac[i + 1]) / (2 * (ac[i - 1] - 2 * ac[i] + ac[i + 1]) + 1e-12)
        p = i + float(np.clip(d, -1, 1))
        pos.append(p)
        ks.append(k)
        wts.append(max(ac[i], 1e-6))
    if len(pos) < 2:
        return s0
    pos = np.array(pos); ks = np.array(ks, np.float64); wts = np.array(wts)
    s = s0
    for _ in range(3):
        keep = np.abs(pos - ks * s) <= max(0.3 * s0, 1.5)
        if keep.sum() < 2:
            break
        s = float(np.sum(wts[keep] * pos[keep] * ks[keep]) /
                  np.sum(wts[keep] * ks[keep] ** 2))
    if not (0.8 * s0 <= s <= 1.25 * s0):
        return s0
    return s


def ceps_score(c, s):
    n = len(c)
    K = int(min((n - 2) / s, 6))
    if K < 1:
        return 0.0
    k = np.arange(1, K + 1, dtype=np.float64)
    w = np.exp(-(k - 1) / 3.0)
    return float(np.sum(w * _interp(c, k * s)) / np.sum(w))


# ---------------------------------------------------------------- per-axis
def axis_estimate(maps, axis, extent):
    """maps: list of (feature_map, weight). Returns (step, candidates)."""
    steps = np.arange(MIN_STEP, min(MAX_STEP, extent / 4.0), STEP_GRID)
    raw = np.zeros_like(steps)
    ac_sum = np.zeros(extent)
    for feat, wgt in maps:
        n_lines = feat.shape[0] if axis == 1 else feat.shape[1]
        for band, bw in ((12, 0.5), (BAND, 1.0), (n_lines, 1.0)):
            ac = band_acf(band_profiles(feat, axis, band))
            ac_sum += wgt * bw * ac
            raw += wgt * bw * comb_score_vec(ac, steps)
    # cepstrum vote on the primary feature map
    c = band_cepstrum(band_profiles(maps[0][0], axis))
    cz = np.array([ceps_score(c, s) for s in steps])
    raw += 0.1 * np.clip(cz, 0, None) * np.clip(raw, 0, None) / max(raw.max(), 1e-9)

    # subharmonic suppression (selection only): m*s0 scores like s0 on the
    # comb, so penalise s by the best score among s/2..s/5.
    pen = np.zeros_like(raw)
    for m in (2, 3, 4, 5):
        s_sub = steps / m
        valid = s_sub >= steps[0]
        val = _interp(raw, np.clip((s_sub - steps[0]) / STEP_GRID, 0, None))
        pen = np.maximum(pen, np.where(valid, np.clip(val, 0, None), 0.0))
    sel = raw - 0.5 * pen

    # local maxima of the selection score; refine position on the raw score
    loc = np.where((sel[1:-1] > sel[:-2]) & (sel[1:-1] >= sel[2:]))[0] + 1
    order = loc[np.argsort(sel[loc])[::-1]][:8]
    cands = []
    for i in order:
        if 0 < i < len(raw) - 1:
            d = (raw[i - 1] - raw[i + 1]) / (2 * (raw[i - 1] - 2 * raw[i] + raw[i + 1]) + 1e-12)
            s = steps[i] + float(np.clip(d, -1, 1)) * STEP_GRID
        else:
            s = steps[i]
        cands.append((float(s), float(sel[i])))
    if not cands:
        i = int(np.argmax(sel))
        cands = [(float(steps[i]), float(sel[i]))]
    # near-tie disambiguation: rational aliases (e.g. 25/9 of the true step
    # under a JPEG lattice) can match the comb score of the fundamental, but
    # the actual peak-train residuals give them away.
    if len(cands) >= 2 and cands[1][1] >= 0.85 * cands[0][1]:
        top = [c for c in cands if c[1] >= 0.85 * cands[0][1]][:3]
        qs = [train_quality(ac_sum / max(ac_sum[0], 1e-12), s) for s, _ in top]
        j = int(np.argmax([z * (0.1 + q) for (s, z), q in zip(top, qs)]))
        if j != 0:
            cands = [top[j]] + [c for c in cands if c is not top[j]]
    # precision: fit the ACF peak train around each top candidate
    cands = [(refine_step_acf(ac_sum, s), z) for s, z in cands]
    best = cands[0][0]
    return best, cands, ac_sum


def local_count(maps, axis, extent, s0):
    """Drift-aware cell count: split the axis into windows, estimate the
    local step in each from its own banded ACF, integrate width/s_local."""
    uniform = extent / s0
    nw = int(np.clip(extent / (14.0 * s0), 1, 8))
    if nw < 2 or uniform < 96:
        # too few periods for drift to matter / for stable local estimates
        return uniform
    edges = np.linspace(0, extent, nw + 1).astype(int)
    total = 0.0
    for a, b in zip(edges[:-1], edges[1:]):
        ac_loc = np.zeros(b - a)
        for feat, wgt in maps:
            sl = feat[:, a:b] if axis == 1 else feat[a:b, :]
            n_lines = sl.shape[0] if axis == 1 else sl.shape[1]
            for band, bw in ((BAND, 1.0), (n_lines, 1.0)):
                ac_loc += wgt * bw * band_acf(band_profiles(sl, axis, band))
        # local re-scan: drift can move the local step well away from s0
        scan = s0 * np.arange(0.85, 1.18, 0.01)
        sc = np.array([comb_score(ac_loc, s, k_max=12, k0=8.0) for s in scan])
        s_glob = comb_score(ac_loc, s0, k_max=12, k0=8.0)
        if sc.max() > max(1.6 * max(s_glob, 0.0), 0.02):
            s_i = refine_step_acf(ac_loc, float(scan[int(np.argmax(sc))]), k_max=12)
        else:
            s_i = refine_step_acf(ac_loc, s0, k_max=10)
        s_i = float(np.clip(s_i, 0.82 * s0, 1.2 * s0))
        total += (b - a) / s_i
    # adopt the integrated count only on clear disagreement (real drift);
    # local windows jitter by ~1 cell on clean images.
    return total if abs(total - uniform) >= 2.5 else uniform


# ---------------------------------------------------------------- detect
def phase_count(maps, axis, extent, step):
    """Cell count by unwrapped-phase span (ported from the ML branch).

    GridNet's key output-size idea, done classically: demodulate the
    boundary-energy profile at the detected frequency (Gabor window a few
    cells wide) to get a local phase theta(x) and coherence |C(x)|; unwrap
    with increment-relative branch selection (low-coherence noise then
    averages to the NOMINAL advance - the right fallback); the total span
    over [0, extent] divided by 2pi is the exact cell count even when the
    grid drifts. Returns (count, coherence)."""
    # phase must come from ONE alignment: the |d2| map peaks at cell
    # centres and the |d1| map at cell boundaries (half a period apart);
    # mixing them cancels the very phase we are tracking. Use the last map
    # (quantized first-difference, cut-aligned).
    dmap = maps[-1][0]
    prof = dmap.sum(axis=0) if axis == 1 else dmap.sum(axis=1)
    prof = prof.astype(np.float64)
    n = len(prof)
    if n < 8 or step < 1.5:
        return None, 0.0
    prof = prof - prof.mean()

    # gabor demodulation at frequency 1/step
    sigma = 2.5 * step
    half = int(min(max(3 * sigma, step * 2), n // 2))
    t = np.arange(-half, half + 1)
    win = np.exp(-0.5 * (t / sigma) ** 2)
    kern = win * np.exp(-2j * np.pi * t / step)
    C = np.convolve(prof, kern[::-1], mode="same")

    theta = np.angle(C)
    mag = np.abs(C)
    coherence = float((mag / (mag.max() + 1e-12)).mean())

    inc = 2.0 * np.pi / step
    d = np.diff(theta) - inc
    d = (d + np.pi) % (2.0 * np.pi) - np.pi
    span = float(np.sum(d + inc)) + inc  # + half-sample extension both ends
    count = span / (2.0 * np.pi)
    return count, coherence


def detect(rgba, pre=None):
    """pre: optional dict(maps_x, maps_y, est_x, est_y) with est_* =
    (step, candidates, acf) from axis_estimate - avoids recomputing the
    profiles/ACFs when the caller already built them."""
    if pre is not None:
        h, w = rgba.shape[:2]
        maps_x, maps_y = pre["maps_x"], pre["maps_y"]
        sx, cx, ac_x = pre["est_x"]
        sy, cy, ac_y = pre["est_y"]
    else:
        g = to_gray(rgba)
        h, w = g.shape
        gq = median_quant(g)
        maps_x = [(d2_along(g, 1), 1.0), (d1_along(gq, 1), 0.7)]
        maps_y = [(d2_along(g, 0), 1.0), (d1_along(gq, 0), 0.7)]
        sx, cx, ac_x = axis_estimate(maps_x, 1, w)
        sy, cy, ac_y = axis_estimate(maps_y, 0, h)

    # cross-axis harmonic reconciliation: comb-minus-anticomb suppresses the
    # true step s when the image also has s/2 texture (dither), because the
    # anti-comb points land on the s/2 peaks. If the axes disagree by a near
    # exact factor of 2 or 3 and the small axis's ACF also has mass at the
    # big step's multiples, promote the small axis to the big step.
    def reconcile(s_small, s_big, ac_small):
        for m in (2, 3):
            if abs(s_big / s_small - m) <= 0.06 * m:
                pc_big = plain_comb(ac_small, s_big)
                pc_small = plain_comb(ac_small, s_small)
                if pc_big >= 0.55 * pc_small and pc_big > 0:
                    return refine_step_acf(ac_small, s_big)
        return s_small
    if sx < sy:
        sx = reconcile(sx, sy, ac_x)
    elif sy < sx:
        sy = reconcile(sy, sx, ac_y)

    # joint square-ish pairing: when the axes disagree (>8%) but both hold
    # strong runner-up candidates that agree, prefer the agreeing pair
    # (pseudo pixel cells are usually near square; heavy phase shuffling can
    # push the true step to rank 2 on both axes independently).
    if abs(np.log(sx / sy)) > 0.08:
        zx0 = max(z for _, z in cx); zy0 = max(z for _, z in cy)
        best_pair, best_sum = None, 0.0
        for s1, z1 in cx[:6]:
            for s2, z2 in cy[:6]:
                if abs(np.log(s1 / s2)) <= 0.08 and z1 >= 0.6 * zx0 and z2 >= 0.6 * zy0:
                    if z1 + z2 > best_sum:
                        best_sum, best_pair = z1 + z2, (s1, s2)
        if best_pair is not None and (abs(np.log(best_pair[0] / sx)) > 1e-6 or
                                      abs(np.log(best_pair[1] / sy)) > 1e-6):
            sx, sy = best_pair
            sx = refine_step_acf(ac_x, sx)
            sy = refine_step_acf(ac_y, sy)

    # looser second pass for mild disagreement (5-13%): pull one axis onto
    # its own candidate that is compatible with the other axis's best.
    if 0.05 < abs(np.log(sx / sy)) <= 0.13:
        zx0 = max(z for _, z in cx); zy0 = max(z for _, z in cy)

        def _pull(cands_a, za0, s_other):
            best = None
            for s, z in cands_a[:6]:
                if abs(np.log(s / s_other)) <= 0.08 and z >= 0.3 * za0:
                    if best is None or z > best[1]:
                        best = (s, z)
            return best
        move_y = _pull(cy, zy0, sx)      # keep sx, move sy
        move_x = _pull(cx, zx0, sy)      # keep sy, move sx
        score_a = zx0 + (move_y[1] if move_y else -1e9)
        score_b = zy0 + (move_x[1] if move_x else -1e9)
        if move_y is not None and score_a >= score_b:
            s_new = refine_step_acf(ac_y, move_y[0])
            sy = s_new if abs(np.log(s_new / move_y[0])) < 0.04 else move_y[0]
        elif move_x is not None:
            s_new = refine_step_acf(ac_x, move_x[0])
            sx = s_new if abs(np.log(s_new / move_x[0])) < 0.04 else move_x[0]

    # drift-aware counting: integrate local (windowed) ACF step estimates
    n_cols = local_count(maps_x, 1, w, sx)
    n_rows = local_count(maps_y, 0, h, sy)

    cands = sorted(cx + cy, key=lambda t: -t[1])[:8]
    return dict(step_x=float(w / n_cols), step_y=float(h / n_rows),
                cols=int(round(n_cols)), rows=int(round(n_rows)),
                phase_x=0.0, phase_y=0.0, candidates=cands)
