# -*- coding: utf-8 -*-
"""把用户在对话里贴的室内截图与候选"壳"素材做像素比对，确定它到底是哪一份。

候选壳：
  Assets/Art/New2_0/School/Source/SchoolShell.png       学校壳 1536x1024
  Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png  初版家壳 1448x1086
  Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png 初版家设计图

只读脚本。用法: python Tools/probe_screenshot_source.py <截图路径>
"""
from __future__ import annotations

import sys

import numpy as np
from PIL import Image

SHOT = sys.argv[1] if len(sys.argv) > 1 else None
CANDS = {
    "SchoolShell": r"E:/大创项目/Assets/Art/New2_0/School/Source/SchoolShell.png",
    "home_new_space": r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png",
    "home_new_reference": r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png",
}


def blocks(a: np.ndarray, n: int = 12) -> np.ndarray:
    """把图缩到 n x n 的颜色指纹（抗缩放/抗裁剪差异）。"""
    im = Image.fromarray(a.astype(np.uint8)).resize((n, n), Image.BOX)
    return np.asarray(im).astype(np.float32).reshape(-1, 3)


def main() -> None:
    if not SHOT:
        print("用法: python Tools/probe_screenshot_source.py <截图路径>")
        return

    shot = np.asarray(Image.open(SHOT).convert("RGB"))
    print(f"截图 {shot.shape[1]}x{shot.shape[0]}")
    shot_fp = blocks(shot)

    print("\n=== 整图指纹距离（越小越像；但截图含 HUD/家具，仅作粗筛） ===")
    for name, path in CANDS.items():
        try:
            cand = np.asarray(Image.open(path).convert("RGB"))
        except Exception as exc:  # noqa: BLE001
            print(f"  {name}: 读取失败 {exc}")
            continue
        fp = blocks(cand)
        d = np.abs(shot_fp - fp).mean()
        print(f"  {name:22s} 平均通道差={d:6.1f}  ({cand.shape[1]}x{cand.shape[0]})")

    # 更针对性的判据：截图里取几块"墙"与"地板"区域的平均色，
    # 与候选壳的对应区域比 —— 布局不同会失准，所以同时打印出来供人工判断
    h, w, _ = shot.shape
    print("\n=== 截图分区平均色（人工核对用） ===")
    zones = {
        "顶部墙带(y5..75)": shot[5:75, int(w * 0.35):int(w * 0.60)],
        "左墙带(x0..110)": shot[100:600, 0:110],
        "地板中部": shot[int(h * 0.45):int(h * 0.65), int(w * 0.3):int(w * 0.7)],
        "护墙板带(y95..130)": shot[95:130, int(w * 0.35):int(w * 0.60)] if h > 130 else shot[:1],
    }
    for k, v in zones.items():
        if v.size == 0:
            continue
        m = v.reshape(-1, 3).mean(axis=0)
        print(f"  {k:22s} RGB=({m[0]:6.1f},{m[1]:6.1f},{m[2]:6.1f})  L={m.mean():6.1f}  R-B={m[0]-m[2]:6.1f}")

    print("\n=== 候选壳的墙面/地板平均色 ===")
    probes = {
        "SchoolShell": {"墙(奶油)": (620, 70, 1130, 220), "护墙板": (620, 235, 1130, 285),
                        "地板": (400, 500, 1100, 800)},
        "home_new_space": {"墙(奶油)": (200, 70, 500, 250), "护墙板": None,
                           "地板": (520, 450, 880, 560)},
        "home_new_reference": {"墙(奶油)": (200, 70, 500, 250), "护墙板": None,
                               "地板": (520, 450, 880, 560)},
    }
    for name, path in CANDS.items():
        try:
            cand = np.asarray(Image.open(path).convert("RGB"))
        except Exception:
            continue
        print(f"  --- {name} ---")
        for label, box in probes[name].items():
            if box is None:
                print(f"      {label:10s} （无此结构）")
                continue
            x0, y0, x1, y1 = box
            x1 = min(x1, cand.shape[1]); y1 = min(y1, cand.shape[0])
            m = cand[y0:y1, x0:x1].reshape(-1, 3).mean(axis=0)
            print(f"      {label:10s} RGB=({m[0]:6.1f},{m[1]:6.1f},{m[2]:6.1f})  L={m.mean():6.1f}  R-B={m[0]-m[2]:6.1f}")


if __name__ == "__main__":
    main()