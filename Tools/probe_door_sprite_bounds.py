# -*- coding: utf-8 -*-
"""量 home_ref_exit_door.png 里"门体"的精确边界，供 make_rooms.py 的 DOOR_CROP 使用。

问题背景（2026-10-21 用户报"家中左右两扇门出现重叠"）：
    墙加厚到 2.5 单位后门洞是 160x160，但门素材 x0..21 是一道**奶油墙**、
    x133+ 是右侧窄墙条。旧 DOOR_CROP=(22,0,150,125) 裁完仍带 18px 墙条，
    放大到 160x160 后墙条变成 ~22px；侧墙门旋转 90° 后，这条墙色就横躺在
    房间地板上 —— 左右两扇门各多出一截，看起来就是"重叠"。

本脚本逐个像素找出行/列上的"墙色"占比，输出干净的门体窗口。

只读脚本。用法: python Tools/probe_door_sprite_bounds.py
"""
from __future__ import annotations

import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Props/home_ref_exit_door.png"

# 奶油墙的判据：亮度高且偏暖（实测墙 RGB≈(184,133,86) L≈134；门体 L≈50..75）
WALL_L_MIN = 118.0


def main() -> None:
    a = np.asarray(Image.open(SRC).convert("RGBA")).astype(np.float32)
    rgb, alpha = a[:, :, :3], a[:, :, 3]
    lum = rgb.mean(axis=2)
    h, w = lum.shape
    print(f"  素材 {w}x{h}")

    opaque = alpha > 8
    wall = (lum >= WALL_L_MIN) & opaque

    print("\n=== 逐列墙色占比（只列 >2% 的列） ===")
    colw = wall.mean(axis=0)
    runs = []
    inrun = False
    for x in range(w):
        if colw[x] > 0.02 and not inrun:
            inrun, start = True, x
        elif colw[x] <= 0.02 and inrun:
            inrun = False
            runs.append((start, x - 1, colw[start:x].max()))
    if inrun:
        runs.append((start, w - 1, colw[start:].max()))
    for s, e, mx in runs:
        print(f"    x {s:3d}..{e:3d}  峰值占比 {mx:.2f}")

    print("\n=== 逐行墙色占比（只列 >2% 的行） ===")
    roww = wall.mean(axis=1)
    runs = []
    inrun = False
    for y in range(h):
        if roww[y] > 0.02 and not inrun:
            inrun, start = True, y
        elif roww[y] <= 0.02 and inrun:
            inrun = False
            runs.append((start, y - 1, roww[start:y].max()))
    if inrun:
        runs.append((start, h - 1, roww[start:].max()))
    for s, e, mx in runs:
        print(f"    y {s:3d}..{e:3d}  峰值占比 {mx:.2f}")

    print("\n=== 候选裁剪窗口评估（裁完缩放到 160x160 后残留墙色） ===")
    cands = [
        ("旧 DOOR_CROP (22,0,150,125)", (22, 0, 150, 125)),
        ("门体紧贴 (22,0,133,125)", (22, 0, 133, 125)),
        ("门体+对称 (20,0,135,125)", (20, 0, 135, 125)),
        ("门体+少量对称 (18,0,136,125)", (18, 0, 136, 125)),
    ]
    for label, (x0, y0, x1, y1) in cands:
        sub = wall[y0:y1, x0:x1]
        frac = sub.mean()
        # 缩放到 160x160 后，占比不变（面积比例）
        print(f"    {label:32s} {x1-x0}x{y1-y0}  墙色占比 {frac*100:5.2f}%  "
              f"-> 160x160 上约 {frac*160*160:.0f} px")


if __name__ == "__main__":
    main()