"""
道具图标生成器 —— 功能饮料（item_energy_drink）

规范（Docs/ArtStyleGuide.md §4）：
- 画布 48x48，透明底，长边撑满 40px 安全区
- 1px #342216 描边，只在轮廓外
- 主色 #E2B24C / #FFE5B8 / #C59B64 / #9A6235，点缀 #B6483C
- 色数 <= 8
- 验收：缩到 24x24 仍能认出是饮料罐

修正（相对第一版）：
1. 高光改成连续竖线——第一版用 y%4 做断点，画出来是锯齿楼梯
2. 罐体收窄到 14px 宽（原 18px 像罐头），长宽比 1:2.9
3. 拉环重画成 6x3 的环形，能看出是拉环
4. 标签上的白色块改成一横一竖：横条暗示文字、方块暗示商标
"""

from PIL import Image

OUTLINE  = (0x34, 0x22, 0x16)
GOLD     = (0xE2, 0xB2, 0x4C)
GOLD_DK  = (0x9A, 0x62, 0x35)
HILIGHT  = (0xFF, 0xE5, 0xB8)
RED      = (0xB6, 0x48, 0x3C)
METAL    = (0x8A, 0x8F, 0x94)

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


# ══ 罐体轮廓 ══════════════════════════════════════════
# 罐体 14 宽 x 41 高（比例 1:2.9），居中：
# 安全区 4..43，中心 24，所以 x 17..30
BODY_L, BODY_R = 17, 30
TOP, BOTTOM = 4, 44


def inset_at(y):
    """按高度返回左右内缩量，做出圆柱的顶底收窄。"""
    if y <= TOP + 2:
        return 2
    if y <= TOP + 4:
        return 1
    if y >= BOTTOM - 3:
        return 2
    if y >= BOTTOM - 5:
        return 1
    return 0


# 描边
for y in range(TOP, BOTTOM + 1):
    ins = inset_at(y)
    hline(BODY_L + ins, y, (BODY_R - ins) - (BODY_L + ins) + 1, OUTLINE)

# 填充
for y in range(TOP + 1, BOTTOM):
    ins = inset_at(y)
    hline(BODY_L + ins + 1, y, (BODY_R - ins - 1) - (BODY_L + ins + 1) + 1, GOLD)

# ══ 顶盖与拉环 ════════════════════════════════════════
# 罐口金属圈
hline(19, 6, 10, METAL)
hline(19, 7, 10, GOLD_DK)
# 拉环：6x3 的环（描边 + 内部金属）
rect(21, 8, 6, 3, OUTLINE)
rect(22, 8, 4, 1, METAL)
rect(23, 9, 2, 1, GOLD_DK)

# ══ 标签带 ════════════════════════════════════════════
hline(18, 19, 12, GOLD_DK)
rect(18, 20, 12, 13, RED)
hline(18, 33, 12, GOLD_DK)

# 标签内容：一条横线（暗示文字）+ 一个小方块（暗示商标）
rect(20, 23, 8, 2, HILIGHT)
rect(20, 27, 3, 3, HILIGHT)
rect(25, 27, 3, 1, HILIGHT)
# 标签底部一条金线
hline(20, 31, 8, GOLD)

# ══ 高光与暗部 ════════════════════════════════════════
# 左侧 1px 连续高光（受光面），标签区跳过
for y in range(11, 39):
    if 21 <= y <= 32:
        continue
    dot(19, y, HILIGHT)
# 右侧 1px 暗部
for y in range(11, 39):
    if 21 <= y <= 32:
        continue
    dot(28, y, GOLD_DK)

# ══ 底部 ══════════════════════════════════════════════
hline(20, 41, 8, GOLD_DK)  # 罐底暗边（原来这里还多画了一行描边，导致底部像凹槽，已删）

img.save("_tmp_img/icons/icon_item_energy_drink.png")
img.resize((W * 8, H * 8), Image.NEAREST).save("_tmp_img/icons/icon_item_energy_drink_8x.png")
img.resize((24, 24), Image.LANCZOS).save("_tmp_img/icons/icon_item_energy_drink_24.png")
img.resize((24, 24), Image.LANCZOS).resize((192, 192), Image.NEAREST).save(
    "_tmp_img/icons/icon_item_energy_drink_24_check.png")

colors = set()
for y in range(H):
    for x in range(W):
        if px[x, y][3] > 0:
            colors.add(px[x, y][:3])
print("尺寸:", img.size, "不透明色数:", len(colors))
for c in sorted(colors):
    print("  #%02X%02X%02X" % c)
