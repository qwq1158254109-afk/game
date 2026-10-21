"""房间壳改造·材质采样器与对照图生成。

从 home_new_space.png 采真实材质（地板 / 墙顶压边 / 墙面 / 踢脚线），
按目标房间几何（1824x1056 @ PPU64，墙厚 侧96/北96/南128）合成新房间壳，
并与当前 make_rooms.py 的平色块版本并排输出对照图。

用法：
  python Tools/build_shell_materials.py measure     # 只测量并打印边界
  python Tools/build_shell_materials.py compare     # 出对照图（Tools/_shell_compare.png）
  python Tools/build_shell_materials.py preview     # 出单张候选壳（Tools/_shell_new.png）
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
TILES = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Tiles"
TOOLS = r"E:/大创项目/Tools"

CANVAS_W, CANVAS_H = 1824, 1056
W_SIDE, W_TOP = 96, 96
W_BOTTOM = 128
DOOR_W = 96
TILE = 48
FLOOR_PATCH_OLD = (566, 630, 694, 758)


# ---------------------------------------------------------------- 探测

def row_profile(a, x0, x1):
    return a[:, x0:x1].mean(axis=(1, 2))


def find_bands():
    """自动定位北墙各材质段的精确像素行区间。"""
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    lum = row_profile(a, 500, 950)
    rgb = a[:, 500:950].mean(axis=1)

    warm = (rgb[:, 0] - rgb[:, 2]) > 25          # 暖色（木/棕）
    bright = lum > 120                            # 亮（墙面/地面）

    # 墙顶压边：从第一个暖色行开始，到首次持续变亮之前
    start = next(y for y in range(20, 200) if warm[y])
    face_start = next(y for y in range(start, 400) if bright[y] and lum[y] > 130)
    # 踢脚线：墙面之后第一次显著变暗
    base_start = next(y for y in range(face_start, 500) if lum[y] < lum[y - 1] - 8 and lum[y] < 130)
    # 地板：踢脚线之后再次稳定变亮
    floor_start = next(y for y in range(base_start, 700) if lum[y] > 70 and lum[y + 30] > 75)

    print("自动探测结果：")
    print("  墙顶压边  y %d .. %d   (%d px)" % (start, face_start, face_start - start))
    print("  墙面      y %d .. %d   (%d px)" % (face_start, base_start, base_start - face_start))
    print("  踢脚线    y %d .. %d   (%d px)" % (base_start, floor_start, floor_start - base_start))
    print("  地板起    y %d" % floor_start)
    return dict(trim=(start, face_start), face=(face_start, base_start),
                base=(base_start, floor_start), floor=floor_start)


# ---------------------------------------------------------------- 材质

class Materials:
    """从源图取出的可平铺材质块。全部 1:1 原始像素。"""

    def __init__(self, bands):
        self.src = Image.open(SRC).convert("RGBA")
        self.bands = bands

        # 地板：大矩形（含木纹走向），取不含内墙立柱的干净区
        self.floor = self.src.crop((520, 620, 900, 750))          # 380x130

        # 墙面：取 x 150..510（远离窗与立柱），纵向用整段墙面
        f0, f1 = bands["face"]
        self.face = self.src.crop((150, f0, 510, f1))              # 360x(face高)

        # 墙顶压边：同一 x 段
        t0, t1 = bands["trim"]
        self.trim = self.src.crop((150, t0, 510, t1))              # 360x(trim高)

        # 踢脚线：同一 x 段
        b0, b1 = bands["base"]
        self.base = self.src.crop((150, b0, 510, b1))              # 360x(base高)

        for n in ("floor", "face", "trim", "base"):
            im = getattr(self, n)
            arr = np.asarray(im.convert("RGB")).reshape(-1, 3)
            uq = len(np.unique(arr, axis=0))
            lum = arr.mean(axis=1)
            print("  材质 %-6s %-10s uniq=%-6d lum=%.1f std=%.2f" % (
                n, "%dx%d" % im.size, uq, lum.mean(), lum.std()))


def tile_h(canvas, patch, y, x_range, mirror_alt=True):
    """水平平铺 patch 到指定行区间。"""
    pw, ph = patch.size
    x0, x1 = x_range
    n = 0
    x = x0
    while x < x1:
        piece = patch if (n % 2 == 0 or not mirror_alt) else patch.transpose(Image.FLIP_LEFT_RIGHT)
        w = min(pw, x1 - x)
        canvas.alpha_composite(piece.crop((0, 0, w, ph)), (x, y))
        x += pw
        n += 1


def tile_v(canvas, patch, x, y_range, mirror_alt=True):
    """垂直平铺 patch 到指定列区间。"""
    pw, ph = patch.size
    y0, y1 = y_range
    n = 0
    y = y0
    while y < y1:
        piece = patch if (n % 2 == 0 or not mirror_alt) else patch.transpose(Image.FLIP_TOP_BOTTOM)
        h = min(ph, y1 - y)
        canvas.alpha_composite(piece.crop((0, 0, pw, h)), (x, y))
        y += ph
        n += 1


def build_wall_band(mats, total_h, horizontal=True):
    """按 源图材质顺序 拼一条墙带：压边 / 墙面 / 踢脚线，总高 total_h。

    材质保持 1:1 像素密度（不缩放）。若源图三段之和 > total_h，
    按比例截取，优先保住压边与踢脚线的完整观感。
    """
    trim, face, base = mats.trim, mats.face, mats.base
    th, fh, bh = trim.size[1], face.size[1], base.size[1]
    src_total = th + fh + bh

    if total_h >= src_total:
        # 目标更厚：墙面段放大（重复平铺）
        keep_f, keep_b, keep_t = fh, bh, th
    else:
        # 目标更薄：三段的截取高度按比例分（压边与踢脚线设下限，避免消失）
        min_t, min_b = min(th, 8), min(bh, 20)
        avail = total_h - min_t - min_b
        keep_f = max(1, avail)
        keep_t, keep_b = min_t, min_b

    def strip(img, keep, from_top):
        w, h = img.size
        if keep >= h:
            return img
        if from_top:
            return img.crop((0, 0, w, keep))
        return img.crop((0, h - keep, w, h))

    if horizontal:
        band = Image.new("RGBA", (CANVAS_W, total_h), (0, 0, 0, 0))
        t = strip(trim, keep_t, True)
        f = strip(face, keep_f, False)      # 靠下截，保住墙面底部与踢脚线的自然衔接
        b = strip(base, keep_b, True)
    else:
        band = Image.new("RGBA", (total_h, CANVAS_H), (0, 0, 0, 0))
        t = strip(trim, keep_t, True).transpose(Image.ROTATE_270)
        f = strip(face, keep_f, False).transpose(Image.ROTATE_270)
        b = strip(base, keep_b, True).transpose(Image.ROTATE_270)

    y = 0
    for piece in (t, f, b):
        if horizontal:
            tile_h(band, piece, y, (0, CANVAS_W))
            y += piece.size[1]
        else:
            tile_v(band, piece, y, (0, CANVAS_H))
            y += piece.size[0]
    return band


# ---------------------------------------------------------------- 两个版本

def cur_shell():
    """A：当前做法。"""
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
    x = TILE
    while x < CANVAS_W:
        canvas.alpha_composite(h_tile.crop((0, 0, min(TILE, CANVAS_W - x), TILE)), (x, 0))
        x += TILE
    draw.rectangle((0, TILE, CANVAS_W, W_TOP), fill=light)
    draw.rectangle((0, TILE + (W_TOP - TILE) * 2 // 3, CANVAS_W, W_TOP), fill=dark)
    draw.rectangle((0, CANVAS_H - W_BOTTOM, CANVAS_W, CANVAS_H), fill=light)
    draw.rectangle((0, 0, W_SIDE, CANVAS_H), fill=light)
    draw.rectangle((CANVAS_W - W_SIDE, 0, CANVAS_W, CANVAS_H), fill=light)
    return canvas.convert("RGB")


def new_shell(mats):
    """B：材质采样版。"""
    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H))

    # 地板：错位 + 镜像平铺
    fp = mats.floor
    pw, ph = fp.size
    for j, ty in enumerate(range(0, CANVAS_H, ph)):
        for i, tx in enumerate(range(0, CANVAS_W, pw)):
            tile = fp if i % 2 == 0 else fp.transpose(Image.FLIP_LEFT_RIGHT)
            canvas.alpha_composite(tile, (tx, ty))

    # 墙：四条带，材质 1:1
    canvas.alpha_composite(build_wall_band(mats, W_TOP, True), (0, 0))
    canvas.alpha_composite(
        build_wall_band(mats, W_BOTTOM, True).transpose(Image.FLIP_TOP_BOTTOM),
        (0, CANVAS_H - W_BOTTOM))
    canvas.alpha_composite(build_wall_band(mats, W_SIDE, False), (0, 0))
    canvas.alpha_composite(
        build_wall_band(mats, W_SIDE, False).transpose(Image.FLIP_LEFT_RIGHT),
        (CANVAS_W - W_SIDE, 0))
    return canvas.convert("RGB")


def label(img, text):
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 560, 34), fill=(0, 0, 0))
    d.text((10, 10), text, fill=(255, 255, 255))
    return img


def stats(name, img):
    arr = np.asarray(img).reshape(-1, 3)
    uq = len(np.unique(arr, axis=0))
    lum = arr.mean(axis=1)
    print("%s: uniq=%-7d lum mean=%.1f std=%.2f" % (name, uq, lum.mean(), lum.std()))


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "compare"
    print("=== 探测源图材质边界 ===")
    bands = find_bands()
    print("\n=== 取出材质块 ===")
    mats = Materials(bands)

    if mode == "measure":
        return

    if mode == "preview":
        out = os.path.join(TOOLS, "_shell_new.png")
        img = new_shell(mats)
        img.save(out)
        stats("B(new)", img)
        print("saved", out, img.size)
        return

    a = cur_shell()
    b = new_shell(mats)
    print()
    stats("A(current)", a)
    stats("B(new)    ", b)

    pre = 0.5
    w, h = int(CANVAS_W * pre), int(CANVAS_H * pre)
    a = label(a, "A  current (flat PIL)").resize((w, h), Image.LANCZOS)
    b = label(b, "B  real materials from home_new_space").resize((w, h), Image.LANCZOS)
    sheet = Image.new("RGB", (w, h * 2 + 8), (20, 20, 20))
    sheet.paste(a, (0, 0))
    sheet.paste(b, (0, h + 8))
    out = os.path.join(TOOLS, "_shell_compare.png")
    sheet.save(out)
    print("saved", out, sheet.size)


if __name__ == "__main__":
    main()