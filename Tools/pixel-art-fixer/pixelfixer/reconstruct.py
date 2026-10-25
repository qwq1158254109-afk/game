"""Reconstruction: detected grid -> native-resolution pixel art.

Lessons folded in from the ML branch's ColorNet (ml/dataset.py pool_cells)
and from pixel-snapper:

  1. PHASE matters: pooling on a phase-0 uniform grid lets cells straddle
     pseudo-pixel boundaries and mixes colors. Estimate the grid phase per
     axis (min within-cell variance over a phase sweep).
  2. Snap every cut to the local gradient maximum within +-30% of a cell
     (pixel-snapper's walker, done vectorized) so wobbly boundaries land
     where the image says they are.
  3. Pool per cell with center weighting (triangular in each axis): mush,
     anti-aliasing and jpeg bleed live at cell borders.
  4. Keep the CENTER PIXEL as a crisp sample; where the cell is busy
     (high std - a detail/outline cell) prefer it over the mean, which
     preserves 1px outlines that averaging washes out.
  5. Optional global palette snap (k-means in Oklab) to de-mix the rest.
"""

from __future__ import annotations

import numpy as np


def _axis_profile(rgba: np.ndarray, axis: int) -> np.ndarray:
    """|d1| of luminance summed across the perpendicular axis."""
    g = (0.299 * rgba[..., 0].astype(np.float32)
         + 0.587 * rgba[..., 1].astype(np.float32)
         + 0.114 * rgba[..., 2].astype(np.float32))
    if rgba.shape[2] == 4:
        a = rgba[..., 3].astype(np.float32) / 255.0
        g = g * a
    d = np.abs(np.diff(g, axis=1 - axis))
    return d.sum(axis=axis)


class _Sat:
    """Integral images for within-cell variance of arbitrary cut sets."""

    def __init__(self, rgba):
        img = rgba[..., :3].astype(np.float64)
        self.h, self.w = img.shape[:2]
        self.S1 = np.zeros((self.h + 1, self.w + 1, 3))
        self.S1[1:, 1:] = img.cumsum(0).cumsum(1)
        self.S2 = np.zeros((self.h + 1, self.w + 1))
        self.S2[1:, 1:] = (img ** 2).sum(-1).cumsum(0).cumsum(1)
        self._S1f = self.S1.reshape(-1, 3)
        self._S2f = self.S2.reshape(-1)
        self._stride = self.w + 1

    def cell_var(self, xs, ys):
        # flat take() beats 2D fancy indexing severalfold on big SATs
        idx = (np.asarray(ys, np.int64)[:, None] * self._stride
               + np.asarray(xs, np.int64)[None, :]).ravel()
        a1 = self._S1f.take(idx, axis=0).reshape(len(ys), len(xs), 3)
        a2 = self._S2f.take(idx).reshape(len(ys), len(xs))
        s1 = a1[1:, 1:] - a1[:-1, 1:] - a1[1:, :-1] + a1[:-1, :-1]
        s2 = a2[1:, 1:] - a2[:-1, 1:] - a2[1:, :-1] + a2[:-1, :-1]
        area = ((ys[1:] - ys[:-1])[:, None]
                * (xs[1:] - xs[:-1])[None, :]).astype(np.float64)
        area = np.maximum(area, 1)
        v = s2 / area - ((s1 / area[..., None]) ** 2).sum(-1)
        return float((v * area).sum() / area.sum())


def _best_phase(rgba: np.ndarray, step_x: float, step_y: float,
                sat: "_Sat" = None) -> tuple:
    """Grid phase minimizing within-cell variance (SAT, coarse sweep)."""
    sat = sat or _Sat(rgba)
    h, w = sat.h, sat.w

    def var_at(px, py):
        xs = np.unique(np.clip(np.round(np.arange(px, w + step_x, step_x))
                               .astype(int), 0, w))
        ys = np.unique(np.clip(np.round(np.arange(py, h + step_y, step_y))
                               .astype(int), 0, h))
        if xs[0] != 0:
            xs = np.concatenate([[0], xs])
        if ys[0] != 0:
            ys = np.concatenate([[0], ys])
        return sat.cell_var(xs, ys)

    n = 5
    best = (0.0, 0.0, np.inf)
    for py in np.arange(n) * (step_y / n):
        for px in np.arange(n) * (step_x / n):
            v = var_at(px, py)
            if v < best[2]:
                best = (px, py, v)
    return best[0], best[1]


