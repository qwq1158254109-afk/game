"""Fusion prototype: instrument every periodicity channel in pixelfixer.grid
on a common candidate-step ladder, dump the per-(image, axis, step) score
table, learn/derive an interpretable fusion rule, and run the bench with it.

Modes
-----
    python tools/methods/fusion.py --extract   # dump channel table (CSV)
    python tools/methods/fusion.py --analyze   # per-channel ranking stats +
                                               # logistic/tree fusion search
    python tools/methods/fusion.py             # run the bench harness

Channels recorded per (axis, step)
----------------------------------
    comb_e1  comb z on pooled E1 (quantized-image edge profile)
    comb_e2  comb z on pooled E2 (original-image curvature profile)
    ray_e1   Rayleigh peak coherence on E1
    ray_e2   Rayleigh peak coherence on E2
    band_e1  Stouffer combo of per-band max(comb, rayleigh), E1 bands
    band_e2  same for E2 bands
    tile_e1  2D-tile Rayleigh (quantized gradient map)
    tile_e2  2D-tile Rayleigh (original curvature map)
    spec_e1  Welch spectral z (quantized gradient map)
    spec_e2  Welch spectral z (original curvature map)
    vc       CellVarContrast detrended z (square cells; same for both axes)
"""

import json
import math
import os
import sys

import numpy as np

from .channels import (                                   # noqa: E402
    axis_profiles, band_profiles, is_jpeg_suspect, is_jpeg_lattice,
    _PooledProfile, _comb_score, _rayleigh_score, _lattice_refine,
    _grad_maps, _tile_peaks, _tiles_ray_z,
    _axis_spectrum, _spectral_background, _spectral_z,
    _jpeg_lattice_strength, _notch_jpeg)
from .quantize import kmeans_quantize                 # noqa: E402
from .varcontrast import CellVarContrast              # noqa: E402

CHANNELS = ["comb_e1", "comb_e2", "ray_e1", "ray_e2",
            "band_e1", "band_e2", "tile_e1", "tile_e2",
            "spec_e1", "spec_e2", "vc"]

OUT_DIR = os.path.join(os.getcwd(), "out", "progress", "methods", "fusion")
TABLE_CSV = os.path.join(OUT_DIR, "channel_table.csv")


def ladder(lo: float = 2.0, hi: float = 64.0, ratio: float = 1.03):
    steps = []
    s = lo
    while s <= hi + 1e-9:
        steps.append(round(s, 4))
        s *= ratio
    return steps


# ------------------------------------------------------------- evidence

