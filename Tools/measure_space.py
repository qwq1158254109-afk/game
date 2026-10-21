"""精确测量 home_new_space.png 的材质分区位置。

目标：确定「北墙顶压边 / 墙面 / 踢脚线 / 地板」各自的精确像素行区间，
以及地板上不含内墙立柱的水平区间，作为可平铺材质的取样矩形。

用法：python Tools/measure_space.py
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"


def segments(mask, min_len=4):
    """把布尔序列切成连续 True 段，返回 [(start, end_exclusive, length)]。"""
    out, start = [], None
    for i, v in enumerate(mask):
        if v and start is None:
            start = i
        elif not v and start is not None:
            if i - start >= min_len:
                out.append((start, i, i - start))
            start = None
    if start is not None and len(mask) - start >= min_len:
        out.append((start, len(mask), len(mask) - start))
    return out


def main():
    src = Image.open(SRC).convert("RGB")
    a = np.asarray(src).astype(np.float32)
    H, W, _ = a.shape
    lum = a.mean(axis=2)
    print("source %dx%d" % (W, H))

    # ---------- 1. 垂直剖面：找墙/地板分界 ----------
    # 用中间开阔段，避开左侧楼梯与右侧内墙
    col_lo, col_hi = 500, 950
    rowprof = lum[:, col_lo:col_hi].mean(axis=1)
    print("\n=== 逐行亮度（x %d-%d），只列有跳变的前后行 ===" % (col_lo, col_hi))
    prev = None
    for y in range(0, 560):
        v = rowprof[y]
        if prev is None or abs(v - prev) >= 3.0:
            print("  y=%-5d lum=%6.1f   d=%+6.1f" % (y, v, 0.0 if prev is None else v - prev))
        prev = v

    # ---------- 2. 找最强的水平边（梯度最大处）----------
    grad = np.abs(np.diff(rowprof))
    top_edges = np.argsort(grad[:600])[::-1][:12]
    print("\n=== x %d-%d 段最强的水平边（y, 梯度）===" % (col_lo, col_hi))
    for y in sorted(top_edges):
        print("  y=%-5d grad=%6.2f  lum: %6.1f -> %6.1f" % (y, grad[y], rowprof[y], rowprof[y + 1]))

    # ---------- 3. 地板区内的垂直剖面：找内墙立柱 ----------
    y_lo, y_hi = 400, 520
    colprof = lum[y_lo:y_hi, :].mean(axis=0)
    cgrad = np.abs(np.diff(colprof))
    print("\n=== 地板带 y %d-%d 内的强竖直边（x, 梯度）——内墙立柱候选 ===" % (y_lo, y_hi))
    strong = np.where(cgrad > 8.0)[0]
    for x in strong:
        print("  x=%-5d grad=%6.2f  lum: %6.1f -> %6.1f" % (x, cgrad[x], colprof[x], colprof[x + 1]))

    print("\n=== 地板带 y %d-%d 的 x 段亮度统计（每 100px）===" % (y_lo, y_hi))
    for x0 in range(0, W, 100):
        seg = lum[y_lo:y_hi, x0:x0 + 100]
        print("  x %4d-%4d  mean=%6.1f std=%5.2f min=%5.1f max=%5.1f" % (
            x0, x0 + 100, seg.mean(), seg.std(), seg.min(), seg.max()))


if __name__ == "__main__":
    main()