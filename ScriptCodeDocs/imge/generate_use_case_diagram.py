from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math


ROOT = Path(r"E:\unity\ML-project")
OUT_DIR = ROOT / "ScriptCodeDocs" / "imge"
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_PATH = OUT_DIR / "student_growth_use_case_diagram.png"

W, H = 2500, 2200
img = Image.new("RGB", (W, H), "white")
d = ImageDraw.Draw(img)

FONT_CANDIDATES = [
    r"C:\Windows\Fonts\msyh.ttc",
    r"C:\Windows\Fonts\simhei.ttf",
    r"C:\Windows\Fonts\Deng.ttf",
    r"C:\Windows\Fonts\simsun.ttc",
]


def load_font(size):
    for path in FONT_CANDIDATES:
        if Path(path).exists():
            try:
                return ImageFont.truetype(path, size=size)
            except OSError:
                pass
    return ImageFont.load_default()


F_TITLE = load_font(48)
F_SUB = load_font(22)
F_USE = load_font(27)
F_ACTOR = load_font(26)
F_REL = load_font(20)
F_FOOT = load_font(20)

BLACK = (18, 18, 18)
GRAY = (96, 96, 96)


def text_size(text, font):
    box = d.multiline_textbbox((0, 0), text, font=font, spacing=5)
    return box[2] - box[0], box[3] - box[1]


def center_text(box, text, font=F_USE, fill=BLACK):
    x1, y1, x2, y2 = box
    tw, th = text_size(text, font)
    d.multiline_text(
        (x1 + (x2 - x1 - tw) / 2, y1 + (y2 - y1 - th) / 2),
        text,
        font=font,
        fill=fill,
        align="center",
        spacing=5,
    )


def use_case(cx, cy, w, h, label):
    box = (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2)
    d.ellipse(box, outline=BLACK, width=3)
    center_text(box, label)
    return box


def actor(cx, cy, label):
    r = 25
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=BLACK, width=4)
    d.line((cx, cy + r, cx, cy + 120), fill=BLACK, width=4)
    d.line((cx - 60, cy + 62, cx + 60, cy + 62), fill=BLACK, width=4)
    d.line((cx, cy + 120, cx - 54, cy + 186), fill=BLACK, width=4)
    d.line((cx, cy + 120, cx + 54, cy + 186), fill=BLACK, width=4)
    tw, _ = text_size(label, F_ACTOR)
    d.multiline_text((cx - tw / 2, cy + 202), label, font=F_ACTOR, fill=BLACK, align="center", spacing=5)


def center(box):
    x1, y1, x2, y2 = box
    return (x1 + x2) / 2, (y1 + y2) / 2


def ellipse_edge(box, toward):
    cx, cy = center(box)
    x1, y1, x2, y2 = box
    rx = (x2 - x1) / 2
    ry = (y2 - y1) / 2
    dx = toward[0] - cx
    dy = toward[1] - cy
    if dx == 0 and dy == 0:
        return cx, cy
    scale = 1 / math.sqrt((dx * dx) / (rx * rx) + (dy * dy) / (ry * ry))
    return cx + dx * scale, cy + dy * scale


def association(point, box):
    p2 = ellipse_edge(box, point)
    d.line((point[0], point[1], p2[0], p2[1]), fill=BLACK, width=2)


def dashed_segment(p1, p2):
    x1, y1 = p1
    x2, y2 = p2
    dx = x2 - x1
    dy = y2 - y1
    length = math.hypot(dx, dy)
    if length <= 0:
        return
    vx = dx / length
    vy = dy / length
    dash = 13
    gap = 8
    t = 0
    while t < length:
        a = t
        b = min(t + dash, length)
        d.line((x1 + vx * a, y1 + vy * a, x1 + vx * b, y1 + vy * b), fill=BLACK, width=2)
        t += dash + gap


def arrow_head(p1, p2):
    angle = math.atan2(p2[1] - p1[1], p2[0] - p1[0])
    size = 16
    spread = math.radians(28)
    points = [(p2[0], p2[1])]
    for sign in (1, -1):
        a = angle + math.pi + sign * spread
        points.append((p2[0] + size * math.cos(a), p2[1] + size * math.sin(a)))
    d.polygon(points, fill=BLACK)


def label_at(x, y, label):
    tw, th = text_size(label, F_REL)
    d.rectangle((x - tw / 2 - 6, y - th / 2 - 4, x + tw / 2 + 6, y + th / 2 + 4), fill="white")
    d.text((x - tw / 2, y - th / 2), label, font=F_REL, fill=BLACK)


def relation(src, dst, label, label_offset=(0, 0)):
    c1 = center(src)
    c2 = center(dst)
    p1 = ellipse_edge(src, c2)
    p2 = ellipse_edge(dst, c1)
    dashed_segment(p1, p2)
    arrow_head(p1, p2)
    mx = (p1[0] + p2[0]) / 2 + label_offset[0]
    my = (p1[1] + p2[1]) / 2 + label_offset[1]
    label_at(mx, my, label)


center_text((0, 34, W, 104), "学生成长模拟卡牌游戏用例图", F_TITLE)
center_text((0, 106, W, 146), "参考《用例图-金徐生.docx》的黑白 UML 样式；内容基于当前 Unity 项目 StudentGrowth MVP/V2 模块。", F_SUB, GRAY)

