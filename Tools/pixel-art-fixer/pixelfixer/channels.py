"""Flexible pixel-grid detection.

Fits a (possibly warped, non-uniform, non-square) grid to an image that is
supposed to be pixel art rendered at a larger-than-native resolution.

Key ideas
---------
1. Two edge signals per axis:
     E1 (edge profile)      - sum of |first difference|. Sharp (nearest
                              neighbour) upscales put peaks exactly on the
                              cut lines between pseudo-pixels.
     E2 (curvature profile) - sum of |second difference|. Smooth (bilinear /
                              bicubic) upscales have *no* E1 peaks (the ramp
                              spreads gradient evenly across a cell) but the
                              piecewise-linear knots at cell centres put
                              sharp peaks in E2.
   Each axis independently picks whichever signal is more periodic and
   remembers whether it locates cuts ("cut" mode) or centres ("knot" mode).
2. Comb scoring with bias-corrected z-scores: for every candidate cell size
   we lay a regular comb over the profile (best phase wins) and measure how
   many standard errors its mean energy sits above chance, minus the
   inflation expected from trying many phases. This makes small and large
   steps statistically comparable and gives a scale-free confidence value.
   Profiles are max-pooled proportionally to the candidate step so wobbly
   (jittered) grids still register.
3. Cut placement by an elastic-chain dynamic program: one cut (or knot) per
   expected boundary, each allowed to deviate from perfect spacing (paying
   a quadratic penalty) to land on real edge energy. Globally optimal - a
   locally bad decision cannot derail the rest of the grid like it can with
   a greedy walker.
4. Warp refinement: the image is split into bands per axis, and every cut
   is re-optimised against the band's own profile with a smoothness prior
   across bands. Cuts become polylines that follow warped pixel rows and
   columns; every input pixel is finally assigned to a (row, col) cell.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Optional

import numpy as np
from scipy.ndimage import gaussian_filter1d, maximum_filter1d


# ---------------------------------------------------------------- profiles


def _flatten_channels(rgba: np.ndarray) -> np.ndarray:
    """float32 (H, W, C) with alpha premultiplied and kept as a channel."""
    img = rgba.astype(np.float32)
    if img.ndim == 2:
        img = img[:, :, None]
    if img.shape[2] == 4:
        a = img[:, :, 3:4] / 255.0
        img = np.concatenate([img[:, :, :3] * a, img[:, :, 3:4]], axis=2)
    return img


def axis_profiles(rgba: np.ndarray) -> dict:
    """Compute E1 (edge) and E2 (curvature) profiles for both axes.

    Returns dict with:
      e1x: (W+1,) energy of a vertical cut at x  (cut positions 0..W)
      e1y: (H+1,)
      e2x: (W,)   curvature energy at column x   (pixel positions)
      e2y: (H,)
    """
    img = _flatten_channels(rgba)
    h, w = img.shape[:2]

    dx = np.sqrt(((img[:, 1:, :] - img[:, :-1, :]) ** 2).sum(axis=2))
    dy = np.sqrt(((img[1:, :, :] - img[:-1, :, :]) ** 2).sum(axis=2))

    e1x = np.zeros(w + 1)
    e1y = np.zeros(h + 1)
    e1x[1:w] = dx.sum(axis=0)
    e1y[1:h] = dy.sum(axis=1)
    top = max(e1x[1:w].max() if w > 1 else 0.0,
              e1y[1:h].max() if h > 1 else 0.0, 1.0)
    e1x[0] = e1x[w] = top   # image borders are always cuts
    e1y[0] = e1y[h] = top

    cxx = img[:, 2:, :] - 2 * img[:, 1:-1, :] + img[:, :-2, :]
    cyy = img[2:, :, :] - 2 * img[1:-1, :, :] + img[:-2, :, :]
    e2x = np.zeros(w)
    e2y = np.zeros(h)
    e2x[1:w - 1] = np.sqrt((cxx ** 2).sum(axis=2)).sum(axis=0)
    e2y[1:h - 1] = np.sqrt((cyy ** 2).sum(axis=2)).sum(axis=1)

    return {"e1x": e1x, "e1y": e1y, "e2x": e2x, "e2y": e2y}


def _normalise(profile: np.ndarray) -> np.ndarray:
    interior = profile[1:-1]
    if interior.size == 0:
        return profile.astype(np.float64)
    scale = np.percentile(interior, 95)
    if scale <= 0:
        scale = interior.max() if interior.max() > 0 else 1.0
    return np.clip(profile / (scale + 1e-9), 0.0, 1.5)


# ------------------------------------------------------- period estimation


class _PooledProfile:
    """Pre-pooled/blurred variants of a profile for jitter-tolerant combs."""

    POOLS = (1, 3, 5, 7)

    def __init__(self, profile: np.ndarray):
        norm = _normalise(profile)
        self.variants = {}
        for p in self.POOLS:
            v = maximum_filter1d(norm, size=p) if p > 1 else norm
            v = gaussian_filter1d(v, sigma=0.6)
            interior = v[1:-1]
            self.variants[p] = (v, float(interior.mean()),
                                float(interior.std()) + 1e-9)

    def for_step(self, step: float):
        """All variants applicable to this step (pool must stay < step)."""
        limit = 3 if step >= 3.5 else 1
        if step >= 8:
            limit = 5
        if step >= 12:
            limit = 7
        return [self.variants[p] for p in self.POOLS if p <= limit]


def _comb_score(pp: _PooledProfile, step: float,
                phase_res: float = 0.25) -> tuple[float, float]:
    """Best (score, phase) of a regular comb of spacing `step`.

    Score is a bias-corrected z-score (see module docstring), maximised over
    the jitter-tolerance pools narrow enough for this step."""
    variants = pp.for_step(step)
    if not variants:
        return 0.0, 0.0
    n = len(variants[0][0]) - 1
    if step < 1.25 or step > n / 4:
        return 0.0, 0.0

    n_cuts = int(round(n / step))
    ks = np.arange(1, n_cuts)
    if len(ks) < 4:
        return 0.0, 0.0

    phases = np.arange(0.0, step, phase_res)
    pos = phases[:, None] + ks[None, :] * step
    valid = pos < n - 0.51
    pos = np.where(valid, pos, 0.0)
    i0 = pos.astype(np.int64)
    frac = pos - i0
    counts = valid.sum(axis=1)
    ok = counts >= 4
    if not ok.any():
        return 0.0, 0.0
    penalty = np.sqrt(2.0 * np.log(max(len(phases), 2)))

    best_score, best_phase = -1e18, 0.0
    for blur, base_mean, base_std in variants:
        vals = blur[i0] * (1 - frac) + blur[i0 + 1] * frac
        means = np.where(ok, (vals * valid).sum(axis=1) / np.maximum(counts, 1),
                         -np.inf)
        z = (means - base_mean) / (base_std / np.sqrt(np.maximum(counts, 1)) + 1e-9)
        b = int(np.argmax(z))
        sc = float(z[b] - penalty)
        if sc > best_score:
            best_score, best_phase = sc, float(phases[b])
    return best_score, best_phase


def _lattice_refine_peaks(peaks: np.ndarray, h: np.ndarray, s0: float,
                          n_iters: int = 4) -> float:
    """Lattice LSQ fit (see _lattice_refine) on a raw peak list."""
    if len(peaks) < 4:
        return s0
    s = float(s0)
    phi = float(peaks[np.argmax(h)]) % s
    for _ in range(n_iters):
        k = np.round((peaks - phi) / s)
        resid = peaks - (phi + k * s)
        w = h * (np.abs(resid) < 0.35 * s)
        if w.sum() <= 0 or len(np.unique(k[w > 0])) < 3:
            return s0
        A = np.stack([np.ones_like(k), k], axis=1)
        try:
            coef, *_ = np.linalg.lstsq(A * w[:, None], peaks * w, rcond=None)
        except np.linalg.LinAlgError:
            return s0
        phi, s = float(coef[0]), float(coef[1])
        if not np.isfinite(s) or s < 1.2 or abs(s - s0) > 0.6 * s0:
            return s0
    return s


def _lattice_refine(profile: np.ndarray, s0: float, n_iters: int = 4) -> float:
    """Sub-pixel period refinement by lattice fitting.

    Comb scores are razor-thin in step-space (a 0.02 px error accumulates to
    a full misalignment across a hundred cuts), so searching the comb score
    directly is hopeless. Instead: detect profile peaks, assign each to its
    nearest lattice index k = round((p - phase)/s), and solve p ~ phase + k*s
    by height-weighted least squares over the inliers. Converges to the true
    fractional step from a rough integer seed."""
    from scipy.signal import find_peaks
    norm = _normalise(profile)
    peaks, props = find_peaks(norm[1:-1], height=0.12,
                              distance=max(1, int(s0 * 0.45)))
    peaks = (peaks + 1).astype(np.float64)
    if len(peaks) < 4:
        return s0
    h = props["peak_heights"]

    s = float(s0)
    phi = float(peaks[np.argmax(h)]) % s
    for _ in range(n_iters):
        k = np.round((peaks - phi) / s)
        resid = peaks - (phi + k * s)
        w = h * (np.abs(resid) < 0.35 * s)
        if w.sum() <= 0 or len(np.unique(k[w > 0])) < 3:
            return s0
        A = np.stack([np.ones_like(k), k], axis=1)
        try:
            coef, *_ = np.linalg.lstsq(A * w[:, None], peaks * w, rcond=None)
        except np.linalg.LinAlgError:
            return s0
        phi, s = float(coef[0]), float(coef[1])
        if not np.isfinite(s) or s < 1.2 or abs(s - s0) > 0.6 * s0:
            return s0
    return s


def _rayleigh_score(profile: np.ndarray, step: float) -> tuple[float, float]:
    """Phase-coherence score of profile peaks against a lattice of `step`.

    A Rayleigh-style test: project every peak onto the unit circle at angle
    2*pi*position/step. Independent per-boundary jitter only attenuates the
    resultant vector (a 30% jitter still leaves |R| ~ 0.3) whereas comb
    sampling collapses entirely, so this channel rescues wobbly grids.
    Scaled by lattice occupancy so half-period harmonics (every second slot
    empty) don't tie with the fundamental. Returns (z_like, phase)."""
    from scipy.signal import find_peaks
    norm = _normalise(profile)
    n = len(norm) - 1
    if step < 2.0 or step > n / 4:
        return 0.0, 0.0
    peaks, props = find_peaks(norm[1:-1], height=0.15,
                              distance=max(1, int(step * 0.4)))
    if len(peaks) < 5:
        return 0.0, 0.0
    p = (peaks + 1).astype(np.float64)
    h = props["peak_heights"]

    ph = np.exp(2j * np.pi * p / step)
    resultant = (h * ph).sum()
    R = np.abs(resultant) / h.sum()
    n_eff = h.sum() ** 2 / (h ** 2).sum()

    # fraction of lattice slots that actually contain a peak
    phase = (np.angle(resultant) / (2 * np.pi) * step) % step
    slots = np.round((p - phase) / step)
    hits = np.abs(p - (phase + slots * step)) < 0.35 * step
    n_slots = max(1, int(n / step) - 1)
    occupancy = min(1.0, len(np.unique(slots[hits])) / n_slots)

    z = np.sqrt(2.0 * n_eff) * R * occupancy
    return float(z), float(phase)


