"""生成家里缺的家具贴图（像素画：3~4 级色阶 + 抖动 + 1px 明暗边）。

上一版是"色块填充"，看着像矢量图；这一版按现有 home_ref_* 的技法重画：
  - 每种材质 3~4 级色阶（高光/亮面/主体/暗面）
  - 明暗过渡用 50% 棋盘抖动，而不是硬切
  - 每个体的下沿/右沿 1px 暗边（接触阴影），上沿 1px 亮边
  - 木头加竖向纹理，布料加缝线，最后统一量化到游戏调色板

口径：PPU 64（1 世界单位 = 64px），pivot 居中，落点按底边中心。

用法：python E:/大创项目/Tools/make_props.py
输出：Assets/Art/Pixel/Generated/HomeReference/Props/home_ref_<name>.png + .meta
"""
import os
import random
import re

from PIL import Image, ImageDraw

OUT = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Props"
META_TEMPLATE = os.path.join(OUT, "home_ref_small_plant.png.meta")

# 木色 4 级
WOOD0 = (196, 150, 104)
WOOD1 = (166, 118, 76)
WOOD2 = (126, 84, 50)
WOOD3 = (88, 56, 32)
WOOD4 = (58, 36, 20)
# 布面 4 级
CLOTH0 = (214, 198, 170)
CLOTH1 = (190, 172, 144)
CLOTH2 = (160, 142, 116)
CLOTH3 = (126, 110, 88)
# 金属 3 级
M0 = (176, 178, 182)
M1 = (140, 142, 148)
M2 = (98, 100, 106)
# 灯罩 / 玻璃
WARM0 = (250, 234, 184)
WARM1 = (232, 206, 146)
WARM2 = (200, 168, 110)
GREEN0 = (132, 156, 96)
GREEN1 = (100, 124, 72)
GREEN2 = (70, 90, 52)


