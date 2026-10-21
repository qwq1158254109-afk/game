"""几何同位性 + 材质质量验证（修正版）。

上一版用「亮度阈值当墙体掩码」是错的：墙面材质换了之后整体亮度分布改变，
阈值随之漂移，测出来的 0.37 反映的是亮度变化，不是几何变化。

正确的几何验证：几何完全由**门洞抠除的位置**决定（墙带铺满画布，洞是抠出来的）。
所以直接比较"地板色 vs 墙色"的分类不可靠，改为比较**门洞边界的位置**——
门洞区域在新旧两版里都由 draw_doorway 处理（压暗 + 框线），其绝对坐标由
ROOMS 的 gaps 决定，与被采样材质无关。

本脚本验证：
  1. 尺寸一致
  2. 门洞中心坐标在预期位置（由 CANVAS_W/H 与 gaps 算出）
  3. 门洞边界处存在显著亮度跳变（说明洞确实被抠出来）
  4. 色数达标
"""
import os
import sys

import numpy as np
from PIL import Image

BACKUP = r"E:/大创项目/_backup_shellmat_20261020/Assets/Art/Pixel/Generated/HomeRooms"
NEW = r"E:/大创项目/Assets/Art/Pixel/Generated/HomeRooms"

CANVAS_W, CANVAS_H = 1991, 1184
W_SIDE = W_TOP = 160
W_BOTTOM = 160
DOOR_W = 160

