"""
道具图标生成器 —— 功能饮料（item_energy_drink）· 能量罐款式

造型参考经典能量饮料罐的色块分布：银底 + 深蓝上段 + 红色标识带。
**不绘制任何品牌 logo**（版权考虑），用抽象色块表达「能量饮料」的视觉语言。

规范（Docs/ArtStyleGuide.md §4）：
- 画布 48x48，透明底，长边撑满 40px 安全区
- 1px #342216 描边
- 色数 <= 8

色板（新增两色，因原暖色板画不出银蓝罐）：
  #342216 描边    #C8CDD2 罐身银    #8A8F94 银暗部
  #1D3C87 深蓝段  #B6483C 红标识带  #E2B24C 黄点缀
  #F5EFE6 高光
"""

from PIL import Image

OUTLINE = (0x34, 0x22, 0x16)
SILVER  = (0xC8, 0xCD, 0xD2)
SILV_DK = (0x8A, 0x8F, 0x94)
BLUE    = (0x1D, 0x3C, 0x87)
RED     = (0xB6, 0x48, 0x3C)
YELLOW  = (0xE2, 0xB2, 0x4C)
WHITE   = (0xF5, 0xEF, 0xE6)

W, H = 48, 48
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
px = img.load()


def rect(x, y, w, h, color):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            if 0 <= xx < W and 0 <= yy < H:
                px[xx, yy] = color + (255,)


def dot(x, y, color):
    if 0 <= x < W and 0 <= y < H:
        px[x, y] = color + (255,)


def hline(x, y, w, color):
    rect(x, y, w, 1, color)


BODY_L, BODY_R = 17, 30
TOP, BOTTOM = 4, 44


def inset_at(y):
    """圆柱顶底收窄。"""
    if y <= TOP + 2:
        return 2
    if y <= TOP + 4:
        return 1
    if y >= BOTTOM - 3:
        return 2
    if y >= BOTTOM - 5:
        return 1
    return 0


# ══ 描边 ══════════════════════════════════════════════
for y in range(TOP, BOTTOM + 1):
    ins = inset_at(y)
    hline(BODY_L + ins, y, (BODY_R - ins) - (BODY_L + ins) + 1, OUTLINE)

# ══ 罐身：银底 ════════════════════════════════════════
for y in range(TOP + 1, BOTTOM):
    ins = inset_at(y)
    hline(BODY_L + ins + 1, y, (BODY_R - ins - 1) - (BODY_L + ins + 1) + 1, SILVER)

# ══ 上段：深蓝色块（能量罐的蓝段）══════════════════════
# y 10..18 填蓝，顶部留 1px 银边
rect(19, 10, 10, 9, BLUE)
# 蓝段内一条白斜纹（用阶梯表现斜线：14px 宽画不出真斜纹）
dot(20, 12, WHITE); dot(21, 12, WHITE)
dot(21, 13, WHITE); dot(22, 13, WHITE)
dot(22, 14, WHITE); dot(23, 14, WHITE)
dot(23, 15, WHITE); dot(24, 15, WHITE)
dot(24, 16, WHITE); dot(25, 16, WHITE)

# ══ 中段：红色标识带 ══════════════════════════════════
hline(19, 19, 10, SILV_DK)
rect(19, 20, 10, 11, RED)
hline(19, 31, 10, SILV_DK)

# 标识带内：单个黄块居中 + 下方一条白线（两个并排黄块会读成“眼睛”）
rect(22, 23, 5, 4, YELLOW)
rect(21, 28, 7, 1, WHITE)

# ══ 下段：银底 + 细线 ═════════════════════════════════
hline(19, 34, 10, SILV_DK)

# ══ 高光与暗部 ════════════════════════════════════════
for y in range(11, 39):
    if 20 <= y <= 31:
        continue
    dot(19, y, WHITE)      # 左侧 1px 连续高光
    dot(28, y, SILV_DK)    # 右侧 1px 暗部

# ══ 顶盖 ══════════════════════════════════════════════
hline(19, 6, 10, WHITE)
hline(19, 7, 10, SILV_DK)
rect(21, 8, 6, 2, OUTLINE)
rect(22, 8, 4, 1, SILV_DK)

# ══ 底部 ══════════════════════════════════════════════
hline(20, 41, 8, SILV_DK)

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