def canvas(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def block(d, x0, y0, x1, y1, tones, grain=0):
    """带 4 级色阶的体块：亮上沿、暗下沿、右沿暗一档，可选竖向纹理。"""
    c0, c1, c2, c3 = tones
    d.rectangle((x0, y0, x1, y1), fill=c1)
    d.rectangle((x0, y0, x1, y1), outline=c2, width=1)
    d.line((x0 + 1, y0 + 1, x1 - 1, y0 + 1), fill=c0)          # 上高光
    d.line((x0 + 1, y1 - 1, x1 - 1, y1 - 1), fill=c3)          # 下暗边
    d.line((x1 - 1, y0 + 1, x1 - 1, y1 - 1), fill=c2)
    if grain:
        for x in range(x0 + 3, x1 - 2, grain):
            d.line((x, y0 + 2, x, y1 - 2), fill=c2)


def dither_band(d, x0, y0, x1, y1, c):
    """50% 棋盘抖动，用在两档色阶之间过渡。"""
    for y in range(y0, y1):
        for x in range(x0, x1):
            if (x + y) % 2 == 0:
                d.point((x, y), fill=c)


def make_chair(w=48, h=92):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # 椅背
    block(d, 5, 1, w - 6, h // 2, (WOOD0, WOOD1, WOOD2, WOOD3), grain=7)
    d.rectangle((9, 7, w - 10, h // 2 - 8), fill=WOOD1)
    d.rectangle((9, 7, w - 10, h // 2 - 8), outline=WOOD2, width=1)
    dither_band(d, 10, 8, w - 11, 13, WOOD0)
    # 坐面
    block(d, 2, h // 2, w - 3, h // 2 + 15, (WOOD0, WOOD1, WOOD2, WOOD3), grain=9)
    dither_band(d, 4, h // 2 + 1, w - 5, h // 2 + 5, WOOD0)
    # 腿
    for x in (7, w - 13):
        d.rectangle((x, h // 2 + 15, x + 5, h - 2), fill=WOOD3)
        d.line((x, h // 2 + 15, x, h - 3), fill=WOOD4)
    return img


def make_armchair(w=92, h=88):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # 靠背
    block(d, 7, 2, w - 8, 36, (CLOTH0, CLOTH1, CLOTH2, CLOTH3))
    dither_band(d, 9, 4, w - 10, 12, CLOTH0)
    d.line((9, 20, w - 10, 20), fill=CLOTH2)
    # 扶手
    block(d, 1, 24, 17, h - 14, (CLOTH0, CLOTH1, CLOTH2, CLOTH3))
    block(d, w - 18, 24, w - 2, h - 14, (CLOTH0, CLOTH1, CLOTH2, CLOTH3))
    # 坐垫
    block(d, 17, 30, w - 19, h - 12, (CLOTH0, CLOTH1, CLOTH2, CLOTH3))
    d.rectangle((22, 36, w - 24, h - 18), outline=CLOTH3, width=1)
    dither_band(d, 18, 31, w - 20, 35, CLOTH0)
    # 木脚
    for x in (8, w - 15):
        d.rectangle((x, h - 12, x + 6, h - 2), fill=WOOD3)
    return img


def make_stool(w=36, h=52):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # 圆凳面（用像素台阶画圆）
    block(d, 3, 3, w - 4, 17, (WOOD0, WOOD1, WOOD2, WOOD3))
    for i in range(4):
        d.point((3 + i, 3 + i), fill=(0, 0, 0, 0))
        d.point((w - 4 - i, 3 + i), fill=(0, 0, 0, 0))
    dither_band(d, 6, 6, w - 7, 10, WOOD0)
    for x in (9, w - 14):
        d.rectangle((x, 17, x + 4, h - 2), fill=WOOD3)
        d.line((x, 17, x, h - 3), fill=WOOD4)
    # 横撑
    d.line((12, h - 12, w - 13, h - 12), fill=WOOD2)
    return img


def make_table_lamp(w=36, h=64):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # 灯罩（梯形 + 抖动）
    for y in range(2, 28):
        t = (y - 2) / 26.0
        half = int(12 + 6 * t)
        d.line((w // 2 - half, y, w // 2 + half, y), fill=WARM1)
    d.line((w // 2 - 12, 2, w // 2 + 12, 2), fill=WARM0)
    d.line((w // 2 - 18, 27, w // 2 + 18, 27), fill=WARM2)
    dither_band(d, w // 2 - 10, 6, w // 2 + 10, 14, WARM0)
    # 杆 + 底座
    d.rectangle((w // 2 - 1, 28, w // 2 + 1, h - 12), fill=M1)
    d.line((w // 2 - 1, 28, w // 2 - 1, h - 12), fill=M0)
    for i in range(6):
        y = h - 12 + i
        half = 3 + i * 2
        d.line((w // 2 - half, y, w // 2 + half, y), fill=M1 if i < 5 else M2)
    return img


def make_wall_clock(w=54, h=54):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    for i in range(26):
        y0 = 1 + i if i < 26 else 1
        half = int((26 ** 2 - (26 - i) ** 2) ** 0.5)
        d.line((27 - half, 1 + i, 27 + half, 1 + i), fill=WOOD1)
        d.line((27 - half, h - 2 - i, 27 + half, h - 2 - i), fill=WOOD1)
    d.ellipse((4, 4, w - 5, h - 5), fill=(236, 230, 206))
    d.ellipse((4, 4, w - 5, h - 5), outline=WOOD2)
    d.line((w // 2, h // 2, w // 2, h // 2 - 14), fill=(52, 44, 36), width=2)
    d.line((w // 2, h // 2, w // 2 + 10, h // 2 + 4), fill=(52, 44, 36), width=2)
    d.rectangle((w // 2 - 1, h // 2 - 1, w // 2 + 1, h // 2 + 1), fill=(52, 44, 36))
    return img


def make_picture(w=72, h=54):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    block(d, 0, 0, w - 1, h - 1, (WOOD0, WOOD1, WOOD2, WOOD3), grain=11)
    d.rectangle((5, 5, w - 6, h - 6), fill=(206, 198, 172))
    # 远景
    d.rectangle((6, 6, w - 7, h - 20), fill=(158, 182, 194))
    dither_band(d, 6, 6, w - 7, 14, (198, 214, 220))
    d.polygon([(6, h - 8), (w // 3, h - 30), (w // 2, h - 14), (w - 7, h - 26), (w - 7, h - 8)],
              fill=GREEN1)
    d.polygon([(6, h - 8), (w // 3, h - 24), (w // 3 + 6, h - 8)], fill=GREEN0)
    d.line((6, h - 9, w - 7, h - 9), fill=GREEN2)
    return img


def make_dresser(w=124, h=132):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    block(d, 2, 8, w - 3, h - 4, (WOOD0, WOOD1, WOOD2, WOOD3), grain=13)
    dither_band(d, 4, 9, w - 5, 14, WOOD0)
    for i in range(3):
        y0 = 26 + i * 34
        block(d, 9, y0, w - 10, y0 + 27, (WOOD0, WOOD1, WOOD2, WOOD3))
        d.rectangle((13, y0 + 4, w - 14, y0 + 23), fill=WOOD1)
        d.line((13, y0 + 4, w - 14, y0 + 4), fill=WOOD2)
        d.rectangle((w // 2 - 7, y0 + 12, w // 2 + 7, y0 + 15), fill=M1)
        d.line((w // 2 - 7, y0 + 12, w // 2 + 7, y0 + 12), fill=M0)
    return img


def make_bench(w=132, h=60):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    block(d, 0, 6, w - 1, 32, (CLOTH0, CLOTH1, CLOTH2, CLOTH3))
    d.line((6, 18, w - 7, 18), fill=CLOTH3)          # 缝线
    dither_band(d, 2, 8, w - 3, 14, CLOTH0)
    for x in (7, w - 14, w // 2 - 3):
        d.rectangle((x, 32, x + 5, h - 2), fill=WOOD3)
        d.line((x, 32, x, h - 3), fill=WOOD4)
    return img


def make_tea_set(w=56, h=40):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # 托盘
    d.ellipse((0, 14, w - 1, h - 1), fill=(150, 112, 72))
    d.ellipse((2, 16, w - 3, h - 4), fill=(184, 144, 98))
    # 壶
    d.ellipse((9, 4, 31, 28), fill=WARM2)
    d.ellipse((11, 6, 29, 26), fill=(240, 236, 220))
    dither_band(d, 13, 8, 27, 14, (252, 250, 240))
    d.rectangle((30, 12, 35, 16), fill=(240, 236, 220))
    d.ellipse((22, 2, 27, 6), fill=(240, 236, 220))
    # 两只杯
    d.ellipse((36, 16, 46, 27), fill=(240, 236, 220))
    d.ellipse((37, 17, 45, 25), fill=(214, 206, 186))
    d.ellipse((46, 21, 54, 30), fill=(240, 236, 220))
    return img


def make_rug(w=112, h=76):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w - 1, h - 1), fill=(172, 154, 118))
    d.rectangle((4, 4, w - 5, h - 5), fill=(196, 180, 146))
    d.rectangle((4, 4, w - 5, h - 5), outline=(142, 124, 92), width=2)
    for x in range(12, w - 10, 14):
        for y in range(12, h - 10, 14):
            d.rectangle((x, y, x + 6, y + 6), fill=(170, 150, 114))
            d.line((x, y, x + 6, y), fill=(204, 190, 158))
    # 边缘流苏
    for x in range(0, w, 4):
        d.line((x, 0, x, 2), fill=(206, 192, 160))
        d.line((x, h - 3, x, h - 1), fill=(206, 192, 160))
    return img


def make_books(w=44, h=32):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    colors = [((150, 70, 60), (110, 48, 42)), ((86, 106, 140), (60, 78, 108)),
              ((110, 132, 78), (82, 100, 56)), ((176, 140, 70), (138, 106, 50))]
    y = h - 5
    for i, (c, c2) in enumerate(colors):
        hh = 6 + (i % 2)
        d.rectangle((2 + i, y - hh, w - 5 - i, y), fill=c)
        d.line((2 + i, y - hh, w - 5 - i, y - hh), fill=c2)
        d.line((3 + i, y - hh + 1, w - 6 - i, y - hh + 1), fill=(236, 232, 216))
        y -= hh + 1
    return img


PROPS = {
    "chair_plain": make_chair,
    "armchair": make_armchair,
    "stool": make_stool,
    "table_lamp": make_table_lamp,
    "wall_clock": make_wall_clock,
    "picture": make_picture,
    "dresser": make_dresser,
    "bench": make_bench,
    "tea_set": make_tea_set,
    "rug_small": make_rug,
    "books_stack": make_books,
}


def write_meta(png_path):
    with open(META_TEMPLATE, encoding="utf-8") as f:
        text = f.read()
    guid = "".join(random.choice("0123456789abcdef") for _ in range(32))
    text = re.sub(r"(guid:\s*)([0-9a-f]{32})", lambda m: m.group(1) + guid, text, count=1)
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in PROPS.items():
        img = fn()
        path = os.path.join(OUT, "home_ref_%s.png" % name)
        img.save(path)
        write_meta(path)
        print("%-14s %3dx%-3d" % (name, img.width, img.height))


if __name__ == "__main__":
    main()