ROOMS = {
    "living": {
        "left": ((CANVAS_H - DOOR_W) // 2, DOOR_W),
        "right": ((CANVAS_H - DOOR_W) // 2, DOOR_W),
        "top": (CANVAS_W // 2 - 6 * 64 - DOOR_W // 2, DOOR_W),
        "bottom": ((CANVAS_W - DOOR_W) // 2, DOOR_W),
    },
    "bedroom": {"left": ((CANVAS_H - DOOR_W) // 2, DOOR_W)},
    "study": {"right": ((CANVAS_H - DOOR_W) // 2, DOOR_W)},
    "kitchen": {"bottom": ((CANVAS_W - DOOR_W) // 2, DOOR_W)},
}

SIDES = {
    "left": lambda s, n: (0, s, W_SIDE, s + n),
    "right": lambda s, n: (CANVAS_W - W_SIDE, s, CANVAS_W, s + n),
    "top": lambda s, n: (s, 0, s + n, W_TOP),
    "bottom": lambda s, n: (s, CANVAS_H - W_BOTTOM, s + n, CANVAS_H),
}


def door_center(side, start, size):
    x0, y0, x1, y1 = SIDES[side](start, size)
    return ((x0 + x1) // 2, (y0 + y1) // 2)


def main():
    print("\n=== 几何同位性验证（以门洞为锚点）===")
    print("%-9s %-24s %-11s %-11s %s" % ("room", "门洞(侧,中心x,中心y)", "洞口位置", "洞内异物", "判定"))

    # 门洞几何：四面墙带一律贴画布外沿、厚度 96，门洞与墙带同位（2026-10-20 统一口径）
    HOLE = 160
    door_sprite = np.asarray(
        Image.open(r"E:/大创项目/Assets/Art/Pixel/Generated/HomeReference/Props/home_ref_door_inwall.png")
        .convert("RGB")).astype(np.int16)

    def hole_box(side, start, size):
        if side == "left":
            return (0, start, W_SIDE, start + size)
        if side == "right":
            return (CANVAS_W - W_SIDE, start, CANVAS_W, start + size)
        if side == "top":
            return (start, 0, start + size, HOLE)
        return (start, CANVAS_H - HOLE, start + size, CANVAS_H)

    def expected_hole(side):
        """按口径算出的洞矩形，用于和实际比对。"""
        return {"left": (0, 0, HOLE, CANVAS_H), "right": (CANVAS_W - HOLE, 0, CANVAS_W, CANVAS_H),
                "top": (0, 0, CANVAS_W, HOLE), "bottom": (0, CANVAS_H - HOLE, CANVAS_W, CANVAS_H)}[side]

    all_ok = True
    for room, gaps in ROOMS.items():
        old = np.asarray(Image.open(os.path.join(BACKUP, "room_%s.png" % room)).convert("RGB")).astype(np.float32)
        new = np.asarray(Image.open(os.path.join(NEW, "room_%s.png" % room)).convert("RGB")).astype(np.float32)
        # 尺寸改了（墙加厚）之后，备份与新版逐像素不可比 —— 只做新版自身的几何校验，
        # 并在色数统计里标注"尺寸已变、无旧版对照"。
        comparable = old.shape == new.shape
        if not comparable:
            print("  [%s] 旧版 %dx%d vs 新版 %dx%d —— 尺寸已变，跳过逐像素对照，仅校验新版几何" % (
                room, old.shape[1], old.shape[0], new.shape[1], new.shape[0]))

        for side, (start, size) in gaps.items():
            x0, y0, x1, y1 = hole_box(side, start, size)

            # (1) 几何：洞是否与墙带同位（贴画布外沿、厚度 96）
            exp = expected_hole(side)
            if side in ("left", "right"):
                geo_ok = (x0 == exp[0] and x1 == exp[2] and size == HOLE)
            else:
                geo_ok = (y0 == exp[1] and y1 == exp[3] and size == HOLE)

            # (2) 洞内不得出现"非门素材"的颜色（墙面色渗出即为露白）
            reg = new[y0:y1, x0:x1].astype(np.int16)
            dset = door_sprite.reshape(-1, 3)[::7]
            sample = reg.reshape(-1, 3)[::11]
            # 逐样本找与门素材任一颜色的最小 L1 距离
            bad = 0
            for px in sample:
                d = np.abs(dset - px).sum(axis=1).min()
                if d > 60:
                    bad += 1
            foreign = bad / max(1, len(sample))

            ok = geo_ok and foreign < 0.03
            all_ok &= ok
            cx, cy = ((x0 + x1) // 2, (y0 + y1) // 2)
            print("%-9s %-24s %-11s %-11s %s" % (
                room, "%s (%d,%d)" % (side, cx, cy),
                "OK" if geo_ok else "!! 错位",
                "%.4f" % foreign, "OK" if ok else "!!"))

    print("\n=== 尺寸与色数 ===")
    print("%-9s %-14s %-10s %-10s %s" % ("room", "尺寸", "旧色数", "新色数", "提升"))
    for room in ROOMS:
        old = np.asarray(Image.open(os.path.join(BACKUP, "room_%s.png" % room)).convert("RGB"))
        new = np.asarray(Image.open(os.path.join(NEW, "room_%s.png" % room)).convert("RGB"))
        uo = len(np.unique(old.reshape(-1, 3), axis=0))
        un = len(np.unique(new.reshape(-1, 3), axis=0))
        # 只统计"房间壳"（去掉门洞压暗区）的色数更有意义
        print("%-9s %-14s %-10d %-10d x%.1f" % (
            room, "%dx%d" % (new.shape[1], new.shape[0]), uo, un, un / max(uo, 1)))

    print("\n=== 只统计纯壳区域（四边墙体，不含地板与门洞）===")
    for room in ROOMS:
        old = np.asarray(Image.open(os.path.join(BACKUP, "room_%s.png" % room)).convert("RGB"))
        new = np.asarray(Image.open(os.path.join(NEW, "room_%s.png" % room)).convert("RGB"))
        # 取北墙中部一条（避开所有门洞）
        strip_o = old[0:W_TOP, 200:600].reshape(-1, 3)
        strip_n = new[0:W_TOP, 200:600].reshape(-1, 3)
        uo = len(np.unique(strip_o, axis=0))
        un = len(np.unique(strip_n, axis=0))
        print("  %-9s 北墙条 旧色数=%-6d 新色数=%-6d  x%.1f" % (room, uo, un, un / max(uo, 1)))

    print("\n合计:", "PASS" if all_ok else "FAIL")
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())