# -*- coding: utf-8 -*-
"""在 SchoolShell 上多点取样，得到可信的"墙 / 护墙板 / 地板"目标色。

上一版 probe 在 y235..285、x620..1130 取样时把**窗帘**算进去了（R-B 1.6、偏绿），
所以这里改为多点、并打印每点的 R-B 与绿色偏移用于剔除异常区。

只读脚本。用法: python Tools/probe_school_shell_colors.py
"""
from __future__ import annotations

import numpy as np
from PIL import Image

SCHOOL = r"E:/大创项目/Assets/Art/New2_0/School/Source/SchoolShell.png"


def stat(a, box, label):
    x0, y0, x1, y1 = box
    reg = a[y0:y1, x0:x1].reshape(-1, 3).astype(np.float32)
    m = reg.mean(axis=0)
    # 绿色偏移：G 明显高于 R 与 B 的均值 -> 疑似植被/窗帘
    greenish = (reg[:, 1] > (reg[:, 0] + reg[:, 2]) / 2 + 8).mean()
    print(f"  {label:34s} y{y0:4d}..{y1:<4d} x{x0:4d}..{x1:<4d} "
          f"RGB=({m[0]:6.1f},{m[1]:6.1f},{m[2]:6.1f}) L={m.mean():6.1f} "
          f"R-B={m[0]-m[2]:6.1f} 偏绿率={greenish:.3f}")


def main() -> None:
    a = np.asarray(Image.open(SCHOOL).convert("RGB"))
    print(f"SchoolShell {a.shape[1]}x{a.shape[0]}")

    print("\n=== 墙面上段（应是大片奶油色，多点验证） ===")
    for i, (x0, x1) in enumerate([(560, 700), (700, 860), (860, 1010), (1010, 1160)]):
        stat(a, (x0, 80, x1, 200), f"墙面 段{i+1} x{x0}..{x1}")

    print("\n=== 墙下段（护墙板带：逐 10 行扫，定位真正边界） ===")
    for y in range(210, 320, 10):
        stat(a, (700, y, 1160, y + 10), f"y{y}..{y+10}")

    print("\n=== 地板（多点，避开课桌/阳光斑） ===")
    for i, box in enumerate([(300, 500, 700, 640), (760, 700, 1160, 840),
                             (300, 820, 700, 960), (760, 860, 1160, 990)]):
        stat(a, box, f"地板 块{i+1}")

    print("\n=== 参考：已知干净区（先前小片模板匹配命中处 y736 x1362） ===")
    stat(a, (1362, 736, 1410, 784), "命中点邻域 48x48")

    print("\n=== 学生桌面色（用于对照，非目标） ===")
    stat(a, (330, 415, 420, 450), "课桌面")


if __name__ == "__main__":
    main()