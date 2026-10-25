"""Server entry point: one call, bytes in, pixel art out.

    from detector.api import process
    result = process(image_bytes)                     # full quality
    result = process(image_bytes, mode="fast")        # bounded latency
    result = process(image_bytes, low_memory=True)    # tight containers

Engineered for server use: input caps, explicit frees, no globals, no
temp files, everything self-contained inside detector/ (numpy + scipy +
cv2 + PIL only).

result dict:
    cols, rows            predicted native resolution
    step_x, step_y        detected cell size (px)
    consensus             decision path (fast:… / arbitrated / fastmode:…)
    confidence            "high" | "medium" | "low"
    png                   reconstructed pixel art, PNG bytes (RGBA)
    detect_s, recon_s     stage timings
"""

from __future__ import annotations

import gc
import io
import time

import numpy as np

MAX_PIXELS = 4_000_000
MIN_SIDE = 16


class InputError(ValueError):
    pass


def _load(image) -> np.ndarray:
    from PIL import Image
    if isinstance(image, np.ndarray):
        if image.ndim != 3 or image.shape[2] not in (3, 4):
            raise InputError("ndarray must be (H, W, 3|4) uint8")
        arr = image if image.shape[2] == 4 else np.dstack(
            [image, np.full(image.shape[:2], 255, np.uint8)])
        return np.ascontiguousarray(arr, dtype=np.uint8)
    im = Image.open(io.BytesIO(image))
    im = im.convert("RGBA")
    return np.array(im)


def process(image, mode: str = "full", low_memory: bool = False,
            dark_stroke: bool = False, palette_snap: bool = False,
            two_stage: bool = True, k_colors: int = 0,
            force_step: float | None = None,
            return_png: bool = True) -> dict:
    """image: PNG/JPG bytes or uint8 ndarray. See module docstring."""
    rgba = _load(image)
    h, w = rgba.shape[:2]
    if min(h, w) < MIN_SIDE:
        raise InputError(f"image too small (min side {MIN_SIDE}px)")
    if h * w > MAX_PIXELS:
        raise InputError(f"image too large ({h*w/1e6:.1f}MP > "
                         f"{MAX_PIXELS/1e6:.0f}MP limit)")

    from .core import detect
    from .reconstruct import reconstruct, two_stage_pack

    t0 = time.time()
    if force_step:
        r = {"step_x": float(force_step), "step_y": float(force_step),
             "cols": max(1, round(w / force_step)),
             "rows": max(1, round(h / force_step)),
             "consensus": "forced"}
    else:
        r = detect(rgba, mode=mode, low_memory=low_memory)
    detect_s = time.time() - t0

    t0 = time.time()
    if two_stage:
        # default: quantise-for-structure + original-pixels-for-colour on a
        # regular even grid (crisp lines, accurate colours, no palette loss)
        low = two_stage_pack(rgba, r["cols"], r["rows"], k_colors=k_colors)
    else:
        low = reconstruct(rgba, r["step_x"], r["step_y"], r["cols"], r["rows"],
                          color="mode", palette_snap=palette_snap,
                          dark_stroke=dark_stroke)
    recon_s = time.time() - t0

    consensus = str(r.get("consensus", ""))
    if consensus.startswith("fast:"):
        confidence = "high"
    elif consensus in ("arbitrated", "forced") or consensus.startswith("fastmode:") and "+" in consensus:
        confidence = "medium"
    else:
        confidence = "low"

    out = {
        "cols": int(r["cols"]), "rows": int(r["rows"]),
        "step_x": round(float(r["step_x"]), 4),
        "step_y": round(float(r["step_y"]), 4),
        "consensus": consensus, "confidence": confidence,
        "detect_s": round(detect_s, 3), "recon_s": round(recon_s, 3),
    }
    if return_png:
        from PIL import Image
        buf = io.BytesIO()
        Image.fromarray(low).save(buf, "PNG")
        out["png"] = buf.getvalue()
    else:
        out["array"] = low

    del rgba
    gc.collect()
    return out
