from __future__ import annotations

import csv
import re
from collections import Counter
from pathlib import Path
from typing import Iterable

from PIL import Image


ROOT = Path(r"E:\unity\ML-project")
OUTPUT_ROOT = ROOT / "Assets" / "Art" / "CroppedObjects"
DOCS_ROOT = ROOT / "ScriptCodeDocs"
MANIFEST_PATH = DOCS_ROOT / "cropped_object_sprites_manifest.csv"
PREVIEW_PATH = DOCS_ROOT / "cropped_object_sprites_preview.png"

SOURCE_HOME_PROPS = ROOT / "Assets" / "Art" / "Props" / "HomeMap"
SOURCE_HOME_CHARACTERS = ROOT / "Assets" / "Art" / "Characters" / "HomeMap"
SOURCE_ANIMATED_32 = ROOT / "Assets" / "Art" / "Props" / "ModernInteriors" / "AnimatedObjects" / "32x32" / "Spritesheets"
SOURCE_TILESET_32 = ROOT / "Assets" / "Art" / "Modern_Interiors" / "1_Tilesets" / "32x32" / "Theme_Sorter_32x32"

MIN_OUTPUT_SIZE = 8
MAX_OUTPUT_SIZE = 256
PADDING = 2
ALPHA_THRESHOLD = 0


def ensure_dirs() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    DOCS_ROOT.mkdir(parents=True, exist_ok=True)


def clean_name(text: str) -> str:
    text = text.replace("32x32", "").replace("16x16", "").replace("48x48", "")
    text = re.sub(r"[^A-Za-z0-9]+", "_", text).strip("_").lower()
    text = re.sub(r"_+", "_", text)
    return text or "sprite"


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    if ALPHA_THRESHOLD <= 0:
        return alpha.getbbox()
    mask = alpha.point(lambda value: 255 if value > ALPHA_THRESHOLD else 0)
    return mask.getbbox()


def trim_transparency(image: Image.Image, padding: int = PADDING) -> Image.Image | None:
    bbox = alpha_bbox(image)
    if bbox is None:
        return None

    x1, y1, x2, y2 = bbox
    x1 = max(0, x1 - padding)
    y1 = max(0, y1 - padding)
    x2 = min(image.width, x2 + padding)
    y2 = min(image.height, y2 + padding)
    return image.crop((x1, y1, x2, y2))


def is_qualified(image: Image.Image) -> bool:
    if image.width < MIN_OUTPUT_SIZE or image.height < MIN_OUTPUT_SIZE:
        return False
    if image.width > MAX_OUTPUT_SIZE or image.height > MAX_OUTPUT_SIZE:
        return False

    alpha = image.getchannel("A")
    histogram = alpha.histogram()
    opaque = sum(histogram[1:])
    if opaque <= 0:
        return False

    alpha_ratio = opaque / float(image.width * image.height)
    if image.width <= 34 and image.height <= 34 and alpha_ratio > 0.92:
        return False
    return True


def save_sprite(
    source: Path,
    image: Image.Image,
    category: str,
    base_name: str,
    index: int | None,
    method: str,
    source_rect: str,
    records: list[dict[str, str]],
    seen_hashes: set[bytes],
) -> bool:
    image = image.convert("RGBA")
    image = trim_transparency(image)
    if image is None or not is_qualified(image):
        return False

    raw = image.tobytes()
    digest = f"{image.width}x{image.height}:".encode("ascii") + raw
    if digest in seen_hashes:
        return False
    seen_hashes.add(digest)

    output_dir = OUTPUT_ROOT / category
    output_dir.mkdir(parents=True, exist_ok=True)

    suffix = f"_{index:03d}" if index is not None else ""
    filename = f"{clean_name(base_name)}{suffix}.png"
    output_path = output_dir / filename
    image.save(output_path)
    records.append(
        {
            "output_path": str(output_path.relative_to(ROOT)).replace("\\", "/"),
            "source_path": str(source.relative_to(ROOT)).replace("\\", "/"),
            "category": category,
            "method": method,
            "source_rect": source_rect,
            "width": str(image.width),
            "height": str(image.height),
        }
    )
    return True


def crop_standalone(files: Iterable[Path], category: str, records: list[dict[str, str]], seen_hashes: set[bytes]) -> int:
    count = 0
    for source in sorted(files):
        with Image.open(source) as opened:
            image = opened.convert("RGBA")
        if save_sprite(source, image, category, source.stem, None, "trim_alpha_standalone", "full", records, seen_hashes):
            count += 1
    return count


def non_empty_column_groups(image: Image.Image) -> list[tuple[int, int]]:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    columns: list[int] = []
    for x in range(image.width):
        if any(pixels[x, y] > 0 for y in range(image.height)):
            columns.append(x)
    if not columns:
        return []

    groups: list[tuple[int, int]] = []
    start = previous = columns[0]
    for x in columns[1:]:
        if x == previous + 1:
            previous = x
            continue
        groups.append((start, previous))
        start = previous = x
    groups.append((start, previous))
    return groups


def infer_first_frame_rect(image: Image.Image) -> tuple[int, int, int, int]:
    groups = non_empty_column_groups(image)
    if len(groups) >= 2:
        starts = [group[0] for group in groups]
        diffs = [b - a for a, b in zip(starts[:-1], starts[1:]) if b > a]
        if diffs:
            frame_width, _ = Counter(diffs).most_common(1)[0]
            if 8 <= frame_width <= image.width and image.width // frame_width >= 2:
                return (0, 0, frame_width, image.height)

    if groups:
        first = groups[0]
        return (first[0], 0, min(image.width, first[1] + 1), image.height)
    return (0, 0, image.width, image.height)


