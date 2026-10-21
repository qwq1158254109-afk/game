"""细读源图结构：把「北墙竖向切片」逐行取样，标出压边/墙面/踢脚线/地板的真实边界。

同时把关键切片导出成图（Tools/_probe_*.png），便于肉眼核对。
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"


def rgb_row(a, y, x0=500, x1=950):
    seg = a[y, x0:x1]
    return seg.mean(axis=0)


def main():
    src = Image.open(SRC).convert("RGB")
    a = np.asarray(src).astype(np.float32)

    print("=== 北墙竖向逐行：y, 平均 RGB, 亮度 ===")
    print(" y     R     G     B    lum   备注")
    for y in range(30, 300):
        r, g, b = rgb_row(a, y)
        lum = (r + g + b) / 3
        note = ""
        if b > r + 8:
            note = "偏冷/带蓝"
        elif r > b + 40:
            note = "暖棕(可能木色)"
        elif lum > 140:
            note = "亮墙面"
        elif lum < 60:
            note = "暗(压边/阴影)"
        print("%4d %5.1f %5.1f %5.1f %6.1f   %s" % (y, r, g, b, lum, note))


if __name__ == "__main__":
    main()