def build_evidence(rgba: np.ndarray, lean: bool = False) -> dict:
    """Construct channel inputs the way fit_grid does (read-only reuse).

    lean=True builds only what the ACTIVE (weighted) channels read:
    profiles, tiles and E1 spectra - skipping the duplicate square-packer
    curve, comb pooled profiles and band stacks roughly halves the cost."""
    h, w = rgba.shape[:2]
    try:
        import cv2
        base = cv2.medianBlur(np.ascontiguousarray(rgba), 3)
    except Exception:
        base = rgba
    quantized, _, _ = kmeans_quantize(base, k=16)

    prof_q = axis_profiles(quantized)   # E1 source
    prof_o = axis_profiles(rgba)        # E2 source
    prof = {"e1x": prof_q["e1x"], "e1y": prof_q["e1y"],
            "e2x": prof_o["e2x"], "e2y": prof_o["e2y"]}

    jpeg_z = max(_jpeg_lattice_strength(prof["e1x"]),
                 _jpeg_lattice_strength(prof["e1y"]))
    if jpeg_z > 5.0:
        for key in prof:
            prof[key] = _notch_jpeg(prof[key])

    n_bands_y = 0 if lean else (4 if h >= 200 else (2 if h >= 96 else 0))
    n_bands_x = 0 if lean else (4 if w >= 200 else (2 if w >= 96 else 0))
    bx1 = band_profiles(quantized, n_bands_y, axis=0, kind="cut") if n_bands_y else None
    bx2 = band_profiles(rgba, n_bands_y, axis=0, kind="knot") if n_bands_y else None
    by1 = band_profiles(quantized, n_bands_x, axis=1, kind="cut") if n_bands_x else None
    by2 = band_profiles(rgba, n_bands_x, axis=1, kind="knot") if n_bands_x else None
    if jpeg_z > 5.0:
        for bands in (bx1, bx2, by1, by2):
            if bands is not None:
                for i in range(bands.shape[0]):
                    bands[i] = _notch_jpeg(bands[i])

    gm = _grad_maps(rgba, quantized)

    def spec(dmap, axis):
        freqs, power = _axis_spectrum([dmap], axis=axis)
        return freqs, power, _spectral_background(power)

    if lean:
        vc = None
        vc_z_of, vc_cands = (lambda s: 0.0), []
    else:
        vc = CellVarContrast(rgba)
        vc_z_of, vc_cands = vc.z_channel()

    def axis_pack(e1, e2, b1, b2, t1, t2, s1, s2, extent):
        return {
            "e1": e1, "e2": e2,
            "pp1": None if lean else _PooledProfile(e1),
            "pp2": None if lean else _PooledProfile(e2),
            "bands1": b1, "bands2": b2,
            "bpp1": [_PooledProfile(b) for b in b1] if b1 is not None else [],
            "bpp2": [_PooledProfile(b) for b in b2] if b2 is not None else [],
            "tiles1": t1, "tiles2": t2,
            "spec1": s1, "spec2": s2,
            "extent": extent,
        }

    ev = {
        "w": w, "h": h, "jpeg_z": float(jpeg_z),
        "vc": vc, "vc_z_of": vc_z_of, "vc_cands": vc_cands,
        "x": axis_pack(prof["e1x"], prof["e2x"], bx1, bx2,
                       _tile_peaks(gm["dqx"], axis=0),
                       _tile_peaks(gm["cox"], axis=0),
                       spec(gm["dqx"], 0), spec(gm["cox"], 0), w),
        "y": axis_pack(prof["e1y"], prof["e2y"], by1, by2,
                       _tile_peaks(gm["dqy"], axis=1),
                       _tile_peaks(gm["coy"], axis=1),
                       spec(gm["dqy"], 1), spec(gm["coy"], 1), h),
    }
    return ev


def _band_stouffer(bpps, bands, step):
    """Stouffer combo of per-band max(comb, rayleigh), as _AxisEvidence.score."""
    if bands is None or len(bpps) < 2:
        return 0.0
    zs = []
    for pp, prof in zip(bpps, bands):
        cz = _comb_score(pp, step)[0]
        rz = _rayleigh_score(prof, step)[0]
        zs.append(max(cz, rz))
    return float(np.sum(zs) / np.sqrt(len(zs)))


def channel_scores(ev: dict, axis: str, step: float, only=None) -> dict:
    ax = ev[axis]
    fns = {
        "comb_e1": lambda: _comb_score(ax["pp1"], step)[0],
        "comb_e2": lambda: _comb_score(ax["pp2"], step)[0],
        "ray_e1": lambda: _rayleigh_score(ax["e1"], step)[0],
        "ray_e2": lambda: _rayleigh_score(ax["e2"], step)[0],
        "band_e1": lambda: _band_stouffer(ax["bpp1"], ax["bands1"], step),
        "band_e2": lambda: _band_stouffer(ax["bpp2"], ax["bands2"], step),
        "tile_e1": lambda: _tiles_ray_z(ax["tiles1"], step),
        "tile_e2": lambda: _tiles_ray_z(ax["tiles2"], step),
        "spec_e1": lambda: _spectral_z(*ax["spec1"], step),
        "spec_e2": lambda: _spectral_z(*ax["spec2"], step),
        "vc": lambda: ev["vc_z_of"](step),
    }
    return {c: float(fn()) for c, fn in fns.items()
            if only is None or c in only}


def channel_matrix(ev: dict, axis: str, steps, only=None) -> np.ndarray:
    """(n_steps, n_channels) raw channel scores.

    `only`: restrict computation to these channels (others stay 0) - the
    fused curve weights most channels at zero, so skipping them cuts the
    evidence build several-fold."""
    rows = []
    for s in steps:
        sc = channel_scores(ev, axis, s, only=only)
        rows.append([sc.get(c, 0.0) for c in CHANNELS])
    return np.array(rows)


ACTIVE_CHANNELS = None  # filled after WEIGHTS below


# ------------------------------------------------------------- extraction

