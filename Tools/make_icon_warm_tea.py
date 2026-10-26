"""
道具图标 —— 热茶（item_warm_tea）

用 Tools/pixel_icon.py 模板画。

造型：陶瓷杯（杯口外翻沿）+ 右侧闭合 D 形把手 + 杯口热气 + 茶汤液面。
配色：米黄陶瓷 / 棕褐茶汤 / 浅色热气，5 色全部在现有暖色板内。

v3 修正（对 v2）：
1. **杯口加外翻沿**（y18..19 宽 29，比杯身 25 宽 4px）——
   v2 是直筒加方角，48px 下读成方盒。杯沿外翻是杯子最强的识别特征。
2. **把手改成闭合 D 环**（7px 宽，内外都封口）——
   v2 的 pattern 中间行只有左竖线，右侧不闭合，读成「从杯身伸出的两根横条」。
3. **热气改 zigzag**（3px 宽，一笔上下折）——
   v2 的 4px S 形在 8 倍放大下像数字「2」。
"""

import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixel_icon import IconCanvas, PALETTE

c = IconCanvas()

# ══ 杯身：杯口外翻沿 + 直筒 + 内收底 ═══════════════════
PROFILE = {}
PROFILE[18] = 29        # 杯沿（外翻，比杯身宽 4px）
PROFILE[19] = 29
for y in range(20, 38):
    PROFILE[y] = 25     # 直筒杯身
PROFILE[38] = 23
PROFILE[39] = 21
PROFILE[40] = 19        # 杯底内收

c.fill_profile(lambda y: PROFILE.get(y), PALETTE["paper"])

# ══ 茶汤：从杯沿内侧往下铺两行 ═════════════════════════
c.hline(12, 20, 25, PALETTE["brown_hi"])
c.hline(12, 21, 25, PALETTE["paper_dk"])

# ══ 把手：闭合 D 环（x 36..42，与杯身右边界 36 重叠）═══
HANDLE = [
    ".####.",
    "##...#",
    "#....#",
    "#....#",
    "#....#",
    "#....#",
    "##...#",
    ".####.",
]
c.pattern(39, 29, HANDLE, PALETTE["paper"])

# ══ 杯身明暗：左亮右暗（先明暗后描边）══════════════════
c.flush_cylinder(20, 39, hi=PALETTE["paper_hi"], dk=PALETTE["paper_dk"])

# ══ 热气：三条 zigzag，高度错开 ═══════════════════════
STEAM = [
    ".##.",
    "##..",
    ".##.",
    "##..",
    ".##.",
]
c.pattern(17, 12, STEAM, PALETTE["paper_hi"])
c.pattern(24, 8, STEAM, PALETTE["paper_hi"])
c.pattern(31, 12, STEAM, PALETTE["paper_hi"])

# ══ 内描边 ═══════════════════════════════════════════
c.outline()

c.save("icon_item_warm_tea")
