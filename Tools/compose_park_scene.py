from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image


SOURCE_ASSETS = Path(
    r"C:\Users\ADMINI~1\AppData\Local\Temp\codex-clipboard-b4484a2b-82ee-43f0-9a4f-eaa1a9219bbe.png"
)
SOURCE_BASE = Path(
    r"C:\Users\ADMINI~1\AppData\Local\Temp\codex-clipboard-6d3718fb-9bd9-48ee-a82e-9f22bcebf091.png"
)
OUTPUT = Path(__file__).resolve().parents[1] / "ParkScene_Complete.png"


@dataclass(frozen=True)
class SpriteSpec:
    crop: tuple[int, int, int, int]
    scale: float = 1.0


SPRITES = {
    "wc": SpriteSpec((205, 35, 440, 305), 0.86),
    "tree_large": SpriteSpec((515, 45, 695, 282), 0.86),
    "tree_medium": SpriteSpec((752, 73, 932, 282), 0.88),
    "tree_small": SpriteSpec((990, 113, 1137, 282), 0.94),
    "bench": SpriteSpec((198, 390, 386, 538), 0.86),
    "lamp": SpriteSpec((470, 315, 542, 548), 0.86),
    "trash": SpriteSpec((628, 395, 731, 535), 0.84),
    "flower_a": SpriteSpec((775, 392, 1055, 535), 0.82),
    "flower_b": SpriteSpec((1090, 392, 1368, 535), 0.82),
    # The cap is reused as a small paving slab. It is sourced from Image 1.
    "stone_cap": SpriteSpec((214, 796, 281, 857), 0.55),
}


@dataclass(frozen=True)
class Placement:
    name: str
    xy: tuple[int, int]
    z: int | None = None


PLACEMENTS = [
    # Perimeter trees: alternate all three source sizes and keep the centre open.
    Placement("tree_large", (36, 76)),
    Placement("tree_medium", (413, -8)),
    Placement("tree_small", (716, -4)),
    Placement("tree_medium", (1262, -2)),
    Placement("tree_large", (42, 300)),
    Placement("tree_small", (69, 690)),
    Placement("tree_medium", (1323, 300)),
    Placement("tree_large", (1334, 551)),
    Placement("tree_small", (1271, 716)),

    # Main visual nodes.
    Placement("wc", (232, 122)),
    Placement("flower_a", (568, 157)),
    Placement("lamp", (392, 439)),
    Placement("lamp", (963, 443)),
    Placement("bench", (242, 606)),
    Placement("bench", (1071, 601)),
    Placement("trash", (1260, 668)),
    Placement("flower_b", (314, 735)),
    Placement("flower_a", (966, 733)),

    # Sparse, staggered WC-to-road stepping stones.
    Placement("stone_cap", (309, 356)),
    Placement("stone_cap", (329, 390)),
    Placement("stone_cap", (302, 427)),
    Placement("stone_cap", (323, 466)),
    Placement("stone_cap", (306, 503)),
]


def neutral_checker_mask(rgb: np.ndarray) -> np.ndarray:
    """Detect the baked white/grey checkerboard without softening sprite pixels."""
    minimum = rgb.min(axis=2)
    spread = rgb.max(axis=2) - minimum
    return (minimum >= 225) & (spread <= 18)


def edge_connected_background(candidate: np.ndarray) -> np.ndarray:
    """Keep enclosed pale sprite highlights while removing checkerboard holes."""
    height, width = candidate.shape
    visited = np.zeros_like(candidate, dtype=bool)
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        if candidate[0, x]:
            queue.append((x, 0))
        if candidate[height - 1, x]:
            queue.append((x, height - 1))
    for y in range(height):
        if candidate[y, 0]:
            queue.append((0, y))
        if candidate[y, width - 1]:
            queue.append((width - 1, y))

    while queue:
        x, y = queue.popleft()
        if visited[y, x] or not candidate[y, x]:
            continue
        visited[y, x] = True
        if x:
            queue.append((x - 1, y))
        if x + 1 < width:
            queue.append((x + 1, y))
        if y:
            queue.append((x, y - 1))
        if y + 1 < height:
            queue.append((x, y + 1))
    return visited


def strip_neutral_halo(rgb: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    """Remove only bright neutral boundary pixels left by the baked checkerboard."""
    result = alpha.copy()
    minimum = rgb.min(axis=2)
    maximum = rgb.max(axis=2)
    spread = maximum - minimum
    luminance = rgb.mean(axis=2)
    neutral_bright = (luminance >= 140) & (spread <= 32)

    # Two hard-edge passes remove the 1-2 source-pixel checker fringe. No color
    # interpolation, erosion, blur, or antialiasing is applied to the sprite body.
    for _ in range(2):
        transparent = result == 0
        adjacent = np.zeros_like(transparent)
        adjacent[1:] |= transparent[:-1]
        adjacent[:-1] |= transparent[1:]
        adjacent[:, 1:] |= transparent[:, :-1]
        adjacent[:, :-1] |= transparent[:, 1:]
        result[(result > 0) & adjacent & neutral_bright] = 0
    return result


def extract_sprite(sheet: Image.Image, spec: SpriteSpec) -> Image.Image:
    crop = sheet.crop(spec.crop).convert("RGB")
    rgb = np.asarray(crop)
    checker = neutral_checker_mask(rgb)
    background = edge_connected_background(checker)

    rgba = np.empty((*rgb.shape[:2], 4), dtype=np.uint8)
    rgba[:, :, :3] = rgb
    alpha = np.where(background, 0, 255).astype(np.uint8)
    rgba[:, :, 3] = strip_neutral_halo(rgb, alpha)
    sprite = Image.fromarray(rgba, "RGBA")

    if spec.scale != 1.0:
        width = max(1, round(sprite.width * spec.scale))
        height = max(1, round(sprite.height * spec.scale))
        sprite = sprite.resize((width, height), Image.Resampling.NEAREST)
    return sprite


def paste_clipped(canvas: Image.Image, sprite: Image.Image, xy: tuple[int, int]) -> None:
    x, y = xy
    left = max(0, -x)
    top = max(0, -y)
    right = min(sprite.width, canvas.width - x)
    bottom = min(sprite.height, canvas.height - y)
    if left >= right or top >= bottom:
        return
    clipped = sprite.crop((left, top, right, bottom))
    canvas.alpha_composite(clipped, (max(0, x), max(0, y)))


def main() -> None:
    sheet = Image.open(SOURCE_ASSETS).convert("RGB")
    base = Image.open(SOURCE_BASE).convert("RGBA")
    if base.size != (1536, 1024):
        raise ValueError(f"Unexpected base size: {base.size}")

    sprites = {name: extract_sprite(sheet, spec) for name, spec in SPRITES.items()}

    # Painter order follows each object's ground contact, with stable source order
    # for ties. This keeps lower objects naturally in front without rotations.
    ordered = sorted(
        enumerate(PLACEMENTS),
        key=lambda item: (
            item[1].z
            if item[1].z is not None
            else item[1].xy[1] + sprites[item[1].name].height,
            item[0],
        ),
    )
    for _, placement in ordered:
        paste_clipped(base, sprites[placement.name], placement.xy)

    # The scene is a complete opaque map; RGBA is retained for lossless PNG output.
    base.save(OUTPUT, format="PNG", optimize=False, compress_level=6)
    print(f"saved={OUTPUT}")
    print(f"size={base.size} mode={base.mode}")
    print("resampling=nearest only")
    print("reference_pixels_used=no")


if __name__ == "__main__":
    main()