def _image_class(path: str) -> str:
    name = os.path.basename(path).lower()
    for tag in ("clean", "bilinear", "bicubic", "jpeg", "shuffle_hard",
                "shuffle", "warp", "kitchen_sink", "noisy_blur",
                "mush_heavy", "blocks_mush", "drift_mush", "ai_soup",
                "rowjit", "mush"):
        if tag in name:
            return tag
    return "real"


def extract_table():
    """Resumable extraction: one CSV part per image, then concatenate."""
    import pandas as pd
    from tools.bench_common import KNOWN, REVIEW, load

    parts_dir = os.path.join(OUT_DIR, "table_parts")
    os.makedirs(parts_dir, exist_ok=True)
    steps = ladder()
    parts = []
    for entry in ([(p, tc, tr) for p, tc, tr, _tol in KNOWN]
                  + [(p, None, None) for p in REVIEW]):
        path, tc, tr = entry
        full = os.path.join(ROOT, path)
        if not os.path.exists(full):
            continue
        stem = os.path.splitext(os.path.basename(path))[0][:48]
        part_csv = os.path.join(parts_dir, stem + ".csv")
        if os.path.exists(part_csv):
            parts.append(part_csv)
            print(f"cached    {path}", flush=True)
            continue
        img = load(path)
        h, w = img.shape[:2]
        import time
        t0 = time.time()
        ev = build_evidence(img)
        cls = _image_class(path)
        rows = []
        for axis, extent, tcells in (("x", w, tc), ("y", h, tr)):
            true_step = extent / tcells if tcells else None
            mat = channel_matrix(ev, axis, steps)
            for i, s in enumerate(steps):
                rec = {"image": os.path.basename(path), "cls": cls,
                       "axis": axis, "extent": extent, "step": s,
                       "true_step": true_step,
                       "rel": (s / true_step) if true_step else None,
                       "is_true": (abs(s - true_step) / true_step <= 0.03)
                                  if true_step else None}
                rec.update({c: mat[i, j] for j, c in enumerate(CHANNELS)})
                rows.append(rec)
        pd.DataFrame(rows).to_csv(part_csv, index=False)
        parts.append(part_csv)
        print(f"extracted {path}  ({time.time()-t0:.1f}s)", flush=True)
    df = pd.concat([pd.read_csv(p) for p in parts], ignore_index=True)
    df.to_csv(TABLE_CSV, index=False)
    print(f"wrote {TABLE_CSV}  ({len(df)} rows)", flush=True)


# ------------------------------------------------------------- analysis

