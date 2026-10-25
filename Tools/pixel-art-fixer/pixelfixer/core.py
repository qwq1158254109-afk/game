"""pixelfixer detector core: consensus-first ensemble of four method prototypes.

autocorr (banded ACF, 17/26), runlengths (boundary combs, 17/26),
fusion (sum-fused channels, 17/26), selfsim (shift dissimilarity, 16/26)
fail on different images; oracle union 18 exact / 26 near.

Stage 1 - consensus: if >=2 proposals agree on the output size (within
max(1 cell, 1%)), adopt it (autocorr steps preferred for precision,
median counts of the agreeing proposals).

Stage 2 - arbitration (real disagreement): per-axis candidate pool from
all four + autocorr's ranked list, scored by
    normalized fused evidence + 0.2 * square-packer z
    + 0.25 * (#sources agreeing - 1) ,
with the smallest-qualified-peak rule (>= 0.78 of best score) that three
agents independently converged on for harmonic hygiene. Aspect guard,
ACF peak-train refinement, drift-aware counting.
"""
import sys, os

import numpy as np

from . import autocorr as A
from . import runlengths as m_rl
from . import fusion as m_fu
from . import selfsim as m_ss

AGREE_TOL = 0.02
AGREE_BONUS = 0.25
VC_W = 0.20
SMALLEST_QUALIFIED = 0.78


def _size_close(a, b):
    tol_c = max(1, round(0.01 * b["cols"]))
    tol_r = max(1, round(0.01 * b["rows"]))
    return (abs(a["cols"] - b["cols"]) <= tol_c and
            abs(a["rows"] - b["rows"]) <= tol_r)


