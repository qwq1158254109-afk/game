"""诊断 home_new_space.png 的区域结构：找出墙面 / 墙顶压边 / 踢脚线 / 地板的像素边界。

输出 ASCII 色块图（8px 一格，按亮度分档）+ 逐行亮度剖面，用来确定各材质的可平铺矩形。
只读，不写任何资产。
"""
import sys

import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
CELL = 8

RAMP = " .:-=+*#%@"


def ascii_map(a, cell=CELL):
    h, w, _ = a.shape
    print("--- ASCII 亮度图（%dpx/格，' '=最暗 '@'=最亮）---" % cell)
    header = "     " + "".join(str((x // cell) % 10) for x in range(0, w, cell))
    print(header)
    for y in range(0, h, cell):
        block = a[y:y + cell]
        row = []
        for x in range(0, w, cell):
            lum = block[:, x:x + cell].mean()
            idx = min(len(RAMP) - 1, int(lum / 256.0 * len(RAMP)))
            row.append(RAMP[idx])
        print("%4d " % y + "".join(row))


def profile(a, axis, label, step=8):
    lum = a.mean(axis=2)
    print("\n--- %s 亮度剖面（步长 %d）---" % (label, step))
    if axis == "row":
        for y in range(0, a.shape[0], step):
            line = lum[y]
            print("y=%-5d mean=%6.1f  min=%5.1f max=%5.1f  | 中心段=%6.1f" % (
                y, line.mean(), line.min(), line.max(), line[500:950].mean()))
    else:
        for x in range(0, a.shape[1], step):
            line = lum[:, x]
            print("x=%-5d mean=%6.1f  min=%5.1f max=%5.1f" % (x, line.mean(), line.min(), line.max()))


def main():
    src = Image.open(SRC).convert("RGB")
    a = np.asarray(src).astype(np.float32)
    print("source: %dx%d" % (src.size[0], src.size[1]))

    mode = sys.argv[1] if len(sys.argv) > 1 else "map"
    if mode == "map":
        ascii_map(a)
    elif mode == "rows":
        profile(a, "row", "逐行")
    elif mode == "cols":
        profile(a, "col", "逐列")
    elif mode == "crop":
        x0, y0, x1, y1 = (int(v) for v in sys.argv[2:6])
        out = sys.argv[6] if len(sys.argv) > 6 else "_probe_crop.png"
        src.crop((x0, y0, x1, y1)).save(out)
        print("saved", out, (x1 - x0, y1 - y0))


if __name__ == "__main__":
    main()