def _comb_phase(profile: np.ndarray, step: float) -> tuple:
    """(phase, strength): sub-pixel comb phase of the cut-energy profile.

    Synthetic upscales have phase ~0 and razor-sharp combs; real images
    wobble. Strength (peak/mean of the phase response) gates whether the
    comb is trusted over the variance sweep."""
    n = len(profile)
    if step < 1.5 or n < 3 * step:
        return 0.0, 0.0
    n_ph = max(8, int(round(step * 4)))
    phases = np.arange(n_ph) * (step / n_ph)
    ks = np.arange(0, int((n - 2) / step) + 1)
    pos = phases[:, None] + ks[None, :] * step
    valid = pos <= n - 2
    i0 = np.clip(pos.astype(int), 0, n - 2)
    f = pos - i0
    vals = profile[i0] * (1 - f) + profile[i0 + 1] * f
    resp = (vals * valid).sum(1) / np.maximum(valid.sum(1), 1)
    b = int(np.argmax(resp))
    strength = float(resp[b] / (resp.mean() + 1e-9))
    # parabolic sub-bin refinement (cyclic)
    lo, hi = resp[(b - 1) % n_ph], resp[(b + 1) % n_ph]
    denom = (lo - 2 * resp[b] + hi)
    d = 0.5 * (lo - hi) / denom if abs(denom) > 1e-12 else 0.0
    ph = (phases[b] + np.clip(d, -1, 1) * (step / n_ph)) % step
    return float(ph), strength


def _snapped_cuts(profile: np.ndarray, step: float, phase: float,
                  extent: int, n_cells: int,
                  snap_ratio: float = 0.30) -> np.ndarray:
    """Exactly n_cells+1 cut positions: lattice targets phase+k*step, each
    interior cut snapped to the local |d1| max (profile[c] = energy of a
    cut between columns c-1 and c). Count is exact by construction."""
    cuts = np.empty(n_cells + 1, dtype=np.int64)
    cuts[0] = 0
    cuts[n_cells] = extent
    rad = max(1, int(round(step * snap_ratio)))
    prof_mean = float(profile.mean()) if len(profile) else 0.0
    # never create slivers: a cell may not shrink below ~55% of the step,
    # or its mode becomes the boundary color (reads as dark tear lines)
    min_gap = max(1, int(np.floor(0.55 * step)))
    first = phase % step
    # interior lattice targets: when the phase offset is small the first
    # interior cut sits one full step in; when it is large (> half a cell)
    # the phase line itself is the first interior cut
    base = first if first >= 0.5 * step else first + step
    prev = 0
    for j in range(1, n_cells):
        t = base + (j - 1) * step
        c = int(round(min(max(t, 1), extent - 1)))
        lo = max(prev + min_gap, c - rad)
        hi = min(extent - 1, c + rad,
                 extent - (n_cells - j) * min_gap)
        if hi < lo:
            c = min(prev + min_gap, extent - (n_cells - j))
        else:
            seg = profile[lo:hi + 1]
            # snap only onto real edge energy (>= 0.5x profile mean);
            # flat regions stay on the lattice instead of chasing noise
            if seg.size and seg.max() >= 0.5 * prof_mean and seg.max() > 1e-9:
                c = lo + int(np.argmax(seg))
            else:
                c = min(max(c, lo), hi)
        c = max(min(c, extent - (n_cells - j)), prev + 1)
        cuts[j] = c
        prev = c
    return cuts