def detect(rgba, mode: str = "full", low_memory: bool = False):
    """mode "full": consensus + arbitration (best accuracy).
    mode "fast": cheap detectors only; on disagreement returns the
    autocorr answer flagged low-confidence (bounded latency, ~1-3s).
    low_memory: serialize the heavy stage-2 builds (~40% lower peak)."""
    h, w = rgba.shape[:2]

    # ---- run the three cheap detectors concurrently (numpy releases the
    # GIL on the large array ops, so threads buy real wall-clock)
    from concurrent.futures import ThreadPoolExecutor

    shared = {}

    def _run_ac():
        g = A.to_gray(rgba)
        gq = A.median_quant(g)
        maps_x = [(A.d2_along(g, 1), 1.0), (A.d1_along(gq, 1), 0.7)]
        maps_y = [(A.d2_along(g, 0), 1.0), (A.d1_along(gq, 0), 0.7)]
        est_x = A.axis_estimate(maps_x, 1, w)
        est_y = A.axis_estimate(maps_y, 0, h)
        shared.update(maps_x=maps_x, maps_y=maps_y,
                      est_x=est_x, est_y=est_y)
        return A.detect(rgba, pre={"maps_x": maps_x, "maps_y": maps_y,
                                   "est_x": est_x, "est_y": est_y})

    props = {}
    with ThreadPoolExecutor(max_workers=3) as pool:
        f_ac = pool.submit(_run_ac)
        f_rl = pool.submit(m_rl.detect, rgba)
        for name, fut in (("ac", f_ac), ("rl", f_rl)):
            try:
                props[name] = fut.result()
            except Exception:
                pass

        # calibrated early exit: runlengths' comb peak height S >= 0.3 was
        # always correct on the benchmark; when ac agrees on the size too,
        # selfsim adds nothing - skip it (biggest fast-path cost)
        if ("ac" in props and "rl" in props
                and _size_close(props["ac"], props["rl"])
                and min(props["rl"].get("score_x", 0.0),
                        props["rl"].get("score_y", 0.0)) >= 0.30):
            a, b = props["ac"], props["rl"]
            cols = int(round((a["cols"] + b["cols"]) / 2))
            rows = int(round((a["rows"] + b["rows"]) / 2))
            return dict(step_x=float(w / cols), step_y=float(h / rows),
                        cols=cols, rows=rows, phase_x=0.0, phase_y=0.0,
                        consensus="fast:ac+rl(S)")

        try:
            props["ss"] = pool.submit(m_ss.detect, rgba).result()
        except Exception:
            pass
    if not props:
        raise RuntimeError("all sub-detectors failed")

    if mode == "fast":
        # bounded latency: no arbitration; prefer any 2-way agreement,
        # else autocorr, flagged low-confidence
        names = list(props)
        for i in range(len(names)):
            for j in range(i + 1, len(names)):
                a, b = props[names[i]], props[names[j]]
                if _size_close(a, b):
                    return dict(step_x=a["step_x"], step_y=a["step_y"],
                                cols=a["cols"], rows=a["rows"],
                                phase_x=0.0, phase_y=0.0,
                                consensus=f"fastmode:{names[i]}+{names[j]}")
        a = props.get("ac") or props[names[0]]
        return dict(step_x=a["step_x"], step_y=a["step_y"],
                    cols=a["cols"], rows=a["rows"],
                    phase_x=0.0, phase_y=0.0,
                    consensus="fastmode:lowconf")
    if "maps_x" not in shared:      # ac thread failed; rebuild serially
        g = A.to_gray(rgba)
        gq = A.median_quant(g)
        shared["maps_x"] = [(A.d2_along(g, 1), 1.0), (A.d1_along(gq, 1), 0.7)]
        shared["maps_y"] = [(A.d2_along(g, 0), 1.0), (A.d1_along(gq, 0), 0.7)]
        shared["est_x"] = A.axis_estimate(shared["maps_x"], 1, w)
        shared["est_y"] = A.axis_estimate(shared["maps_y"], 0, h)
    maps_x, maps_y = shared["maps_x"], shared["maps_y"]
    est_x, est_y = shared["est_x"], shared["est_y"]

    # fast path: when the three cheap detectors already agree on the size,
    # skip the expensive fusion/arbitration machinery entirely
    if len(props) >= 3:
        fast = list(props)
        agree = [n for n in fast if _size_close(props[n], props[fast[0]])]
        r0 = (props[fast[0]]["cols"] / max(props[fast[0]]["rows"], 1)) / (w / h)
        if len(agree) >= 3 and abs(np.log(r0)) < 0.35:
            cols = int(np.median([props[n]["cols"] for n in agree]))
            rows = int(np.median([props[n]["rows"] for n in agree]))
            return dict(step_x=float(w / cols), step_y=float(h / rows),
                        cols=cols, rows=rows, phase_x=0.0, phase_y=0.0,
                        consensus="fast:" + "+".join(agree))

    # slow evidence needed. The three heavy builds (fused evidence,
    # square-packer curve, distillability tables) are independent - build
    # them concurrently, then derive the fused-argmax fourth voter.
    from concurrent.futures import ThreadPoolExecutor as _TPE
    from .varcontrast import CellVarContrast
    from . import reconsearch as R

    def _build_ev():
        return m_fu.build_evidence(rgba, lean=True)

    def _build_vc():
        vc = CellVarContrast(rgba)
        vc_z, vc_cands_ = vc.z_channel()
        return vc, vc_z, vc_cands_

    def _build_recon():
        ch = R._prep(rgba)
        recon_ = {}
        for axis, extent in (("x", w), ("y", h)):
            ad = R.AxisData(ch, 0 if axis == "x" else 1)
            s_list = R._s_grid(extent)[::3]
            eb, er = R._coarse_curves(ad, s_list)
            recon_[axis] = (ad, R._trend_fn(s_list, eb))
        return recon_

    if low_memory:
        ev = _build_ev()
        vc, vc_z, _vc_cands = _build_vc()
        recon = _build_recon()
    else:
        with _TPE(max_workers=3) as pool:
            f_ev = pool.submit(_build_ev)
            f_vc = pool.submit(_build_vc)
            f_re = pool.submit(_build_recon)
            ev = f_ev.result()
            vc, vc_z, _vc_cands = f_vc.result()
            recon = f_re.result()

    steps = m_fu.ladder()
    log_steps = np.log(steps)
    curves = {}
    for axis in ("x", "y"):
        mat = m_fu.channel_matrix(ev, axis, steps, only=m_fu.ACTIVE_CHANNELS)
        c = m_fu.fused_curve(mat)
        m = float(np.max(c))
        curves[axis] = c / m if m > 0 else c
    fu_prop = {
        "step_x": float(steps[int(np.argmax(curves["x"]))]),
        "step_y": float(steps[int(np.argmax(curves["y"]))]),
    }
    fu_prop["cols"] = max(1, round(w / fu_prop["step_x"]))
    fu_prop["rows"] = max(1, round(h / fu_prop["step_y"]))
    props["fu"] = fu_prop

    # ---- stage 1: consensus on output size
    names = list(props)
    groups = []
    for n1 in names:
        grp = [n2 for n2 in names if _size_close(props[n2], props[n1])]
        groups.append((len(grp), n1, grp))
    groups.sort(key=lambda t: -t[0])
    # correlated failures make 2-of-4 unsafe (methods share texture/content
    # locks); only a supermajority short-circuits arbitration, and it must
    # respect the image's aspect (cols/rows ~ w/h for near-square cells)
    def _aspect_ok(n1):
        r = (props[n1]["cols"] / max(props[n1]["rows"], 1)) / (w / h)
        return abs(np.log(r)) < 0.35
    groups = [g for g in groups if _aspect_ok(g[1])]
    if groups and groups[0][0] >= 3:
        grp = groups[0][2]
        cols = int(np.median([props[n]["cols"] for n in grp]))
        rows = int(np.median([props[n]["rows"] for n in grp]))
        base = props["ac"] if "ac" in grp else props[grp[0]]
        return dict(step_x=float(w / cols), step_y=float(h / rows),
                    cols=cols, rows=rows,
                    phase_x=base.get("phase_x", 0.0),
                    phase_y=base.get("phase_y", 0.0),
                    consensus="+".join(grp))

    # ---- stage 2: arbitration
    sx_ac, cand_x, ac_x = est_x
    sy_ac, cand_y, ac_y = est_y

    # detail-scale bound: a cell is flat inside, so the finest feature scale
    # (ACF central peak half-width) caps the plausible step. 60px cells
    # cannot coexist with 3px detail.
    def _acf_width(ac):
        tail = ac[32:96] if len(ac) > 40 else ac[len(ac) // 2:]
        base = float(np.median(tail)) if len(tail) else 0.0
        c0 = max(ac[0] - base, 1e-9)
        for lag in range(1, min(len(ac), 64)):
            if ac[lag] - base < 0.30 * c0:
                return float(lag)
        return 64.0
    detail_cap_x = 8.0 * _acf_width(ac_x)
    detail_cap_y = 8.0 * _acf_width(ac_y)


    def recon_at(axis, s):
        ad, trend = recon[axis]
        if s < 2.0 or s > (w if axis == "x" else h) / 8.0:
            return 0.0
        eb1, er1 = ad.eval_s(s)
        return R._score(eb1, er1, trend(s))

    def fused_at(axis, s):
        if s < steps[0] or s > steps[-1]:
            return 0.0
        return float(np.interp(np.log(s), log_steps, curves[axis]))

    def vc_at(s):
        return max(0.0, min(vc_z(s) / 10.0, 1.0))

    def pick_axis(axis, ac_cands, extent):
        key = "step_x" if axis == "x" else "step_y"
        sources = {n: float(props[n][key]) for n in props}
        pool = list(sources.values()) + [s for s, _z in ac_cands[:5]]

        cands = []
        seen = []
        for s in pool:
            if s <= 1.2 or s > extent / 3:
                continue
            if any(abs(np.log(s / s2)) < 0.01 for s2 in seen):
                continue
            seen.append(s)
            cands.append(s)
        if not cands:
            return sources.get("ac", 4.0)

        rn = {s: recon_at(axis, s) for s in cands}
        rmax = max(rn.values())
        # a lattice must actually exist for the distillability term to mean
        # anything; below this floor it is pure noise (warped-cell images)
        recon_ok = rmax > 0.005

        cap = detail_cap_x if axis == "x" else detail_cap_y
        scored = []
        for s in cands:
            agree = sum(1 for v in sources.values()
                        if abs(np.log(max(v, 1e-9) / s)) < AGREE_TOL)
            sc = (fused_at(axis, s) + VC_W * vc_at(s)
                  + AGREE_BONUS * max(0, agree - 1))
            if recon_ok:
                sc += 0.6 * (rn[s] / rmax)
            if s > cap:
                sc *= 0.35   # step incompatible with the finest detail
            scored.append((s, sc))
        best = max(sc for _s, sc in scored)
        qualified = [s for s, sc in scored if sc >= SMALLEST_QUALIFIED * best]
        return min(qualified)

    with _TPE(max_workers=2) as _pool2:
        _fx = _pool2.submit(pick_axis, "x", cand_x, w)
        _fy = _pool2.submit(pick_axis, "y", cand_y, h)
        sx = _fx.result()
        sy = _fy.result()

    # aspect guard: pseudo-pixel cells are never wildly non-square (worst
    # confirmed real case is 1.5:1). On wild disagreement, prefer the FINER
    # step when it has real support on its own axis: a too-fine grid loses
    # nothing (pure subdivision) while a too-coarse grid destroys detail.
    if abs(np.log(sx / sy)) > 0.45:
        s_fine, ax_fine = (sx, "x") if sx < sy else (sy, "y")
        if fused_at(ax_fine, s_fine) >= 0.35:
            sx = sy = s_fine
    if abs(np.log(sx / sy)) > 0.45:
        def _both(s):
            rx, ry = recon_at("x", s), recon_at("y", s)
            r_term = 0.5 * (rx + ry) / max(rx, ry, 1e-9)                 if max(rx, ry) > 0.005 else 0.0
            v = r_term + fused_at("x", s)                 + fused_at("y", s) + 2 * VC_W * vc_at(s)
            if s > min(detail_cap_x, detail_cap_y):
                v *= 0.35
            return v
        sx, sy = (sx, sx) if _both(sx) >= _both(sy) else (sy, sy)

    sx = A.refine_step_acf(ac_x, sx)
    sy = A.refine_step_acf(ac_y, sy)

    if abs(np.log(sx / sy)) < 0.08 and abs(sx - sy) > 1e-6:
        s = 2.0 * sx * sy / (sx + sy)
        sx = A.refine_step_acf(ac_x, s)
        sy = A.refine_step_acf(ac_y, s)

    n_cols = A.local_count(maps_x, 1, w, sx)
    n_rows = A.local_count(maps_y, 0, h, sy)

    return dict(step_x=float(w / n_cols), step_y=float(h / n_rows),
                cols=int(round(n_cols)), rows=int(round(n_rows)),
                phase_x=0.0, phase_y=0.0, consensus="arbitrated")
