"""房间壳材质砖生成器：从初版家房间壳 home_new_space.png 生成可平铺的地板砖与墙带。

2026-10-20 晚（第四轮）——**按初版比例校正取材口径**：
    用户要求「参照初版家庭环境去设计房间壳」。核对 git 后确认
    `home_new_space.png` / `home_new_reference.png` 自初版 commit 900848a 起
    blob 未变（`git rev-parse 900848a:<path>` == `HEAD:<path>`），
    所以初版设计就是这两个文件本身，本轮直接把取材口径对齐它们。

    初版壳北墙逐行精量（x200..500 净面，避开窗与立柱）：
        y 28.. 35   图外暗边                L≈ 8.7
        y 36        极暗铰链线              L≈ 3.8
        y 38.. 63   深木上框                L≈46 -> 58
        y 64        暗分线                  L≈43
        y 65.. 67   过渡（木→奶油）          L≈106 -> 142
        y 68..265   奶油墙面（整段）         L≈148 -> 124（自带纵深变暗）
        y266..      直接切进地板（木色）     L≈62

    **初版没有独立踢脚线**——这是本轮最容易做错的一处：
    先前版本把"墙脚下段（y200..264）"当成踢脚去取，但那段其实就是奶油墙面的下段，
    结果在墙内侧压出一条**亮奶油边**，读起来像"墙里又嵌了一圈框"。
    现在改成三段：外沿(暗) + 深木上框 + 奶油墙面，踢脚段取消。

    另外两处修正：
      1. 旧 FACE 取 y68..263 整段 195px 压到 87px；现在**逐像素 1:1** 取 108px，
         保住墙面固有纵深（缩放会把它压扁，墙会"发飘"）。
      2. 旧 BASE 取 y264..296（那是**地板**），把地板当墙用，几何没错但观感错位。

    段高分配（和 = BAND_H = 128）：EDGE 4 / TOP 16 / FACE 108。

产出（都在 Assets/Art/Pixel/Generated/HomeRooms/Materials/）：
    floor_tile.png   360x360  地板砖，水平无缝（纵向留给房间级光照曲线）
    wall_band_h.png  240x128  横墙带（上=外沿+深木上框 / 下=奶油墙面，含墙脚纵深）
    wall_band_v.png  128x240  竖墙带，由横带 ROTATE_-90 而来

素材依据见 Tools/probe_initial_shell.py / probe_initial_wall_layers.py。
"""
import hashlib
import os
import re

import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/New2_0/School/Source/SchoolShell.png"
OUT_DIR = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeRooms/Materials"

# 地板：从**最新版地点壳 SchoolShell.png** 取木地板（用户要求"地板和 git 上保持一致"）。
# 模板匹配证据（Tools/probe_floor_provenance.py）：用户截图的地板小块在 SchoolShell 上
# 命中 MAD=4.66（最小），home_new_space.png 为 9.44 —— 即游戏里看到的地板就出自 SchoolShell。
# 色差也吻合：SchoolShell 地板 RGB(150.4,96.6,57.4) L=101.5 R-B=93.0；
# 家源图同区 L=104.4 但 R-B=122（明显偏橙），对不上。
FLOOR_X = (300, 700)          # 宽 400
FLOOR_Y = (500, 620)          # 高 120
FLOOR_STACK = 3               # 纵向叠 3 段 + 交叉淡化
CROSSFADE = 12                # 段间淡化行数

# 墙：从 SchoolShell 取，按它的四段节奏组装（多点取样见 probe_school_shell_colors.py）
WALL_X = (700, 1160)          # 宽 460，避开窗帘/窗（之前误取窗帘导致色偏）
EDGE_Y = (28, 31)             # 外沿（最上沿的深色收边）
TOP_Y = (31, 45)              # 木框/上沿
FACE_Y = (80, 200)            # 奶油墙面上段（四点实测一致：L≈163.5 R-B≈75.9）
WAIN_Y = (226, 300)           # 深木护墙板（比 128 版向下多取 14px；避开 y300+ 的墙脚过渡带）
BASE_Y = (300, 312)           # 墙脚收边

