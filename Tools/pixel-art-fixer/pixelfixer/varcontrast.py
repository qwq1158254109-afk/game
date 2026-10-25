"""Variance-contrast grid scoring - the "square packer" channel.

Idea (from Kopf et al.'s content-adaptive kernels, turned into a detector):
a pixel grid is correct when the image decomposes into cells that are each
internally color-homogeneous. Measure, for a candidate cell size s, the mean
within-cell color variance at the BEST grid phase versus the WORST phase:

  contrast(s) = (var_worst_phase(s) - var_best_phase(s)) / total_variance

* true cell size: best phase aligns cells with pseudo-pixels (low variance),
  worst phase makes every cell straddle boundaries (high variance)
  -> large contrast
* half the true size (sub-harmonic): every phase nests inside pseudo-pixels
  -> contrast ~ 0
* multiples / junk sizes: phase barely matters -> contrast small

So the contrast curve peaks at the FUNDAMENTAL, needs no edges (works on
mushy, lumpy AI art where gradient profiles fail), and yields the grid phase
for free. Summed-area tables make each evaluation O(number of cells).

The measure must use true 2D cells: full-height strips dilute the signal
below noise (a strip's variance is dominated by content along the other
axis). 2D summed-area tables give O(1) per-cell moments, so one grid
evaluation costs O(number of cells).
"""

from __future__ import annotations

import numpy as np


def _axis_moments(rgba: np.ndarray, axis: int) -> tuple[np.ndarray, np.ndarray, int]:
    """Per-scanline color moments collapsed along the other axis.

    Returns (S1, S2, count_per_line):
      S1: (L+1, C) prefix sums of per-line channel sums
      S2: (L+1,)   prefix sums of per-line total squared values
    where L is the length of `axis`.
    """
    img = rgba.astype(np.float64)
    if img.shape[2] == 4:
        a = img[:, :, 3:4] / 255.0
        img = np.concatenate([img[:, :, :3] * a, img[:, :, 3:4]], axis=2)
    if axis == 1:
        img = np.transpose(img, (1, 0, 2))
    # img now (other, L, C); collapse `other`
    line_sum = img.sum(axis=0)                    # (L, C)
    line_sq = (img ** 2).sum(axis=(0, 2))         # (L,)
    count = img.shape[0]                          # pixels per line
    S1 = np.zeros((line_sum.shape[0] + 1, line_sum.shape[1]))
    S1[1:] = np.cumsum(line_sum, axis=0)
    S2 = np.zeros(line_sq.shape[0] + 1)
    S2[1:] = np.cumsum(line_sq)
    return S1, S2, count


def _strip_variance(S1: np.ndarray, S2: np.ndarray, count: int,
                    cuts: np.ndarray) -> float:
    """Mean within-strip variance (weighted by strip size) for strips
    bounded by fractional cut positions. Linear interpolation of the
    prefix sums handles fractional cuts."""
    L = S2.shape[0] - 1
    idx = np.clip(cuts, 0.0, L)
    i0 = np.floor(idx).astype(int)
    frac = idx - i0
    i0c = np.minimum(i0 + 1, L)
    s1 = S1[i0] * (1 - frac[:, None]) + S1[i0c] * frac[:, None]   # (K+1, C)
    s2 = S2[i0] * (1 - frac) + S2[i0c] * frac                     # (K+1,)

    d1 = s1[1:] - s1[:-1]          # per-strip channel sums
    d2 = s2[1:] - s2[:-1]          # per-strip squared sums
    widths = idx[1:] - idx[:-1]
    n = widths * count             # pixels per strip
    ok = n > 1e-9
    if not ok.any():
        return 0.0
    d1, d2, n = d1[ok], d2[ok], n[ok]
    mean_sq = (d1 ** 2).sum(axis=1) / (n ** 2)
    var = d2 / n - mean_sq
    return float((var * n).sum() / n.sum())


def _grid_cuts(length: float, step: float, phase: float) -> np.ndarray:
    first = phase % step
    if first > 1e-9:
        cuts = np.concatenate([[0.0], np.arange(first, length, step), [length]])
    else:
        cuts = np.concatenate([np.arange(0.0, length, step), [length]])
    return np.unique(np.clip(cuts, 0, length))


