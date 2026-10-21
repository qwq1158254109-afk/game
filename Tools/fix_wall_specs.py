# -*- coding: utf-8 -*-
"""修正 HomeRoomsBuilder.cs 里加厚时算错的 WallSpec（门洞被堵 / 洞口过宽）。

2026-10-21 事故：
    `Tools/apply_room_thickness.py` 把侧墙两段写成 cy=±4.0 / h=8.0，
    合起来 y[-8,8] —— **完全没有门洞**，门被墙堵死（用户报"走到门上没反应"）。
    北墙洞口也被算成 4.5 宽（应为 2.5）。

正确几何（墙厚 2.5，画布中心为原点）：
    外沿 x±15.555 / y±9.25；内沿 x±13.055 / y −6.75..6.75
    墙中心 x±14.305 / y±8.0；门洞净宽 2.5（= 底图 160px）

用法: python Tools/fix_wall_specs.py            # 预演
      python Tools/fix_wall_specs.py --write    # 写入
"""
from __future__ import annotations

import sys

PATH = "Assets/Editor/StudentGrowth/HomeRoomsBuilder.cs"

W = 2.5
WALL_CX = 14.305
WALL_CY = 8.0
INNER_T, INNER_B = 6.75, -6.75
DH = 1.25          # 门洞半宽

# 侧墙带门：下段 y[-6.75,-1.25]、上段 y[1.25,6.75]
SIDE_LO_CY = (-DH + INNER_B) / 2          # -4.0
SIDE_LO_H = (-DH) - INNER_B               # 5.5
SIDE_HI_CY = (INNER_T + DH) / 2           # 4.0
SIDE_HI_H = INNER_T - DH                  # 5.5

# 南墙带门（中心 x=0）
S_LEFT_CX = (-DH + (-WALL_CX)) / 2
S_LEFT_W = (-DH) - (-WALL_CX)
S_RIGHT_CX = (WALL_CX + DH) / 2
S_RIGHT_W = WALL_CX - DH

# 北墙带门（中心 x=-6）
N_LEFT_CX = (-6 - DH + (-WALL_CX)) / 2
N_LEFT_W = (-6 - DH) - (-WALL_CX)
N_RIGHT_CX = (WALL_CX + (-6 + DH)) / 2
N_RIGHT_W = WALL_CX - (-6 + DH)

# 整幅墙（无门）
FULL_CY = WALL_CY
FULL_SIDE_W = WALL_CX * 2      # 31.11（= RoomW）


def f(v: float) -> str:
    """C# 浮点字面量（保留必要精度、去掉多余 0）。"""
    s = f"{v:.4f}".rstrip("0").rstrip(".")
    if "." not in s:
        s += "f"
    else:
        s += "f"
    return s


REPLACEMENTS: list[tuple[str, str, int]] = [
    # ── 带门侧墙（客厅西/东、卧室西、书房东、厨房西/东的带门段）──
    # 现错误值：cy=±4.0 h=8.0（合起来无洞）
    ("new WallSpec(-14.305f, -4.0f, 2.5f, 8.0f),",
     f"new WallSpec(-14.305f, {f(SIDE_LO_CY)}, 2.5f, {f(SIDE_LO_H)}),", 2),
    ("new WallSpec(-14.305f, 4.0f, 2.5f, 8.0f),",
     f"new WallSpec(-14.305f, {f(SIDE_HI_CY)}, 2.5f, {f(SIDE_HI_H)}),", 2),
    ("new WallSpec(14.305f, -4.0f, 2.5f, 8.0f),",
     f"new WallSpec(14.305f, {f(SIDE_LO_CY)}, 2.5f, {f(SIDE_LO_H)}),", 2),
    ("new WallSpec(14.305f, 4.0f, 2.5f, 8.0f),",
     f"new WallSpec(14.305f, {f(SIDE_HI_CY)}, 2.5f, {f(SIDE_HI_H)}),", 2),

    # ── 南墙带门（客厅 + 厨房共用，门洞中心 x=0）──
    ("new WallSpec(-8.4025f, -8.0f, 14.305f, 2.5f),",
     f"new WallSpec({f(S_LEFT_CX)}, -8f, {f(S_LEFT_W)}, 2.5f),", 2),
    ("new WallSpec(9.4025f, -8.0f, 12.305f, 2.5f),",
     f"new WallSpec({f(S_RIGHT_CX)}, -8f, {f(S_RIGHT_W)}, 2.5f),", 2),

    # ── 北墙带门（客厅厨房门，门洞中心 x=-6）──
    ("new WallSpec(-11.4025f, 8.0f, 8.305f, 2.5f),",
     f"new WallSpec({f(N_LEFT_CX)}, 8f, {f(N_LEFT_W)}, 2.5f),", 1),
    ("new WallSpec(6.4025f, 8.0f, 18.305f, 2.5f),",
     f"new WallSpec({f(N_RIGHT_CX)}, 8f, {f(N_RIGHT_W)}, 2.5f),", 1),
]


def main() -> None:
    write = "--write" in sys.argv
    src = open(PATH, encoding="utf-8").read()
    out = src
    total = 0
    problems = []

    for old, new, expect in REPLACEMENTS:
        actual = out.count(old)
        if actual != expect:
            problems.append(f"命中数不符: 期望 {expect} 实际 {actual}  [{old[:60]}]")
            continue
        out = out.replace(old, new)
        total += actual

    if problems:
        print("=== 中止：未按预期命中，未写入 ===")
        for p in problems:
            print("  " + p)
        return

    print(f"=== 共替换 {total} 处 ===")
    print(f"  侧墙带门: cy={SIDE_LO_CY:+.3f}/h={SIDE_LO_H} 与 cy={SIDE_HI_CY:+.3f}/h={SIDE_HI_H}")
    print(f"  南墙: 左 cx={S_LEFT_CX:.4f} w={S_LEFT_W} / 右 cx={S_RIGHT_CX:.4f} w={S_RIGHT_W}")
    print(f"  北墙: 左 cx={N_LEFT_CX:.4f} w={N_LEFT_W} / 右 cx={N_RIGHT_CX:.4f} w={N_RIGHT_W}")

    if write and out != src:
        open(PATH, "w", encoding="utf-8", newline="\n").write(out)
        print(f"  已写入 {PATH}")
    elif not write:
        print("  （预演，未写入；加 --write 落盘）")


if __name__ == "__main__":
    main()