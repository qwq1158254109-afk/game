"""Fast color quantization helpers.

k-means quantization (cv2) is used as a *pre-processing* step exactly like
spritefusion-pixel-snapper does: reducing to ~16 colors before computing
edge profiles turns anti-aliased / bilinear ramps into hard steps located at
the true cell boundaries and suppresses jpeg noise, which makes the grid
detector's job dramatically easier. The same labels are reused later for
per-cell color voting during downscale.
"""

from __future__ import annotations

import numpy as np

try:
    import cv2
    _HAS_CV2 = True
except ImportError:  # pragma: no cover
    _HAS_CV2 = False


def kmeans_quantize(rgba: np.ndarray, k: int = 16, sample_max: int = 60_000,
                    seed: int = 42) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Quantize an image with k-means in RGB.

    Returns (quantized_rgba uint8, labels (H,W) int32, centers (k,3) float32).
    Fully transparent pixels keep their color but get label of their nearest
    center anyway (harmless: alpha handling is done downstream).
    """
    h, w = rgba.shape[:2]
    rgb = rgba[:, :, :3].reshape(-1, 3).astype(np.float32)

    rng = np.random.default_rng(seed)
    if rgb.shape[0] > sample_max:
        idx = rng.choice(rgb.shape[0], sample_max, replace=False)
        sample = rgb[idx]
    else:
        sample = rgb

    uniq = np.unique(sample, axis=0)
    k_eff = int(min(k, len(uniq)))
    if k_eff <= 1:
        centers = uniq if len(uniq) else np.zeros((1, 3), np.float32)
        labels = np.zeros((h, w), np.int32)
        return rgba.copy(), labels, centers.astype(np.float32)

    if _HAS_CV2:
        criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 12, 0.5)
        _, _, centers = cv2.kmeans(sample, k_eff, None, criteria, 2,
                                   cv2.KMEANS_PP_CENTERS)
    else:
        from sklearn.cluster import KMeans
        km = KMeans(n_clusters=k_eff, n_init=2, max_iter=15,
                    random_state=seed).fit(sample)
        centers = km.cluster_centers_.astype(np.float32)

    # assign every pixel to nearest center (chunked to bound memory)
    labels_flat = np.empty(rgb.shape[0], dtype=np.int32)
    chunk = 1 << 20
    for s in range(0, rgb.shape[0], chunk):
        block = rgb[s:s + chunk]
        d = ((block[:, None, :] - centers[None, :, :]) ** 2).sum(axis=2)
        labels_flat[s:s + chunk] = np.argmin(d, axis=1)

    labels = labels_flat.reshape(h, w)
    out = rgba.copy()
    out[:, :, :3] = np.clip(np.rint(centers[labels_flat]), 0, 255) \
        .astype(np.uint8).reshape(h, w, 3)
    return out, labels, centers.astype(np.float32)
