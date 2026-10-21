"""房间壳改造·对照样张生成器（只出对照图，不碰任何游戏资产）。

目的：把「从 home_new_space.png 取真实材质」与「当前 make_rooms.py 的平色块」
放在同一张图上对比，让用户能肉眼判断值不值得做。

当前做法（A 侧）直接调用 make_rooms.py 的等价逻辑；候选做法（B 侧）从源图取
更大的地板 / 墙面矩形平铺，并保留源图自带的木纹走向与墙面明暗。

用法：python Tools/probe_shell_compare.py
输出：Docs/Screenshots/shell_compare_<date>.png
"""
import datetime
import os

import numpy as np
from PIL import Image, ImageDraw

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
TILES = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Tiles"
OUT_DIR = r"E:/大创项目/Docs/Screenshots"

# 与 make_rooms.py 保持一致的口径
CANVAS_W, CANVAS_H = 1824, 1056
W_SIDE = W_TOP = 96
W_BOTTOM = 128
FLOOR_PATCH_OLD = (566, 630, 694, 758)   # make_rooms.py 现用：全图最均匀的 128x128

# 候选材质矩形（从 profile 读出的干净区，见 probe_space.py）
FLOOR_BIG = (520, 620, 900, 750)          # 380x130 真实地板（含木纹）
WALL_FACE = (150, 96, 510, 264)           # 360x168 墙面（含抹灰质感与竖向明暗）

PREVIEW = 0.5   # 对照图缩到一半，方便一屏看完


def cur_shell():
    """A：当前做法——平铺"最均匀"地板 patch + 纯色墙带。"""
    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H))
    patch = Image.open(SRC).convert("RGBA").crop(FLOOR_PATCH_OLD)
    for j, ty in enumerate(range(0, CANVAS_H, patch.height)):
        for i, tx in enumerate(range(0, CANVAS_W, patch.width)):
            tile = patch
            if i % 2:
                tile = tile.transpose(Image.FLIP_LEFT_RIGHT)
            if j % 2:
                tile = tile.transpose(Image.FLIP_TOP_BOTTOM)
            canvas.alpha_composite(tile, (tx, ty))

    h_tile = Image.open(os.path.join(TILES, "home_ref_partition_h.png")).convert("RGBA")
    light, dark = h_tile.getpixel((24, 20)), h_tile.getpixel((24, 40))
    draw = ImageDraw.Draw(canvas)
    canvas.alpha_composite(h_tile, (0, 0))
    x = 48
    while x < CANVAS_W:
        canvas.alpha_composite(h_tile.crop((0, 0, min(48, CANVAS_W - x), 48)), (x, 0))
        x += 48
    draw.rectangle((0, 48, CANVAS_W, W_TOP), fill=light)
    draw.rectangle((0, 48 + (W_TOP - 48) * 2 // 3, CANVAS_W, W_TOP), fill=dark)
    draw.rectangle((0, CANVAS_H - W_BOTTOM, CANVAS_W, CANVAS_H), fill=light)
    draw.rectangle((0, 0, W_SIDE, CANVAS_H), fill=light)
    draw.rectangle((CANVAS_W - W_SIDE, 0, CANVAS_W, CANVAS_H), fill=light)
    return canvas.convert("RGB")


def new_shell():
    """B：候选做法——地板用大矩形真实材质，墙用源图墙面材质。"""
    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H))

    # --- 地板：384x130 横条，奇偶行错位 + 左右镜像，避免出现明显网格 ---
    patch = Image.open(SRC).convert("RGBA").crop(FLOOR_BIG)
    pw, ph = patch.size
    for j, ty in enumerate(range(0, CANVAS_H, ph)):
        for i, tx in enumerate(range(0, CANVAS_W, pw)):
            tile = patch if i % 2 == 0 else patch.transpose(Image.FLIP_LEFT_RIGHT)
            canvas.alpha_composite(tile, (tx, ty))

    # --- 墙：整条墙面矩形，水平平铺；顶部压边与踢脚线取自源图同一列 ---
    wall = Image.open(SRC).convert("RGBA").crop(WALL_FACE)
    ww, wh = wall.size
    top_band = Image.new("RGBA", (CANVAS_W, W_TOP))
    for tx in range(0, CANVAS_W, ww):
        top_band.alpha_composite(wall.crop((0, 0, min(ww, CANVAS_W - tx), wh)), (tx, 0))

    canvas.alpha_composite(top_band, (0, 0))
    canvas.alpha_composite(top_band.transpose(Image.FLIP_TOP_BOTTOM), (0, CANVAS_H - W_BOTTOM))

    side_band = Image.new("RGBA", (W_SIDE, CANVAS_H))
    for ty in range(0, CANVAS_H, wh):
        side_band.alpha_composite(wall.crop((0, 0, ww, min(wh, CANVAS_H - ty))), (0, ty))
    canvas.alpha_composite(side_band, (0, 0))
    canvas.alpha_composite(side_band.transpose(Image.FLIP_LEFT_RIGHT), (CANVAS_W - W_SIDE, 0))

    return canvas.convert("RGB")


def label(img, text):
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 420, 34), fill=(0, 0, 0))
    d.text((10, 10), text, fill=(255, 255, 255))
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    a = cur_shell()
    b = new_shell()
    a = label(a, "A  current (flat PIL walls + flattest floor patch)")
    b = label(b, "B  candidate (real materials sampled from home_new_space)")

    w = int(CANVAS_W * PREVIEW)
    h = int(CANVAS_H * PREVIEW)
    a = a.resize((w, h), Image.LANCZOS)
    b = b.resize((w, h), Image.LANCZOS)

    sheet = Image.new("RGB", (w, h * 2 + 8), (20, 20, 20))
    sheet.paste(a, (0, 0))
    sheet.paste(b, (0, h + 8))

    stamp = datetime.datetime.now().strftime("%Y%m%d")
    out = os.path.join(OUT_DIR, "shell_compare_%s.png" % stamp)
    sheet.save(out)

    for name, im in (("A", a), ("B", b)):
        arr = np.asarray(im).reshape(-1, 3)
        uniq = len(np.unique(arr, axis=0))
        lum = arr.mean(axis=1)
        print("%s: uniq=%-6d lum mean=%.1f std=%.2f" % (name, uniq, lum.mean(), lum.std()))
    print("saved", out, sheet.size)


if __name__ == "__main__":
    main()