def analyze():
    import pandas as pd
    df = pd.read_csv(TABLE_CSV)
    known = df[df["true_step"].notna()].copy()

    print("=== per-channel ranking of the true step (known images) ===")
    print("channel: median rank of best true-step row within its axis scan "
          "(1 = channel's argmax is the true step); top1 = fraction of "
          "(image, axis) scans where argmax is within 3% of truth\n")
    stats = []
    groups = list(known.groupby(["image", "axis"]))
    for c in CHANNELS:
        ranks, top1, top1_harm = [], 0, 0
        for (im, ax), g in groups:
            v = g[c].to_numpy()
            if v.max() <= 0:
                ranks.append(len(g))
                continue
            order = np.argsort(-v)
            true_rows = np.where(g["is_true"].to_numpy())[0]
            if len(true_rows) == 0:
                continue
            best_rank = min(np.where(np.isin(order, true_rows))[0]) + 1
            ranks.append(best_rank)
            top1 += int(order[0] in true_rows)
            rel = g["rel"].to_numpy()[order[0]]
            mult = rel / np.round(rel) if np.round(rel) >= 1 else np.inf
            top1_harm += int(abs(rel - round(rel)) <= 0.05 * max(rel, 1)
                             and round(rel) >= 1)
        stats.append((c, float(np.median(ranks)), top1 / len(groups),
                      top1_harm / len(groups)))
        print(f"  {c:8s} med_rank {np.median(ranks):5.1f}  "
              f"top1 {top1 / len(groups):.2f}  top1_or_multiple "
              f"{top1_harm / len(groups):.2f}")

    print("\n=== per-class channel top1 ===")
    for cls, gcls in known.groupby("cls"):
        parts = []
        for c in CHANNELS:
            t = 0
            n = 0
            for (im, ax), g in gcls.groupby(["image", "axis"]):
                v = g[c].to_numpy()
                true_rows = np.where(g["is_true"].to_numpy())[0]
                if len(true_rows) == 0 or v.max() <= 0:
                    n += 1
                    continue
                t += int(np.argmax(v) in true_rows)
                n += 1
            parts.append(f"{c}:{t}/{n}")
        print(f"  {cls:14s} " + "  ".join(parts))

    # ---- learned fusion: logistic regression on per-scan-normalized scores
    print("\n=== logistic regression (per-scan max-normalized channels) ===")
    from sklearn.linear_model import LogisticRegression
    from sklearn.tree import DecisionTreeClassifier, export_text

    X, y = [], []
    for (im, ax), g in known.groupby(["image", "axis"]):
        m = g[CHANNELS].to_numpy()
        norm = m / np.maximum(m.max(axis=0, keepdims=True), 1e-9)
        norm = np.clip(norm, 0, None)
        X.append(norm)
        y.append(g["is_true"].to_numpy().astype(int))
    X = np.vstack(X)
    y = np.concatenate(y)
    lr = LogisticRegression(max_iter=2000, class_weight="balanced")
    lr.fit(X, y)
    for c, w in sorted(zip(CHANNELS, lr.coef_[0]), key=lambda t: -abs(t[1])):
        print(f"  {c:8s} {w:+.2f}")
    print(f"  intercept {lr.intercept_[0]:+.2f}")

    # per-scan argmax accuracy of the LR score
    correct = 0
    total = 0
    for (im, ax), g in known.groupby(["image", "axis"]):
        m = g[CHANNELS].to_numpy()
        norm = m / np.maximum(m.max(axis=0, keepdims=True), 1e-9)
        score = norm @ lr.coef_[0]
        true_rows = np.where(g["is_true"].to_numpy())[0]
        if len(true_rows) == 0:
            continue
        correct += int(np.argmax(score) in true_rows)
        total += 1
    print(f"  LR argmax accuracy: {correct}/{total}")

    print("\n=== decision tree (depth 3) ===")
    dt = DecisionTreeClassifier(max_depth=3, class_weight="balanced",
                                min_samples_leaf=20)
    dt.fit(X, y)
    print(export_text(dt, feature_names=CHANNELS))


# ------------------------------------------------------------- detection

# Fusion rule (from --analyze + fuse_search on the channel table):
# equal-weight sum of per-scan max-normalized {ray_e1, tile_e1, tile_e2,
# spec_e1}. This "core4" reaches 36/40 scan-argmax accuracy on knowns vs 32
# for the best single channel (tile_e1); oracle over all channels is 37.
# Adding comb, band, ray_e2, spec_e2, vc or reliability floors never helped
# (ray_e2 pulls half-steps on clean sprites, floors cost 1-2 scans).
WEIGHTS = {c: 0.0 for c in CHANNELS}
WEIGHTS.update({"ray_e1": 1.0, "tile_e1": 1.0, "tile_e2": 1.0,
                "spec_e1": 1.0})

# Rescoring refined steps additionally uses the per-band Stouffer channels:
# the fused core4 ranks the right PEAK but is too blunt to pick the exact
# rational step under warp/mush (tile phases are local, spectral bins are
# coarse); the band combs collapse hard when the step is 1-2% off, which is
# exactly the sub-peak discrimination refinement needs.
RESCORE_WEIGHTS = dict(WEIGHTS)
ACTIVE_CHANNELS = [c for c in CHANNELS if WEIGHTS.get(c, 0.0) > 0.0]
RESCORE_WEIGHTS.update({"band_e1": 1.0, "band_e2": 1.0})

CACHE_DIR = os.path.join(OUT_DIR, "cache")


def _cached_matrices(rgba, ev, steps):
    """Channel matrices for both axes, cached on disk by image content."""
    import hashlib
    os.makedirs(CACHE_DIR, exist_ok=True)
    key = hashlib.md5(rgba.tobytes()).hexdigest()[:16]
    path = os.path.join(CACHE_DIR, f"{key}_{len(steps)}.npz")
    if os.path.exists(path):
        d = np.load(path)
        return d["mx"], d["my"]
    mx = channel_matrix(ev, "x", steps)
    my = channel_matrix(ev, "y", steps)
    np.savez_compressed(path, mx=mx, my=my)
    return mx, my