def _refine_step(pp: _PooledProfile, profile: np.ndarray,
                 step: float) -> tuple[float, float, float]:
    """Refine a candidate step and rescore it -> (step, score, phase).

    Tries the raw seed and its lattice-fitted refinement; scores each by the
    better of comb z (regular grids) and Rayleigh coherence (wobbly grids)."""
    candidates = {round(step, 4)}
    refined = _lattice_refine(profile, step)
    candidates.add(round(refined, 4))
    best = (step, -1e18, 0.0)
    for s in candidates:
        score, phase = _comb_score(pp, s)
        rz, rphase = _rayleigh_score(profile, s)
        if rz > score:
            score, phase = rz, rphase
        if score > best[1]:
            best = (s, score, phase)
    return best


def _spacing_candidates(profile: np.ndarray, min_step: float,
                        max_step: float) -> list[float]:
    """Candidate periods from spacings between adjacent profile peaks.

    This is pixeldetector's core idea (median of np.diff(find_peaks(...)))
    - phase-free and immune to harmonics, so it makes an excellent seed for
    comb refinement even when the blind scan struggles."""
    from scipy.signal import find_peaks
    norm = _normalise(profile)
    cands: list[float] = []
    for height in (0.10, 0.30):
        peaks, _ = find_peaks(norm[1:-1], height=height, distance=2)
        if len(peaks) < 4:
            continue
        sp = np.diff(peaks).astype(np.float64)
        sp = sp[(sp >= max(1.5, min_step * 0.6)) & (sp <= max_step * 1.5)]
        if len(sp) < 3:
            continue
        cands.append(float(np.median(sp)))
        vals, counts = np.unique(sp, return_counts=True)
        cands.append(float(vals[np.argmax(counts)]))
    return cands


def estimate_period(profile: np.ndarray, min_step: float = 2.0,
                    max_step: Optional[float] = None,
                    harmonic_tol: float = 0.88) -> tuple[Optional[float], float, float]:
    """Estimate the dominant cell size along one axis of one profile.

    Returns (step, score, phase); step None if nothing periodic."""
    n = len(profile) - 1
    if max_step is None:
        max_step = min(max(4.0, n / 8.0), 64.0)

    pp = _PooledProfile(profile)

    steps_list = []
    s = min_step
    while s <= max_step:
        steps_list.append(float(s))
        s += 0.5 if s < 16 else 1.0
    if not steps_list:
        return None, 0.0, 0.0
    scores = np.array([_comb_score(pp, s)[0] for s in steps_list])
    steps = np.array(steps_list)
    order = np.argsort(-scores)

    refined: list[tuple[float, float, float]] = []
    seen: list[float] = []

    # peak-spacing candidates first: strong, harmonic-free priors
    for s0 in _spacing_candidates(profile, min_step, max_step):
        if s0 < min_step or s0 > max_step:
            continue
        if any(abs(s0 - s1) < 0.6 for s1 in seen):
            continue
        seen.append(s0)
        refined.append(_refine_step(pp, profile, s0))

    for idx in order[:6]:
        if scores[idx] <= 0:
            break
        s0 = steps[idx]
        if any(abs(s0 - s1) < 0.6 for s1 in seen):
            continue
        seen.append(s0)
        refined.append(_refine_step(pp, profile, s0))

    if not refined:
        return None, 0.0, 0.0
    best_score = max(r[1] for r in refined)
    if best_score <= 0:
        return None, 0.0, 0.0

    # prefer the smallest step among near-best (multiples of the true step
    # also score well; divisors score clearly lower)
    good = [r for r in refined if r[1] >= harmonic_tol * best_score]
    good.sort(key=lambda r: r[0])
    step, score, phase = good[0]

    # test integer divisors of the winner - catches a missed fundamental
    # (content structure often repeats at small multiples of the pixel size).
    # The absolute floor keeps this from swapping harmonics of pure noise.
    improved = True
    while improved:
        improved = False
        for div in (2, 3, 4, 5):
            sub = step / div
            if sub < min_step:
                continue
            s_sub, sc_sub, ph_sub = _refine_step(pp, profile, sub)
            if (sc_sub >= max(harmonic_tol * score, 3.0)
                    and abs(s_sub * div - step) < 0.6 * div):
                step, score, phase = s_sub, sc_sub, ph_sub
                improved = True
                break

    # jpeg trap: quantization amplifies the 8x8 block lattice (and its 4.0 /
    # 2.67 harmonics), always at phase 0 relative to the image origin. If the
    # winner looks exactly like the jpeg grid but a credible non-jpeg
    # candidate exists, prefer that candidate.
    if is_jpeg_suspect(step):
        alts = [r for r in refined
                if not is_jpeg_suspect(r[0]) and r[1] >= 0.55 * score]
        if alts:
            alts.sort(key=lambda r: -r[1])
            step, score, phase = alts[0]
        else:
            # still on the jpeg family: deflate so the sibling channel /
            # axis with a real grid wins downstream comparisons
            score *= 0.5

    return step, score, phase


def is_jpeg_suspect(step: float) -> bool:
    """True when a step sits exactly on the jpeg block lattice family,
    regardless of phase (band/Rayleigh channels report drifted phases)."""
    return any(abs(step - b) < 0.09 for b in (8.0, 4.0, 8.0 / 3.0, 16.0, 24.0))


def is_jpeg_lattice(step: float, phase: float) -> bool:
    """True when (step, phase) matches the 8x8 jpeg block grid or one of its
    integer subdivisions, aligned to the image origin."""
    for base in (8.0, 4.0, 8.0 / 3.0, 16.0, 24.0):
        if abs(step - base) < 0.09:
            m = phase % base
            if m < 0.6 or m > base - 0.6:
                return True
    return False




