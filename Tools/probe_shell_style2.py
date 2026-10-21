# -*- coding: utf-8 -*-
"""补量：学校护墙板的竖条周期 + 家源图北墙对照。

只读脚本。用法: python Tools/probe_shell_style2.py
"""
from __future__ import annotations

import numpy as np
from PIL import Image

SCHOOL_SHELL = r"E:/大创项目/Assets/Art/New2_0/School/Source/SchoolShell.png"
HOME_SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"


def hue(a, box, label):
    x0, y0, x1, y1 = box
    reg = a[y0:y1, x0:x1].astype(np.float32).reshape(-1, 3)
    m = reg.mean(axis=0)
    print(f"  {label:30s} mean=({m[0]:6.1f},{m[1]:6.1f},{m[2]:6.1f})  R-B={m[0]-m[2]:6.1f}")
    return m


def main() -> None:
    school = np.asarray(Image.open(SCHOOL_SHELL).convert("RGB"))
    home = np.asarray(Image.open(HOME_SRC).convert("RGB"))

    print("=== 学校护墙板竖条周期（y240..288 取一行亮度） ===")
    row = school[240:288, 400:1200].astype(np.float32).mean(axis=(0, 2))
    d = np.abs(np.diff(row))
    thr = d.mean() + d.std() * 0.8
    peaks = list(np.where(d > thr)[0])
    # 合并邻近峰
    merged = []
    for p in peaks:
        if not merged or p - merged[-1] > 4:
            merged.append(p)
    if len(merged) > 2:
        gaps = np.diff(merged)
        print(f"  检出边界 {len(merged)} 个  间距: 中位={int(np.median(gaps))}px "
              f"均值={gaps.mean():.1f}px  最小={gaps.min()} 最大={gaps.max()}")
        print(f"  前 12 个间距: {list(gaps[:12])}")
    else:
        print(f"  未检出（峰数={len(merged)}）")

    print("\n=== 学校墙面/护墙板/踢脚的精确分段（x620..1130 无窗净面） ===")
    hue(school, (620, 40, 1130, 56), "上框/压边顶部")
    hue(school, (620, 57, 1130, 226), "奶油墙面")
    hue(school, (620, 232, 1130, 288), "护墙板(竖条)")
    hue(school, (620, 289, 1130, 300), "踢脚/地面交界")

    print("\n=== 家源图北墙竖向剖面（x660..900）采样点 ===")
    reg = home[20:340, 660:900].astype(np.float32)
    prof = reg.mean(axis=1)
    lum = prof.mean(axis=1)
    step = 10
    for i in range(0, len(lum), step):
        y = 20 + i
        print(f"  y={y:4d}  RGB=({prof[i][0]:6.1f},{prof[i][1]:6.1f},{prof[i][2]:6.1f})"
              f"  L={lum[i]:6.1f}")

    print("\n=== 家源图北墙分段均值（用于确认没有护墙板） ===")
    hue(home, (660, 40, 900, 66), "家 压边 TRIM_Y")
    hue(home, (660, 68, 900, 263), "家 墙面 FACE_Y")
    hue(home, (660, 264, 900, 296), "家 踢脚 BASE_Y")


if __name__ == "__main__":
    main()