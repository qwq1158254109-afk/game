"""
扭蛋机像素图生成器 v4 —— 72x96（2026-10-26）

放大原因：48x64 在商店地图里只有约 55px 宽，比旁边促销牌还小，
作为交互点不够醒目。改为 72x96（同 3:4，1.5 倍），
PPU 16 → 显示 4.5x6 世界单位，并借多出来的画布补回 48px 下被迫省略的细节。

规格：72x96、真像素 1x、PPU 16、番茄红 #D9433F、10 色
结构（自上而下）：
   6-20   灯箱（含红色竖标 / 品牌横条 / 黄块）
  23-52   玻璃罩（圆角上沿 + 9 颗扭蛋 + 左上反光）
  55-68   机身面板（投币口 / 退币口 / 价格标签 / 旋钮）
  72-84   出货托盘（外凸，上暗下亮）
  86-92   底座与支脚
  70-82   右侧散热孔（48px 下省略的细节，这里补回）
"""

from PIL import Image

OUTLINE   = (0x2A, 0x1F, 0x1A)
RED       = (0xD9, 0x43, 0x3F)
RED_DARK  = (0xA6, 0x2E, 0x2C)
GLASS     = (0xC7, 0xD9, 0xE4)
GLASS_DK  = (0x8F, 0xA8, 0xB8)
METAL     = (0x8A, 0x8F, 0x94)
METAL_DK  = (0x5C, 0x61, 0x66)
YELLOW    = (0xF2, 0xC7, 0x44)
DARK      = (0x3A, 0x32, 0x2C)
WHITE     = (0xF5, 0xEF, 0xE6)

W, H = 72, 96
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


def pattern(cx, cy, rows, color):
    h = len(rows)
    w = len(rows[0])
    ox = cx - w // 2
    oy = cy - h // 2
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch == '#':
                dot(ox + dx, oy + dy, color)


BALL = [
    "..###..",
    ".#####.",
    "#######",
    "#######",
    "#######",
    ".#####.",
    "..###..",
]
KNOB = [
    "...###...",
    ".#######.",
    "#########",
    "#########",
    "#########",
    "#########",
    "#########",
    ".#######.",
    "...###...",
]
PIN = [
    ".###.",
    "#####",
    "#####",
    "#####",
    ".###.",
]

# ══ 机身 ══════════════════════════════════════════════
BODY_X, BODY_Y, BODY_W, BODY_H = 4, 3, 64, 90
rect(BODY_X, BODY_Y, BODY_W, BODY_H, OUTLINE)
rect(BODY_X + 1, BODY_Y + 1, BODY_W - 2, BODY_H - 2, RED)
# 顶面压暗
rect(BODY_X + 1, BODY_Y + 1, BODY_W - 2, 1, RED_DARK)
# 右侧背光：贯穿灯箱下方到底座
for i in range(22, 90):
    dot(66, i, RED_DARK)
    dot(65, i, RED_DARK)

# ══ 灯箱 y=6..20 ══════════════════════════════════════
LAMP_X, LAMP_Y, LAMP_W, LAMP_H = 10, 6, 52, 15
rect(LAMP_X, LAMP_Y, LAMP_W, LAMP_H, OUTLINE)
rect(LAMP_X + 1, LAMP_Y + 1, LAMP_W - 2, LAMP_H - 2, WHITE)
# 左侧红色竖标（LOGO 位）
rect(LAMP_X + 4, LAMP_Y + 3, 3, 8, RED)
# 中部品牌横条（两段，中间留白像字间距）
rect(LAMP_X + 10, LAMP_Y + 4, 11, 6, RED)
rect(LAMP_X + 23, LAMP_Y + 4, 9, 6, RED)
# 右侧黄块（促销点）
rect(LAMP_X + 38, LAMP_Y + 4, 6, 6, YELLOW)
# 灯箱底缘暗线
rect(LAMP_X + 1, LAMP_Y + LAMP_H - 2, LAMP_W - 2, 1, GLASS_DK)

# ══ 玻璃罩 y=23..52（圆角上沿）═════════════════════════
GL_X, GL_Y, GL_W, GL_H = 9, 23, 54, 30
CORNER = 3


def glass_inside(x, y):
    if x < GL_X or x >= GL_X + GL_W or y < GL_Y or y >= GL_Y + GL_H:
        return False
    if (x - GL_X) + (y - GL_Y) < CORNER:
        return False
    if (GL_X + GL_W - 1 - x) + (y - GL_Y) < CORNER:
        return False
    return True


for yy in range(GL_Y - 1, GL_Y + GL_H + 1):
    for xx in range(GL_X - 1, GL_X + GL_W + 1):
        if glass_inside(xx, yy):
            dot(xx, yy, GLASS)
for yy in range(GL_Y - 1, GL_Y + GL_H + 1):
    for xx in range(GL_X - 1, GL_X + GL_W + 1):
        if glass_inside(xx, yy):
            continue
        if (glass_inside(xx - 1, yy) or glass_inside(xx + 1, yy)
                or glass_inside(xx, yy - 1) or glass_inside(xx, yy + 1)):
            dot(xx, yy, OUTLINE)

