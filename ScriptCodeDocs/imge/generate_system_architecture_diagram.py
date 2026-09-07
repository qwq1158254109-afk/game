from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math


ROOT = Path(r"E:\unity\ML-project")
OUT_DIR = ROOT / "ScriptCodeDocs"
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_PATH = OUT_DIR / "student_growth_system_architecture.png"

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
F_LAYER = load_font(30, True)
F_BOX = load_font(19, True)
F_SIDE = load_font(19, True)
F_BOTTOM = load_font(22, True)
F_NOTE = load_font(16)

BLUE = (33, 91, 235)
PURPLE = (136, 78, 236)
GREEN = (39, 143, 70)
ORANGE = (239, 88, 47)
TEXT = (55, 68, 96)
MUTED = (94, 108, 134)
ARROW = (122, 145, 172)
WHITE = (255, 255, 255)


def text_size(text, font):
    box = d.multiline_textbbox((0, 0), text, font=font, spacing=5)
    return box[2] - box[0], box[3] - box[1]


def center_text(box, text, font, fill=TEXT, spacing=5):
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


def arrow_down(cx, y1, y2):
    d.line((cx, y1, cx, y2), fill=ARROW, width=5)
    d.polygon([(cx, y2 + 13), (cx - 12, y2 - 3), (cx + 12, y2 - 3)], fill=ARROW)


def layer_panel(x, y, w, h, color, fill, title, items):
    d.rounded_rectangle((x, y, x + w, y + h), radius=16, fill=fill, outline=color, width=4)
    d.text((x + 48, y + 37), title, font=F_LAYER, fill=color)
    d.line((x + 190, y + 26, x + 190, y + h - 26), fill=color, width=3)

    item_w = 225
    item_h = 58
    gap = 43
    start_x = x + 250
    cy = y + h / 2
    for i, item in enumerate(items):
        ix = start_x + i * (item_w + gap)
        iy = cy - item_h / 2
        d.rounded_rectangle((ix, iy, ix + item_w, iy + item_h), radius=12, fill=WHITE, outline=color, width=3)
        center_text((ix + 8, iy + 5, ix + item_w - 8, iy + item_h - 5), item, F_BOX, TEXT)


def side_strip(x, y, w, h, labels):
    d.rounded_rectangle((x, y, x + w, y + h), radius=18, fill=(245, 249, 255), outline=BLUE, width=3)
    cx = x + w / 2
    section_h = h / len(labels)
    for i, label in enumerate(labels):
        top = y + i * section_h
        center_text((x + 7, top + 8, x + w - 7, top + section_h - 18), label, F_SIDE, BLUE, spacing=7)
        if i < len(labels) - 1:
            arrow_down(cx, top + section_h - 22, top + section_h + 16)


center_text((0, 18, W, 72), "图5  学生成长模拟卡牌游戏原型系统功能架构图", F_TITLE, BLUE)
center_text((0, 78, W, 112), "定位为可演示 Unity MVP 与项目文档支撑系统，不追求真实商业化部署", F_SUBTITLE, MUTED)
d.line((82, 127, W - 82, 127), fill=(204, 216, 232), width=2)

side_strip(78, 170, 68, 590, ["数据层", "逻辑计算", "场景交互", "系统展示"])

panel_x = 172
panel_w = W - 238
layer_h = 116
layer_gap = 34
layers = [
    (
        168,
        BLUE,
        (244, 249, 255),
        "数据层",
        ["游戏数据资产", "卡牌/道具/活动", "地点与地图配置", "美术资源目录"],
    ),
    (
        168 + layer_h + layer_gap,
        PURPLE,
        (249, 245, 255),
        "逻辑层",
        ["阶段推进控制", "卡牌战斗模型", "奖励/事件解析", "物品效果处理"],
    ),
    (
        168 + (layer_h + layer_gap) * 2,
        GREEN,
        (243, 252, 245),
        "场景交互层",
        ["地图场景路由", "地点解锁判定", "玩家移动交互", "UI面板切换"],
    ),
    (
        168 + (layer_h + layer_gap) * 3,
        ORANGE,
        (255, 248, 243),
        "展示应用层",
        ["阶段地图展示", "地点/活动界面", "卡牌战斗UI", "结局与文档输出"],
    ),
]

for y, color, fill, title, items in layers:
    layer_panel(panel_x, y, panel_w, layer_h, color, fill, title, items)

center_x = panel_x + panel_w / 2
for i in range(len(layers) - 1):
    y1 = layers[i][0] + layer_h + 8
    y2 = layers[i + 1][0] - 12
    arrow_down(center_x, y1, y2)

d.rounded_rectangle((205, 790, W - 205, 846), radius=12, fill=WHITE, outline=(214, 224, 238), width=2)
center_text(
    (215, 796, W - 215, 840),
    "成果形态：可演示 Unity 原型系统 + 美术资源接入成果 + 测试验证材料 + 项目文档图表",
    F_BOTTOM,
    BLUE,
)

d.text((78, 862), "保存位置：ScriptCodeDocs/student_growth_system_architecture.png", font=F_NOTE, fill=MUTED)

img.save(OUT_PATH)
print(OUT_PATH)
