"""
道具图标批量生成 —— 7 张（错题本 / 幸运笔 / 文具盒 / 耳机 / 闹钟 / 便条 / 星星贴纸）

全部用 Tools/pixel_icon.py 模板。规范：48x48、透明底、内描边、<=8 色。
准考证按要求暂不做。

统一原则：
- 物件长边撑满 36..40px 安全区
- 主色落在暖色板内，金属/深色件允许用银、深蓝
- 每张先画形状 -> 再明暗 -> 最后 outline()
"""

import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixel_icon import IconCanvas, PALETTE
from PIL import Image, ImageDraw

OUT_DIR = "_tmp_img/icons"


# ══════════════════════════════════════════════════════
# 1. 错题本 wrong_book —— 立着的本子，封面红，右下角一个叉
# ══════════════════════════════════════════════════════
def wrong_book():
    c = IconCanvas()
    # 封面 26x34，x 11..36，y 7..40
    c.rect(11, 7, 26, 34, PALETTE["red"])
    # 书脊：左侧 4px 压暗
    c.rect(11, 7, 4, 34, PALETTE["brown"])
    # 封面上一块白色标签
    c.rect(19, 13, 13, 9, PALETTE["paper"])
    # 标签上一个叉（两笔，用点阵）
    X = [
        "#.#",
        ".#.",
        "#.#",
    ]
    c.pattern(25, 17, X, PALETTE["red"])
    # 下缘三条横线（暗示书页）
    for i, y in enumerate((30, 33, 36)):
        c.hline(19, y, 13 - i, PALETTE["paper_hi"])
    # 左侧高光 / 右侧暗部
    c.flush_cylinder(9, 39, hi=PALETTE["paper_hi"], dk=PALETTE["brown"])
    c.outline()
    c.save("icon_item_wrong_book", OUT_DIR)


# ══════════════════════════════════════════════════════
# 2. 幸运笔 lucky_pen —— 竖放的笔，笔尖朝下，笔杆带星
# ══════════════════════════════════════════════════════
def lucky_pen():
    c = IconCanvas()
    # 笔杆 8x28，x 20..27，y 8..35
    c.rect(20, 6, 8, 26, PALETTE["gold_hi"])
    # 笔杆右侧暗部
    c.rect(25, 6, 3, 26, PALETTE["gold_dk"])
    # 笔杆左侧高光
    c.rect(20, 6, 2, 26, PALETTE["paper_hi"])
    # 笔尖：逐行收窄成一点（v1 用点阵画成了方块，读成试管）
    TIP_WIDTHS = [(32, 8), (33, 7), (34, 6), (35, 5), (36, 4), (37, 3), (38, 2), (39, 1)]
    for y, w in TIP_WIDTHS:
        l, r = c.span(w)
        c.hline(l, y, w, PALETTE["paper_dk"])
    # 笔尾帽：顶部一段红
    c.rect(19, 3, 10, 4, PALETTE["red"])
    # 笔杆上的小星（幸运标记，5x5 才看得出是星）
    STAR_S = [
        "..#..",
        ".###.",
        "#####",
        ".###.",
        "..#..",
    ]
    c.pattern(24, 20, STAR_S, PALETTE["paper_hi"])
    c.outline()
    c.save("icon_item_lucky_pen", OUT_DIR)


# ══════════════════════════════════════════════════════
# 3. 文具盒 pencil_case —— 横放的长方盒，盖子一道缝
# ══════════════════════════════════════════════════════
def pencil_case():
    c = IconCanvas()
    # 盒体 34x18，x 7..40，y 16..33
    c.rect(7, 16, 34, 18, PALETTE["navy"])
    # 盖子：上半部分略亮
    c.rect(7, 16, 34, 7, PALETTE["brown"])
    # 盖缝
    c.hline(7, 23, 34, PALETTE["ink"])
    # 盒身正面两条装饰线
    c.hline(10, 27, 28, PALETTE["paper_dk"])
    # 锁扣：中间一个小方块
    c.rect(21, 21, 6, 6, PALETTE["gold"])
    c.rect(23, 23, 2, 2, PALETTE["brown"])
    # 左端高光 / 右端暗部
    c.rect(8, 17, 2, 16, PALETTE["paper_hi"])
    c.rect(38, 17, 2, 16, PALETTE["ink"])
    c.outline()
    c.save("icon_item_pencil_case", OUT_DIR)