def _banded_cuts(rgba, base_cuts, step, axis, rad_ratio=0.3):
    """Bend global cut lines into per-band polylines ("drag pixels back
    into shape"): each band re-snaps every cut to its own local |d1|
    profile within +-rad of the global position, with one smoothing pass
    across bands. Returns (band edges, cuts_per_band [n_bands, n_cuts])."""
    h, w = rgba.shape[:2]
    perp = h if axis == 0 else w
    extent = w if axis == 0 else h
    n_bands = int(np.clip(perp / (step * 7), 1, 12))
    edges = np.linspace(0, perp, n_bands + 1).astype(int)
    if n_bands <= 1:
        return edges, base_cuts[None, :].copy()

    g = (0.299 * rgba[..., 0].astype(np.float32)
         + 0.587 * rgba[..., 1].astype(np.float32)
         + 0.114 * rgba[..., 2].astype(np.float32))
    if rgba.shape[2] == 4:
        g = g * (rgba[..., 3].astype(np.float32) / 255.0)
    d = np.abs(np.diff(g, axis=1 - axis))

    rad = max(1, int(round(step * rad_ratio)))
    min_gap = max(1, int(np.floor(0.55 * step)))
    cuts = np.tile(base_cuts.astype(np.float64), (n_bands, 1))
    for b in range(n_bands):
        seg = d[edges[b]:edges[b + 1]] if axis == 0 else             d[:, edges[b]:edges[b + 1]]
        prof = np.concatenate([[0], seg.sum(axis=axis)])
        pmean = float(prof.mean())
        for j in range(1, len(base_cuts) - 1):
            c = int(base_cuts[j])
            lo = max(int(cuts[b, j - 1]) + min_gap, c - rad)
            hi = min(extent - 1, c + rad,
                     int(base_cuts[j + 1]) - 1)
            if hi <= lo:
                cuts[b, j] = max(cuts[b, j - 1] + min_gap,
                                 min(float(c), extent - 1.0))
                continue
            sl = prof[lo:hi + 1]
            if sl.size and sl.max() >= 0.5 * pmean and sl.max() > 1e-9:
                cuts[b, j] = lo + int(np.argmax(sl))
            else:
                cuts[b, j] = min(max(float(c), lo), hi)
    if n_bands >= 3:   # cut lines bend smoothly, they don't teleport
        sm = cuts.copy()
        sm[1:-1] = 0.5 * cuts[1:-1] + 0.25 * (cuts[:-2] + cuts[2:])
        cuts = sm
    cuts = np.maximum.accumulate(cuts, axis=1)   # float; interpolated later
    cuts[:, 0] = 0.0
    cuts[:, -1] = float(extent)
    return edges, cuts


def _warped_cell_index(w, h, xs_bands, x_edges, ys_bands, y_edges,
                       ncx, ncy):
    """Per-pixel (ix, iy) from banded cut polylines.

    Cut positions are INTERPOLATED continuously across scanlines (control
    points at band centres) - per-band constant cuts shear entire bands at
    their boundaries, which reads as tearing in the reconstruction."""
    px = np.arange(w) + 0.5
    ph = np.arange(h) + 0.5

    def _index(bands, edges, perp_len, extent, n_cells):
        n_b = bands.shape[0]
        if n_b == 1:
            row = np.searchsorted(bands[0], np.arange(extent) + 0.5,
                                  side="right") - 1
            return np.tile(row, (perp_len, 1))
        centers = (edges[:-1] + edges[1:]) / 2.0
        t = np.arange(perp_len, dtype=np.float64)
        # (n_cuts, perp_len) continuous polylines
        lines = np.empty((bands.shape[1], perp_len))
        for j in range(bands.shape[1]):
            lines[j] = np.interp(t, centers, bands[:, j])
        lines = np.maximum.accumulate(lines, axis=0)
        out = np.empty((perp_len, extent), np.int64)
        coords = np.arange(extent) + 0.5
        for r in range(perp_len):
            out[r] = np.searchsorted(lines[:, r], coords, side="right") - 1
        return out

    ix = _index(xs_bands, x_edges, h, w, ncx)          # (h, w)
    iy = _index(ys_bands, y_edges, w, h, ncy).T        # (h, w)
    return np.clip(ix, 0, ncx - 1), np.clip(iy, 0, ncy - 1)


