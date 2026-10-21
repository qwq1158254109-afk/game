"""逐列重测北墙墙面：找出真正无窗、无立柱的列区间。

上一版把 x 250..700 当净面是错的——那里面就有一扇窗（实测约 x 275..410）。
本脚本按「该列在墙面 y 区间内是否出现冷色/暗色异常」逐列判定。
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
FACE_Y = (68, 260)


def main():
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    y0, y1 = FACE_Y
    reg = a[y0:y1]

    lum = reg.mean(axis=2)
    col_lum = lum.mean(axis=0)
    col_std = lum.std(axis=0)
    cold = (reg[:, :, 2] > reg[:, :, 0] + 5).mean(axis=0)      # 偏冷 = 玻璃
    dark = (lum < 90).mean(axis=0)                              # 偏暗 = 窗框/立柱

    print("=== 逐列判定（每列是否可作墙面材质）===")
    print(" x     亮度  标准差  偏冷率  偏暗率  判定")
    bad = (cold > 0.02) | (dark > 0.02) | (col_std > 6.0)
    runs, start = [], None
    for x in range(a.shape[1]):
        if not bad[x] and start is None:
            start = x
        elif bad[x] and start is not None:
            runs.append((start, x, x - start))
            start = None
    if start is not None:
        runs.append((start, a.shape[1], a.shape[1] - start))

    for x in range(0, a.shape[1], 20):
        c = col_lum[x:x + 20].mean()
        s = col_std[x:x + 20].mean()
        cl = cold[x:x + 20].mean()
        dk = dark[x:x + 20].mean()
        flag = "OK" if not bad[x:x + 20].any() else "--"
        print("%4d  %6.1f %6.2f  %5.3f  %5.3f  %s" % (x, c, s, cl, dk, flag))

    print("\n=== 可用的连续干净列区间（宽 >= 60）===")
    for s, e, ln in sorted(runs, key=lambda r: -r[2])[:10]:
        if ln < 60:
            continue
        seg = lum[:, s:e]
        print("   x %4d..%4d  宽 %4d   亮度 mean=%6.1f std=%5.2f" % (
            s, e, ln, seg.mean(), seg.std()))


if __name__ == "__main__":
    main()