# ══════════════════════════════════════════════════════
# 4. 耳机 headphones —— 头梁弧 + 两个耳罩
# ══════════════════════════════════════════════════════
def headphones():
    c = IconCanvas()
    # 头梁：用参数方程画连续半圆环（v1 的点阵每行宽不齐，成了虚线）
    import math
    cx, cy = 24.0, 22.0
    R_OUT, R_IN = 15.0, 12.0
    for deg in range(180, 361):
        a = math.radians(deg)
        for r in (R_OUT, R_IN):
            x = int(round(cx + r * math.cos(a)))
            y = int(round(cy + r * math.sin(a)))
            c.dot(x, y, PALETTE["silver"])
    # 头梁填充：内外半径之间
    for y in range(6, 23):
        dy = y - cy
        if abs(dy) > R_OUT:
            continue
        outer = math.sqrt(max(0.0, R_OUT ** 2 - dy ** 2))
        inner = math.sqrt(max(0.0, R_IN ** 2 - dy ** 2))
        l_out, r_out = int(round(cx - outer)), int(round(cx + outer))
        l_in, r_in = int(round(cx - inner)), int(round(cx + inner))
        c.hline(l_out, y, l_in - l_out + 1, PALETTE["silver"])
        c.hline(r_in, y, r_out - r_in + 1, PALETTE["silver"])
    # 耳罩：外挂到两侧，与头梁末端相接
    c.rect(8, 22, 11, 16, PALETTE["navy"])
    c.rect(29, 22, 11, 16, PALETTE["navy"])
    # 耳罩内侧（朝里）压暗
    c.rect(15, 26, 4, 9, PALETTE["ink"])
    c.rect(29, 26, 4, 9, PALETTE["ink"])
    # 耳罩外侧高光
    c.rect(9, 23, 2, 14, PALETTE["paper_dk"])
    c.rect(30, 23, 2, 14, PALETTE["paper_dk"])
    c.outline()
    c.save("icon_item_headphones", OUT_DIR)


# ══════════════════════════════════════════════════════
# 5. 闹钟 alarm_clock —— 圆形钟面 + 两只铃 + 指针
# ══════════════════════════════════════════════════════
def alarm_clock():
    c = IconCanvas()
    # 钟面：32 直径的圆（用三行不同宽度堆出）
    FACE = {}
    for y in range(10, 41):
        dy = y - 25
        # 半圆近似
        import math
        half = int((15 ** 2 - dy ** 2) ** 0.5) if abs(dy) <= 15 else 0
        w = half * 2 + 1
        if w > 0:
            FACE[y] = w
    c.fill_profile(lambda y: FACE.get(y), PALETTE["paper"])
    # 外圈压暗
    for y, w in FACE.items():
        l, r = c.span(w)
        c.dot(l + 1, y, PALETTE["brown"])
        c.dot(r - 1, y, PALETTE["brown"])
    # 指针：时针短、分针长，都从中心 (24,25) 出发
    for i in range(11):
        c.dot(24, 25 - i, PALETTE["ink"])      # 分针朝上，加长到 11px
    for i in range(7):
        c.dot(24 + i, 25, PALETTE["red"])      # 时针朝右，加长到 7px
    c.dot(24, 25, PALETTE["ink"])
    # 两只铃：顶部左右各一个圆
    BELL = [
        "..###..",
        ".#####.",
        "#######",
        "#######",
        "#######",
        ".#####.",
    ]
    c.pattern(14, 9, BELL, PALETTE["gold"])
    c.pattern(34, 9, BELL, PALETTE["gold"])
    # 两只脚
    c.rect(13, 41, 4, 4, PALETTE["brown"])
    c.rect(31, 41, 4, 4, PALETTE["brown"])
    c.outline()
    c.save("icon_item_alarm_clock", OUT_DIR)


