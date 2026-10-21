# -*- coding: utf-8 -*-
"""量化对比「学校壳」与「家庭壳」的墙面结构，给改造提供实测依据。

只读脚本，不改任何资产。
用法: python Tools/probe_shell_style.py
"""
from __future__ import annotations

import numpy as np
from PIL import Image

SCHOOL_SHELL = r"E:/大创项目/Assets/Art/New2_0/School/Source/SchoolShell.png"
HOME_SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
HOME_BAND = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeRooms/Materials/wall_band_h.png"


def col_profile(a: np.ndarray, x0: int, x1: int, y0: int, y1: int, label: str):
    """打印某段墙面区域的逐行平均色 + 行间跳变，用来定位分段边界。"""
    print(f"\n=== {label}  x{x0}..{x1} ===")
    reg = a[y0:y1, x0:x1].astype(np.float32)
    prof = reg.mean(axis=1)                       # (h, 3)
    lum = prof.mean(axis=1)
    prev = None
    for i, (row, l) in enumerate(zip(prof, lum)):
        y = y0 + i
        jump = "" if prev is None else f"  Δ={l - prev:+6.1f}"
        if prev is None or abs(l - prev) > 4.0 or i % 8 == 0:
            print(f"  y={y:4d}  RGB=({row[0]:6.1f},{row[1]:6.1f},{row[2]:6.1f})"
                  f"  L={l:6.1f}{jump}")
        prev = l


def hue_stats(a: np.ndarray, box: tuple[int, int, int, int], label: str):
    x0, y0, x1, y1 = box
    reg = a[y0:y1, x0:x1].astype(np.float32).reshape(-1, 3)
    m = reg.mean(axis=0)
    print(f"  {label:24s} mean=({m[0]:5.1f},{m[1]:5.1f},{m[2]:5.1f})  R-B={m[0]-m[2]:6.1f}")
    return m


def main() -> None:
    school = np.asarray(Image.open(SCHOOL_SHELL).convert("RGB"))
    home = np.asarray(Image.open(HOME_SRC).convert("RGB"))
    band = np.asarray(Image.open(HOME_BAND).convert("RGB"))

    print(f"学校壳 {school.shape[1]}x{school.shape[0]}")
    print(f"家源图 {home.shape[1]}x{home.shape[0]}")
    print(f"家墙带 {band.shape[1]}x{band.shape[0]}")

    # 学校后墙：取一段无窗无立柱的净面（避开 y<40 的框、x<200 的窗）
    col_profile(school, 620, 1130, 20, 300, "学校后墙竖向剖面（找护墙板边界）")

    print("\n=== 学校后墙各段均值 ===")
    hue_stats(school, (620, 40, 1130, 220), "后墙 上部（墙面）")
    hue_stats(school, (620, 225, 1130, 285), "后墙 下部（疑似护墙板）")
    hue_stats(school, (620, 45, 1130, 60), "后墙上沿（木框）")

    print("\n=== 家源图北墙各段均值（对应现有 wall_band_h 的取源） ===")
    col_profile(home, 660, 900, 30, 300, "家源图北墙竖向剖面")
    hue_stats(home, (660, 68, 900, 263), "家 墙面取源 FACE_Y")
    hue_stats(home, (660, 264, 900, 296), "家 踢脚取源 BASE_Y")

    print("\n=== 现有家墙带分段（EDGE4 / TRIM13 / FACE87 / BASE24） ===")
    for name, y0, y1 in (("edge外沿", 0, 4), ("trim压边", 4, 17),
                         ("face墙面", 17, 104), ("base踢脚", 104, 128)):
        hue_stats(band, (0, y0, 240, y1), f"  {name} y{y0}..{y1}")

    # 竖向护墙板的水平周期：在护墙板段取一行，看亮度自相关/跳变
    print("\n=== 学校护墙板的水平节奏（找竖条周期） ===")
    row = school[250:270, 620:1130].astype(np.float32).mean(axis=(0, 2))
    d = np.abs(np.diff(row))
    peaks = np.where(d > d.mean() + d.std())[0]
    if len(peaks) > 1:
        gaps = np.diff(peaks)
        gaps = gaps[gaps > 3]
        if len(gaps):
            print(f"  疑似竖条边界 {len(peaks)} 个，间距中位数={int(np.median(gaps))}px"
                  f"  均值={gaps.mean():.1f}px")
        else:
            print("  未找到明显的周期性竖条（可能对比不足）")
    else:
        print("  该行未检出竖条边界")


if __name__ == "__main__":
    main()