# 左上反光两道
rect(GL_X + 5, GL_Y + 5, 15, 2, WHITE)
rect(GL_X + 5, GL_Y + 9, 8, 2, WHITE)
# 罩底不画任何横条：实测无论宽窄，横贯的深色带都会被读成「水面」。
# 球的堆叠感交给 y 抖动与相互遮挡表达。

# 罩内 9 颗扭蛋：三排中心统一在 x≈35（玻璃罩内区 x 10..61，中心 35.5）
# y 各带 ±1 抖动，避免排成完美 4/3/2 菱形网格（旧版看起来像台球摆三角）
balls = [
    (21, 45, RED), (30, 46, YELLOW), (39, 45, WHITE), (49, 44, RED),
    (25, 38, YELLOW), (35, 37, RED), (44, 38, WHITE),
    (30, 31, WHITE), (40, 30, YELLOW),
]
for bx, by, col in balls:
    pattern(bx, by, BALL, col)
    # 高光只在深色球上加白点；白球改用浅蓝灰，避免看起来像破洞/血点
    dot(bx - 1, by - 2, GLASS_DK if col == WHITE else WHITE)

# ══ 机身面板 y=55..69 ═════════════════════════════════
# 投币口（左）：竖槽 + 顶部黄指示灯
rect(13, 56, 10, 11, OUTLINE)
rect(14, 58, 8, 8, DARK)
rect(14, 57, 8, 1, YELLOW)
# 退币口：投币口正下方
rect(14, 68, 8, 3, OUTLINE)
rect(15, 68, 6, 2, METAL_DK)

# 价格标签（中）：白底 + 一段红价格条 + 一条灰分隔线，不再是三个并列色块
rect(28, 56, 21, 10, OUTLINE)
rect(29, 57, 19, 8, WHITE)
rect(31, 59, 12, 4, RED)
rect(45, 59, 2, 4, METAL_DK)
rect(31, 63, 16, 1, GLASS_DK)

# 旋钮（右）：9x9 圆盘 + 中心销
pattern(54, 62, KNOB, OUTLINE)
pattern(54, 62, PIN, METAL)
pattern(54, 62, [".#.", "###", ".#."], METAL_DK)
dot(54, 62, OUTLINE)

# ══ 出货托盘 y=72..84（外凸：上沿压暗、下沿金属亮条）══════
TRAY_X, TRAY_Y, TRAY_W, TRAY_H = 13, 72, 46, 13
rect(TRAY_X, TRAY_Y, TRAY_W, TRAY_H, OUTLINE)
rect(TRAY_X + 1, TRAY_Y + 1, TRAY_W - 2, TRAY_H - 2, DARK)
# 上沿压暗一线（凹进去的顶面）
rect(TRAY_X + 1, TRAY_Y + 1, TRAY_W - 2, 2, OUTLINE)
# 内部开口加深
rect(TRAY_X + 6, TRAY_Y + 4, TRAY_W - 12, 3, OUTLINE)
# 两侧卡榫
rect(TRAY_X + 1, TRAY_Y + 3, 3, 6, METAL_DK)
rect(TRAY_X + TRAY_W - 4, TRAY_Y + 3, 3, 6, METAL_DK)
# 下沿金属亮条 = 凸出的前缘
rect(TRAY_X + 1, TRAY_Y + TRAY_H - 4, TRAY_W - 2, 2, METAL)
rect(TRAY_X + 1, TRAY_Y + TRAY_H - 2, TRAY_W - 2, 1, METAL_DK)

# ══ 右侧散热孔 y=70..82（48px 下省略，这里补回）══════════
for i in range(3):
    sx = 62 + i * 2
    rect(sx, 70, 1, 11, METAL_DK)

# ══ 底座与支脚 y=86..92 ═══════════════════════════════
rect(BODY_X + 1, 86, BODY_W - 2, 2, RED_DARK)
for fx in (9, 48):
    rect(fx, 88, 15, 5, OUTLINE)
    rect(fx + 1, 89, 13, 3, METAL_DK)
# 中间矮脚
rect(29, 89, 14, 4, OUTLINE)
rect(30, 90, 12, 2, METAL_DK)
# 底部极暗线
rect(BODY_X + 1, 92, BODY_W - 2, 1, OUTLINE)

img.save("_tmp_img/capsule_machine/machine_72x96.png")
img.resize((W * 5, H * 5), Image.NEAREST).save("_tmp_img/capsule_machine/machine_72x96_preview_5x.png")

colors = set()
for y in range(H):
    for x in range(W):
        if px[x, y][3] > 0:
            colors.add(px[x, y][:3])
print("尺寸:", img.size, "不透明色数:", len(colors))
for c in sorted(colors):
    print("  #%02X%02X%02X" % c)