actor(260, 680, "玩家")
actor(260, 1765, "内容配置人员\n(Unity Editor)")

boxes = {}


def add(key, x, y, w, h, label):
    boxes[key] = use_case(x, y, w, h, label)


main_x = 680
mid_x = 1220
right_x = 1780

main_cases = [
    ("start", 240, 320, 86, "开始新游戏"),
    ("map", 420, 320, 86, "查看阶段地图"),
    ("choose_location", 600, 320, 86, "选择地点"),
    ("action", 780, 330, 88, "执行地点操作"),
    ("battle", 960, 330, 88, "进行卡牌挑战"),
    ("boss", 1140, 330, 88, "挑战阶段 Boss"),
    ("ending", 1420, 300, 82, "查看结局"),
]

for key, y, w, h, label in main_cases:
    add(key, main_x, y, w, h, label)

sub_cases = [
    ("load_data", mid_x, 200, 300, 78, "加载数据资产"),
    ("init_state", mid_x, 280, 320, 78, "初始化玩家状态"),
    ("status", mid_x, 380, 300, 78, "显示学生状态"),
    ("stage_goal", mid_x, 460, 300, 78, "显示阶段目标"),
    ("unlock", mid_x, 600, 300, 78, "检查地点解锁"),
    ("lock_reason", right_x, 600, 300, 78, "显示锁定原因"),
    ("cost", mid_x, 720, 335, 78, "扣除时间/精力/金钱"),
    ("reward", mid_x, 840, 300, 78, "结算奖励"),
    ("optional_tools", right_x, 780, 370, 92, "使用道具/购买商品\n/管理卡组"),
    ("draw_play", mid_x, 920, 320, 78, "抽牌/出牌/弃牌"),
    ("card_effect", mid_x, 1000, 320, 78, "解析卡牌效果"),
    ("boss_reward", mid_x, 1140, 320, 78, "发放胜败奖励"),
    ("next_stage", right_x, 1260, 330, 86, "进入下一阶段\n/查看结局"),
    ("ending_select", mid_x, 1420, 360, 78, "选择最高优先级结局"),
]

for key, x, y, w, h, label in sub_cases:
    add(key, x, y, w, h, label)

admin_cases = [
    ("config_core", main_x, 1680, 360, 94, "配置核心数据\n阶段/活动/卡牌/道具"),
    ("config_location", main_x, 1840, 360, 94, "配置地点与操作\nLocation/Action"),
    ("bind_art", main_x, 2000, 330, 88, "绑定美术资源"),
    ("core_assets", mid_x, 1680, 340, 84, "ScriptableObject\n数据资产"),
    ("location_assets", mid_x, 1840, 350, 84, "LocationData /\nActionData"),
    ("sprite_refs", mid_x, 2000, 350, 84, "Sprite 字段 /\nResources 路径"),
    ("sample_assets", right_x, 1760, 360, 84, "生成示例数据资产"),
    ("runtime_ui", right_x, 2000, 360, 84, "运行时自动创建 UI"),
]

for key, x, y, w, h, label in admin_cases:
    add(key, x, y, w, h, label)

player_anchor = (323, 742)
for key, *_ in main_cases:
    association(player_anchor, boxes[key])

admin_anchor = (323, 1827)
for key in ("config_core", "config_location", "bind_art"):
    association(admin_anchor, boxes[key])

relations = [
    ("start", "load_data", "<<include>>", (0, -16)),
    ("start", "init_state", "<<include>>", (0, 18)),
    ("map", "status", "<<include>>", (0, -15)),
    ("map", "stage_goal", "<<include>>", (0, 16)),
    ("choose_location", "unlock", "<<include>>", (0, -16)),
    ("unlock", "lock_reason", "<<include>>", (0, -16)),
    ("action", "cost", "<<include>>", (0, -18)),
    ("action", "reward", "<<include>>", (0, 18)),
    ("optional_tools", "action", "<<extend>>", (0, 22)),
    ("battle", "draw_play", "<<include>>", (0, -16)),
    ("battle", "card_effect", "<<include>>", (0, 18)),
    ("boss", "boss_reward", "<<include>>", (0, -18)),
    ("boss", "next_stage", "<<extend>>", (0, -18)),
    ("ending", "ending_select", "<<include>>", (0, -16)),
    ("config_core", "core_assets", "<<include>>", (0, -18)),
    ("config_location", "location_assets", "<<include>>", (0, -18)),
    ("core_assets", "sample_assets", "<<include>>", (0, -18)),
    ("location_assets", "sample_assets", "<<include>>", (0, 18)),
    ("bind_art", "sprite_refs", "<<include>>", (0, -18)),
    ("sprite_refs", "runtime_ui", "<<include>>", (0, 16)),
]

for source, target, label, offset in relations:
    relation(boxes[source], boxes[target], label, offset)

d.text((76, H - 62), "保存位置：ScriptCodeDocs/imge/student_growth_use_case_diagram.png", font=F_FOOT, fill=GRAY)

img.save(OUT_PATH)
print(OUT_PATH)
