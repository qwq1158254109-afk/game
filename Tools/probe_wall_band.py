"""墙带两种方案的对照生成（不写入资产，只出图）。

源图北墙的真实竖向结构（实测，x 250..450 净面）：
    外沿暗边   y 36..38   高  2  RGB(12,9,6)
    压边(暖棕) y 38..65   高 27  RGB(80,49,31) -> 高光在 y59..60
    墙面(奶白) y 65..263  高 198 RGB(200,152,107)，底部转暗
    踢脚(深棕) y 263..295 高 32  RGB(97,52,25)，y281 有暗分线
    ------------------------------------------------
    合计 259 px，而游戏的墙带只有 96 px（1.5 世界单位）

方案 A（等比压缩）：整段 259px 压到 96px，保住"三段俱全"，代价是纵向细节被压掉 63%。
方案 B（1:1 截取）  ：从源图取连续 96px 窗口（含压边高光 + 墙面 + 踢脚起线），
                      不缩放，细节密度与源图完全一致。
"""
import os

import numpy as np
from PIL import Image, ImageDraw

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
OUT = r"E:/大创项目/Tools/_wall_band_compare.png"

BAND_H = 96
CLEAN_X = (250, 450)          # 墙面净面 x 区间（实测 std<3.4）


def norm_band(segs, target_h, order):
    """把源图切片映射到 target_h：order 给出目标各段高度，返回合成条带（1:1 取样）。"""
    bands = []
    for (sy0, sy1), th in zip(segs, order):
        src = img_full.crop((CLEAN_X[0], sy0, CLEAN_X[1], sy1))
        if src.size[1] != th:
            # 只在"等比压缩"方案里走这里：用 LANCZOS 重采样
            src = src.resize((src.size[0], th), Image.LANCZOS)
        bands.append(src)
    out = Image.new("RGBA", (CLEAN_X[1] - CLEAN_X[0], target_h), (0, 0, 0, 0))
    y = 0
    for b in bands:
        out.alpha_composite(b, (0, y))
        y += b.size[1]
    return out


def build_a():
    """方案 A：整段等比压缩到 96px。"""
    segs = [(36, 38), (38, 65), (65, 263), (263, 295)]
    total = sum(b - a for a, b in segs)          # 259
    order, acc = [], 0
    for i, (a, b) in enumerate(segs):
        h = round((b - a) / total * BAND_H)
        order.append(max(1, h))
        acc += h
    order[-1] += BAND_H - acc                    # 补齐舍入
    return norm_band(segs, BAND_H, order), segs, order


def build_b():
    """方案 B：从源图取连续 96px 窗口（压边高光 + 墙面 + 踢脚起线）。"""
    win = (36, 36 + BAND_H)
    return img_full.crop((CLEAN_X[0], win[0], CLEAN_X[1], win[1])), [win], [BAND_H]


img_full = Image.open(SRC).convert("RGBA")


def tile_to_width(band, width=1824):
    out = Image.new("RGBA", (width, band.size[1]))
    x, n = 0, 0
    while x < width:
        piece = band if n % 2 == 0 else band.transpose(Image.FLIP_LEFT_RIGHT)
        w = min(band.size[0], width - x)
        out.alpha_composite(piece.crop((0, 0, w, piece.size[1])), (x, 0))
        x += band.size[0]
        n += 1
    return out


def main():
    a, segs, order = build_a()
    b, win, _ = build_b()

    print("方案 A 各段目标高度:", order, "(源图", [y1 - y0 for y0, y1 in segs], ")")
    print("方案 B 窗口:", win)

    W = 1824
    ta, tb = tile_to_width(a, W), tile_to_width(b, W)
    src_strip = img_full.crop((CLEAN_X[0], 36, CLEAN_X[1], 295))
    ts = tile_to_width(src_strip, W)

    rows = [("SOURCE  (260px, 1:1)", ts),
            ("A  equal-ratio compress -> 96px", ta),
            ("B  1:1 96px window", tb)]
    sheet = Image.new("RGB", (W, sum(r.size[1] for _, r in rows) + 6 * 2), (255, 0, 0))
    y = 0
    for label, im in rows:
        rgb = im.convert("RGB")
        d = ImageDraw.Draw(rgb)
        d.rectangle((0, 0, 420, 30), fill=(0, 0, 0))
        d.text((8, 9), label, fill=(255, 255, 255))
        sheet.paste(rgb, (0, y))
        y += im.size[1] + 6

    scale = 0.5
    sheet = sheet.resize((int(W * scale), int(sheet.size[1] * scale)), Image.LANCZOS)
    sheet.save(OUT)
    print("saved", OUT, sheet.size)

    for name, im in (("source", ts), ("A", ta), ("B", tb)):
        arr = np.asarray(im.convert("RGB")).reshape(-1, 3)
        uq = len(np.unique(arr, axis=0))
        m = arr.mean(axis=0)
        print("%-7s 色数=%-6d R-B=%5.1f" % (name, uq, m[0] - m[2]))


if __name__ == "__main__":
    main()