def fused_curve(mat: np.ndarray) -> np.ndarray:
    """Weighted sum of per-scan max-normalized channels."""
    fused = np.zeros(mat.shape[0])
    for j, c in enumerate(CHANNELS):
        if WEIGHTS[c] == 0.0:
            continue
        col = np.clip(mat[:, j], 0.0, None)
        m = col.max()
        if m <= 1e-9:
            continue
        fused += WEIGHTS[c] * (col / m)
    return fused


def _local_maxima(steps, curve):
    idx = [i for i in range(len(steps))
           if curve[i] > 0
           and curve[i] >= (curve[i - 1] if i > 0 else -1e18)
           and curve[i] >= (curve[i + 1] if i < len(steps) - 1 else -1e18)]
    idx.sort(key=lambda i: -curve[i])
    return idx


def _axis_detect(ev, axis, steps, mat):
    """Pick + refine the step for one axis.

    Returns dict(step, score, steps, curve, rescore) - rescore is reused by
    the cross-axis reconciliation."""
    from .channels import _exclusive_slot_occupancy
    ax = ev[axis]
    extent = ax["extent"]
    curve = fused_curve(mat)

    # channel normalizers for rescoring refined (off-ladder) steps
    maxes = np.maximum(mat.max(axis=0), 1e-9)
    active = [(j, c) for j, c in enumerate(CHANNELS)
              if RESCORE_WEIGHTS[c] != 0.0]
    memo = {}

    def rescore(s):
        key = round(float(s), 4)
        if key in memo:
            return memo[key]
        sc = channel_scores(ev, axis, key, only={c for _j, c in active})
        v = float(sum(RESCORE_WEIGHTS[c] * max(sc[c], 0.0) / maxes[j]
                      for j, c in active))
        memo[key] = v
        return v

    def snaps(s, span=2):
        """Integer cell-count snaps around the count implied by s."""
        out = set()
        c0 = extent / s
        for dc in range(-span, span + 1):
            c = int(round(c0)) + dc
            if c >= 2 and extent / c >= 1.9:
                out.add(round(extent / c, 4))
        return out

    def refine(s0, full=False):
        """Refined candidates near s0 -> (best step, fused score).

        Seeds: iterated lattice LSQ fits on E1/E2 (single fits crawl by
        ~0.005/iter and stall - see FINDINGS), a fine geometric scan when
        `full`, and integer-cell-count snaps of every seed. The fused
        rescore picks; exact rational steps (extent/c) win by a huge margin
        whenever the grid truly spans the image."""
        cands = {round(float(s0), 4)}
        for prof in (ax["e1"], ax["e2"]):
            s = float(s0)
            for _ in range(3):
                s2 = float(_lattice_refine(prof, s))
                if abs(s2 - s) < 5e-4:
                    break
                s = s2
            cands.add(round(s, 4))
        if full:
            for m in np.linspace(0.955, 1.045, 19):
                cands.add(round(s0 * m, 4))
        for s in list(cands):
            cands |= snaps(s, span=3 if full else 2)
        best = (float(s0), -1e18)
        for s in sorted(cands):
            if s < 1.9 or s > extent / 3:
                continue
            v = rescore(s)
            if v > best[1]:
                best = (float(s), v)
        return best

    def excl_occ(s_small, s_big):
        """Occupancy of s_small's lattice slots not shared with s_big."""
        ph = _rayleigh_score(ax["e1"], s_small)[1]
        return _exclusive_slot_occupancy(ax["e1"], s_small, ph, s_big)

    peaks = _local_maxima(steps, curve)
    if not peaks:
        return {"step": None, "score": 0.0, "steps": steps,
                "curve": curve, "rescore": rescore}
    best_val = curve[peaks[0]]

    # candidate pool: strong fused local maxima, refined (top-2 get the
    # full fine scan, the rest a light refine)
    pool = []
    for rank, i in enumerate(peaks[:6]):
        if curve[i] < 0.45 * best_val:
            break
        pool.append(refine(steps[i], full=(rank < 2)))
    pool.sort(key=lambda t: -t[1])
    step, score = pool[0]

    # fundamental check: an integer divisor that keeps most of the fused
    # score AND populates its own exclusive lattice slots is the real cell
    # size (content repeats at multiples of the pixel size)
    improved = True
    while improved:
        improved = False
        for div in (2, 3, 4):
            sub = step / div
            if sub < 1.9:
                continue
            s_sub, sc_sub = refine(sub)
            if (sc_sub >= 0.88 * score and abs(s_sub * div - step) < 0.6 * div
                    and excl_occ(s_sub, step) >= 0.30):
                step, score = s_sub, sc_sub
                improved = True
                break

    # anti-harmonic: if the winner is an integer subdivision of a decent
    # pool mate but owns no exclusive edge energy, it is a spurious
    # subdivision (blur-scale periodicity, jpeg 8/3 lattice, ...) - climb up
    for s_big, sc_big in pool:
        ratio = s_big / step
        k = int(round(ratio))
        if (2 <= k <= 5 and abs(ratio - k) < 0.08 * k
                and sc_big >= 0.60 * score
                and excl_occ(step, s_big) < 0.30):
            step, score = refine(s_big, full=True)
            break

    return {"step": step, "score": score, "steps": steps,
            "curve": curve, "rescore": rescore}


