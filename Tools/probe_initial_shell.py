# -*- coding: utf-8 -*-
"""量出「初版家房间壳」的真实分段与比例，作为重构依据。

依据文件（均为初版 900848a 引入、至今未改）：
  home_new_space.png    初版空房间壳（权威壳）
  home_new_reference.png 初版室内设计图（家具版，用于交叉验证墙色）

只读脚本。用法: python Tools/probe_initial_shell.py
"""
from __future__ import annotations

import numpy as np
from PIL import Image

SPACE = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
REF = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png"


def band_means(a, x0, x1, label, y0=0, y1=None):
    y1 = a.shape[0] if y1 is None else y1
    reg = a[y0:y1, x0:x1].astype(np.float32)
    prof = reg.mean(axis=1)
    lum = prof.mean(axis=1)
    print(f"\n=== {label}  x{x0}..{x1} y{y0}..{y1} ===")
    # 找出亮度跳变 >3 的行，作为分段边界候选
    d = np.abs(np.diff(lum))
    edges = [y0 + i + 1 for i in np.where(d > 3.0)[0]]
    merged = []
    for e in edges:
        if not merged or e - merged[-1] > 3:
            merged.append(e)
    print(f"  跳变边界候选（{len(merged)} 个）: {merged[:24]}")
    return prof, lum, merged


def seg(a, y0, y1, x0, x1, label):
    reg = a[y0:y1, x0:x1].astype(np.float32).reshape(-1, 3)
    m = reg.mean(axis=0)
    print(f"  {label:26s} y{y0:4d}..{y1:4d} (h={y1-y0:3d})  "
          f"mean=({m[0]:6.1f},{m[1]:6.1f},{m[2]:6.1f})  L={m.mean():6.1f}  R-B={m[0]-m[2]:6.1f}")
    return m


def main() -> None:
    space = np.asarray(Image.open(SPACE).convert("RGB"))
    ref = np.asarray(Image.open(REF).convert("RGB"))
    H, W, _ = space.shape
    print(f"初版壳 {W}x{H}")

    # 北墙净面（避开中间的窗 x~560..660 与立柱）
    prof, lum, edges = band_means(space, 200, 500, "初版壳 北墙净面竖向剖面", 20, 340)

    print("\n=== 初版壳 北墙逐行亮度（每 6 行） ===")
    for i in range(0, len(lum), 6):
        y = 20 + i
        r = prof[i]
        print(f"  y={y:4d}  RGB=({r[0]:6.1f},{r[1]:6.1f},{r[2]:6.1f})  L={lum[i]:6.1f}")

    print("\n=== 初版壳：北墙分段（按测得的边界猜分段，稍后据实修正） ===")
    # 依据上方剖面人工确认后再改这几个数；这里先给一组待验证的切分
    for label, y0, y1 in (("上沿/顶框", 20, 60), ("墙面", 60, 168),
                          ("踢脚/压边", 168, 200), ("地板起始", 200, 240)):
        seg(space, y0, y1, 200, 500, label)

    print("\n=== 交叉验证：初版设计图(ref) 同位置墙色 ===")
    for label, y0, y1 in (("上沿/顶框", 20, 60), ("墙面", 60, 168),
                          ("踢脚/压边", 168, 200)):
        seg(ref, y0, y1, 200, 500, label)

    print("\n=== 地板：初版壳中段一行，取木纹周期 ===")
    row_y = 600
    row = space[row_y:row_y + 40, 200:900].astype(np.float32).mean(axis=(0, 2))
    d = np.abs(np.diff(row))
    print(f"  行 y={row_y} 亮度均值={row.mean():.1f} 相邻跳变均值={d.mean():.2f} "
          f"最大={d.max():.1f}")


if __name__ == "__main__":
    main()