def _jpeg_lattice_strength(profile: np.ndarray) -> float:
    """Differential z-score of the phase-0 8px lattice (jpeg block edges).

    A real 4px art grid puts energy on 8k AND 8k+4; jpeg blocks only on 8k.
    Scoring the difference keeps true 4/8px grids from being mistaken for
    compression artifacts."""
    norm = gaussian_filter1d(_normalise(profile), sigma=0.6)
    n = len(norm) - 1
    on = np.arange(8, n - 7, 8)
    off = np.arange(4, n - 3, 8)
    if len(on) < 4 or len(off) < 4:
        return 0.0
    interior = norm[1:-1]
    base_mean = float(interior.mean())
    base_std = float(interior.std()) + 1e-9
    z_on = (norm[on].mean() - base_mean) / (base_std / np.sqrt(len(on)))
    z_off = (norm[off].mean() - base_mean) / (base_std / np.sqrt(len(off)))
    return float(z_on - max(z_off, 0.0))


def _notch_jpeg(profile: np.ndarray, width: float = 1.0) -> np.ndarray:
    """Remove the 8px block lattice: values within `width` of a multiple of 8
    are replaced by interpolation from unaffected neighbours."""
    n = len(profile) - 1
    pos = np.arange(n + 1)
    m = np.minimum(pos % 8, 8 - (pos % 8))
    mask = m <= width
    mask[0] = mask[-1] = False
    if not mask.any() or mask.all():
        return profile
    out = profile.astype(np.float64).copy()
    good = ~mask
    out[mask] = np.interp(pos[mask], pos[good], out[good])
    return out



def _grad_maps(rgba, quantized):
    """2D gradient/curvature magnitude maps used for tile evidence.

    dqx: (H, W-1) first-diff magnitude of the quantized image along x
    dqy: (H-1, W) along y;  cox/coy: curvature magnitudes of the original."""
    q = _flatten_channels(quantized)
    o = _flatten_channels(rgba)
    return {
        "dqx": np.sqrt(((q[:, 1:, :] - q[:, :-1, :]) ** 2).sum(axis=2)),
        "dqy": np.sqrt(((q[1:, :, :] - q[:-1, :, :]) ** 2).sum(axis=2)),
        "cox": np.sqrt(((o[:, 2:, :] - 2 * o[:, 1:-1, :] + o[:, :-2, :]) ** 2).sum(axis=2)),
        "coy": np.sqrt(((o[2:, :, :] - 2 * o[1:-1, :, :] + o[:-2, :, :]) ** 2).sum(axis=2)),
    }


