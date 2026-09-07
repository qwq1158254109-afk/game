from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_SHEET = PROJECT_ROOT / "Assets/Art/New2_0/Park/Source/ParkFacilitiesSheet.png"
OUTPUT_ROOT = PROJECT_ROOT / "Assets/Art/New2_0/Park/Sprites"


@dataclass(frozen=True)
class SpriteSpec:
    output_name: str
    crop: tuple[int, int, int, int]
    scale: float


SPRITES = (
    SpriteSpec("Park_WC.png", (205, 35, 440, 305), 0.86),
    SpriteSpec("Park_Tree_Large.png", (515, 45, 695, 282), 0.86),
    SpriteSpec("Park_Tree_Medium.png", (752, 73, 932, 282), 0.88),
    SpriteSpec("Park_Tree_Small.png", (990, 113, 1137, 282), 0.94),
    SpriteSpec("Park_Bench.png", (198, 390, 386, 538), 0.86),
    SpriteSpec("Park_StreetLight.png", (470, 315, 542, 548), 0.86),
    SpriteSpec("Park_TrashBin.png", (628, 395, 731, 535), 0.84),
    SpriteSpec("Park_FlowerBed_A.png", (775, 392, 1055, 535), 0.82),
    SpriteSpec("Park_FlowerBed_B.png", (1090, 392, 1368, 535), 0.82),
    SpriteSpec("Park_SteppingStone.png", (214, 796, 281, 857), 0.55),
)


def neutral_checker_mask(rgb: np.ndarray) -> np.ndarray:
    minimum = rgb.min(axis=2)
    spread = rgb.max(axis=2) - minimum
    return (minimum >= 225) & (spread <= 18)


def edge_connected_background(candidate: np.ndarray) -> np.ndarray:
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
        if x > 0:
            queue.append((x - 1, y))
        if x + 1 < width:
            queue.append((x + 1, y))
        if y > 0:
            queue.append((x, y - 1))
        if y + 1 < height:
            queue.append((x, y + 1))
    return visited


def strip_neutral_halo(rgb: np.ndarray, alpha: np.ndarray) -> np.ndarray:
    result = alpha.copy()
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    luminance = rgb.mean(axis=2)
    neutral_bright = (luminance >= 140) & (spread <= 32)

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

    width = max(1, round(sprite.width * spec.scale))
    height = max(1, round(sprite.height * spec.scale))
    return sprite.resize((width, height), Image.Resampling.NEAREST)


def main() -> None:
    if not SOURCE_SHEET.is_file():
        raise FileNotFoundError(f"Missing park facility source sheet: {SOURCE_SHEET}")

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    sheet = Image.open(SOURCE_SHEET).convert("RGB")
    if sheet.size != (1536, 1024):
        raise ValueError(f"Unexpected park facility sheet size: {sheet.size}")

    for spec in SPRITES:
        sprite = extract_sprite(sheet, spec)
        output_path = OUTPUT_ROOT / spec.output_name
        sprite.save(output_path, format="PNG", optimize=False, compress_level=6)
        alpha = np.asarray(sprite.getchannel("A"))
        print(
            f"{output_path.relative_to(PROJECT_ROOT)} "
            f"size={sprite.size} opaque_pixels={int(np.count_nonzero(alpha))}"
        )


if __name__ == "__main__":
    main()
