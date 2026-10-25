"""sRGB <-> Oklab, closed form (self-contained for the server build)."""

import numpy as np


def srgb_to_oklab(rgb: np.ndarray) -> np.ndarray:
    """rgb float in [0,1], shape (..., 3) -> Oklab (..., 3)."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]

    def lin(u):
        return np.where(u <= 0.04045, u / 12.92, ((u + 0.055) / 1.055) ** 2.4)

    r, g, b = lin(r), lin(g), lin(b)
    l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    l, m, s = np.cbrt(l), np.cbrt(m), np.cbrt(s)
    return np.stack([
        0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
        1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
        0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
    ], axis=-1)


def oklab_to_srgb(lab: np.ndarray) -> np.ndarray:
    L, a, bb = lab[..., 0], lab[..., 1], lab[..., 2]
    l = (L + 0.3963377774 * a + 0.2158037573 * bb) ** 3
    m = (L - 0.1055613458 * a - 0.0638541728 * bb) ** 3
    s = (L - 0.0894841775 * a - 1.2914855480 * bb) ** 3
    r = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s
    g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s
    b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s

    def unlin(u):
        u = np.clip(u, 0.0, 1.0)
        return np.where(u <= 0.0031308, 12.92 * u,
                        1.055 * np.power(u, 1 / 2.4) - 0.055)

    return np.stack([unlin(r), unlin(g), unlin(b)], axis=-1)
