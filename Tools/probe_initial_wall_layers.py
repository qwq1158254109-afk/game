# -*- coding: utf-8 -*-
"""逐行精量初版壳北墙的层次，判定"深木框 / 压边 / 墙面"的真实归属。

只读脚本。用法: python Tools/probe_initial_wall_layers.py
"""
from __future__ import annotations

import numpy as np
from PIL import Image

SPACE = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
REF = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png"


def profile(a, x0, x1, y0, y1, label):
    print(f"\n=== {label}  x{x0}..{x1}  y{y0}..{y1} ===")
    reg = a[y0:y1, x0:x1].astype(np.float32)
    prof = reg.mean(axis=1)
    lum = prof.mean(axis=1)
    for i in range(len(lum)):
        y = y0 + i
        # 只打印前 70 行与亮度跳变处，避免刷屏
        if i < 70 or (i > 0 and abs(lum[i] - lum[i - 1]) > 6):
            print(f"  y={y:4d}  RGB=({prof[i][0]:6.1f},{prof[i][1]:6.1f},{prof[i][2]:6.1f})  L={lum[i]:6.1f}")


def main() -> None:
    space = np.asarray(Image.open(SPACE).convert("RGB"))
    ref = np.asarray(Image.open(REF).convert("RGB"))

    profile(space, 200, 500, 28, 90, "初版壳 北墙最上层（判定框/压边）")
    print("\n--- 该段最亮/最暗的 8 行 ---")
    reg = space[28:90, 200:500].astype(np.float32)
    lum = reg.mean(axis=(1, 2))
    order = np.argsort(lum)
    for i in list(order[:4]) + list(order[-4:]):
        y = 28 + i
        r = reg[i].mean(axis=0)
        print(f"  y={y:4d}  L={lum[i]:6.1f}  RGB=({r[0]:6.1f},{r[1]:6.1f},{r[2]:6.1f})")

    print("\n=== 同位置 初版设计图(ref) ===")
    reg2 = ref[28:90, 200:500].astype(np.float32)
    lum2 = reg2.mean(axis=(1, 2))
    for i in range(0, len(lum2), 4):
        y = 28 + i
        r = reg2[i].mean(axis=0)
        print(f"  y={y:4d}  L={lum2[i]:6.1f}  RGB=({r[0]:6.1f},{r[1]:6.1f},{r[2]:6.1f})")

    print("\n=== 结论辅助：三段候选均值对比 ===")
    for label, y0, y1 in (("y28..36", 28, 36), ("y36..39", 36, 39),
                          ("y40..64", 40, 64), ("y64..68", 64, 68),
                          ("y68..266", 68, 266)):
        r = space[y0:y1, 200:500].astype(np.float32).reshape(-1, 3).mean(axis=0)
        print(f"  {label:12s} L={r.mean():6.1f}  RGB=({r[0]:6.1f},{r[1]:6.1f},{r[2]:6.1f})")


if __name__ == "__main__":
    main()