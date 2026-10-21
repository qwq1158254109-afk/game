"""无缝材质砖生成器 v2：offset + 正确的线性 cross-fade。

标准做法：
  1) 把补丁滚动半个周期（roll by w/2），此时左半与右半各自内部连续，
     接缝落在原来补丁的正中央（即滚动后的中间过渡带）。
  2) 只在过渡带内用 0→1 线性 alpha 把"原图"与"滚动图"融合，
     过渡带两端各自等于其中一张，因此拼接处必然连续。
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
OUT_FLOOR = r"E:/大创项目/Tools/_floor_tile.png"
OUT_WALL = r"E:/大创项目/Tools/_wall_face_tile.png"
CHECK = r"E:/大创项目/Tools/_floor_seam_check.png"


def detrend(reg, strength=1.0):
    """去掉低通光照梯度，保留木纹/质感与色相。

    关键：重新锚定必须**逐通道**做。若写成 `reg / bg * bg.mean()`，
    `bg.mean()` 是三通道总均值（标量），会把每个通道都拉到同一个灰度值，
    颜色被洗成灰——2026-10-20 实测 R-B 从 125 掉到 0.1。

    正确做法：先 `reg / bg * bg.mean(axis=(0,1))` 让每通道回到原均值（保色相），
    再用 strength 在「原图」与「提平图」之间插值，避免纹理被完全抹掉。
    """
    h, w, _ = reg.shape
    small = Image.fromarray(reg.astype(np.uint8)).resize(
        (max(1, w // 8), max(1, h // 8)), Image.BOX)
    bg = np.asarray(small.resize((w, h), Image.BICUBIC)).astype(np.float32)
    bg = np.clip(bg, 1.0, None)
    anchor = bg.mean(axis=(0, 1))          # 逐通道均值，形状 (3,)
    flattened = reg / bg * anchor
    out = reg * (1.0 - strength) + flattened * strength
    return np.clip(out, 0, 255)


def _blend_axis(reg, axis, band_frac=0.35):
    """沿指定轴做 offset+crossfade，返回无缝结果。"""
    n = reg.shape[axis]
    half = n // 2
    rolled = np.roll(reg, half, axis=axis)

    # 过渡带：以滚动后的接缝位置（= half 附近）为中心的窗口
    band = max(2, int(n * band_frac))
    lo = half - band // 2
    hi = lo + band

    alpha = np.zeros(n, dtype=np.float32)
    ramp = np.linspace(0.0, 1.0, band, dtype=np.float32)
    alpha[lo:hi] = ramp
    alpha[:lo] = 0.0
    alpha[hi:] = 1.0

    if axis == 0:
        a = alpha[:, None, None]
    else:
        a = alpha[None, :, None]
    return reg * (1.0 - a) + rolled * a


def make_seamless(reg, band_frac=0.35):
    reg = _blend_axis(reg, 1, band_frac)   # 水平
    reg = _blend_axis(reg, 0, band_frac)   # 垂直
    return reg


def seam_score(arr):
    f = arr.astype(np.float32)
    tb = np.abs(f[0] - f[-1]).mean()
    lr = np.abs(f[:, 0] - f[:, -1]).mean()
    in_r = np.abs(f[1] - f[0]).mean()
    in_c = np.abs(f[:, 1] - f[:, 0]).mean()
    return tb, lr, (in_r + in_c) / 2


def main():
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)

    floor = detrend(a[600:776, 520:980])
    wall = detrend(a[70:260, 700:900])

    for name, reg, out in (("floor", floor, OUT_FLOOR), ("wall", wall, OUT_WALL)):
        for bf in (0.25, 0.35, 0.5):
            sm = make_seamless(reg.copy(), bf)
            u8 = np.clip(sm, 0, 255).astype(np.uint8)
            tb, lr, inb = seam_score(u8)
            print("%-6s band=%.2f  上下列缝=%.2f 左右列缝=%.2f  内部基准=%.2f" % (
                name, bf, tb, lr, inb))
        sm = make_seamless(reg.copy(), 0.35)
        u8 = np.clip(sm, 0, 255).astype(np.uint8)
        Image.fromarray(u8).save(out)

    ft = np.asarray(Image.open(OUT_FLOOR)).astype(np.uint8)
    h, w, _ = ft.shape
    tiled = Image.new("RGB", (w * 3, h * 3))
    for j in range(3):
        for i in range(3):
            tiled.paste(Image.fromarray(ft), (i * w, j * h))
    tiled = tiled.resize((w * 3 // 2, h * 3 // 2), Image.LANCZOS)
    sheet = Image.new("RGB", (w * 3 // 2, h * 3 // 2 + h + 6), (255, 0, 0))
    sheet.paste(Image.fromarray(ft), (0, 0))
    sheet.paste(tiled, (0, h + 6))
    sheet.save(CHECK)
    print("saved", CHECK)


if __name__ == "__main__":
    main()