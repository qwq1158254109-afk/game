"""
道具图标生成器 —— 功能饮料（item_energy_drink）· 矮宽金罐 v3

造型依据用户参考图（中国版能量饮料 250ml 罐）：银白罐口 / 琥珀金罐身 /
深蓝近黑底带。**按要求去掉全部文字与品牌图形**。

v3 修正（v1/v2 都读成「金色盒子」）：
1. **宽度改用奇数**：v2 的宽度序列是 18/22/24/26（全偶数），
   每次变化左右边界各跳 2px，收口变成台阶、描边看着像断开。
   改奇数后边界每次只移动 1px，弧线才连得上。
2. **高度拉到 38**（y 5..42）：v2 的 26x32 长宽比 1:1.23 太方，
   48 画布上怎么收口都像个盒。现在 23x38 = 1:1.65，
   仍是矮罐（标准罐约 1:1.9），但保住了「罐」的竖直感。
3. **明暗加宽**：左侧 2px 高光、右侧 3px 暗部，中间留纯金，
   三段拉开才能读出圆柱；v2 只有 1px 高光，等于平涂。

规范：48x48、透明底、1px #342216 描边、<=8 色
"""

from PIL import Image

OUTLINE = (0x34, 0x22, 0x16)
GOLD    = (0xE8, 0xB3, 0x3C)
GOLD_DK = (0xC8, 0x92, 0x2A)
NAVY    = (0x1A, 0x27, 0x44)
SILVER  = (0xD8, 0xD8, 0xD8)
WHITE   = (0xFF, 0xE5, 0xB8)

W, H = 48, 48
CENTER = 24

# 逐行宽度表（全部奇数，保证左右边界平滑移动）
# 高度 y 5..42 = 38，最大宽 23，长宽比 1:1.65
PROFILE = [
    (5, 17), (6, 19), (7, 21),
    (8, 23), (9, 23), (10, 23), (11, 23), (12, 23), (13, 23), (14, 23),
    (15, 23), (16, 23), (17, 23), (18, 23), (19, 23), (20, 23), (21, 23),
    (22, 23), (23, 23), (24, 23), (25, 23), (26, 23), (27, 23), (28, 23),
    (29, 23), (30, 23), (31, 23), (32, 23), (33, 23), (34, 23), (35, 23),
    (36, 23), (37, 23), (38, 23), (39, 23),
    (40, 21), (41, 19), (42, 17),
]

img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
px = img.load()


def dot(x, y, color):
    if 0 <= x < W and 0 <= y < H:
        px[x, y] = color + (255,)


def span(width):
    left = CENTER - width // 2
    return left, left + width - 1


def fill_row(y, width, color):
    l, r = span(width)
    for x in range(l, r + 1):
        dot(x, y, color)


# ══ 描边 ══════════════════════════════════════════════
for y, width in PROFILE:
    fill_row(y, width, OUTLINE)

# ══ 主体分区 ══════════════════════════════════════════
# 银口 6..8 / 金 9..33 / 过渡 34 / 深蓝 35..40 / 银底 41
for y, width in PROFILE:
    l, r = span(width)
    inner_l, inner_r = l + 1, r - 1

    if y <= 8:
        color = SILVER
    elif y <= 33:
        color = GOLD
    elif y <= 34:
        color = GOLD_DK
    elif y <= 40:
        color = NAVY
    else:
        color = SILVER

    for x in range(inner_l, inner_r + 1):
        dot(x, y, color)

# ══ 圆柱明暗：左 2px 高光 / 右 3px 暗部 ═══════════════
for y, width in PROFILE:
    l, r = span(width)
    if 9 <= y <= 34:
        # 金色段：左 1px 高光 / 右 2px 暗部 / 中间纯金 —— 三段才读得出圆柱
        dot(l + 1, y, WHITE)
        dot(r - 1, y, GOLD_DK)
        dot(r - 2, y, GOLD_DK)
    elif 35 <= y <= 40:
        # 深蓝段：只做右侧暗部，保持色带干净
        dot(l + 1, y, GOLD_DK)
        dot(r - 1, y, OUTLINE)

# 罐口与罐身的分界暗线
l10, r10 = span(23)
for x in range(l10 + 2, r10 - 1):
    dot(x, 8, GOLD_DK)

img.save("_tmp_img/icons/icon_item_energy_drink.png")
img.resize((W * 8, H * 8), Image.NEAREST).save("_tmp_img/icons/icon_item_energy_drink_8x.png")

colors = set()
for y in range(H):
    for x in range(W):
        if px[x, y][3] > 0:
            colors.add(px[x, y][:3])
print("尺寸:", img.size, "不透明色数:", len(colors))
for c in sorted(colors):
    print("  #%02X%02X%02X" % c)