def _tile_peaks(dmap, axis, offset=1, max_tiles=360):
    """Peak lists for 2D tiles of a gradient map.

    axis=0: peaks along x within each tile (dmap rows summed).
    Returns [(positions, heights, tile_extent), ...] with positions in
    absolute axis coordinates. Local tiles keep their own grid phase, which
    is what sprite sheets and heavily warped images need."""
    from scipy.signal import find_peaks
    if axis == 1:
        dmap = dmap.T
    H, W = dmap.shape
    tw = int(np.clip(W // 6, 48, 192))
    th = int(np.clip(H // 6, 48, 192))
    tiles = []
    for y0 in range(0, max(H - th // 2, 1), th):
        for x0 in range(0, max(W - tw // 2, 1), tw):
            seg = dmap[y0:y0 + th, x0:x0 + tw]
            if seg.size == 0:
                continue
            prof = seg.sum(axis=0)
            scale = np.percentile(prof, 95)
            if scale <= 0:
                continue
            norm = np.clip(prof / scale, 0, 1.5)
            pk, props = find_peaks(norm, height=0.2, distance=2)
            if len(pk) < 4:
                continue
            tiles.append(((pk + x0 + offset).astype(np.float64),
                          props["peak_heights"], len(prof)))
    if len(tiles) > max_tiles:
        idx = np.linspace(0, len(tiles) - 1, max_tiles).astype(int)
        tiles = [tiles[i] for i in idx]
    return tiles


def _tiles_ray_z(tiles, step):
    """Stouffer-combined per-tile Rayleigh coherence at `step`.

    Every tile gets its own phase; only tiles with enough peaks vote."""
    if not tiles or step < 2.0:
        return 0.0
    zs = []
    for p, h, ext in tiles:
        if step > ext / 3 or len(p) < 4:
            continue
        ph = np.exp(2j * np.pi * p / step)
        resultant = (h * ph).sum()
        R = np.abs(resultant) / (h.sum() + 1e-9)
        n_eff = h.sum() ** 2 / ((h ** 2).sum() + 1e-9)
        phase = (np.angle(resultant) / (2 * np.pi) * step) % step
        slots = np.round((p - phase) / step)
        hits = np.abs(p - (phase + slots * step)) < 0.35 * step
        n_slots = max(1, int(ext / step))
        occ = min(1.0, len(np.unique(slots[hits])) / n_slots)
        # centre so that noise tiles average to ~0 instead of diluting
        zs.append(np.sqrt(2.0 * n_eff) * R * occ - 1.0)
    if len(zs) < 2:
        return 0.0
    return float(np.sum(zs) / np.sqrt(len(zs)))


def _tile_spacing_modes(tiles, min_step, max_step, top=3):
    """Candidate steps from the distribution of per-tile peak spacings."""
    votes = []
    for p, h, _ in tiles:
        if len(p) < 5:
            continue
        sp = np.diff(p)
        sp = sp[(sp >= min_step * 0.7) & (sp <= max_step * 1.4)]
        if len(sp) >= 3:
            votes.append(float(np.median(sp)))
    if len(votes) < 4:
        return []
    votes = np.array(votes)
    out = []
    hist_vals = votes.copy()
    for _ in range(top):
        if len(hist_vals) < 3:
            break
        med = float(np.median(hist_vals))
        out.append(med)
        keep = np.abs(hist_vals - med) > 0.75
        hist_vals = hist_vals[keep]
    return out



def _axis_spectrum(dmaps, axis, row_group=4, max_win=1024):
    """Welch-averaged power spectrum of gradient scanline groups.

    Magnitude spectra are phase-free, so mushy, warped, per-sprite-shifted
    grids all contribute power at the same frequency comb k/step. Rows are
    summed in small groups (local phase is coherent over a few rows even
    under warp), Hann-windowed, and averaged. Returns (freqs, power)."""
    specs = None
    count = 0
    for dmap in dmaps:
        d = dmap.T if axis == 1 else dmap
        H, W = d.shape
        win = int(min(W, max_win))
        if win < 32:
            continue
        window = np.hanning(win)
        hop = max(win // 2, 1)
        for y0 in range(0, H - row_group + 1, row_group):
            prof = d[y0:y0 + row_group].sum(axis=0)
            prof = prof - prof.mean()
            for x0 in range(0, W - win + 1, hop):
                seg = prof[x0:x0 + win] * window
                sp = np.abs(np.fft.rfft(seg)) ** 2
                if specs is None:
                    specs = sp
                else:
                    specs = specs + sp
                count += 1
    if specs is None or count == 0:
        return np.array([0.0]), np.array([0.0])
    freqs = np.fft.rfftfreq(win)
    return freqs, specs / count


def _spectral_background(power):
    """Smooth local background of a power spectrum (median filter)."""
    from scipy.ndimage import median_filter
    k = max(5, len(power) // 24)
    if k % 2 == 0:
        k += 1
    return median_filter(power, size=k, mode="nearest")


def _spectral_z(freqs, power, bg, step):
    """Prominence (z-like) of the spectral peak at frequency 1/step."""
    if step < 2.0 or len(freqs) < 8:
        return 0.0
    f = 1.0 / step
    if f <= freqs[1] or f >= freqs[-1]:
        return 0.0
    # sample peak power with +-1 bin tolerance; log-ratio scale so the
    # score is commensurate with the comb/Rayleigh z channels
    idx = np.searchsorted(freqs, f)
    lo, hi = max(1, idx - 1), min(len(power) - 1, idx + 1)
    p = power[lo:hi + 1].max()
    b = bg[idx] + 1e-12
    ratio = max(p / b, 1e-6)
    return float(6.0 * np.log10(ratio))


def _spectral_candidates(freqs, power, bg, min_step, max_step, top=4):
    """Fundamental-period candidates: strong spectral peaks, preferring the
    lowest-frequency member of each harmonic comb."""
    from scipy.signal import find_peaks
    if len(freqs) < 8:
        return []
    resid = 6.0 * np.log10(np.maximum(power, 1e-12) / (bg + 1e-12))
    pk, props = find_peaks(resid, height=4.0)
    if len(pk) == 0:
        return []
    heights = props["peak_heights"]
    periods = np.where(freqs[pk] > 0, 1.0 / np.maximum(freqs[pk], 1e-9), 0.0)
    ok = (periods >= min_step) & (periods <= max_step)
    pk, heights, periods = pk[ok], heights[ok], periods[ok]
    if len(pk) == 0:
        return []
    order = np.argsort(-heights)
    cands = []
    for i in order:
        s = float(periods[i])
        # skip if s is a harmonic (integer divisor) of an already-kept step
        is_harm = False
        for s0 in cands:
            ratio = s0 / s
            if abs(ratio - round(ratio)) < 0.06 and round(ratio) >= 2:
                is_harm = True
                break
        if not is_harm:
            cands.append(s)
        if len(cands) >= top:
            break
    # also offer the *longest* period whose comb members appear: for each
    # kept candidate, check small multiples with spectral support
    extra = []
    for s in list(cands):
        for m in (2, 3, 4, 5):
            sm = s * m
            if sm > max_step:
                break
            if _spectral_z(freqs, power, bg, sm) > 3.0:
                extra.append(float(sm))
    return cands + extra


class _AxisEvidence:
    """All periodicity evidence for one axis of one signal type.

    Holds the global profile plus B band-restricted profiles. Scoring a
    candidate step combines per-band scores (each band free to choose its own
    phase, via comb or Rayleigh) with a Stouffer sum: warped images stay
    phase-coherent *within* a band even when global coherence is gone, so
    this dominates a global comb whenever warp exists, while equalling it on
    rigid grids."""

    def __init__(self, profile: np.ndarray, bands: Optional[np.ndarray],
                 tiles: Optional[list] = None, spectrum=None,
                 extra_z=None, extra_candidates=None):
        self.extra_z = extra_z
        self.extra_candidates = extra_candidates or []
        from scipy.signal import find_peaks
        self.profile = profile
        self.pp_global = _PooledProfile(profile)
        self.bands = bands if bands is not None else np.empty((0, 1))
        self.pps = [_PooledProfile(b) for b in self.bands]
        self.tiles = tiles or []
        if spectrum is not None:
            self.freqs, self.power = spectrum
            self.bg = _spectral_background(self.power)
        else:
            self.freqs = self.power = self.bg = None
        # cached peak lists for the cheap fractional sweep
        self._peaks = []
        for prof in [profile] + list(self.bands):
            norm = _normalise(prof)
            pk, props = find_peaks(norm[1:-1], height=0.15, distance=2)
            self._peaks.append(((pk + 1).astype(np.float64),
                                props["peak_heights"], len(norm) - 1))

    def score(self, step: float) -> tuple[float, float]:
        """(score, phase) for a candidate step."""
        gz, gp = _comb_score(self.pp_global, step)
        grz, grp = _rayleigh_score(self.profile, step)
        if grz > gz:
            gz, gp = grz, grp

        if len(self.pps) >= 2:
            zs, phs = [], []
            for pp, prof in zip(self.pps, self.bands):
                cz, cp = _comb_score(pp, step)
                rz, rp = _rayleigh_score(prof, step)
                if rz > cz:
                    cz, cp = rz, rp
                zs.append(cz)
                phs.append(cp)
            zb = float(np.sum(zs) / np.sqrt(len(zs)))
            if zb > gz:
                gz, gp = zb, float(phs[int(np.argmax(zs))])
        tz = _tiles_ray_z(self.tiles, step)
        if tz > gz:
            gz = tz  # tile phases are local; keep the best global phase
        if self.freqs is not None:
            sz = _spectral_z(self.freqs, self.power, self.bg, step)
            if sz > gz:
                gz = sz
        if self.extra_z is not None:
            xz = self.extra_z(step)
            if xz > gz:
                gz = xz
        return gz, gp

    def candidate_steps(self, min_step: float, max_step: float) -> list[float]:
        cands = _spacing_candidates(self.profile, min_step, max_step)
        for b in self.bands:
            cands += _spacing_candidates(b, min_step, max_step)
        cands += _tile_spacing_modes(self.tiles, min_step, max_step)
        if self.freqs is not None:
            cands += _spectral_candidates(self.freqs, self.power, self.bg,
                                          min_step, max_step)
        cands += [s for s in self.extra_candidates
                  if min_step <= s <= max_step]
        cands += self.sweep_candidates(min_step, max_step)
        return cands

    def _ray_quick(self, step: float) -> float:
        """Cheap Rayleigh coherence from cached peaks, Stouffer over bands."""
        zs = []
        for p, h, n in self._peaks:
            if len(p) < 5 or step > n / 4:
                zs.append(0.0)
                continue
            ph = np.exp(2j * np.pi * p / step)
            resultant = (h * ph).sum()
            R = np.abs(resultant) / (h.sum() + 1e-9)
            n_eff = h.sum() ** 2 / ((h ** 2).sum() + 1e-9)
            phase = (np.angle(resultant) / (2 * np.pi) * step) % step
            slots = np.round((p - phase) / step)
            hits = np.abs(p - (phase + slots * step)) < 0.35 * step
            n_slots = max(1, int(n / step) - 1)
            occ = min(1.0, len(np.unique(slots[hits])) / n_slots)
            zs.append(np.sqrt(2.0 * n_eff) * R * occ)
        band_z = (float(np.sum(zs[1:]) / np.sqrt(len(zs) - 1))
                  if len(zs) > 2 else (float(zs[0]) if zs else 0.0))
        tile_z = _tiles_ray_z(self.tiles, step)
        return max(band_z, tile_z)

    def sweep_candidates(self, min_step: float, max_step: float,
                         top: int = 6) -> list[float]:
        """Dense fractional sweep: integer seeds can't reach steps like 5.6
        on heavily degraded grids, so scan a geometric grid with the cheap
        coherence metric and seed the full scorer from its local maxima."""
        steps = []
        s = min_step
        while s <= max_step:
            steps.append(s)
            s *= 1.02
        if len(steps) < 5:
            return []
        zs = np.array([self._ray_quick(s) for s in steps])
        order = np.argsort(-zs)
        out = []
        for i in order:
            if zs[i] <= 0:
                break
            s0 = steps[i]
            if any(abs(s0 - s1) < max(0.12, 0.04 * s0) for s1 in out):
                continue
            out.append(float(s0))
            if len(out) >= top:
                break
        return out

    def refine(self, step: float) -> float:
        """Lattice-fit refinement: global + band fits + per-tile fits.

        Tiles matter most: on sprite sheets and warped art only the local
        phase is coherent, so per-tile fits (median-combined) recover the
        fractional step when the global fit cannot move at all."""
        fits = [_lattice_refine(self.profile, step)]
        for b in self.bands:
            fits.append(_lattice_refine(b, step))
        tile_fits = []
        for p, h, _ in self.tiles:
            if len(p) >= 6:
                f = _lattice_refine_peaks(p, h, step)
                if abs(f - step) > 1e-9:
                    tile_fits.append(f)
        if len(tile_fits) >= 4:
            fits = tile_fits          # tiles outvote global when plentiful
        return float(np.median(fits))


def _evidence_refine_step(ev: _AxisEvidence, step: float) -> tuple[float, float, float]:
    """(step, score, phase): try seed and lattice refinement, keep better."""
    candidates = {round(step, 4), round(ev.refine(step), 4)}
    best = (step, -1e18, 0.0)
    for s in candidates:
        score, phase = ev.score(s)
        if score > best[1]:
            best = (s, score, phase)
    return best



def _chain_energy_z(profile: np.ndarray, step: float, phase: float) -> float:
    """How well a full elastic chain at `step` explains the profile.

    Runs the lattice DP and z-scores the mean edge energy under the chosen
    cuts, corrected for the expected max-of-window inflation, so candidates
    with different window sizes compare fairly. The decisive test under
    heavy jitter: a true step snaps every cut onto a peak, a wrong step
    cannot."""
    n = len(profile) - 1
    if step < 1.5 or step > n / 3:
        return -1e9
    chain = lattice_dp(profile, step, phase,
                       n_targets=max(1, int(round(n / step)) - 1))
    if len(chain) < 3:
        return -1e9
    norm = gaussian_filter1d(_normalise(profile), sigma=0.6)
    interior = norm[1:-1]
    base_mean = float(interior.mean())
    base_std = float(interior.std()) + 1e-9
    D = max(1, int(round(step * 0.45)))
    inflation = np.sqrt(2.0 * np.log(2 * D + 1))
    K = len(chain)
    return float((norm[chain].mean() - base_mean - base_std * inflation)
                 / (base_std / np.sqrt(K)))


def estimate_period_ev(ev: _AxisEvidence, min_step: float = 2.0,
                       max_step: Optional[float] = None,
                       harmonic_tol: float = 0.88) -> tuple[Optional[float], float, float]:
    """Estimate the dominant cell size from an axis's evidence.

    Returns (step, score, phase); step None if nothing periodic."""
    n = len(ev.profile) - 1
    if max_step is None:
        max_step = min(max(4.0, n / 8.0), 64.0)

    steps_list = []
    s = min_step
    while s <= max_step:
        steps_list.append(float(s))
        s += 0.5 if s < 16 else 1.0
    if not steps_list:
        return None, 0.0, 0.0
    coarse = np.array([ev.score(s)[0] for s in steps_list])
    steps = np.array(steps_list)
    order = np.argsort(-coarse)

    refined: list[tuple[float, float, float]] = []
    seen: list[float] = []

    def _dedupe_r(s):
        return max(0.16, 0.05 * s)

    for s0 in ev.candidate_steps(min_step, max_step):
        if s0 < min_step or s0 > max_step:
            continue
        if any(abs(s0 - s1) < _dedupe_r(s0) for s1 in seen):
            continue
        seen.append(s0)
        refined.append(_evidence_refine_step(ev, s0))

    for idx in order[:6]:
        if coarse[idx] <= 0:
            break
        s0 = steps[idx]
        if any(abs(s0 - s1) < _dedupe_r(s0) for s1 in seen):
            continue
        seen.append(s0)
        refined.append(_evidence_refine_step(ev, s0))

    if not refined:
        return None, 0.0, 0.0

    # smoothed noise has quasi-periodic local maxima at the blur scale
    # (2-3 px) that stay coherent within short bands; a genuinely small grid
    # step must also be visible to the *global* comb
    gated = []
    for s, sc, ph in refined:
        if s < 4.0:
            gz, _ = _comb_score(ev.pp_global, s)
            tz = _tiles_ray_z(ev.tiles, s)
            if gz < 1.8 and tz < 3.0:
                continue
        gated.append((s, sc, ph))
    refined = gated
    if not refined:
        return None, 0.0, 0.0
    best_score = max(r[1] for r in refined)
    if best_score <= 0:
        return None, 0.0, 0.0

    good = [r for r in refined if r[1] >= harmonic_tol * best_score]
    good.sort(key=lambda r: r[0])
    top = max(refined, key=lambda r: r[1])
    step, score, phase = good[0]
    # structural guard: a smaller near-best step must actually populate the
    # lattice slots the top-scoring step doesn't share
    if step < top[0] - 0.6:
        ratio = top[0] / step
        if abs(ratio - round(ratio)) < 0.15 and round(ratio) >= 2:
            occ = _exclusive_slot_occupancy(ev.profile, step, phase, top[0])
            if occ < 0.30:
                step, score, phase = top

    improved = True
    while improved:
        improved = False
        for div in (2, 3, 4, 5):
            sub = step / div
            if sub < min_step:
                continue
            s_sub, sc_sub, ph_sub = _evidence_refine_step(ev, sub)
            if (s_sub < 4.0 and _comb_score(ev.pp_global, s_sub)[0] < 1.8
                    and _tiles_ray_z(ev.tiles, s_sub) < 3.0):
                continue
            if (sc_sub >= max(harmonic_tol * score, 3.0)
                    and abs(s_sub * div - step) < 0.6 * div
                    and _exclusive_slot_occupancy(ev.profile, s_sub,
                                                  ph_sub, step) >= 0.30):
                step, score, phase = s_sub, sc_sub, ph_sub
                improved = True
                break

    if is_jpeg_suspect(step):
        alts = [r for r in refined
                if not is_jpeg_suspect(r[0]) and r[1] >= 0.55 * score]
        if alts:
            alts.sort(key=lambda r: -r[1])
            step, score, phase = alts[0]
        else:
            # still on the jpeg family: deflate so the sibling channel /
            # axis with a real grid wins downstream comparisons
            score *= 0.5

    return step, score, phase


def estimate_axis_ev(ev1: _AxisEvidence, ev2: _AxisEvidence,
                     min_step: float = 2.0) -> tuple[Optional[float], float, str, float]:
    """Pick the better of edge (cut) / curvature (knot) evidence for one axis."""
    s1, sc1, ph1 = estimate_period_ev(ev1, min_step=min_step)
    s2, sc2, ph2 = estimate_period_ev(ev2, min_step=min_step)
    if s1 is None and s2 is None:
        return None, 0.0, "cut", 0.0
    if s2 is None or (s1 is not None and sc1 >= sc2):
        return s1, sc1, "cut", ph1
    return s2, sc2, "knot", ph2




def _exclusive_slot_occupancy(profile: np.ndarray, s_small: float,
                              phase_small: float, s_big: float) -> float:
    """Occupancy of s_small's lattice slots that do NOT coincide with the
    s_big lattice. Near zero when s_small is a spurious subdivision."""
    from scipy.signal import find_peaks
    norm = _normalise(profile)
    n = len(norm) - 1
    peaks, _ = find_peaks(norm[1:-1], height=0.15,
                          distance=max(1, int(s_small * 0.4)))
    p = (peaks + 1).astype(np.float64)
    if len(p) == 0:
        return 0.0
    slots = np.arange(1, int(n / s_small))
    pos = phase_small + slots * s_small
    pos = pos[(pos > 1) & (pos < n - 1)]
    # exclusive = not within 0.3*s_small of a multiple of s_big
    m = np.abs((pos / s_big) - np.round(pos / s_big)) * s_big
    excl = pos[m > 0.3 * s_small]
    if len(excl) < 3:
        return 1.0  # nothing to test
    d = np.min(np.abs(excl[:, None] - p[None, :]), axis=1)
    return float(np.mean(d < 0.3 * s_small))


def estimate_axis(e1: np.ndarray, e2: np.ndarray,
                  min_step: float = 2.0) -> tuple[Optional[float], float, str, float]:
    """Pick the better of edge/curvature periodicity for one axis.

    Returns (step, score, mode, phase) with mode in {"cut", "knot"}."""
    s1, sc1, ph1 = estimate_period(e1, min_step=min_step)
    s2, sc2, ph2 = estimate_period(e2, min_step=min_step)
    if s1 is None and s2 is None:
        return None, 0.0, "cut", 0.0
    if s2 is None or (s1 is not None and sc1 >= sc2):
        return s1, sc1, "cut", ph1
    return s2, sc2, "knot", ph2


# ------------------------------------------------------------ cut placement


def lattice_dp(profile: np.ndarray, step: float, phase: float,
               n_targets: Optional[int] = None,
               dev_ratio: float = 0.45, stiffness: float = 4.0,
               anchor: float = 0.5, margin: float = 0.35) -> np.ndarray:
    """Fixed-count lattice-anchored elastic dynamic program.

    The period estimate is sub-pixel accurate, so the number of boundaries is
    *known*: one per lattice target phase + k*step. Each is allowed to deviate
    within +-dev_ratio*step to land on real edge energy, paying quadratic
    penalties on spacing irregularity (stiffness) and on distance from its
    target (anchor). Fixing the count removes the stretch/squeeze bias a free
    chain has in flat image regions, and the DP is globally optimal.

    Returns integer positions of the interior chain elements (cuts or knots).
    """
    norm = gaussian_filter1d(_normalise(profile), sigma=0.6)
    n = len(norm) - 1
    D = max(1, int(round(step * dev_ratio)))

    lo_t, hi_t = step * margin, n - step * margin
    k0 = int(np.ceil((lo_t - phase) / step))
    k1 = int(np.floor((hi_t - phase) / step))
    if k1 < k0:
        return np.array([], dtype=int)
    targets = phase + np.arange(k0, k1 + 1) * step

    # the period estimate is accurate, so the boundary count is known;
    # phase noise must not add or drop a cell
    if n_targets is not None and n_targets >= 1:
        while len(targets) > n_targets:
            targets = targets[1:] if targets[0] < n - targets[-1] else targets[:-1]
        while len(targets) < n_targets:
            lo_gap = targets[0]
            hi_gap = n - targets[-1]
            if lo_gap >= hi_gap:
                targets = np.concatenate([[targets[0] - step], targets])
            else:
                targets = np.concatenate([targets, [targets[-1] + step]])
        targets = np.clip(targets, 1.0, n - 1.0)
    K = len(targets)

    wins = []
    for t in targets:
        c = int(round(t))
        wins.append(np.arange(max(1, c - D), min(n - 1, c + D) + 1))

    NEG = -1e18
    prev_score = None
    parents: list = []
    for k in range(K):
        win = wins[k]
        gain = norm[win] - anchor * ((win - targets[k]) / step) ** 2
        if k == 0:
            score = gain
            parents.append(None)
        else:
            pw = wins[k - 1]
            d = (win[:, None] - pw[None, :] - step) / step
            trans = prev_score[None, :] - stiffness * d ** 2
            trans = np.where(win[:, None] > pw[None, :], trans, NEG)
            bi = np.argmax(trans, axis=1)
            score = trans[np.arange(len(win)), bi] + gain
            parents.append(bi)
        prev_score = score

    out = np.empty(K, dtype=int)
    j = int(np.argmax(prev_score))
    for k in range(K - 1, -1, -1):
        out[k] = wins[k][j]
        if parents[k] is not None:
            j = int(parents[k][j])
    return out



def _axis_chain(profile: np.ndarray, step: float, phase: float,
                extent: int, mode: str) -> np.ndarray:
    """DP chain for one axis, resolving ambiguous cell counts.

    When extent/step falls near a half cell, both candidate counts are tried
    and the chain whose cuts capture more edge energy wins."""
    r = extent / step
    base = int(round(r))
    counts = {base, base - 1, base + 1}
    if abs(r - base) > 0.15:
        counts.update({base - 2, base + 2})
    counts = {c for c in counts if c >= 1}

    norm = gaussian_filter1d(_normalise(profile), sigma=0.6)
    best_chain, best_e = None, -1e18
    for c in sorted(counts):
        nt = (c - 1) if mode == "cut" else c
        if nt < 1:
            chain = np.array([], dtype=int)
            e = 0.0
        else:
            s = extent / c
            chain = lattice_dp(profile, s, phase, n_targets=nt)
            e = float(norm[chain].mean()) if len(chain) else -1e18
            e -= 0.08 * abs(c - r)   # stay close to the step-implied count
            frac_int = abs(extent / c - round(extent / c))
            e += 0.06 * max(0.0, 1.0 - 4.0 * frac_int)  # integer steps common
            if len(chain) > 3:
                # true grids have regular spacing even when they drift;
                # wrong counts force irregular squeezes
                sp = np.diff(chain)
                e -= 0.30 * float(sp.std()) / max(float(sp.mean()), 1e-9)
        if e > best_e:
            best_e, best_chain = e, chain
    return best_chain if best_chain is not None else np.array([], dtype=int)


def chain_to_cuts(positions: np.ndarray, extent: int, mode: str) -> np.ndarray:
    """Interior chain positions -> full cut array [0 .. extent]."""
    if mode == "knot":
        if len(positions) < 2:
            return np.array([0, extent], dtype=int)
        mids = (positions[:-1] + positions[1:] + 1) // 2
        cuts = np.concatenate([[0], mids, [extent]])
    else:
        cuts = np.concatenate([[0], positions, [extent]])
    return np.unique(np.clip(cuts, 0, extent))


# ----------------------------------------------------------- warp refinement


def band_profiles(rgba: np.ndarray, n_bands: int, axis: int, kind: str) -> np.ndarray:
    """Edge/curvature profiles restricted to bands.

    axis=0 -> vertical cuts scored within horizontal row-bands: (n_bands, W+1)
    kind: "cut" uses first differences, "knot" second differences."""
    img = _flatten_channels(rgba)
    h, w = img.shape[:2]
    if axis == 0:
        if kind == "cut":
            d = np.sqrt(((img[:, 1:, :] - img[:, :-1, :]) ** 2).sum(axis=2))
            m = w + 1
            off = 1
        else:
            d = np.sqrt(((img[:, 2:, :] - 2 * img[:, 1:-1, :] + img[:, :-2, :]) ** 2).sum(axis=2))
            m = w
            off = 1
        bounds = np.linspace(0, h, n_bands + 1).astype(int)
        out = np.zeros((n_bands, m))
        for b in range(n_bands):
            seg = d[bounds[b]:bounds[b + 1]]
            out[b, off:off + seg.shape[1]] = seg.sum(axis=0)
    else:
        if kind == "cut":
            d = np.sqrt(((img[1:, :, :] - img[:-1, :, :]) ** 2).sum(axis=2))
            m = h + 1
            off = 1
        else:
            d = np.sqrt(((img[2:, :, :] - 2 * img[1:-1, :, :] + img[:-2, :, :]) ** 2).sum(axis=2))
            m = h
            off = 1
        bounds = np.linspace(0, w, n_bands + 1).astype(int)
        out = np.zeros((n_bands, m))
        for b in range(n_bands):
            seg = d[:, bounds[b]:bounds[b + 1]]
            out[b, off:off + seg.shape[0]] = seg.sum(axis=1)
    return out


def refine_positions_per_band(bands: np.ndarray, positions: np.ndarray,
                              step: float, dev_ratio: float = 0.4,
                              prior: float = 3.0, smooth: float = 4.0,
                              n_iters: int = 3) -> np.ndarray:
    """Let each position drift per band to follow warped boundaries.

    bands: (B, L) profiles; positions: (K,) global positions.
    Returns (B, K) float positions."""
    n_bands = bands.shape[0]
    L = bands.shape[1]
    norm = np.stack([gaussian_filter1d(_normalise(bp), 0.8) for bp in bands])

    pos = np.tile(positions.astype(np.float64), (n_bands, 1))
    dev = max(1, int(round(step * dev_ratio)))

    for _ in range(n_iters):
        for b in range(n_bands):
            for k in range(len(positions)):
                c = int(positions[k])
                lo = max(0, c - dev)
                hi = min(L - 1, c + dev)
                if hi <= lo:
                    continue
                cand = np.arange(lo, hi + 1)
                score = norm[b, cand].astype(np.float64)
                score -= prior * ((cand - c) / step) ** 2
                neigh = []
                if b > 0:
                    neigh.append(pos[b - 1, k])
                if b < n_bands - 1:
                    neigh.append(pos[b + 1, k])
                if neigh:
                    m = float(np.mean(neigh))
                    score -= smooth * ((cand - m) / step) ** 2
                pos[b, k] = cand[int(np.argmax(score))]
        pos = np.maximum.accumulate(pos, axis=1)
    return pos


# ----------------------------------------------------------------- grid fit


@dataclass
class GridFit:
    width: int
    height: int
    cols: int
    rows: int
    step_x: float
    step_y: float
    score_x: float
    score_y: float
    mode_x: str
    mode_y: str
    is_periodic: bool
    xcuts: np.ndarray = field(repr=False)      # (cols+1, H) float
    ycuts: np.ndarray = field(repr=False)      # (rows+1, W) float
    col_index: np.ndarray = field(repr=False)  # (H, W) int32
    row_index: np.ndarray = field(repr=False)  # (H, W) int32

    @property
    def cell_index(self) -> np.ndarray:
        return self.row_index.astype(np.int32) * self.cols + self.col_index


def _rasterise_cuts(band_pos: np.ndarray, extent: int, length: int) -> np.ndarray:
    """(n_bands, n_cuts) control points -> (n_cuts, length) scanline positions."""
    n_bands, n_cuts = band_pos.shape
    if n_bands == 1:
        out = np.tile(band_pos[0][:, None], (1, length)).astype(np.float32)
    else:
        edges = np.linspace(0, length, n_bands + 1)
        centers = (edges[:-1] + edges[1:]) / 2.0
        ys = np.arange(length, dtype=np.float64)
        out = np.empty((n_cuts, length), dtype=np.float32)
        for k in range(n_cuts):
            out[k] = np.interp(ys, centers, band_pos[:, k])
    out = np.maximum.accumulate(out, axis=0)
    out[0] = 0.0
    out[-1] = extent
    return out


def _index_map_from_cuts(cuts_per_line: np.ndarray, extent: int) -> np.ndarray:
    """(n_cuts, L) cut positions -> (L, extent) int32 cell indices."""
    n_cuts, L = cuts_per_line.shape
    coords = np.arange(extent, dtype=np.float32) + 0.5
    out = np.empty((L, extent), dtype=np.int32)
    for i in range(L):
        idx = np.searchsorted(cuts_per_line[:, i], coords, side="right") - 1
        out[i] = np.clip(idx, 0, n_cuts - 2)
    return out


def _knot_cuts_per_band(band_knots: np.ndarray, extent: int) -> np.ndarray:
    """(B, K) knot positions -> (B, K+1) cut positions (midpoints + borders)."""
    B, K = band_knots.shape
    cuts = np.empty((B, K + 1))
    cuts[:, 0] = 0.0
    cuts[:, -1] = extent
    if K > 1:
        cuts[:, 1:-1] = (band_knots[:, :-1] + band_knots[:, 1:]) / 2.0 + 0.5
    return cuts


def fit_grid(rgba: np.ndarray,
             target_cells: Optional[int] = None,
             force_step: Optional[float] = None,
             allow_warp: bool = True,
             min_score: float = 2.2,
             max_output: int = 512,
             quantized: Optional[np.ndarray] = None,
             quantize_colors: int = 16) -> GridFit:
    """Fit a pixel grid to an image. rgba: uint8 (H, W, 3|4).

    Edge (E1) profiles are computed on a color-quantized copy - like
    pixel-snapper, this turns anti-aliased ramps into hard steps at the true
    boundaries. Curvature (E2) profiles use the original image, whose
    interpolation knots sit at cell centres.
    """
    h, w = rgba.shape[:2]

    if quantized is None:
        from .quantize import kmeans_quantize
        # median denoise before quantizing: kills jpeg/gaussian noise while
        # keeping the step edges the profiles depend on
        try:
            import cv2
            base = cv2.medianBlur(np.ascontiguousarray(rgba), 3)
        except Exception:
            base = rgba
        quantized, _, _ = kmeans_quantize(base, k=quantize_colors)

    prof_q = axis_profiles(quantized)   # E1 source
    prof_o = axis_profiles(rgba)        # E2 source
    prof = {"e1x": prof_q["e1x"], "e1y": prof_q["e1y"],
            "e2x": prof_o["e2x"], "e2y": prof_o["e2y"]}

    mode_x = mode_y = "cut"
    phase_x = phase_y = 0.0
    if force_step is not None:
        step_x = step_y = float(force_step)
        score_x = score_y = float("inf")
    else:
        # jpeg contamination: if the phase-0 8px block lattice is present,
        # notch it out of every profile before any scoring (a true 4/8 px art
        # grid survives via the lattice slots jpeg doesn't own)
        jpeg_z = max(_jpeg_lattice_strength(prof["e1x"]),
                     _jpeg_lattice_strength(prof["e1y"]))
        if jpeg_z > 5.0:
            for key in prof:
                prof[key] = _notch_jpeg(prof[key])

        # multi-band evidence: warped images stay phase-coherent within a
        # band even when global coherence is gone
        n_bands_est_y = 4 if h >= 200 else (2 if h >= 96 else 0)
        n_bands_est_x = 4 if w >= 200 else (2 if w >= 96 else 0)
        bx1 = band_profiles(quantized, n_bands_est_y, axis=0, kind="cut") if n_bands_est_y else None
        bx2 = band_profiles(rgba, n_bands_est_y, axis=0, kind="knot") if n_bands_est_y else None
        by1 = band_profiles(quantized, n_bands_est_x, axis=1, kind="cut") if n_bands_est_x else None
        by2 = band_profiles(rgba, n_bands_est_x, axis=1, kind="knot") if n_bands_est_x else None
        if jpeg_z > 5.0:
            for bands in (bx1, bx2, by1, by2):
                if bands is not None:
                    for i in range(bands.shape[0]):
                        bands[i] = _notch_jpeg(bands[i])

        # square-packer channel: within-cell variance contrast (see
        # varcontrast.py) - carries mushy/lumpy art where edges fail
        from .varcontrast import CellVarContrast
        vc = CellVarContrast(rgba)
        vc_z, vc_cands = vc.z_channel()
        vc_steps = [s for s, _z in vc_cands]

        gm = _grad_maps(rgba, quantized)
        tiles_x1 = _tile_peaks(gm["dqx"], axis=0)
        tiles_x2 = _tile_peaks(gm["cox"], axis=0)
        tiles_y1 = _tile_peaks(gm["dqy"], axis=1)
        tiles_y2 = _tile_peaks(gm["coy"], axis=1)

        spec_x1 = _axis_spectrum([gm["dqx"]], axis=0)
        spec_x2 = _axis_spectrum([gm["cox"]], axis=0)
        spec_y1 = _axis_spectrum([gm["dqy"]], axis=1)
        spec_y2 = _axis_spectrum([gm["coy"]], axis=1)

        # vc feeds candidates only: its square assumption must not poison
        # per-axis scoring of genuinely non-square grids
        ev_x1 = _AxisEvidence(prof["e1x"], bx1, tiles_x1, spec_x1,
                              None, vc_steps)
        ev_x2 = _AxisEvidence(prof["e2x"], bx2, tiles_x2, spec_x2,
                              None, vc_steps)
        ev_y1 = _AxisEvidence(prof["e1y"], by1, tiles_y1, spec_y1,
                              None, vc_steps)
        ev_y2 = _AxisEvidence(prof["e2y"], by2, tiles_y2, spec_y2,
                              None, vc_steps)

        step_x, score_x, mode_x, phase_x = estimate_axis_ev(ev_x1, ev_x2)
        step_y, score_y, mode_y, phase_y = estimate_axis_ev(ev_y1, ev_y2)

        if step_x is not None and score_x < min_score:
            step_x = None
        if step_y is not None and score_y < min_score:
            step_y = None

        # borrow the sibling axis when one fails, but re-derive the phase on
        # this axis's own profile
        if step_x is None and step_y is not None:
            step_x, mode_x = step_y, mode_y
            score_x = score_y
            ev = ev_x1 if mode_x == "cut" else ev_x2
            phase_x = ev.score(step_x)[1]
        elif step_y is None and step_x is not None:
            step_y, mode_y = step_x, mode_x
            score_y = score_x
            ev = ev_y1 if mode_y == "cut" else ev_y2
            phase_y = ev.score(step_y)[1]

        # harmonic reconciliation across axes: if one axis locked onto ~2x or
        # ~3x the other's step, test the smaller step on the larger axis
        if step_x is not None and step_y is not None:
            for a, b in ((0, 1), (1, 0)):
                s_small = (step_x, step_y)[a]
                s_large = (step_x, step_y)[b]
                if s_large > s_small * 1.5:
                    mult = s_large / s_small
                    if abs(mult - round(mult)) < 0.12 * mult and round(mult) in (2, 3):
                        if b == 0:
                            ev = ev_x1 if mode_x == "cut" else ev_x2
                        else:
                            ev = ev_y1 if mode_y == "cut" else ev_y2
                        sc, ph = ev.score(s_small)
                        if sc >= 0.55 * (score_x, score_y)[b]:
                            if b == 0:
                                step_x, phase_x = s_small, ph
                            else:
                                step_y, phase_y = s_small, ph

        # square-pixel prior (mild disagreement): adopt the stronger axis's
        # step on the weaker axis when it holds up there
        if step_x is not None and step_y is not None:
            ratio = max(step_x, step_y) / min(step_x, step_y)
            if 1.04 < ratio <= 1.35:
                if score_x >= score_y:
                    s, evs = step_x, (ev_y1, ev_y2)
                else:
                    s, evs = step_y, (ev_x1, ev_x2)
                best = (-1e18, 0.0, "cut")
                for ev, m in zip(evs, ("cut", "knot")):
                    sc, ph = ev.score(s)
                    if sc > best[0]:
                        best = (sc, ph, m)
                if score_x >= score_y and best[0] >= 0.45 * score_y:
                    step_y, score_y, phase_y, mode_y = s, best[0], best[1], best[2]
                elif score_y > score_x and best[0] >= 0.45 * score_x:
                    step_x, score_x, phase_x, mode_x = s, best[0], best[1], best[2]

        # square-pixel prior: pixels are almost always square, so when the
        # axes wildly disagree (and are not integer multiples), test each
        # axis's step on the other and keep the consistent pair
        if step_x is not None and step_y is not None:
            ratio = max(step_x, step_y) / min(step_x, step_y)
            near_int = abs(ratio - round(ratio)) < 0.12 * ratio
            if ratio > 1.35 and not near_int:
                evs_x = (ev_x1, ev_x2)
                evs_y = (ev_y1, ev_y2)
                def _best_on(evs, s):
                    out = (-1e18, 0.0, "cut")
                    for ev, m in zip(evs, ("cut", "knot")):
                        sc, ph = ev.score(s)
                        if sc > out[0]:
                            out = (sc, ph, m)
                    return out
                x_on_y = _best_on(evs_y, step_x)
                y_on_x = _best_on(evs_x, step_y)
                keep = score_x + score_y
                use_x = score_x + x_on_y[0]
                use_y = score_y + y_on_x[0]
                best = max(keep, use_x, use_y)
                if best == use_x and use_x > keep * 1.1:
                    step_y = step_x
                    score_y, phase_y, mode_y = x_on_y
                elif best == use_y and use_y > keep * 1.1:
                    step_x = step_y
                    score_x, phase_x, mode_x = y_on_x

        # cross-axis jpeg arbitration: if exactly one axis sits on the jpeg
        # block lattice, try the other axis's step on it
        if step_x is not None and step_y is not None:
            jx = is_jpeg_lattice(step_x, phase_x)
            jy = is_jpeg_lattice(step_y, phase_y)
            if jx != jy:
                if jx:
                    evs, own_score = (ev_x1, ev_x2), score_x
                    s_alt = step_y
                else:
                    evs, own_score = (ev_y1, ev_y2), score_y
                    s_alt = step_x
                best = (None, -1e18, 0.0, "cut")
                for ev, m in zip(evs, ("cut", "knot")):
                    sc, ph = ev.score(s_alt)
                    if sc > best[1]:
                        best = (s_alt, sc, ph, m)
                if best[1] >= 0.45 * own_score:
                    if jx:
                        step_x, score_x, phase_x, mode_x = best
                    else:
                        step_y, score_y, phase_y, mode_y = best

        # square-packer fallback: when profile channels see nothing but the
        # cell-variance channel has a confident square candidate, take it
        vc_best = vc_cands[0] if vc_cands else None
        if vc_best is not None and vc_best[1] >= 5.0:
            if step_x is None or score_x < min_score:
                if step_y is None or score_y < min_score:
                    step_x = step_y = vc_best[0]
                    score_x = score_y = vc_best[1]
                    _c, phase_x, phase_y = vc.contrast(vc_best[0],
                                                       n_phases=12)
                    mode_x = mode_y = "cut"

        # pair arbitration: the 2D cell-variance contrast picks between the
        # per-axis winners, square vc candidates, and swaps
        if step_x is not None and step_y is not None:
            pairs = {(round(step_x, 3), round(step_y, 3))}
            pairs.add((round(step_x, 3), round(step_x, 3)))
            pairs.add((round(step_y, 3), round(step_y, 3)))
            axis_weak = max(score_x, score_y) < 6.0
            for s, z in (vc_cands or [])[:2]:
                if z >= (3.0 if axis_weak else 6.0):
                    pairs.add((round(s, 3), round(s, 3)))
            if len(pairs) > 1:
                ranked = vc.best_pair(sorted(pairs))
                # tiny steps only when the profile channels saw nothing
                if not axis_weak:
                    ranked = [r for r in ranked
                              if min(r[0], r[1]) >= 3.0
                              or (r[0], r[1]) == (round(step_x, 3), round(step_y, 3))]
                bx_, by_, bq = ranked[0]
                cur_q = next(q for sx_, sy_, q in ranked
                             if (sx_, sy_) == (round(step_x, 3), round(step_y, 3)))
                # switch only on clear dominance to avoid churn on noise
                if bq > max(cur_q * 2.0, cur_q + 0.05) and bq > 0.05:
                    if abs(bx_ - step_x) > 1e-6:
                        ev = ev_x1 if mode_x == "cut" else ev_x2
                        sc, ph = ev.score(bx_)
                        step_x, phase_x = bx_, ph
                        score_x = max(sc, score_x * 0.8)
                    if abs(by_ - step_y) > 1e-6:
                        ev = ev_y1 if mode_y == "cut" else ev_y2
                        sc, ph = ev.score(by_)
                        step_y, phase_y = by_, ph
                        score_y = max(sc, score_y * 0.8)

    is_periodic = step_x is not None

    if not is_periodic:
        target = target_cells or 128
        step = max(1.0, max(w, h) / float(target))
        step_x = step_y = step
        phase_x = phase_y = 0.0

    if step_x <= 1.05 and step_y <= 1.05:
        xcuts = np.tile(np.arange(w + 1, dtype=np.float32)[:, None], (1, h))
        ycuts = np.tile(np.arange(h + 1, dtype=np.float32)[:, None], (1, w))
        col_index = np.tile(np.arange(w, dtype=np.int32), (h, 1))
        row_index = np.tile(np.arange(h, dtype=np.int32)[:, None], (1, w))
        return GridFit(w, h, w, h, 1.0, 1.0, score_x, score_y,
                       mode_x, mode_y, is_periodic,
                       xcuts, ycuts, col_index, row_index)

    step_x = max(step_x, w / float(max_output))
    step_y = max(step_y, h / float(max_output))

    # global chains on the mode-appropriate profiles
    px = prof["e1x"] if mode_x == "cut" else prof["e2x"]
    py = prof["e1y"] if mode_y == "cut" else prof["e2y"]

    col_chain = _axis_chain(px, step_x, phase_x, w, mode_x)
    row_chain = _axis_chain(py, step_y, phase_y, h, mode_y)

    n_bands_y = int(np.clip(h / (step_y * 8), 1, 12)) if allow_warp and is_periodic else 1
    n_bands_x = int(np.clip(w / (step_x * 8), 1, 12)) if allow_warp and is_periodic else 1

    # columns (vertical cuts)
    if n_bands_y > 1 and len(col_chain):
        bp = band_profiles(quantized if mode_x == "cut" else rgba,
                           n_bands_y, axis=0, kind=mode_x)
        band_chain = refine_positions_per_band(bp, col_chain, step_x)
    else:
        band_chain = col_chain[None, :].astype(np.float64)
    if mode_x == "cut":
        band_cuts = np.concatenate([
            np.zeros((band_chain.shape[0], 1)), band_chain,
            np.full((band_chain.shape[0], 1), w)], axis=1)
    else:
        band_cuts = _knot_cuts_per_band(band_chain, w)
    xcuts = _rasterise_cuts(band_cuts, w, h)

    # rows (horizontal cuts)
    if n_bands_x > 1 and len(row_chain):
        bp = band_profiles(quantized if mode_y == "cut" else rgba,
                           n_bands_x, axis=1, kind=mode_y)
        band_chain = refine_positions_per_band(bp, row_chain, step_y)
    else:
        band_chain = row_chain[None, :].astype(np.float64)
    if mode_y == "cut":
        band_cuts = np.concatenate([
            np.zeros((band_chain.shape[0], 1)), band_chain,
            np.full((band_chain.shape[0], 1), h)], axis=1)
    else:
        band_cuts = _knot_cuts_per_band(band_chain, h)
    ycuts = _rasterise_cuts(band_cuts, h, w)

    col_index = _index_map_from_cuts(xcuts, w)
    row_index = _index_map_from_cuts(ycuts, h).T

    cols = xcuts.shape[0] - 1
    rows = ycuts.shape[0] - 1

    return GridFit(w, h, cols, rows, float(step_x), float(step_y),
                   float(score_x), float(score_y), mode_x, mode_y,
                   is_periodic, xcuts, ycuts, col_index, row_index)


# ------------------------------------------------------------------ overlay


def render_grid_overlay(rgba: np.ndarray, grid: GridFit,
                        color=(255, 40, 220), alpha: float = 0.85) -> np.ndarray:
    """Draw the fitted cut polylines onto a copy of the image."""
    out = rgba[:, :, :3].astype(np.float32).copy()
    h, w = out.shape[:2]
    col = np.array(color, dtype=np.float32)

    ys = np.arange(h)
    for k in range(grid.xcuts.shape[0]):
        xs = np.clip(np.round(grid.xcuts[k]).astype(int), 0, w - 1)
        out[ys, xs] = (1 - alpha) * out[ys, xs] + alpha * col
    xs_all = np.arange(w)
    for k in range(grid.ycuts.shape[0]):
        ys2 = np.clip(np.round(grid.ycuts[k]).astype(int), 0, h - 1)
        out[ys2, xs_all] = (1 - alpha) * out[ys2, xs_all] + alpha * col

    result = np.dstack([np.clip(out, 0, 255).astype(np.uint8),
                        np.full((h, w), 255, np.uint8)])
    return result
