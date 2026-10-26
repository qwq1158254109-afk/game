"""
像素图标基础模板（Tools/pixel_icon.py）

把「功能饮料」那张返修四轮踩出来的做法固化下来，后续 9 张道具图标直接套。

════════ 四轮返修得出的三条硬规则 ════════

规则 1：描边用「内描边」，不要手画外框
    手画外框（先画一圈 OUTLINE 再填内部）在宽度逐行变化时必然在对角处断开，
    因为步长 1px 时相邻两行的边缘像素是斜对的，4 邻域连不上。
    本模板改为：先把形状画满，再把**边缘像素本身**染成描边色。
    这样描边永远连续，也不会超出画布。

规则 2：逐行宽度必须全用奇数
    偶数宽度序列（18/22/24/26）每换一级，左右边界各跳 2px，
    收口会变成台阶、视觉上像描边断了。奇数（17/19/21/23）每次只移 1px。

规则 3：圆柱的立体感靠「左 1px 高光 + 右 2px 暗部」，不靠横线
    在罐身加一条横向反光线，会被读成「盒子上贴了一条胶带」。
    竖向明暗才是圆柱的语言。

════════ 用法 ════════

    from pixel_icon import IconCanvas, PALETTE

    c = IconCanvas()
    c.fill_profile(lambda y: 23 if 8 <= y <= 40 else None, PALETTE["gold"])
    c.flush_cylinder(8, 40)          # 自动加左高光 / 右暗部
    c.outline()                      # 内描边
    c.save("icon_item_xxx")

    # 输出：<name>.png + <name>_8x.png（预览）+ <name>_24.png（验收）+ 色数报告
"""

from PIL import Image

# ════════ 色板 ════════
# 前 9 色 = Docs/ArtStyleGuide.md 主色板
# 后 4 色 = 2026-10-26 做饮料罐时新增（银 / 金亮 / 暗金 / 深蓝），
#          道具里的金属与液体类离不开它们，规范待同步。
PALETTE = {
    # 暖色主调
    "paper":     (0xF0, 0xD9, 0xA5),
    "paper_hi":  (0xFF, 0xE5, 0xB8),
    "paper_dk":  (0xC5, 0x9B, 0x64),
    "ink":       (0x34, 0x22, 0x16),
    "brown":     (0x6D, 0x3E, 0x24),
    "brown_hi":  (0x9A, 0x62, 0x35),
    "gold":      (0xE2, 0xB2, 0x4C),
    "red":       (0xB6, 0x48, 0x3C),
    "green":     (0x6F, 0x8F, 0x5A),
    # 新增：金属与液体
    "silver":    (0xD8, 0xD8, 0xD8),
    "gold_hi":   (0xE8, 0xB3, 0x3C),
    "gold_dk":   (0xC8, 0x92, 0x2A),
    "navy":      (0x1A, 0x27, 0x44),
}

OUTLINE = PALETTE["ink"]
MAX_COLORS = 8          # 规范的色数上限（含描边）
CANVAS = 48


class IconCanvas:
    """48x48 图标画布。所有方法都在自身上作画，最后 save() 统一导出。"""

    def __init__(self, size=CANVAS):
        self.size = size
        self.img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        self.px = self.img.load()

    # ── 基础绘制 ──────────────────────────────────────
    def dot(self, x, y, color):
        if 0 <= x < self.size and 0 <= y < self.size:
            self.px[x, y] = color + (255,)

    def rect(self, x, y, w, h, color):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.dot(xx, yy, color)

    def hline(self, x, y, w, color):
        self.rect(x, y, w, 1, color)

    def vline(self, x, y, h, color):
        self.rect(x, y, 1, h, color)

    def pattern(self, cx, cy, rows, color):
        """以 (cx, cy) 为中心画字符画，'#' 为着色点。"""
        h = len(rows)
        w = len(rows[0])
        ox = cx - w // 2
        oy = cy - h // 2
        for dy, row in enumerate(rows):
            for dx, ch in enumerate(row):
                if ch == '#':
                    self.dot(ox + dx, oy + dy, color)

    # ── 逐行轮廓（画罐/瓶/书脊类纵向物件的主力）──────────
    def fill_profile(self, width_at, color, center=None):
        """
        width_at(y) -> int | None
            返回该行的外宽，返回 None 表示该行不画。
            传 int 表示所有行同色；传 dict{y:color} 不适用，请分行调用。
        """
        cx = center if center is not None else self.size // 2
        for y in range(self.size):
            w = width_at(y)
            if not w:
                continue
            left = cx - w // 2
            for x in range(left, left + w):
                self.dot(x, y, color)

    def span(self, width, center=None):
        cx = center if center is not None else self.size // 2
        left = cx - width // 2
        return left, left + width - 1

    # ── 圆柱明暗 ──────────────────────────────────────
    def flush_cylinder(self, y_top, y_bottom, hi=None, dk=None, center=None):
        """
        左 1px 高光 + 右 2px 暗部。只处理已着色的行，不会画到空白处。
        规则 3：立体感靠竖向明暗，不靠横线。
        """
        hi = hi or PALETTE["paper_hi"]
        dk = dk or PALETTE["gold_dk"]
        cx = center if center is not None else self.size // 2
        for y in range(y_top, y_bottom + 1):
            xs = [x for x in range(self.size) if self.px[x, y][3] > 0]
            if not xs:
                continue
            l, r = min(xs), max(xs)
            self.dot(l + 1, y, hi)
            self.dot(r - 1, y, dk)
            if r - 2 > l + 1:
                self.dot(r - 2, y, dk)

    # ── 内描边 ────────────────────────────────────────
    def outline(self, color=None):
        """
        把「边缘像素本身」染成描边色：
            - 自身不透明
            - 且 4 邻域中存在透明 或 越界
        规则 1：内描边永远不会断开，也不改变外轮廓。
        """
        color = color or OUTLINE
        s = self.size
        targets = []
        for y in range(s):
            for x in range(s):
                if self.px[x, y][3] == 0:
                    continue
                edge = False
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < s and 0 <= ny < s) or self.px[nx, ny][3] == 0:
                        edge = True
                        break
                if edge:
                    targets.append((x, y))
        for x, y in targets:
            self.px[x, y] = color + (255,)

    # ── 导出与自检 ────────────────────────────────────
    def color_count(self):
        seen = set()
        s = self.size
        for y in range(s):
            for x in range(s):
                if self.px[x, y][3] > 0:
                    seen.add(self.px[x, y][:3])
        return seen

    def save(self, name, out_dir="_tmp_img/icons"):
        import os
        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, name + ".png")
        self.img.save(path)
        self.img.resize((self.size * 8, self.size * 8), Image.NEAREST).save(
            os.path.join(out_dir, name + "_8x.png"))
        self.img.resize((24, 24), Image.LANCZOS).save(
            os.path.join(out_dir, name + "_24.png"))

        colors = self.color_count()
        print("尺寸:", self.img.size, "不透明色数:", len(colors),
              "" if len(colors) <= MAX_COLORS else "  <-- 超出上限 %d" % MAX_COLORS)
        for c in sorted(colors):
            print("  #%02X%02X%02X" % c)
        return path
