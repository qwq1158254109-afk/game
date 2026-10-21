# -*- coding: utf-8 -*-
"""用截图里的纯地板小块做模板匹配，判定"地板到底出自哪个 git 壳"。

方法：截图与壳图都是同尺度像素画，取截图一块无家具地板，
在各壳的地板区域按其原始尺度滑动搜索，取平均绝对差(MAD)最小者。
（不做缩放，避免重采样影响判断。）

只读脚本。用法: python Tools/probe_floor_provenance.py
"""
from __future__ import annotations

import glob

import numpy as np
from PIL import Image

SHOT = r"E:/大创项目/Tools/_user_room_shot.png"

SHELLS = sorted(glob.glob(r"E:/大创项目/Assets/Art/New2_0/*/Source/*Shell.png")) + [
    r"E:/大创项目/Assets/Art/New2_0/Library/LibraryComposite.png",
    r"E:/大创项目/Assets/Art/New2_0/Park/Source/ParkBase.png",
    r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png",
    r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png",
]


def best_match(shell: np.ndarray, patch: np.ndarray, stride: int = 6, yfrac=(0.45, 1.0)):
    """在壳图下半部滑动搜索 patch，返回 (MAD, y, x)。"""
    ph, pw, _ = patch.shape
    H, W, _ = shell.shape
    y0, y1 = int(H * yfrac[0]), H - ph
    best = (1e9, -1, -1)
    for y in range(y0, max(y0 + 1, y1), stride):
        for x in range(0, max(1, W - pw), stride):
            reg = shell[y:y + ph, x:x + pw]
            mad = np.abs(reg - patch).mean()
            if mad < best[0]:
                best = (mad, y, x)
    return best


def main() -> None:
    shot = np.asarray(Image.open(SHOT).convert("RGB")).astype(np.float32)
    # 截图里一块确定无家具的地板：左侧中段（避开课桌与玩家）
    py, px, s = 620, 130, 48
    patch = shot[py:py + s, px:px + s]
    print(f"截图 {shot.shape[1]}x{shot.shape[0]}；取模板 左上=({px},{py}) 尺寸={s}x{s}")
    print(f"模板均值 RGB=({patch.reshape(-1,3).mean(axis=0)[0]:.1f},"
          f"{patch.reshape(-1,3).mean(axis=0)[1]:.1f},"
          f"{patch.reshape(-1,3).mean(axis=0)[2]:.1f})")
    print()

    results = []
    for path in SHELLS:
        name = path.split("New2_0/")[-1] if "New2_0" in path else path.split("/")[-1]
        try:
            shell = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
        except Exception as exc:  # noqa: BLE001
            print(f"  {name:44s} 读取失败 {exc}")
            continue
        mad, y, x = best_match(shell, patch)
        results.append((mad, name, y, x, shell.shape))
        print(f"  {name:44s} MAD={mad:6.2f}  最佳位置=({x},{y})  壳尺寸={shell.shape[1]}x{shell.shape[0]}")

    results.sort()
    print("\n=== 排名（MAD 越小越可能是地板来源） ===")
    for i, (mad, name, y, x, shp) in enumerate(results[:6], 1):
        print(f"  {i}. MAD={mad:6.2f}  {name}")


if __name__ == "__main__":
    main()