class VarContrast:
    """Axis-separable variance-contrast scorer for one image."""

    def __init__(self, rgba: np.ndarray):
        self.S1x, self.S2x, self.cx = _axis_moments(rgba, axis=0)
        self.S1y, self.S2y, self.cy = _axis_moments(rgba, axis=1)
        self.W = self.S2x.shape[0] - 1
        self.H = self.S2y.shape[0] - 1
        # total variance per axis for normalisation
        self.total_x = self._var_x(np.array([0.0, float(self.W)]))
        self.total_y = self._var_y(np.array([0.0, float(self.H)]))

    def _var_x(self, cuts):
        return _strip_variance(self.S1x, self.S2x, self.cx, cuts)

    def _var_y(self, cuts):
        return _strip_variance(self.S1y, self.S2y, self.cy, cuts)

    def contrast(self, axis: int, step: float, n_phases: int = 6):
        """(contrast, best_phase) at a candidate step for one axis."""
        L = self.W if axis == 0 else self.H
        total = self.total_x if axis == 0 else self.total_y
        var_fn = self._var_x if axis == 0 else self._var_y
        if step < 1.5 or step > L / 3 or total <= 1e-9:
            return 0.0, 0.0
        phases = np.arange(n_phases) * (step / n_phases)
        vs = np.array([var_fn(_grid_cuts(L, step, p)) for p in phases])
        best = int(np.argmin(vs))
        return float((vs.max() - vs.min()) / total), float(phases[best])

    def curve(self, axis: int, steps: np.ndarray, n_phases: int = 6):
        return np.array([self.contrast(axis, s, n_phases)[0] for s in steps])

    def refine(self, axis: int, step: float, span: float = 0.6,
               n_phases: int = 8) -> tuple[float, float, float]:
        """Parabolic-ish local refinement -> (step, contrast, phase)."""
        candidates = np.linspace(max(1.6, step - span), step + span, 13)
        cs = self.curve(axis, candidates, n_phases)
        i = int(np.argmax(cs))
        # second, finer pass
        lo = candidates[max(0, i - 1)]
        hi = candidates[min(len(candidates) - 1, i + 1)]
        fine = np.linspace(lo, hi, 9)
        cf = self.curve(axis, fine, n_phases)
        j = int(np.argmax(cf))
        c, ph = self.contrast(axis, float(fine[j]), n_phases=12)
        return float(fine[j]), c, ph

    def candidates(self, axis: int, min_step: float = 2.0,
                   max_step: float = None, top: int = 5) -> list[tuple[float, float, float]]:
        """Scan the contrast curve, return refined (step, contrast, phase)
        for its local maxima."""
        L = self.W if axis == 0 else self.H
        if max_step is None:
            max_step = min(max(4.0, L / 8.0), 64.0)
        steps = []
        s = min_step
        while s <= max_step:
            steps.append(s)
            s *= 1.04
        steps = np.array(steps)
        cs = self.curve(axis, steps, n_phases=5)
        # local maxima of the coarse curve
        cand_idx = [i for i in range(len(steps))
                    if cs[i] > 0.01
                    and cs[i] >= (cs[i - 1] if i > 0 else -1)
                    and cs[i] >= (cs[i + 1] if i < len(steps) - 1 else -1)]
        cand_idx.sort(key=lambda i: -cs[i])
        out = []
        seen = []
        for i in cand_idx[:top * 2]:
            s0 = float(steps[i])
            if any(abs(s0 - s1) / s1 < 0.08 for s1 in seen):
                continue
            seen.append(s0)
            out.append(self.refine(axis, s0))
            if len(out) >= top:
                break
        out.sort(key=lambda r: -r[1])
        return out


# ---------------------------------------------------------------- 2D cells