# ------------------------------------------------------ two-stage packing

def adaptive_k(rgba: np.ndarray, lo: int = 16, hi: int = 48,
               share: float = 0.003) -> int:
    """Structure-only colour count from the image's complexity.

    The quantisation in two-stage packing only has to SEPARATE regions for the
    placement vote (the final colour comes from the original pixels), so K
    should be generous. Count coarse (4-bit) colour bins holding >= `share` of
    the opaque pixels - roughly how many meaningful regions the art has - and
    clamp to [lo, hi]."""
    rgb = rgba[:, :, :3].reshape(-1, 3).astype(np.int64)
    if rgba.shape[2] == 4:
        rgb = rgb[rgba[:, :, 3].reshape(-1) > 0]
    if len(rgb) == 0:
        return lo
    key = ((rgb[:, 0] >> 4) << 8) | ((rgb[:, 1] >> 4) << 4) | (rgb[:, 2] >> 4)
    cnt = np.bincount(key)
    k = int((cnt / cnt.sum() >= share).sum())
    return int(np.clip(k, lo, hi))


def two_stage_pack(rgba: np.ndarray, cols: int, rows: int,
                   k_colors: int = 0) -> np.ndarray:
    """Two-stage reconstruction on a regular even grid.

    Stage 1 (STRUCTURE): quantise the image to a small palette and let each
    cell vote among the clean quantised LABELS - a crisp placement decision
    (which region a cell belongs to), the source of pixel-snapper's clean
    lines, but used only to decide placement.

    Stage 2 (COLOUR): colour each cell from the ORIGINAL pixels carrying the
    winning label (a centre-weighted mean of just those pixels). So a boundary
    cell picks 'outline' cleanly, then gets the TRUE outline colour - crisp
    lines AND accurate, un-clamped colours (rare colours survive).
    """
    from .quantize import kmeans_quantize
    h, w = rgba.shape[:2]
    K = k_colors if k_colors > 0 else adaptive_k(rgba)
    _, labels, _ = kmeans_quantize(rgba, k=K)
    lab = labels.reshape(-1).astype(np.int64)
    K = int(lab.max()) + 1
    n = cols * rows

    rgb = rgba[:, :, :3].reshape(-1, 3).astype(np.float64) / 255.0
    ix = np.clip((np.arange(w) * cols) // w, 0, cols - 1)
    iy = np.clip((np.arange(h) * rows) // h, 0, rows - 1)
    cell = (iy[:, None] * cols + ix[None, :]).ravel()

    # triangular centre weight within each even cell
    fx = (np.arange(w) + 0.5 - ix * (w / cols)) / (w / cols)
    fy = (np.arange(h) + 0.5 - iy * (h / rows)) / (h / rows)
    wx = 1.0 - 2.0 * np.abs(fx - 0.5)
    wy = 1.0 - 2.0 * np.abs(fy - 0.5)
    wgt = (wy[:, None] * wx[None, :]).ravel() + 1e-4

    # stage 1: winning label per cell
    comp = cell * K + lab
    wsum = np.bincount(comp, weights=wgt, minlength=n * K).reshape(n, K)
    win_label = np.argmax(wsum, axis=1)

    # stage 2: colour from original pixels carrying the winning label
    sel = (lab == win_label[cell])
    wsel = wgt * sel
    denom = np.maximum(np.bincount(cell, weights=wsel, minlength=n), 1e-9)
    out = np.stack([np.bincount(cell, weights=rgb[:, c] * wsel, minlength=n)
                    for c in range(3)], 1) / denom[:, None]
    bad = np.bincount(cell, weights=sel.astype(float), minlength=n) < 0.5
    if bad.any():
        cnt = np.maximum(np.bincount(cell, minlength=n), 1)
        mean = np.stack([np.bincount(cell, weights=rgb[:, c], minlength=n)
                         for c in range(3)], 1) / cnt[:, None]
        out[bad] = mean[bad]

    low = np.clip(np.rint(out * 255), 0, 255).astype(np.uint8).reshape(rows, cols, 3)
    if rgba.shape[2] == 4:
        a = (rgba[:, :, 3].reshape(-1) > 127).astype(np.float64)
        cnt = np.maximum(np.bincount(cell, minlength=n), 1)
        mask = (np.bincount(cell, weights=a, minlength=n) / cnt > 0.5).reshape(rows, cols)
        low = np.dstack([low, (mask * 255).astype(np.uint8)])
    return low


def reconstruct(rgba: np.ndarray, step_x: float, step_y: float,
                cols: int, rows: int,
                palette_snap: bool = True,
                use_phase: bool = True,
                use_snap: bool = True,
                color: str = "auto",
                dark_stroke: bool = False,
                std_thresh: float = 0.085) -> np.ndarray:
    """Legacy grid-cut reconstructor (superseded by two_stage_pack).

    Kept as the two_stage=False fallback: snaps per-axis cut lines to the
    |d1| lattice, refines them into per-band warp polylines, then colours each
    cell by a center-weighted 5-bit local mode. two_stage_pack is the default
    server path; this remains for A/B and for the phase-locked synthetic case.

    color: "mode" (5-bit binned local mode, center-weighted, global bin means)
    | "cw" center-weighted mean | "auto" cw with center-pixel for busy cells.
    dark_stroke: opt-in 1px-outline re-vote (helps AI mixels, hurts exact-GT
    on jittered synthetics)."""
    h, w = rgba.shape[:2]
    sat = _Sat(rgba)

    # profile[c] = |d1| energy of a cut between columns c-1 and c
    prof_x = np.concatenate([[0], _axis_profile(rgba, 0)])
    prof_y = np.concatenate([[0], _axis_profile(rgba, 1)])

    if use_phase:
        px, sx_str = _comb_phase(prof_x, step_x)
        py, sy_str = _comb_phase(prof_y, step_y)
        if min(sx_str, sy_str) < 1.35:   # mushy: comb unreliable
            px, py = _best_phase(rgba, step_x, step_y, sat)
    else:
        px, py = 0.0, 0.0
    if not use_snap:
        prof_x = np.zeros_like(prof_x)
        prof_y = np.zeros_like(prof_y)

    # candidate cut sets per axis: snapped / phase-lattice / phase-0
    # lattice (the legacy extractor). The within-cell variance objective
    # picks per axis, so this can only match or beat the legacy grid.
    flat_x = np.zeros_like(prof_x)
    flat_y = np.zeros_like(prof_y)
    # candidate cut sets per axis: snapped / phase-lattice / phase-0 lattice.
    # The within-cell variance objective picks per axis.
    cand_x = [_snapped_cuts(prof_x, step_x, px, w, cols),
              _snapped_cuts(flat_x, step_x, px, w, cols),
              _snapped_cuts(flat_x, step_x, 0.0, w, cols)]
    cand_y = [_snapped_cuts(prof_y, step_y, py, h, rows),
              _snapped_cuts(flat_y, step_y, py, h, rows),
              _snapped_cuts(flat_y, step_y, 0.0, h, rows)]
    xs = min(cand_x, key=lambda c: sat.cell_var(c, cand_y[2]))
    ys = min(cand_y, key=lambda c: sat.cell_var(xs, c))

    # per-pixel cell index from the straight cut lists
    ncx = len(xs) - 1
    ncy = len(ys) - 1
    ix = np.clip(np.searchsorted(xs, np.arange(w), side="right") - 1,
                 0, ncx - 1)
    iy = np.clip(np.searchsorted(ys, np.arange(h), side="right") - 1,
                 0, ncy - 1)
    cell = (iy[:, None] * ncx + ix[None, :]).ravel()
    n = ncx * ncy

    rgb = rgba[..., :3].reshape(-1, 3).astype(np.float64) / 255.0

    def _pooled_var(cell_idx):
        c1 = np.maximum(np.bincount(cell_idx, minlength=n), 1).astype(float)
        m = np.stack([np.bincount(cell_idx, weights=rgb[:, c], minlength=n)
                      for c in range(3)], 1) / c1[:, None]
        q = np.stack([np.bincount(cell_idx, weights=rgb[:, c] ** 2,
                                  minlength=n) for c in range(3)], 1) / c1[:, None]
        return float((np.maximum(q - m ** 2, 0).sum(1) * c1).sum() / c1.sum())

    # banded (warped) refinement: adopt only when it measurably reduces
    # within-cell variance (blocks/warp cases; straight grids unaffected)
    if use_snap:
        x_edges, xs_b = _banded_cuts(rgba, xs, step_x, 0)
        y_edges, ys_b = _banded_cuts(rgba, ys, step_y, 1)
        if xs_b.shape[0] > 1 or ys_b.shape[0] > 1:
            ixw, iyw = _warped_cell_index(w, h, xs_b, x_edges,
                                          ys_b, y_edges, ncx, ncy)
            cell_w = (iyw * ncx + ixw).ravel()
            if _pooled_var(cell_w) < 0.995 * _pooled_var(cell):
                cell = cell_w

    # center weights: triangular within each cell span
    cx_lo = xs[ix]
    cx_hi = xs[ix + 1]
    fx = (np.arange(w) + 0.5 - cx_lo) / np.maximum(cx_hi - cx_lo, 1)
    cy_lo = ys[iy]
    cy_hi = ys[iy + 1]
    fy = (np.arange(h) + 0.5 - cy_lo) / np.maximum(cy_hi - cy_lo, 1)
    wx = 1.0 - 2.0 * np.abs(fx - 0.5)
    wy = 1.0 - 2.0 * np.abs(fy - 0.5)
    wgt = (wy[:, None] * wx[None, :]).ravel() + 1e-4

    cnt = np.maximum(np.bincount(cell, minlength=n), 1).astype(np.float64)
    wsum = np.maximum(np.bincount(cell, weights=wgt, minlength=n), 1e-6)
    cw = np.stack([np.bincount(cell, weights=rgb[:, c] * wgt, minlength=n)
                   for c in range(3)], 1) / wsum[:, None]
    mean = np.stack([np.bincount(cell, weights=rgb[:, c], minlength=n)
                     for c in range(3)], 1) / cnt[:, None]
    sq = np.stack([np.bincount(cell, weights=rgb[:, c] ** 2, minlength=n)
                   for c in range(3)], 1) / cnt[:, None]
    std = np.sqrt(np.maximum(sq - mean ** 2, 0)).mean(1)

    # center pixel per cell (max weight)
    order = np.lexsort((wgt, cell))
    o = order[::-1]
    first = np.unique(cell[o], return_index=True)
    cpx = np.zeros((n, 3))
    cpx[cell[o][first[1]]] = rgb[o][first[1]]

    if color == "mode":
        # 5-bit-binned LOCAL mode with center-weighted votes (pixel-art-lab):
        # near-duplicate AA colors merge into one bin instead of splitting
        # the vote, and rare local colors can win their own cell - a global
        # k-means codebook cannot represent them.
        r5 = (rgba[..., 0].reshape(-1).astype(np.int64) >> 3)
        g5 = (rgba[..., 1].reshape(-1).astype(np.int64) >> 3)
        b5 = (rgba[..., 2].reshape(-1).astype(np.int64) >> 3)
        key = (r5 << 10) | (g5 << 5) | b5

        # global per-bin mean colors: denoises like k-means centers but
        # every rare local bin keeps its own representative
        gcnt = np.bincount(key, minlength=32768).astype(np.float64)
        gmean = np.stack([np.bincount(key, weights=rgb[:, c],
                                      minlength=32768) for c in range(3)], 1)
        gmean /= np.maximum(gcnt, 1)[:, None]

        def _binned_mode(sel):
            """Per-cell winning bin (weighted votes); color = the bin's
            image-wide mean."""
            comp = cell[sel] * 32768 + key[sel]
            uniq, inv = np.unique(comp, return_inverse=True)
            wsum = np.bincount(inv, weights=wgt[sel])
            cells_of = uniq >> 15
            order = np.lexsort((wsum, cells_of))
            co = cells_of[order]
            last = np.flatnonzero(np.r_[co[1:] != co[:-1], True])
            win_key = uniq[order[last]] & 32767
            colr = np.full((n, 3), np.nan)
            colr[co[last]] = gmean[win_key]
            return colr

        out = _binned_mode(np.ones(len(cell), bool))
        bad = np.isnan(out[:, 0])
        out[bad] = mean[bad]

        # dark-stroke minority re-vote (pixel-art-lab): when a cell's chosen
        # color is much brighter than its darkest pixels AND those dark
        # pixels are a minority (a 1px outline clipping the cell), re-vote
        # among the dark pixels only. Preserves outlines mode voting drops.
        if not dark_stroke:
            out_final = out
        T = 38.0 / 255.0
        luma_px = (0.299 * rgb[:, 0] + 0.587 * rgb[:, 1] + 0.114 * rgb[:, 2])
        min_luma = np.full(n, 2.0)
        np.minimum.at(min_luma, cell, luma_px)
        out_luma = (0.299 * out[:, 0] + 0.587 * out[:, 1] + 0.114 * out[:, 2])
        dark_cut = min_luma + np.maximum(8.0 / 255.0, 0.35 * T)
        dark_px = luma_px <= dark_cut[cell]
        dark_cnt = np.bincount(cell, weights=dark_px.astype(float), minlength=n)
        need = ((out_luma - min_luma >= T)
                & (dark_cnt >= np.maximum(2, 0.08 * cnt))   # not lone noise
                & (dark_cnt <= np.ceil(0.42 * cnt)))
        sel = need[cell] & dark_px
        if dark_stroke and sel.any():
            dark_col = _binned_mode(sel)
            ok = need & ~np.isnan(dark_col[:, 0])
            out[ok] = dark_col[ok]

    else:
        out = cw.copy()
        if color == "auto":
            busy = std > std_thresh
            out[busy] = cpx[busy]

    if palette_snap:
        # global palette from cell colors; snap in Oklab
        from .colorspace import srgb_to_oklab, oklab_to_srgb
        try:
            import cv2
            k = int(min(48, max(8, n ** 0.5 // 2)))
            data = out.astype(np.float32)
            crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 12, 0.5)
            _, labels, centers = cv2.kmeans(data, k, None, crit, 2,
                                            cv2.KMEANS_PP_CENTERS)
            lab = srgb_to_oklab(out)
            clab = srgb_to_oklab(centers.astype(np.float64))
            d = ((lab[:, None, :] - clab[None, :, :]) ** 2).sum(-1)
            out = centers.astype(np.float64)[np.argmin(d, 1)]
        except Exception:
            pass

    low = np.clip(np.rint(out * 255), 0, 255).astype(np.uint8) \
        .reshape(ncy, ncx, 3)

    if rgba.shape[2] == 4:
        a = (rgba[..., 3].reshape(-1) > 127).astype(np.float64)
        asum = np.bincount(cell, weights=a, minlength=n)
        mask = (asum / cnt > 0.5).reshape(ncy, ncx)
        alpha = (mask * 255).astype(np.uint8)
    else:
        alpha = np.full((ncy, ncx), 255, np.uint8)
    low = np.dstack([low, alpha])

    return low
