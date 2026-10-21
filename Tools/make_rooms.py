"""把家的烘焙底图裁成「一个房间一张图」：纯地板镜像平铺 + 四面薄墙 + 可通行的门洞。

2026-10-19 改版（第二轮）三处变化：
  1) 墙厚 3.0 → 侧/北 1.5、南 2.0（南墙要装下 2.0 单位高的正门）；
     室内可走区因此从 24x14 扩到 27x14.5，画面里的大块深色墙边基本消失。
  2) 门洞不再是纯黑矩形：地板铺过去 + 压暗 + 门槛 + 双线门框，读作"能走过去的通道"。
  3) 四间房统一 29.5x17.5（1888x1120）@ PPU 64，相机 ortho 8.75（可见 31.09x17.5）。
     墙加厚到 2.0 单位（128px），门同步到 128x128（1.5x2.0 世界单位）。
     可走区仍为 25.5x13.5 —— 宽高各 +1.0 全部给了墙（2.0*2 + 25.5 = 29.5）。
     加厚必须同时抬 ortho，否则 17.5 高的画布会被裁掉 0.75/边。

2026-10-20 改版（第三轮）——房间壳改用源图真实材质：
  旧做法把地板取成"全图最均匀的 128x128"、墙用两个像素的颜色 draw.rectangle 平涂，
  结果房间壳只有 14~22 色，而同源家具是 13k~32k 色，观感割裂（用户原话"太光秃"）。
  现在地板与墙带都从 `home_new_space.png` 采真实材质并做无缝化（见 `make_shell_tex.py`）。
  几何（画布 / 墙厚 / 门洞 / 可走区）**一个像素都没动**。

口径：PPU 64 → 世界尺寸 = 像素 / 64；世界原点 = 图片中心（1888/2=944, 1120/2=560）。
      世界 x → 像素 px = 944 + x*64 ；世界 y → 像素 py = 560 - y*64
      （注释而已，脚本不依赖；画布内一切按像素绝对坐标布局）

用法：python E:/大创项目/Tools/make_rooms.py
"""
import hashlib
import os
import re

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
TILES = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Tiles"
MATERIALS = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeRooms/Materials"
OUT = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeRooms"
DOOR_OUT = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Props"

TILE = 48                     # 墙片像素尺寸（旧版墙砖用，保留兼容）
# 2026-10-20 第五轮（方案B 加厚，用户选定）：
#   四墙等厚 2.0 -> 2.5 世界单位（128 -> 160 px），门洞同步。
#   画布 1888x1120 -> 1991x1184；ortho 8.75 -> 9.25。
#   取 1991 而非 2106：1991/64 = 31.109 单位，与 ortho 9.25 在 16:9 下的
#   可见宽严丝合缝（纵向零裁、横向每边仅余 0.89 单位外沿），
#   而 2106 会左右各留 0.89 单位空隙。
W_SIDE, W_TOP = 160, 160      # 侧墙 / 北墙厚度（2.5 世界单位）
W_BOTTOM = 160                # 南墙厚度（2.5 世界单位，与其余三面一致）
DOOR_W = 160                  # 门洞沿墙方向的净宽
DOOR_HOLE = 160               # 洞口沿墙厚方向的尺寸 = 墙厚 = 2.5 世界单位
DOOR_SPRITE = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Props/home_ref_exit_door.png"
DOOR_CROP = (22, 0, 150, 125)  # 裁掉左侧 22px 墙色条（128x125），再缩放到 160x160
FLOOR_PATCH = (566, 630, 694, 758)   # 全图最均匀的 128x128 纯木地板（旧版用，保留对照）

CANVAS_W, CANVAS_H = 1991, 1184

# 光照曲线的取样区（与 make_shell_tex.py 的地板材质同源，避开立柱与楼梯）
LIGHT_X = (520, 880)
LIGHT_Y = (330, 1010)

