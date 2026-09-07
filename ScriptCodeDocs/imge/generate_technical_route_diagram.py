from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math


ROOT = Path(r"E:\unity\ML-project")
OUT_DIR = ROOT / "ScriptCodeDocs"
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_PATH = OUT_DIR / "student_growth_technical_route.png"

W, H = 1600, 900
img = Image.new("RGB", (W, H), (250, 253, 255))
d = ImageDraw.Draw(img)

FONT_CANDIDATES = [
    r"C:\Windows\Fonts\msyhbd.ttc",
    r"C:\Windows\Fonts\msyh.ttc",
    r"C:\Windows\Fonts\simhei.ttf",
    r"C:\Windows\Fonts\Deng.ttf",
    r"C:\Windows\Fonts\simsun.ttc",
]


def load_font(size, bold=False):
    preferred = []
    if bold:
        preferred = [r"C:\Windows\Fonts\msyhbd.ttc", r"C:\Windows\Fonts\simhei.ttf"]
    for path in preferred + FONT_CANDIDATES:
        if Path(path).exists():
            try:
                return ImageFont.truetype(path, size=size)
            except OSError:
                pass
    return ImageFont.load_default()


F_TITLE = load_font(42, True)
F_SUBTITLE = load_font(21)
F_CARD_TITLE = load_font(22, True)
F_BODY = load_font(17)
F_LOOP_TITLE = load_font(28, True)
F_FOOT = load_font(22, True)

BLUE = (27, 83, 186)
TEXT = (56, 70, 96)
MUTED = (96, 111, 137)
PANEL_EDGE = (206, 219, 235)
ARROW = (120, 143, 170)
RED = (224, 52, 52)


def text_size(text, font):
    box = d.multiline_textbbox((0, 0), text, font=font, spacing=6)
    return box[2] - box[0], box[3] - box[1]


def center_text(box, text, font, fill=TEXT, spacing=6):
    x1, y1, x2, y2 = box
    tw, th = text_size(text, font)
    d.multiline_text(
        (x1 + (x2 - x1 - tw) / 2, y1 + (y2 - y1 - th) / 2),
        text,
        font=font,
        fill=fill,
        align="center",
        spacing=spacing,
    )


def draw_arrow(x1, y1, x2, y2, color=ARROW):
    d.line((x1, y1, x2, y2), fill=color, width=4)
    angle = math.atan2(y2 - y1, x2 - x1)
    size = 15
    spread = math.radians(28)
    points = [(x2, y2)]
    for sign in (1, -1):
        a = angle + math.pi + sign * spread
        points.append((x2 + size * math.cos(a), y2 + size * math.sin(a)))
    d.polygon(points, fill=color)


def card(x, y, w, h, title, body, color, fill):
    d.rounded_rectangle((x, y, x + w, y + h), radius=16, fill=fill, outline=color, width=4)
    d.line((x + 24, y + 58, x + w - 24, y + 58), fill=color, width=3)
    center_text((x + 10, y + 14, x + w - 10, y + 50), title, F_CARD_TITLE, color)
    center_text((x + 18, y + 76, x + w - 18, y + h - 16), body, F_BODY, TEXT)


def small_card(x, y, w, h, title, body):
    d.rounded_rectangle((x, y, x + w, y + h), radius=12, fill=(250, 252, 255), outline=(142, 166, 199), width=3)
    center_text((x + 10, y + 12, x + w - 10, y + 42), title, F_CARD_TITLE, BLUE)
    center_text((x + 14, y + 48, x + w - 14, y + h - 10), body, F_BODY, TEXT)


center_text((0, 18, W, 72), "图1  学生成长模拟卡牌游戏项目总体技术路线图", F_TITLE, BLUE)
center_text(
    (0, 78, W, 112),
    "围绕“数据资产构建—玩法逻辑实现—美术资源接入—地图场景扩展—运行时展示—测试文档交付”构建可演示 Unity MVP",
    F_SUBTITLE,
    MUTED,
)
d.line((80, 126, W - 80, 126), fill=(205, 217, 233), width=2)

top_y = 175
card_w = 195
card_h = 182
gap = 27
start_x = 54
steps = [
    ("数据资产构建", "ScriptableObject\n阶段/活动/卡牌/道具\n地点与结局数据", (41, 107, 245), (239, 247, 255)),
    ("核心玩法建模", "PlayerState\nDeckState\n挑战与奖励结算\n事件条件判定", (29, 151, 124), (235, 250, 245)),
    ("美术资源接入", "Sprite 字段绑定\nResources 路径适配\n卡牌/地点/UI 图片", (125, 76, 230), (248, 244, 255)),
    ("地图地点扩展", "LocationData\nActionData\n场景路由/地点交互\n阶段地图状态", (230, 94, 32), (255, 247, 238)),
    ("运行时UI整合", "RuntimeUi 自动生成\n地图/地点/战斗面板\nTooltip 与操作反馈", (42, 145, 70), (240, 252, 241)),
    ("测试与文档输出", "EditMode/PlayMode\n手动功能验证\n用例图/类图/路线图", (230, 55, 55), (255, 245, 245)),
]

for i, (title, body, color, fill) in enumerate(steps):
    x = start_x + i * (card_w + gap)
    card(x, top_y, card_w, card_h, title, body, color, fill)
    if i < len(steps) - 1:
        draw_arrow(x + card_w + 7, top_y + card_h / 2, x + card_w + gap - 7, top_y + card_h / 2)

panel_x, panel_y = 66, 430
panel_w, panel_h = W - 132, 285
d.rounded_rectangle((panel_x, panel_y, panel_x + panel_w, panel_y + panel_h), radius=24, fill=(255, 255, 255), outline=PANEL_EDGE, width=3)
d.text((panel_x + 54, panel_y + 42), "验证与交付闭环", font=F_LOOP_TITLE, fill=BLUE)

loop_cards = [
    ("Unity 编译检查", "确认 C# 脚本无编译错误\n保留现有玩法入口"),
    ("功能流程验证", "开始游戏/阶段推进\n活动/战斗/Boss/结局"),
    ("资源显示验证", "卡牌图/地点图/UI 图标\n场景地图与提示文本"),
    ("交付材料沉淀", "代码结构文档\n用例图/类图/技术路线图"),
]

loop_y = panel_y + 108
loop_w = 255
loop_h = 108
loop_gap = 48
loop_start = panel_x + 54
for i, (title, body) in enumerate(loop_cards):
    x = loop_start + i * (loop_w + loop_gap)
    small_card(x, loop_y, loop_w, loop_h, title, body)
    if i < len(loop_cards) - 1:
        draw_arrow(x + loop_w + 12, loop_y + loop_h / 2, x + loop_w + loop_gap - 12, loop_y + loop_h / 2)

d.rounded_rectangle((250, 750, W - 250, 812), radius=12, fill=(255, 255, 255), outline=(216, 225, 238), width=2)
center_text(
    (260, 754, W - 260, 808),
    "定位：不追求完整商业化部署，重点完成可演示 Unity MVP、方法验证、资源接入与文档图表支撑",
    F_FOOT,
    RED,
)

d.text((72, 848), "保存位置：ScriptCodeDocs/student_growth_technical_route.png", font=F_BODY, fill=MUTED)

img.save(OUT_PATH)
print(OUT_PATH)
