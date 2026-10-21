"""量化源图的阴影：墙脚接触阴影（AO）、墙面内沿自阴影。

源图 y 结构（x 250..450 净面，实测）：
    0..36    画面外/墙外沿
    36..38   外沿暗边
    40..65   压边
    68..263  墙面
    264..295 踢脚线
    298+     地板（墙脚另有一层接触阴影）
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
X0, X1 = 520, 880


def main():
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)

    print("=== 1. 墙脚接触阴影：北墙下沿（地板 y 298..380）===")
    for y in range(298, 384, 4):
        m = a[y:y + 4, X0:X1].mean(axis=(0, 1))
        # 以 y=380 附近为"无阴影基准"（继续往下就是正常地板）
        print("  y=%3d..%3d  lum=%6.1f  RGB(%5.1f,%5.1f,%5.1f)" % (
            y, y + 4, m.mean(), m[0], m[1], m[2]))

    base = a[376:400, X0:X1].mean()
    print("\n  基准（y376..400）= %.1f" % base)

    print("\n=== 2. 南墙脚接触阴影（地板底端 y 940..1010）===")
    for y in range(930, 1012, 4):
        m = a[y:y + 4, X0:X1].mean()
        print("  y=%3d..%3d  lum=%6.1f" % (y, y + 4, m))

    print("\n=== 3. 侧墙脚：横向剖面（地板带 y 600..760，x 100..400 靠左墙）===")
    for x in range(96, 420, 20):
        m = a[600:760, x:x + 20].mean()
        print("  x=%3d..%3d  lum=%6.1f" % (x, x + 20, m))

    print("\n=== 4. 墙面内沿自阴影：墙面靠上/靠下（y 68..263）===")
    for y in range(68, 264, 12):
        m = a[y:y + 12, 660:900].mean()
        print("  y=%3d..%3d  lum=%6.1f" % (y, y + 12, m))


if __name__ == "__main__":
    main()