BAND_H = 160                  # 墙带高度 = 2.5 世界单位 @64PPU（= 墙厚；方案B 加厚）
# 五段按比例重分（和 = BAND_H = 160）。各段取源区间按新高度等比放大，
# 保证每一段内部的纹理密度与 128 版一致、不产生压缩感。
EDGE_H = 4
TOP_H = 15
FACE_H = 76
WAIN_H = 51
BASE_H = BAND_H - EDGE_H - TOP_H - FACE_H - WAIN_H        # 14


# ------------------------------------------------------------------ 工具

def detrend(reg, strength=0.35):
    """逐通道去低通光照梯度。

    必须逐通道重新锚定（`bg.mean(axis=(0,1))`）。若写成标量 `bg.mean()`，
    三个通道会被拉到同一个灰度值，颜色洗成灰——2026-10-20 实测 R-B 从 125 掉到 0.1。
    """
    h, w, _ = reg.shape
    small = Image.fromarray(reg.astype(np.uint8)).resize(
        (max(1, w // 8), max(1, h // 8)), Image.BOX)
    bg = np.asarray(small.resize((w, h), Image.BICUBIC)).astype(np.float32)
    bg = np.clip(bg, 1.0, None)
    flat = reg / bg * bg.mean(axis=(0, 1))
    return np.clip(reg * (1 - strength) + flat * strength, 0, 255)


def mirror_seamless(reg, axis):
    """镜像无缝化：取前一半 + 其镜像，拼接处天然连续，平铺零接缝。

    比 np.roll + crossfade 更可靠——roll 会把对侧边缘像素循环过来，产生硬跳变
    （2026-10-20 实测缝 26.7，镜像法为 0.00）。
    """
    n = reg.shape[axis]
    half = n // 2
    if axis == 1:
        left = reg[:, :half]
        return np.concatenate([left, left[:, ::-1]], axis=1)
    top = reg[:half]
    return np.concatenate([top, top[::-1]], axis=0)


def crossfade_stack(strips, fade):
    """把多条同宽横带纵向叠成一条，接缝处做 fade 行线性淡化（消除硬边）。"""
    h, w, _ = strips[0].shape
    total = h * len(strips)
    out = np.zeros((total, w, 3), dtype=np.float32)
    for i, s in enumerate(strips):
        out[i * h:(i + 1) * h] = s
    for i in range(1, len(strips)):
        y0 = i * h
        lo = max(0, y0 - fade // 2)
        hi = min(total, y0 + fade // 2)
        ramp = np.linspace(0.0, 1.0, hi - lo, dtype=np.float32)[:, None, None]
        # 用相邻两段在接缝处互相淡化
        a = out[lo:hi]
        b = np.roll(out, h, axis=0)[lo:hi]
        out[lo:hi] = a * (1 - ramp) + b * ramp
    return out


# ------------------------------------------------------------------ 生成

def build_floor(a):
    x0, x1 = FLOOR_X
    y0, y1 = FLOOR_Y
    base = detrend(a[y0:y1, x0:x1], 0.30)

    strips = []
    for i in range(FLOOR_STACK):
        shift = i * 37                       # 每段错开，避免同一内容重复
        s = np.roll(base, shift, axis=0)
        strips.append(s)
    stacked = crossfade_stack(strips, CROSSFADE)

    stacked = mirror_seamless(stacked, 1)     # 水平镜像无缝

    # 纵向刻意**不做**镜像：纵向明暗交给房间级的 floor_light_profile()（源图光照纵深）。
    # 若这里也留渐变、外面再镜像平铺，每块砖会形成"V 形"折返，
    # 与光照曲线相撞后出现可见的横向色带（2026-10-20 实测 y≈600 一条亮带一条暗带）。
    # 所以纵向只做"轻度提平"，让砖本身在纵向尽量中性。
    flat = detrend(stacked, 0.85)
    return np.clip(flat, 0, 255).astype(np.uint8)


def build_wall_band(a):
    """按**最新版地点壳（SchoolShell）**的四段节奏组装横墙带。

    段序（自上而下）与学校后墙一致：
        外沿(暗) → 木框/上沿 → 奶油墙面 → 深木护墙板 → 墙脚收边
    护墙板是本轮新增的关键段落——此前家的墙带**没有护墙板**，
    这正是"照学校做"时最明显的缺失。

    取源按学校实测：奶油墙面 y80..200（四点实测 L≈163.5 / R-B≈75.9）、
    护墙板 y240..300（逐 10 行实测 L≈59 / R-B≈48）。
    """
    x0, x1 = WALL_X
    w = x1 - x0

    edge = a[EDGE_Y[0]:EDGE_Y[0] + EDGE_H, x0:x1]
    top = _resize_rows(a[TOP_Y[0]:TOP_Y[1], x0:x1], w, TOP_H)
    face = _resize_rows(a[FACE_Y[0]:FACE_Y[1], x0:x1], w, FACE_H)
    wain = _resize_rows(a[WAIN_Y[0]:WAIN_Y[1], x0:x1], w, WAIN_H)
    base = _resize_rows(a[BASE_Y[0]:BASE_Y[1], x0:x1], w, BASE_H)

    parts = [edge, top, face, wain, base]
    heights = (EDGE_H, TOP_H, FACE_H, WAIN_H, BASE_H)
    names = ("edge", "top", "face", "wain", "base")
    for n, p, h in zip(names, parts, heights):
        assert p.shape[0] == h, (n, p.shape, h)

    # 去低通光照梯度：只压掉左右不均，保留纵向纵深
    parts = [detrend(p, 0.30) for p in parts]

    # 护墙板特殊处理：源区（学校墙下段）带**竖向木板纹理**，
    # 墙带却是横向平铺的 —— 直接铺会把木板纹理重复成"横向条纹"。
    # 改用 crossfade_seamless：首尾交叉淡化，平铺时天然连续，且**不会像镜像那样
    # 在中心产生突变**（镜像法在 460px 周期处会留一道硬缝，实测就是那条最明显的条纹）。
    wain_index = names.index("wain")
    parts[wain_index] = crossfade_seamless(parts[wain_index])

    other = [i for i in range(len(parts)) if i != wain_index]
    for i in other:
        parts[i] = mirror_seamless(parts[i], 1)

    wmin = min(p.shape[1] for p in parts)
    parts = [p[:, :wmin] for p in parts]

    band = np.concatenate(parts, axis=0)
    assert band.shape[0] == BAND_H, band.shape
    return np.clip(band, 0, 255).astype(np.uint8)


def crossfade_seamless(reg, fade_ratio=0.18):
    """首尾交叉淡化，使横向平铺天然连续（不产生镜像法那种中心突变）。

    做法：取一条与 reg 等宽的线性 ramp，用它把"左移半宽的版本"与"原图"混合，
    于是左边界像素逐渐过渡成右边界像素。
    """
    w = reg.shape[1]
    if w < 8:
        return reg
    fade = max(2, int(w * fade_ratio))
    shifted = np.roll(reg, -w // 2, axis=1)
    ramp = np.ones((1, w, 1), dtype=np.float32)
    left = np.linspace(0.0, 1.0, fade, dtype=np.float32)[None, :, None]
    ramp[:, :fade] = left
    ramp[:, -fade:] = left[:, ::-1]
    return reg * ramp + shifted * (1.0 - ramp)


def _resize_rows(src, width, height):
    """把一段源图缩放到 (height, width)，LANCZOS 保留木纹/纸纹。"""
    img = Image.fromarray(np.clip(src, 0, 255).astype(np.uint8)).resize(
        (width, height), Image.LANCZOS)
    return np.asarray(img).astype(np.float32)


def report(name, u8, extra=""):
    uniq = len(np.unique(u8.reshape(-1, 3), axis=0))
    m = u8.reshape(-1, 3).mean(axis=0)
    cold = (u8[:, :, 2].astype(int) > u8[:, :, 0].astype(int) + 5).mean()
    print("  %-14s %-10s 色数=%-6d R-B=%5.1f 偏冷率=%.4f %s" % (
        name, "%dx%d" % (u8.shape[1], u8.shape[0]), uniq, m[0] - m[2], cold, extra))


def write_meta(png_path, template_meta):
    """给材质砖补导入设置：64 PPU / Point 过滤 / 无压缩 / 无 mipmap。

    与家具素材（home_ref_*.png）完全同规格。GUID 由文件名 md5 派生，重复跑不会换 GUID。
    """
    if not os.path.exists(template_meta):
        return False
    meta_path = png_path + ".meta"
    with open(template_meta, encoding="utf-8") as f:
        template = f.read()
    name = os.path.basename(png_path)
    guid = hashlib.md5(name.encode("utf-8")).hexdigest()
    if os.path.exists(meta_path):
        with open(meta_path, encoding="utf-8") as f:
            existing = f.read()
        guid = re.search(r"guid:\s*([0-9a-f]{32})", existing).group(1)
    text = re.sub(r"(guid:\s*)([0-9a-f]{32})", lambda m: m.group(1) + guid, template, count=1)
    text = re.sub(r"(spritePixelsToUnits:\s*)[\d.]+", r"\g<1>64", text)
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return True


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)

    floor = build_floor(a)
    band = build_wall_band(a)

    floor_p = os.path.join(OUT_DIR, "floor_tile.png")
    band_p = os.path.join(OUT_DIR, "wall_band_h.png")
    vband_p = os.path.join(OUT_DIR, "wall_band_v.png")

    Image.fromarray(floor).save(floor_p)
    Image.fromarray(band).save(band_p)

    # 竖墙带：左=外沿+压边（朝墙外），右=踢脚线（朝房间内）
    v = np.asarray(Image.fromarray(band).rotate(-90, expand=True))
    Image.fromarray(v).save(vband_p)

    template = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Props/home_ref_sofa.png.meta"
    for p in (floor_p, band_p, vband_p):
        write_meta(p, template)

    print("=== 材质砖 ===")
    f = floor.astype(np.float32)
    report("floor_tile", floor, "水平缝=%.2f 垂直缝=%.2f 内部=%.2f" % (
        np.abs(f[:, 0] - f[:, -1]).mean(), np.abs(f[0] - f[-1]).mean(),
        np.abs(f[:, 2] - f[:, 1]).mean()))
    b = band.astype(np.float32)
    report("wall_band_h", band, "水平缝=%.2f 内部=%.2f" % (
        np.abs(b[:, 0] - b[:, -1]).mean(), np.abs(b[:, 2] - b[:, 1]).mean()))
    report("wall_band_v", v, "垂直缝=%.2f" % np.abs(v[0] - v[-1]).mean().mean())

    # 目检图
    fh, fw, _ = floor.shape
    tiled = Image.new("RGB", (fw * 2, fh * 2))
    for i in range(2):
        for j in range(2):
            tiled.paste(Image.fromarray(floor), (i * fw, j * fh))
    sheet = Image.new("RGB", (fw * 3, fh + band.shape[0] + 6), (255, 0, 0))
    sheet.paste(tiled.resize((fw, fh), Image.LANCZOS), (0, 0))
    sheet.paste(Image.fromarray(band), (0, fh + 6))
    sheet.paste(Image.fromarray(band), (band.shape[1], fh + 6))
    sheet.save(r"E:/大创项目/Tools/_materials_final.png")
    print("  saved Tools/_materials_final.png（左上=地板2x2缩略 下=墙带x2）")


if __name__ == "__main__":
    main()