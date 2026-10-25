# -*- coding: utf-8 -*-
"""像素图检测/修复门控批处理：AI 生成素材进 Unity 前的最后一道工序。

用法：
  python Tools/fix_pixel_art.py <文件或目录>                # 检测 + 脏图自动修复
  python Tools/fix_pixel_art.py <路径> --downscale-clean    # 干净整数放大图也输出 1x 缩小版
  python Tools/fix_pixel_art.py <路径> --out <目录>         # 输出目录（默认 _tmp_img/pixel_fix_out）

流程（每张图）：
  1. 门槛检测：按 2..16 的整数步长分块，统计块内不均匀比例。存在某步长块均匀
     => 判定 CLEAN（本来就是真像素的整数放大，不需要修复器，直接整数缩小即可）。
  2. 无干净整数步长 => 判定 DIRTY（伪像素：网格漂移/非整数格/边缘发糊），
     交给 vendored 的 pixel-art-fixer 重建 1x 真像素，输出 <名>_fixed.png。
  3. 复核标记 REVIEW：confidence=low，或修复后颜色数不减反增。
     颜色坍缩（几万→几千）才是真修复特征；干净源图被误检时颜色几乎不减
     （2026-10-24 实测 warm_tea：源图实为干净 4x/56 色，修复器判成 step 3.893，
      输出 132x192 且颜色 56->64——门槛检测已把这类图挡在 CLEAN 分支，双保险）。

不改任何源文件；产物全部落输出目录。依赖：numpy scipy opencv-python Pillow。

实测基线（2026-10-24，FanworkPixel/Generated/PNG 全量 54 张 + 1 图标）：
  CLEAN 31 张：卡牌主体系，@2x/@4x 块不均匀 0.00%，几十色，无需修复
  修复 21 张：颜色 4~6 万 -> 几千（真伪像素图），如 word_dictation 43484->4944
  REVIEW 2 张：sample_prop_exam/focus（confidence=low），另 icon_energy_drink 8->9 色
  门槛检测对 warm_tea 类"干净但修复器会误检"的图给出 CLEAN@4x，与手测 0.00% 一致
"""
import argparse
import io
import json
import os
import sys

import numpy as np
from PIL import Image

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PIXELFIXER_ROOT = os.path.join(PROJECT_ROOT, "Tools", "pixel-art-fixer")
DEFAULT_OUT = os.path.join(PROJECT_ROOT, "_tmp_img", "pixel_fix_out")

CLEAN_RATIO_MAX = 0.005  # 块不均匀比例 <= 0.5% 视为干净整数放大
MAX_STEP = 16            # 门槛检测的最大整数步长
IMG_EXTS = (".png", ".jpg", ".jpeg")


def block_nonuniform_ratio(arr, step):
    """按 step 分块，返回块内 RGBA 不完全一致的比例（0~1）。"""
    h, w = arr.shape[:2]
    bh, bw = h // step, w // step
    if bh < 4 or bw < 4:
        return 1.0
    blocks = arr[: bh * step, : bw * step].reshape(bh, step, bw, step, -1)
    spread = blocks.max(axis=(1, 3)) - blocks.min(axis=(1, 3))
    return float((spread.sum(axis=-1) > 0).mean())


def gate_check(arr):
    """返回 (是否干净, 最佳步长, 最佳不均匀比例)。"""
    best_step, best_ratio = 0, 1.0
    h, w = arr.shape[:2]
    for step in range(2, min(MAX_STEP, h, w) + 1):
        if h % step or w % step:
            continue
        r = block_nonuniform_ratio(arr, step)
        if r < best_ratio:
            best_step, best_ratio = step, r
        if r <= CLEAN_RATIO_MAX:
            return True, step, r
    return best_ratio <= CLEAN_RATIO_MAX, best_step, best_ratio


def color_count(arr):
    return int(len(np.unique(arr.reshape(-1, arr.shape[-1]), axis=0)))


def iter_images(path):
    if os.path.isfile(path):
        yield path
        return
    for dirpath, _, files in os.walk(path):
        for f in sorted(files):
            if f.lower().endswith(IMG_EXTS):
                yield os.path.join(dirpath, f)


def process_image(path, out_dir, downscale_clean, report):
    name = os.path.splitext(os.path.basename(path))[0]
    with open(path, "rb") as f:
        raw = f.read()
    img = Image.open(io.BytesIO(raw)).convert("RGBA")
    arr = np.array(img)
    src_colors = color_count(arr)

    clean, step, ratio = gate_check(arr)
    entry = {"name": name, "source": [img.width, img.height], "src_colors": src_colors}
    rel = os.path.relpath(path, PROJECT_ROOT)

    if clean:
        entry.update(status="CLEAN", step=step, block_ratio=round(ratio, 5))
        print(f"CLEAN   {rel}  @{step}x (block_nonuniform={ratio*100:.2f}%, {src_colors} colors)")
        if downscale_clean and step > 1:
            small = arr[::step, ::step]
            out = os.path.join(out_dir, f"{name}_x1.png")
            Image.fromarray(small).save(out)
            entry["downscaled"] = out
    else:
        sys.path.insert(0, PIXELFIXER_ROOT)
        from pixelfixer.api import process  # noqa: E402

        result = process(raw)
        fixed = np.array(Image.open(io.BytesIO(result["png"])).convert("RGBA"))
        fix_colors = color_count(fixed)
        out = os.path.join(out_dir, f"{name}_fixed.png")
        with open(out, "wb") as f:
            f.write(result["png"])
        review = result.get("confidence") == "low" or fix_colors >= src_colors
        entry.update(
            status="REVIEW" if review else "FIXED",
            detected=[result["cols"], result["rows"]],
            step=[round(result["step_x"], 3), round(result["step_y"], 3)],
            consensus=result.get("consensus"),
            confidence=result.get("confidence"),
            fixed_colors=fix_colors,
            output=out,
        )
        print(f"{entry['status']:7s} {rel}  -> {result['cols']}x{result['rows']} "
              f"(step {result['step_x']:.2f}/{result['step_y']:.2f}, "
              f"{result.get('confidence')}, {result.get('consensus')}, "
              f"colors {src_colors}->{fix_colors})")
    report.append(entry)


def main():
    ap = argparse.ArgumentParser(description="像素图门控检测/修复（AI 素材入库前工序）")
    ap.add_argument("input", help="图片文件或目录")
    ap.add_argument("--out", default=DEFAULT_OUT, help="输出目录（默认 _tmp_img/pixel_fix_out）")
    ap.add_argument("--downscale-clean", action="store_true",
                    help="CLEAN 图也输出 1x 最近邻缩小版")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    report = []
    files = list(iter_images(args.input))
    if not files:
        print("no image found:", args.input)
        sys.exit(1)
    for p in files:
        process_image(p, args.out, args.downscale_clean, report)

    report_path = os.path.join(args.out, "report.json")
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    n_clean = sum(1 for e in report if e["status"] == "CLEAN")
    n_fixed = sum(1 for e in report if e["status"] == "FIXED")
    n_review = sum(1 for e in report if e["status"] == "REVIEW")
    print(f"\nsummary: {len(report)} images -> CLEAN {n_clean}, FIXED {n_fixed}, REVIEW {n_review}")
    print("report ->", report_path)


if __name__ == "__main__":
    main()