class CellVarContrast:
    """2D within-cell variance contrast scorer (local-phase, activity-aware).

    Two properties make this work on real AI sheets:
    * activity weighting - only cells covering "active" image regions vote,
      so flat backgrounds cannot dilute the signal;
    * local phase - active cells are grouped into coarse tiles and each tile
      picks its own best/worst grid phase (same step), so sprite sheets and
      warped grids, whose pseudo-pixel phase drifts between regions, still
      produce a sharp contrast at the true cell size.

    contrast(s) = (worst - best) / (best + 0.05 * total) where best/worst are
    activity-weighted means of per-tile extreme within-cell variances. The
    flatness normalisation suppresses content-scale periodicity (big cells
    are never flat inside).
    """

    def __init__(self, rgba: np.ndarray, max_points: int = 2600,
                 tile_px: int = 112):
        img = rgba.astype(np.float64)
        if img.shape[2] == 4:
            a = img[:, :, 3:4] / 255.0
            img = np.concatenate([img[:, :, :3] * a, img[:, :, 3:4]], axis=2)
        h, w = img.shape[:2]
        self.H, self.W = h, w
        C = img.shape[2]
        self.S1 = np.zeros((h + 1, w + 1, C))
        self.S1[1:, 1:] = img.cumsum(axis=0).cumsum(axis=1)
        sq = (img ** 2).sum(axis=2)
        self.S2 = np.zeros((h + 1, w + 1))
        self.S2[1:, 1:] = sq.cumsum(axis=0).cumsum(axis=1)
        # fused SAT (channels + sqsum) flattened for single-gather sampling
        self.SC = np.concatenate([self.S1, self.S2[:, :, None]], axis=2)
        self.SCflat = self.SC.reshape(-1, C + 1)
        self.C = C

        n = h * w
        tot_mean = self.S1[-1, -1] / n
        self.total_var = float(self.S2[-1, -1] / n - (tot_mean ** 2).sum())

        # --- activity map: variance of 8x8 blocks
        bs = 8
        by = np.arange(0, h - bs + 1, bs)
        bx = np.arange(0, w - bs + 1, bs)
        if len(by) == 0 or len(bx) == 0:
            by = np.array([0]); bx = np.array([0]); bs = min(h, w)
        s1 = self._rect_sum(self.S1, by, bx, bs)
        s2 = self._rect_sum(self.S2, by, bx, bs)
        area = float(bs * bs)
        bvar = s2 / area - ((s1 / area) ** 2).sum(axis=-1)
        thresh = max(1e-6, 0.02 * self.total_var)
        ys, xs = np.nonzero(bvar > thresh)
        if len(ys) < 8:   # fallback: everything is active
            ys, xs = np.nonzero(bvar >= 0)
        px = bx[xs] + bs / 2.0
        py = by[ys] + bs / 2.0
        if len(px) > max_points:
            idx = np.linspace(0, len(px) - 1, max_points).astype(int)
            order = np.argsort(py[idx] * w + px[idx])
            px, py = px[idx][order], py[idx][order]
        self.px, self.py = px.astype(np.float64), py.astype(np.float64)

        # tile grouping for local phase
        self.tile_id = ((self.py // tile_px).astype(np.int64) * 64
                        + (self.px // tile_px).astype(np.int64))
        _, self.tile_id = np.unique(self.tile_id, return_inverse=True)
        self.n_tiles = int(self.tile_id.max()) + 1
        # activity-region variance for normalisation: mean block var of
        # active blocks
        self.active_var = float(bvar[bvar > thresh].mean()) \
            if (bvar > thresh).any() else self.total_var

    @staticmethod
    def _rect_sum(S, ys, xs, size):
        """Block sums of `size` starting at integer offsets ys, xs."""
        a = S[ys][:, xs]
        b = S[ys][:, xs + size]
        c = S[ys + size][:, xs]
        d = S[ys + size][:, xs + size]
        return d - b - c + a

    def _sample_sat(self, S, pos_y, pos_x):
        """Bilinear SAT sample at fractional (pos_y, pos_x) 1D arrays."""
        iy = np.clip(np.floor(pos_y).astype(int), 0, self.H)
        fy = np.clip(pos_y - iy, 0, 1)
        iy1 = np.minimum(iy + 1, self.H)
        ix = np.clip(np.floor(pos_x).astype(int), 0, self.W)
        fx = np.clip(pos_x - ix, 0, 1)
        ix1 = np.minimum(ix + 1, self.W)
        v00 = S[iy, ix]; v01 = S[iy, ix1]
        v10 = S[iy1, ix]; v11 = S[iy1, ix1]
        if S.ndim == 3:
            fy = fy[:, None]; fx = fx[:, None]
        return (v00 * (1 - fy) * (1 - fx) + v01 * (1 - fy) * fx +
                v10 * fy * (1 - fx) + v11 * fy * fx)

    def _cells_variance(self, step_x, step_y, phase_x, phase_y):
        """Within-cell variance of the cell covering each active point.

        Cell corners are snapped to integers so each corner needs a single
        fused-SAT gather (4 gathers per cell total) - ~6x faster than the
        bilinear path with negligible effect on the score curves."""
        x0 = phase_x + np.floor((self.px - phase_x) / step_x) * step_x
        y0 = phase_y + np.floor((self.py - phase_y) / step_y) * step_y
        ix0 = np.clip(np.rint(x0), 0, self.W - 1).astype(np.int64)
        iy0 = np.clip(np.rint(y0), 0, self.H - 1).astype(np.int64)
        ix1 = np.clip(np.rint(x0 + step_x), ix0 + 1, self.W).astype(np.int64)
        iy1 = np.clip(np.rint(y0 + step_y), iy0 + 1, self.H).astype(np.int64)

        W1 = self.W + 1
        f = self.SCflat
        m = (f.take(iy1 * W1 + ix1, axis=0) - f.take(iy1 * W1 + ix0, axis=0)
             - f.take(iy0 * W1 + ix1, axis=0) + f.take(iy0 * W1 + ix0, axis=0))
        area = ((ix1 - ix0) * (iy1 - iy0)).astype(np.float64)
        s1 = m[:, :self.C]
        s2 = m[:, self.C]
        var = s2 / area - ((s1 / area[:, None]) ** 2).sum(axis=1)
        return np.maximum(var, 0.0)

    def contrast_local(self, step_x, step_y=None, n_phases: int = 3):
        """(contrast, best_phase_x, best_phase_y) with per-tile local phase.

        Phases are searched per tile; the returned best phase is the global
        activity-weighted winner (used only as a hint downstream). Used for
        pair arbitration among vetted candidates - per-tile phase freedom
        overfits small steps, so this must not drive the open scan."""
        if step_y is None:
            step_y = step_x
        if (step_x < 1.5 or step_y < 1.5 or step_x > self.W / 3
                or step_y > self.H / 3 or self.total_var <= 1e-9
                or len(self.px) == 0):
            return 0.0, 0.0, 0.0
        pxs = np.arange(n_phases) * (step_x / n_phases)
        pys = np.arange(n_phases) * (step_y / n_phases)
        nt = self.n_tiles
        counts = np.bincount(self.tile_id, minlength=nt).astype(np.float64)
        counts = np.maximum(counts, 1)
        tile_best = np.full(nt, np.inf)
        tile_worst = np.full(nt, -np.inf)
        gbest, gph = np.inf, (0.0, 0.0)
        for py_ in pys:
            for px_ in pxs:
                v = self._cells_variance(step_x, step_y, px_, py_)
                tv = np.bincount(self.tile_id, weights=v, minlength=nt) / counts
                np.minimum(tile_best, tv, out=tile_best)
                np.maximum(tile_worst, tv, out=tile_worst)
                g = float(tv.mean())
                if g < gbest:
                    gbest, gph = g, (px_, py_)
        w = counts / counts.sum()
        best = float((tile_best * w).sum())
        worst = float((tile_worst * w).sum())
        q = (worst - best) / (best + 0.05 * self.active_var)
        return float(q), float(gph[0]), float(gph[1])


    def contrast(self, step_x, step_y=None, n_phases: int = 3):
        """(contrast, best_phase_x, best_phase_y) with a single global
        phase - the stable form used for the candidate scan."""
        if step_y is None:
            step_y = step_x
        if (step_x < 1.5 or step_y < 1.5 or step_x > self.W / 3
                or step_y > self.H / 3 or self.total_var <= 1e-9
                or len(self.px) == 0):
            return 0.0, 0.0, 0.0
        pxs = np.arange(n_phases) * (step_x / n_phases)
        pys = np.arange(n_phases) * (step_y / n_phases)
        best, worst = np.inf, -np.inf
        bx = by = 0.0
        for py_ in pys:
            for px_ in pxs:
                v = float(self._cells_variance(step_x, step_y, px_, py_).mean())
                if v < best:
                    best, bx, by = v, px_, py_
                if v > worst:
                    worst = v
        q = (worst - best) / (best + 0.05 * self.active_var)
        return float(q), float(bx), float(by)

    def grid_variance(self, step_x, step_y, phase_x, phase_y):
        """Mean within-cell variance over active cells (single phase)."""
        return float(self._cells_variance(step_x, step_y,
                                          phase_x, phase_y).mean())

    def pair_q(self, step_x: float, step_y: float, n_phases: int = 4) -> float:
        """Null-corrected local-phase contrast for an (x, y) pair.

        Per-tile phase freedom inflates q for any step (overfit bias), but
        only a true lattice loses its q when the step is pushed ~19% off.
        Subtracting the off-lattice q cancels the overfit bias."""
        q = self.contrast_local(step_x, step_y, n_phases=n_phases)[0]
        null1 = self.contrast_local(step_x * 1.19, step_y * 1.19,
                                    n_phases=n_phases)[0]
        null2 = self.contrast_local(step_x * 0.84, step_y * 0.84,
                                    n_phases=n_phases)[0]
        return q - max(null1, null2, 0.0)

    def best_pair(self, pairs, n_phases: int = 4):
        """Evaluate candidate (step_x, step_y) pairs, return them scored."""
        out = []
        for sx, sy in pairs:
            out.append((float(sx), float(sy), self.pair_q(sx, sy, n_phases)))
        out.sort(key=lambda r: -r[2])
        return out

    def scored_curve(self, min_step: float = 2.0, max_step=None):
        """Detrended-prominence z curve over square cell sizes."""
        from scipy.ndimage import median_filter
        L = min(self.W, self.H)
        if max_step is None:
            max_step = min(max(4.0, L / 8.0), 64.0)
        steps = []
        s = min_step
        while s <= max_step:
            steps.append(s)
            s *= 1.04
        steps = np.array(steps)
        cs = np.array([self.contrast(s, n_phases=3)[0] for s in steps])
        base = median_filter(cs, size=15, mode="nearest")
        resid = cs - base
        sigma = np.median(np.abs(resid)) * 1.4826 + 1e-9
        z = resid / sigma
        return steps, z, cs

    def refine(self, step, span_ratio: float = 0.1, n_phases: int = 4):
        """Local refinement of a square-cell candidate -> (step, contrast)."""
        span = max(0.4, step * span_ratio)
        coarse = np.linspace(max(1.6, step - span), step + span, 9)
        cs = [self.contrast(s, n_phases=n_phases)[0] for s in coarse]
        i = int(np.argmax(cs))
        lo = coarse[max(0, i - 1)]
        hi = coarse[min(len(coarse) - 1, i + 1)]
        fine = np.linspace(lo, hi, 7)
        cf = [self.contrast(s, n_phases=n_phases)[0] for s in fine]
        j = int(np.argmax(cf))
        return float(fine[j]), float(cf[j])

    def z_channel(self, min_step: float = 2.0, max_step=None):
        """(z_of_step callable, [(step, z)] candidates from curve maxima)."""
        steps, z, cs = self.scored_curve(min_step, max_step)
        logs = np.log(steps)

        def z_of(step):
            if step < steps[0] or step > steps[-1]:
                return 0.0
            return float(np.interp(np.log(step), logs, z))

        idx = [i for i in range(len(steps))
               if z[i] > 2.0
               and z[i] >= (z[i - 1] if i > 0 else -1e9)
               and z[i] >= (z[i + 1] if i < len(steps) - 1 else -1e9)]
        idx.sort(key=lambda i: -z[i])
        cands, seen = [], []
        for i in idx[:8]:
            s0 = float(steps[i])
            if any(abs(s0 - s1) / s1 < 0.06 for s1 in seen):
                continue
            seen.append(s0)
            s_ref, _c = self.refine(s0)
            cands.append((s_ref, float(z[i])))
        return z_of, cands