# gaps：像素起点；None = 居中
ROOMS = {
    "living": dict(
        gaps={
            "left": [((CANVAS_H - DOOR_W) // 2, DOOR_W)],      # 西墙 → 书房（y=0）
            "right": [((CANVAS_H - DOOR_W) // 2, DOOR_W)],     # 东墙 → 卧室（y=0）
            "top": [(CANVAS_W // 2 - 6 * 64 - DOOR_W // 2, DOOR_W)],   # 北墙 → 厨房（x=-6）
            "bottom": [((CANVAS_W - DOOR_W) // 2, DOOR_W)],            # 南墙正门（x=0）
        },
    ),
    "bedroom": dict(gaps={"left": [((CANVAS_H - DOOR_W) // 2, DOOR_W)]}),
    "study": dict(gaps={"right": [((CANVAS_H - DOOR_W) // 2, DOOR_W)]}),
    "kitchen": dict(gaps={"bottom": [((CANVAS_W - DOOR_W) // 2, DOOR_W)]}),
}


def resolve_gaps(spec_gaps, side, length):
    out = []
    for anchor, size in spec_gaps.get(side, []):
        start = (length - size) // 2 if anchor is None else int(anchor)
        out.append((start, size))
    return out


# 门素材与门洞的统一口径见下方 _load_door_sprite() / door_hole_box() 的注释


def _neutralize_wall_pixels(img):
    """把门素材里残留的"奶油墙"像素换成深木门框色。

    2026-10-21（用户报"家中左右两扇门出现重叠"）：
      门洞是 160x160（2.5 单位见方），而素材 x0..23 是一道**奶油墙**
      （实测 RGB(183,132,85) L=133 饱和 0.55）。`crop(22,0,150,125)` 裁完仍带
      左墙 1~2px 与底部一段墙色（探针实测占 7.8% ≈ 2000px），放大到 160 后
      就成了左边缘 + 底边的一圈墙色。
      侧墙门要 `ROTATE_90/270`，这圈墙色旋转后**正好横躺在房间地板上**——
      左右两扇门各多出一截，看起来就是"重叠"。

    做法：饱和度高（>0.42）且亮度高（>=110）的像素判为墙（窗玻璃饱和只有 0.40，
    不会误伤），整体替换为门框深木色。比单纯裁窄更好：裁窄会连带切掉门体。
    """
    a = np.asarray(img).astype(np.float32)
    rgb, alpha = a[:, :, :3], a[:, :, 3]
    lum = rgb.mean(axis=2)
    mx = rgb.max(axis=2)
    mn = rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1.0), 0.0)
    wall = (lum >= 110.0) & (sat >= 0.42) & (alpha > 8)

    if not wall.any():
        return img

    # 门框深木色取素材自身的深色木（右侧框条实测 RGB(91,48,21)）
    rgb[wall] = np.array([91.0, 48.0, 21.0], dtype=np.float32)
    a[:, :, :3] = rgb
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def _load_door_sprite():
    """现成的像素门素材（与家具同一画风，AI 生成那套）。

    原素材 150×125：x 0..23 是贴在墙上的墙色（会和我们的墙带打架），
    所以裁 `x 22..150`（宽 128）。纵向保留整段 125（含门楣与门扇），
    再缩放到洞口尺寸 160×160，并抹掉残留墙色（见 _neutralize_wall_pixels）。
    """
    src = Image.open(DOOR_SPRITE).convert("RGBA").crop(DOOR_CROP)
    src = _neutralize_wall_pixels(src)
    return src.resize((DOOR_W, DOOR_HOLE), Image.LANCZOS)


def door_hole_box(side, start, size):
    """洞口矩形 (x0, y0, x1, y1)。

    沿墙方向宽 128、沿墙厚方向高 96；**居中于门洞**，四侧口径一致。
    洞口比墙带的墙面段高（96 vs 96 相等），门素材 1:1 填满，不留缝。
    """
    if side == "left":
        return (0, start, W_SIDE, start + size)
    if side == "right":
        return (CANVAS_W - W_SIDE, start, CANVAS_W, start + size)
    if side == "top":
        return (start, 0, start + size, DOOR_HOLE)
    return (start, CANVAS_H - DOOR_HOLE, start + size, CANVAS_H)


def draw_door_in_wall(canvas, box, side):
    """把门素材贴进门洞（洞与素材同为 160×160，1:1 不缩放）。

    朝向：门底(外侧)朝墙外、门顶(内侧)朝房间。
      横墙 不旋转（素材本身就是门底在下）
      西墙 ROTATE_90 —— 逆时针；东墙 ROTATE_270
    （2026-10-20 修：此前用 rotate(±90) 把西/东墙的门底转到了墙外，踢脚线朝向是反的。）
    """
    img = _load_door_sprite()

    if side == "left":
        img = img.transpose(Image.ROTATE_90)
    elif side == "right":
        img = img.transpose(Image.ROTATE_270)
    elif side == "bottom":
        img = img.transpose(Image.FLIP_TOP_BOTTOM)

    canvas.alpha_composite(img, (box[0], box[1]))


def _load_material(name):
    path = os.path.join(MATERIALS, name)
    if not os.path.exists(path):
        raise FileNotFoundError(
            "缺少材质砖 %s —— 请先跑 python Tools/make_shell_tex.py" % path)
    return Image.open(path).convert("RGBA")


def paint_floor(canvas):
    """用真实木地板砖平铺**可走区**（y W_TOP..CANVAS_H-W_TOP，x W_SIDE..CANVAS_W-W_SIDE）。

    2026-10-20 修：原先铺满整个画布，而墙带贴画布外沿、厚度只有 96，
    侧墙与南北墙在**四个角落互相压盖**，视觉上多出一截台阶。
    现在地板与墙带互不重叠：地板占内沿以内，墙带占内沿以外。

    砖已做**水平**无缝（见 make_shell_tex.py），纵向刻意留中性——
    纵向明暗由 apply_floor_light() 统一施加。
    """
    patch = _load_material("floor_tile.png")
    pw, ph = patch.size
    y0, y1 = W_TOP, CANVAS_H - W_TOP
    x0, x1 = W_SIDE, CANVAS_W - W_SIDE
    for j, ty in enumerate(range(y0, y1, ph)):
        for i, tx in enumerate(range(x0, x1, pw)):
            tile = patch if i % 2 == 0 else patch.transpose(Image.FLIP_LEFT_RIGHT)
            canvas.alpha_composite(
                tile.crop((0, 0, min(pw, x1 - tx), min(ph, y1 - ty))), (tx, ty))


def _gap_runs(gaps, lo, hi):
    """把 [lo,hi) 里**不在任何门洞内**的连续区间切出来。

    2026-10-20 修：早期版本用「整块起点是否落在洞里」来决定跳过整块砖，
    当门洞宽度（128）与墙砖宽度（240）不对齐时，会连带跳过洞旁边一大段，
    导致墙带出现一个整块大小的缺口、露出地板
    （实测南墙 x 976..1199 露出地板色，就是块 x=960 被整块跳过造成的）。
    现在改为逐像素求补集，只挖掉真正的洞口。
    """
    runs = []
    x = lo
    while x < hi:
        if any(g0 <= x < g0 + g1 for g0, g1 in gaps):
            x += 1
            continue
        start = x
        while x < hi and not any(g0 <= x < g0 + g1 for g0, g1 in gaps):
            x += 1
        runs.append((start, x))
    return runs


def _tile_runs_h(canvas, band, y0, runs, alt_flip=True):
    """把横墙砖铺到若干连续 x 区间上（左右交替镜像以打散周期）。"""
    bw, bh = band.size
    for i, (lo, hi) in enumerate(runs):
        x = lo
        n = 0
        while x < hi:
            piece = band if (n % 2 == 0 or not alt_flip) else band.transpose(Image.FLIP_LEFT_RIGHT)
            # 砖内偏移：让纹理在整条墙上连续，而不是每段都从头开始
            off = (x - lo) % bw
            w = min(bw - off, hi - x)
            canvas.alpha_composite(piece.crop((off, 0, off + w, bh)), (x, y0))
            x += w
            n += 1


def _tile_runs_v(canvas, band, x0, runs, alt_flip=True):
    """把竖墙砖铺到若干连续 y 区间上。"""
    bw, bh = band.size
    for i, (lo, hi) in enumerate(runs):
        y = lo
        n = 0
        while y < hi:
            piece = band if (n % 2 == 0 or not alt_flip) else band.transpose(Image.FLIP_TOP_BOTTOM)
            off = (y - lo) % bh
            h = min(bh - off, hi - y)
            canvas.alpha_composite(piece.crop((0, off, bw, off + h)), (x0, y))
            y += h
            n += 1


def wall_band_h(canvas, y0, thickness, gaps, x_range=None):
    """横向墙带：真实墙带材质 1:1 平铺，只挖掉真正的门洞像素。

    x_range 用来把墙带限制在某段内，避免四条墙带在**四角互相压盖**
    （2026-10-20：南/北墙带只铺到 x = CANVAS_W - W_SIDE，右侧那段留给东墙带）。
    """
    band = _load_material("wall_band_h.png")
    bw, bh = band.size
    assert bh == W_TOP, (bh, W_TOP)
    lo, hi = x_range if x_range else (0, CANVAS_W)
    runs = _gap_runs(gaps, lo, hi)
    _tile_runs_h(canvas, band, y0, runs)


def wall_band_v(canvas, x0, thickness, gaps, y_range=None):
    """竖向墙带：旋转后的竖墙带材质 1:1 平铺，只挖掉真正的门洞像素。

    y_range 用来避开南/北墙带（侧墙只占内沿之间 y = W_TOP .. CANVAS_H - W_TOP）。
    """
    band = _load_material("wall_band_v.png")
    bw, bh = band.size
    assert bw == W_SIDE, (bw, W_SIDE)
    lo, hi = y_range if y_range else (0, CANVAS_H)
    runs = _gap_runs(gaps, lo, hi)
    _tile_runs_v(canvas, band, x0, runs)


def draw_doorway(canvas, box, with_threshold=True):
    """门洞：把洞里的地板压暗成"通道深处"，再画门槛 + 双线门框，避免读成黑洞。

    注意：ImageDraw 的 4 元色在这条管线里不会做 alpha 混合（实测直接落成不透明），
    所以这里用"裁剪 → 调亮度 → 贴回"的方式压暗，完全避开 alpha。
    """
    x0, y0, x1, y1 = box
    region = canvas.crop(box)
    region = ImageEnhance.Brightness(region).enhance(0.45)
    canvas.paste(region, (x0, y0))

    # 洞口内侧一条亮色地板，读作"门这边还是屋子"
    if with_threshold:
        canvas.paste(Image.new("RGBA", (x1 - x0, 4), (150, 108, 68, 255)), (x0, y0))

    draw = ImageDraw.Draw(canvas)
    draw.rectangle((x0, y0, x1 - 1, y1 - 1), outline=(16, 11, 7, 255), width=6)
    draw.rectangle((x0 + 6, y0 + 6, x1 - 7, y1 - 7), outline=(120, 84, 54, 255), width=2)


def floor_light_profile():
    """从源图地板取出"光照纵深"，拟合成光滑曲线后归一化到均值 1。

    源图地板不是平的：近墙处暗、中部亮。这是源图有空间感的关键，用户明确要求保留。

    但**不能逐行照抄源图的亮度**——那里面混着楼梯、厨房隔墙、家具投影等内容起伏，
    照抄会在房间里留下尖峰和骤降（2026-10-20 实测 y780 尖峰 +7、y900 骤降 -22.7）。
    所以这里只拟合"两端暗、中间亮"的整体趋势：低阶多项式 + 加宽的高斯凸起。
    """
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    x0, x1 = LIGHT_X
    prof = a[LIGHT_Y[0]:LIGHT_Y[1], x0:x1].mean(axis=(1, 2))

    n = len(prof)
    t = np.linspace(-1.0, 1.0, n)                  # 归一化位置

    # 低阶多项式拟合整体走势（3 阶足够，避免跟随内容起伏）
    poly = np.polyfit(t, prof, 3)
    trend = np.polyval(poly, t)

    # 叠一个宽高斯，强化"中部亮"的观感（源图实测中部比两端亮约 10%）
    gauss = np.exp(-(t / 0.55) ** 2)
    shaped = trend * (0.94 + 0.12 * gauss)

    shaped = shaped / shaped.mean()                # 均值为 1：只改明暗，不改色调
    return shaped


def ao_field(decay=26.0):
    """环境光遮蔽场：贴墙最暗、按指数向外衰减。返回 (h, w) 的乘数图，<=1。

    深度与衰减长度**全部来自对源图的实测**：
      北墙脚  贴墙 66.9 / 无遮蔽 89.5  ->  深度 (1-0.25)=0.75，约 70px 恢复
      南墙脚  贴墙约 44   / 参考   93   ->  深度 (1-0.53)=0.47
      左墙脚  贴墙 68.7 / 无遮蔽 86.8  ->  深度 (1-0.21)=0.79，约 90px 恢复

    为什么用解析式而不是逐行采样源图剖面：
    源图南端叠了门槛、地垫、台阶三层结构（y 950~954 是亮边 103，
    y 956~968 是阴影核心 44，y 1030 才是墙脚），逐行采样会把门槛和地垫
    当成"光照"搬进来（2026-10-20 实测：前缀最大值整条被锁死、剖面变常数）。
    这里只保留"贴墙暗多少 + 多远恢复"两个可测参数，形态用标准 AO 衰减。
    """
    h = CANVAS_H - 2 * W_TOP
    w = CANVAS_W - 2 * W_SIDE
    ys = np.arange(h, dtype=np.float32)
    xs = np.arange(w, dtype=np.float32)

    def edge(dist, depth):
        """dist 为到墙的距离（像素），返回该处的乘数。"""
        return 1.0 - depth * np.exp(-dist / decay)

    # 深度取值说明：南端实测 0.53，但源图那里叠了门洞内凹的投影，
    # 我们这版房间的三面墙都是紧贴画布外沿、没有内凹，照搬会显得过重，
    # 所以收敛到 0.40（视觉上仍明显重于北/侧墙，符合"南墙是门那面"的观感）。
    depth_n, depth_s, depth_side = 0.25, 0.40, 0.21

    near_n = edge(ys, depth_n)                 # 贴北墙（顶部最暗）
    near_s = edge(h - 1 - ys, depth_s)         # 贴南墙（底部最暗）
    col = np.minimum(near_n, near_s)

    near_w = edge(xs, depth_side)              # 贴西墙
    near_e = edge(w - 1 - xs, depth_side)      # 贴东墙
    row = np.minimum(near_w, near_e)

    return np.minimum(col[:, None], row[None, :])


def apply_ao(canvas):
    """给可走区叠**环境光遮蔽**（墙脚接触阴影）。

    2026-10-20 补：此前版本为了消除光照色带，把源图里这段接触阴影当成
    "内容起伏"削掉了（当时实测 y900 骤降 -22.7，误判为楼梯/隔墙）。
    实际那是真实的墙脚遮蔽，是"地图像有体积"的关键，必须补回。

    只压暗、不改色调；范围严格限制在可走区（地板区）。
    """
    x0, y0 = W_SIDE, W_TOP
    x1, y1 = CANVAS_W - W_SIDE, CANVAS_H - W_TOP
    field = ao_field()

    arr = np.asarray(canvas).astype(np.float32)
    arr[y0:y1, x0:x1] *= field[:, :, None]
    np.clip(arr, 0, 255, out=arr)
    canvas.paste(Image.fromarray(arr.astype(np.uint8)), (0, 0))
    return field


def apply_floor_light(canvas):
    """把源图的光照纵深铺到**可走区**（与 paint_floor 的范围严格一致）。

    注意：这里只负责"整体亮度趋势"（近墙暗、中部亮）。
    墙脚那层**接触阴影**由 apply_ao() 单独负责，两者不要混在一起。
    """
    prof = floor_light_profile()
    y0, y1 = W_TOP, CANVAS_H - W_TOP
    x0, x1 = W_SIDE, CANVAS_W - W_SIDE
    src_y = np.linspace(0, len(prof) - 1, y1 - y0)
    curve = np.interp(src_y, np.arange(len(prof)), prof).astype(np.float32)

    arr = np.asarray(canvas).astype(np.float32)
    arr[y0:y1, x0:x1] *= curve[:, None, None]
    np.clip(arr, 0, 255, out=arr)
    canvas.paste(Image.fromarray(arr.astype(np.uint8)), (0, 0))


def build(name, spec):
    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H))
    paint_floor(canvas)
    apply_floor_light(canvas)     # 整体亮度趋势
    apply_ao(canvas)              # 墙脚接触阴影

    gaps = spec.get("gaps", {})
    side = {k: resolve_gaps(gaps, k, CANVAS_H) for k in ("left", "right")}
    horiz = {k: resolve_gaps(gaps, k, CANVAS_W) for k in ("top", "bottom")}

    # 四面墙带一律贴画布外沿、厚度统一 96；彼此不重叠：
    #   北/南墙带铺满内沿之间的宽度（到 x = CANVAS_W - W_SIDE），右侧那段留给东墙带
    #   侧墙带只占内沿之间的高度（y = W_TOP .. CANVAS_H - W_TOP）
    # 2026-10-20 修：此前南墙铺在 y=928..1024 而门洞在 y=960..1056，两者差 32px，
    # 洞口上方就露出墙面色；侧墙又通铺到画布底，四角互相压出一截"台阶"。
    wall_band_h(canvas, 0, W_TOP, horiz["top"], x_range=(0, CANVAS_W - W_SIDE))
    wall_band_h(canvas, CANVAS_H - W_TOP, W_TOP, horiz["bottom"], x_range=(0, CANVAS_W - W_SIDE))
    wall_band_v(canvas, 0, W_SIDE, side["left"], y_range=(W_TOP, CANVAS_H - W_TOP))
    wall_band_v(canvas, CANVAS_W - W_SIDE, W_SIDE, side["right"], y_range=(W_TOP, CANVAS_H - W_TOP))
    # 东北 / 东南两个角：用竖墙带补齐（侧墙带没铺到的那 96×96 方块）
    for cx in (CANVAS_W - W_SIDE,):
        for cy in (0, CANVAS_H - W_TOP):
            canvas.alpha_composite(_load_material("wall_band_v.png"), (cx, cy))

    holes = []
    for start, size in horiz["top"]:
        holes.append(door_hole_box("top", start, size) + ("top",))
    for start, size in horiz["bottom"]:
        holes.append(door_hole_box("bottom", start, size) + ("bottom",))
    for start, size in side["left"]:
        holes.append(door_hole_box("left", start, size) + ("left",))
    for start, size in side["right"]:
        holes.append(door_hole_box("right", start, size) + ("right",))

    # 抠洞：露出地板（洞口现在透光，不再是一块黑）
    for box in holes:
        x0, y0, x1, y1 = box[:4]
        canvas.paste(Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0)), (x0, y0))
        paint_hole_floor(canvas, (x0, y0, x1, y1))

    for box in holes:
        x0, y0, x1, y1, side_name = box
        draw_doorway(canvas, (x0, y0, x1, y1), with_threshold=False)
        draw_door_in_wall(canvas, (x0, y0, x1, y1), side_name)

    path = os.path.join(OUT, "room_%s.png" % name)
    canvas.convert("RGB").save(path)
    inner_w = (CANVAS_W - 2 * W_SIDE) / 64.0
    inner_h = (CANVAS_H - W_TOP - W_TOP) / 64.0
    print("%-8s %dx%d  可走区 %.2f x %.2f  ->  %s" % (name, CANVAS_W, CANVAS_H, inner_w, inner_h, path))


def paint_hole_floor(canvas, box):
    """洞口里补一块地板（与周围同源，只是被压暗）。"""
    patch = _load_material("floor_tile.png")
    x0, y0, x1, y1 = box
    for yy in range(y0, y1, patch.height):
        for xx in range(x0, x1, patch.width):
            canvas.alpha_composite(
                patch.crop((0, 0, min(patch.width, x1 - xx), min(patch.height, y1 - yy))),
                (xx, yy))


def export_door_frames():
    """把裁好的门帧导出成资产，给场景里的门对象（MapDoorAnimator）用。

    底图里的门和场景里的门必须严格同位同尺寸，所以两边共用同一套裁剪结果：
    这里写的 `home_ref_door_inwall*.png` 就是 `DOOR_CROP` 的产物
    （128×96 @64PPU = 2.0×1.5 世界单位）。
    meta 只在缺失时写，GUID 由文件名 md5 派生 —— 重复跑脚本不会换 GUID，场景引用不会断。
    """
    frames = [("home_ref_door_inwall.png", DOOR_SPRITE)]
    for i in range(1, 4):
        frames.append(("home_ref_door_inwall_open_0%d.png" % i,
                       DOOR_SPRITE.replace(".png", "_open_0%d.png" % i)))

    with open(DOOR_SPRITE + ".meta", encoding="utf-8") as f:
        template = f.read()

    for name, src_path in frames:
        path = os.path.join(DOOR_OUT, name)
        img = Image.open(src_path).convert("RGBA").crop(DOOR_CROP)
        img = img.resize((DOOR_W, DOOR_HOLE), Image.LANCZOS)
        img.save(path)
        meta_path = path + ".meta"
        if not os.path.exists(meta_path):
            guid = hashlib.md5(name.encode("utf-8")).hexdigest()
            with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
                f.write(re.sub(r"(guid:\s*)([0-9a-f]{32})", lambda m: m.group(1) + guid, template, count=1))
        print("门帧 -> %s (%dx%d)" % (path, DOOR_W, DOOR_HOLE))


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, spec in ROOMS.items():
        build(name, spec)
    export_door_frames()


if __name__ == "__main__":
    main()