def crop_animated_first_frames(records: list[dict[str, str]], seen_hashes: set[bytes]) -> int:
    if not SOURCE_ANIMATED_32.exists():
        return 0

    count = 0
    for source in sorted(SOURCE_ANIMATED_32.glob("*.png")):
        with Image.open(source) as opened:
            image = opened.convert("RGBA")
        rect = infer_first_frame_rect(image)
        cropped = image.crop(rect)
        if save_sprite(
            source,
            cropped,
            "ModernInteriors_AnimatedFirstFrame",
            source.stem,
            None,
            "first_animation_frame",
            ",".join(map(str, rect)),
            records,
            seen_hashes,
        ):
            count += 1
    return count


def parse_unity_sprite_rects(meta_path: Path) -> list[dict[str, int | str]]:
    if not meta_path.exists():
        return []

    rects: list[dict[str, int | str]] = []
    in_sheet = False
    current: dict[str, int | str] | None = None
    in_rect = False
    rect_keys = {"x", "y", "width", "height"}

    for raw_line in meta_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = raw_line.strip()
        if line == "spriteSheet:":
            in_sheet = True
            continue
        if not in_sheet:
            continue

        if line.startswith("- serializedVersion:"):
            if current and current.get("name") and all(key in current for key in rect_keys):
                rects.append(current)
            current = {}
            in_rect = False
            continue

        if current is None:
            continue

        if line.startswith("name: "):
            current["name"] = line.split("name: ", 1)[1].strip()
        elif line == "rect:":
            in_rect = True
        elif in_rect:
            for key in rect_keys:
                prefix = f"{key}: "
                if line.startswith(prefix):
                    value_text = line.split(prefix, 1)[1].strip()
                    try:
                        current[key] = int(float(value_text))
                    except ValueError:
                        pass

    if current and current.get("name") and all(key in current for key in rect_keys):
        rects.append(current)

    return rects


def crop_tileset_sprites(records: list[dict[str, str]], seen_hashes: set[bytes]) -> int:
    if not SOURCE_TILESET_32.exists():
        return 0

    count = 0
    source_files = [
        path
        for path in sorted(SOURCE_TILESET_32.glob("*.png"))
        if "floor" not in path.name.lower() and "ceiling" not in path.name.lower()
    ]

    for source in source_files:
        rects = parse_unity_sprite_rects(source.with_suffix(source.suffix + ".meta"))
        if not rects:
            continue
        with Image.open(source) as opened:
            image = opened.convert("RGBA")

        for index, rect in enumerate(rects):
            x = int(rect["x"])
            y = int(rect["y"])
            width = int(rect["width"])
            height = int(rect["height"])
            if width < MIN_OUTPUT_SIZE or height < MIN_OUTPUT_SIZE:
                continue
            left = x
            top = image.height - y - height
            right = left + width
            bottom = top + height
            if left < 0 or top < 0 or right > image.width or bottom > image.height:
                continue
            sprite = image.crop((left, top, right, bottom))
            name = str(rect.get("name") or source.stem)
            if save_sprite(
                source,
                sprite,
                "ModernInteriors_TilesetObjects",
                name,
                None,
                "unity_meta_sprite_rect",
                f"{x},{y},{width},{height}",
                records,
                seen_hashes,
            ):
                count += 1
    return count


def write_manifest(records: list[dict[str, str]]) -> None:
    fieldnames = ["output_path", "source_path", "category", "method", "source_rect", "width", "height"]
    with MANIFEST_PATH.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(records)


def write_preview(records: list[dict[str, str]], limit: int = 140) -> None:
    thumbs = []
    for record in records[:limit]:
        path = ROOT / record["output_path"]
        try:
            with Image.open(path) as opened:
                sprite = opened.convert("RGBA")
        except OSError:
            continue
        sprite.thumbnail((64, 64), Image.Resampling.NEAREST)
        thumbs.append((record, sprite.copy()))

    if not thumbs:
        return

    cols = 14
    cell = 84
    label_h = 18
    rows = (len(thumbs) + cols - 1) // cols
    preview = Image.new("RGBA", (cols * cell, rows * (cell + label_h)), (255, 255, 255, 255))

    for i, (_, sprite) in enumerate(thumbs):
        col = i % cols
        row = i // cols
        x = col * cell + (cell - sprite.width) // 2
        y = row * (cell + label_h) + (cell - sprite.height) // 2
        preview.alpha_composite(sprite, (x, y))

    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    preview.convert("RGB").save(PREVIEW_PATH)


def main() -> None:
    ensure_dirs()
    records: list[dict[str, str]] = []
    seen_hashes: set[bytes] = set()

    home_prop_count = crop_standalone(SOURCE_HOME_PROPS.glob("*.png"), "HomeMap_Props", records, seen_hashes)
    home_character_count = crop_standalone(
        SOURCE_HOME_CHARACTERS.glob("*.png"), "HomeMap_Characters", records, seen_hashes
    )
    animated_count = crop_animated_first_frames(records, seen_hashes)
    tileset_count = crop_tileset_sprites(records, seen_hashes)

    write_manifest(records)
    write_preview(records)

    print(f"output_root={OUTPUT_ROOT}")
    print(f"manifest={MANIFEST_PATH}")
    print(f"preview={PREVIEW_PATH}")
    print(f"home_props={home_prop_count}")
    print(f"home_characters={home_character_count}")
    print(f"animated_first_frames={animated_count}")
    print(f"tileset_objects={tileset_count}")
    print(f"total={len(records)}")


if __name__ == "__main__":
    main()