# ══════════════════════════════════════════════════════
# 6. 家长便条 parent_note —— 一张纸，右下折角，几行字
# ══════════════════════════════════════════════════════
def parent_note():
    c = IconCanvas()
    # 纸 26x32，x 11..36，y 9..40
    c.rect(11, 9, 26, 32, PALETTE["paper_hi"])
    # 右下折角：切掉三角
    for i in range(6):
        for j in range(6 - i):
            x = 36 - j
            y = 40 - i
            c.px[x, y] = (0, 0, 0, 0)
    # 折角背面（浅色）
    CORNER = [
        ".....#",
        "....##",
        "...###",
        "..####",
        ".#####",
        "######",
    ]
    c.pattern(34, 38, CORNER, PALETTE["paper_dk"])
    # 纸上几行字
    for y, w in ((15, 18), (19, 16), (23, 18), (27, 12)):
        c.hline(14, y, w, PALETTE["paper_dk"])
    # 左边缘暗一线，右边缘亮一线
    c.rect(11, 9, 1, 32, PALETTE["paper_dk"])
    c.outline()
    c.save("icon_item_parent_note", OUT_DIR)


# ══════════════════════════════════════════════════════
# 7. 星星贴纸 sticker_star —— 五角星
# ══════════════════════════════════════════════════════
def sticker_star():
    c = IconCanvas()
    # 21x21 五角星点阵（中心 24,24）
    STAR = [
        "..........#..........",
        ".........###.........",
        ".........###.........",
        "........#####........",
        "........#####........",
        "####### ##### #######".replace(" ", ""),
        ".####################.",
        "..##################..",
        "...################...",
        "....##############....",
        ".....############.....",
        "......##########......",
        "......##########......",
        ".....############.....",
        "....####....####....",
        "...####......####...",
        "..####........####..",
        "..###..........###..",
        "..##............##..",
    ]
    # 上面的点阵行宽不齐，直接用几何法画星更稳
    c2 = IconCanvas()
    import math
    cx, cy = 24, 24
    outer, inner = 18, 8
    pts = []
    for i in range(10):
        ang = -math.pi / 2 + i * math.pi / 5
        r = outer if i % 2 == 0 else inner
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    # 逐个像素判断是否在星形内（射线法）
    def inside(px_, py_):
        n = len(pts)
        j = n - 1
        res = False
        for i in range(n):
            xi, yi = pts[i]
            xj, yj = pts[j]
            if (yi > py_) != (yj > py_):
                if px_ < (xj - xi) * (py_ - yi) / (yj - yi) + xi:
                    res = not res
            j = i
        return res

    for y in range(48):
        for x in range(48):
            if inside(x, y):
                c2.dot(x, y, PALETTE["gold_hi"])
    # 左上一块高光
    for y in range(12, 24):
        for x in range(14, 24):
            if c2.px[x, y][3] > 0 and (x - 14) + (y - 12) < 8:
                c2.dot(x, y, PALETTE["paper_hi"])
    c2.outline()
    c2.save("icon_item_sticker_star", OUT_DIR)


if __name__ == "__main__":
    wrong_book()
    lucky_pen()
    pencil_case()
    headphones()
    alarm_clock()
    parent_note()
    sticker_star()

    # 总览：7 张排成 4 列
    names = [
        "icon_item_wrong_book", "icon_item_lucky_pen", "icon_item_pencil_case",
        "icon_item_headphones", "icon_item_alarm_clock", "icon_item_parent_note",
        "icon_item_sticker_star",
    ]
    CELL = 240
    COLS = 4
    rows = (len(names) + COLS - 1) // COLS
    out = Image.new("RGB", (COLS * (CELL + 10) + 10, rows * (CELL + 10) + 10), (28, 24, 20))
    for i, n in enumerate(names):
        im = Image.open(os.path.join(OUT_DIR, n + ".png")).convert("RGBA")
        slot = Image.new("RGBA", (48, 48), (84, 71, 59, 255))
        slot.alpha_composite(im)
        out.paste(slot.resize((CELL, CELL), Image.NEAREST).convert("RGB"),
                  (10 + (i % COLS) * (CELL + 10), 10 + (i // COLS) * (CELL + 10)))
    out.save(os.path.join(OUT_DIR, "_batch7_overview.png"))
    print("\n总览已出:", os.path.join(OUT_DIR, "_batch7_overview.png"))