def detect(rgba: np.ndarray) -> dict:
    h, w = rgba.shape[:2]
    ev = build_evidence(rgba)
    all_steps = np.array(ladder())
    mx, my = _cached_matrices(rgba, ev, all_steps)

    results = {}
    for axis, extent, mat in (("x", w, mx), ("y", h, my)):
        max_step = min(max(4.0, extent / 8.0), 64.0)
        mask = all_steps <= max_step
        results[axis] = _axis_detect(ev, axis, all_steps[mask], mat[mask])

    rx, ry = results["x"], results["y"]
    step_x, score_x = rx["step"], rx["score"]
    step_y, score_y = ry["step"], ry["score"]

    if step_x is None and step_y is None:
        step_x = step_y = max(1.0, max(w, h) / 128.0)
        score_x = score_y = 0.0
    elif step_x is None:
        step_x, score_x = step_y, score_y
    elif step_y is None:
        step_y, score_y = step_x, score_x

    # square-pixel reconciliation: when the axes disagree by a non-integer
    # ratio, test each axis's step on the other axis with the fused rescore
    # and adopt the square pair only if it clearly beats keeping both
    ratio = max(step_x, step_y) / min(step_x, step_y)
    near_int = abs(ratio - round(ratio)) < 0.10 and round(ratio) >= 2
    if ratio > 1.02 and not near_int:
        keep = score_x + score_y
        best = (None, keep * 1.05)
        for s in (step_x, step_y):
            tot = rx["rescore"](s) + ry["rescore"](s)
            if tot > best[1]:
                best = (s, tot)
        if best[0] is not None:
            step_x = step_y = best[0]

    # cell-count arbitration: when extent/step sits near a half cell the
    # rounded count is a coin flip; let the fused rescore compare the exact
    # rationals for both counts instead (grid.py solves this inside the DP
    # with an integer-step prior; here the channels themselves decide)
    def pick_count(res, extent, step):
        c0 = extent / step
        frac = abs(c0 - round(c0))
        if frac < 0.2 or res["step"] is None:
            return int(round(c0)), step
        lo, hi = int(np.floor(c0)), int(np.ceil(c0))
        best = (int(round(c0)), step, -1e18)
        for c in (lo, hi):
            if c < 1:
                continue
            s = extent / c
            v = res["rescore"](s)
            if v > best[2]:
                best = (c, s, v)
        return best[0], best[1]

    cols, step_x = pick_count(rx, w, step_x)
    rows, step_y = pick_count(ry, h, step_y)

    cands = sorted(zip(rx["steps"], rx["curve"]), key=lambda t: -t[1])[:8]
    return {
        "step_x": float(step_x), "step_y": float(step_y),
        "cols": int(cols), "rows": int(rows),
        "phase_x": 0.0, "phase_y": 0.0,
        "candidates": [(float(s), float(c)) for s, c in cands],
    }


if __name__ == "__main__" and "--extract" in sys.argv:
    extract_table()
    sys.exit(0)
if __name__ == "__main__" and "--analyze" in sys.argv:
    analyze()
    sys.exit(0)
