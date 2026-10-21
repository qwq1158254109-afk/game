# -*- coding: utf-8 -*-
"""按方案B（四墙等厚 2.5 单位）改写 HomeRoomsBuilder.cs 的几何常量与规格。

只做字符串级替换，替换前逐条校验命中数，命中不等于预期就整批中止（不落盘）。

用法: python Tools/apply_room_thickness.py           # 预演（dry-run）
      python Tools/apply_room_thickness.py --write    # 实际写入
"""
from __future__ import annotations

import sys

PATH = "Assets/Editor/StudentGrowth/HomeRoomsBuilder.cs"

# ── 新几何（世界单位，画布中心为原点）────────────────────────────────
WALL = 2.5                     # 墙厚
HALF_W, HALF_H = 15.555, 9.25     # 画布外沿
INNER_HW = 13.055              # 室内 x 边界（±）
INNER_T, INNER_B = 6.75, -6.75    # 室内 y 边界
WALL_CX = 14.305               # 侧墙中心 x（±）
WALL_CY = 8.0                  # 南北墙中心 y（±）
DOOR_HALF = 1.25               # 门洞半宽（净宽 2.5）

# ── 替换表：(旧, 新, 预期命中次数) ────────────────────────────────────
REPLACEMENTS: list[tuple[str, str, int]] = [
    # 1) 顶部常量
    ("private const float WallThick = 2.0f;",
     "private const float WallThick = 2.5f;", 1),
    ("private const float RoomW = 29.5f;",
     "private const float RoomW = 31.11f;", 1),
    ("private const float RoomH = 17.5f;",
     "private const float RoomH = 18.5f;", 1),
    ("private const float Ortho = 8.75f;",
     "private const float Ortho = 9.25f;", 1),
    ("private const float WalkW = 25.5f;",
     "private const float WalkW = 26.11f;", 1),
    ("private const float LivingOrtho = 8.75f;",
     "private const float LivingOrtho = 9.25f;", 1),
    ("private const float InnerHalfW = 14.75f;",
     "private const float InnerHalfW = 13.055f;", 1),
    ("private const float InnerTop = 6.75f;",
     "private const float InnerTop = 6.75f;", 1),          # 不变，占位校验
    ("private const float InnerBottom = -6.75f;",
     "private const float InnerBottom = -6.75f;", 1),      # 不变

    # 2) 客厅（Living）+ 厨房（Kitchen）共用同一组南墙段（门洞都在 x=0）
    #    南墙（y -9.25..-6.75，门洞 x=0 宽 2.5）
    ("new WallSpec(-7.375f, -7.75f, 14.75f, 2.0f),",
     f"new WallSpec(-8.4025f, -8.0f, 14.305f, {WALL}f),", 2),
    ("new WallSpec(8.375f, -7.75f, 12.75f, 2.0f),",
     f"new WallSpec(9.4025f, -8.0f, 12.305f, {WALL}f),", 2),
    #    北墙（门洞 x=-6 宽 2.5）
    ("new WallSpec(-10.375f, 7.75f, 8.75f, 2.0f),",
     f"new WallSpec(-11.4025f, 8.0f, 8.305f, {WALL}f),", 1),
    ("new WallSpec(5.375f, 7.75f, 18.75f, 2.0f),",
     f"new WallSpec(6.4025f, 8.0f, 18.305f, {WALL}f),", 1),
    #    侧墙带门段（客厅西/东、卧室西、书房东）
    ("new WallSpec(-13.75f, -3.375f, 2.0f, 6.75f),",
     f"new WallSpec(-14.305f, -4.0f, {WALL}f, 8.0f),", 2),
    ("new WallSpec(-13.75f, 4.375f, 2.0f, 4.75f),",
     f"new WallSpec(-14.305f, 4.0f, {WALL}f, 8.0f),", 2),
    ("new WallSpec(13.75f, -3.375f, 2.0f, 6.75f),",
     f"new WallSpec(14.305f, -4.0f, {WALL}f, 8.0f),", 2),
    ("new WallSpec(13.75f, 4.375f, 2.0f, 4.75f),",
     f"new WallSpec(14.305f, 4.0f, {WALL}f, 8.0f),", 2),
    #    客厅门规格
    ('new DoorSpec("DoorToBedroom", 13.75f, 0f, .9f, 1.9f,',
     'new DoorSpec("DoorToBedroom", 14.305f, 0f, .9f, 1.9f,', 1),
    ('new DoorSpec("DoorToStudy", -13.75f, 0f, .9f, 1.9f,',
     'new DoorSpec("DoorToStudy", -14.305f, 0f, .9f, 1.9f,', 1),
    ('new DoorSpec("DoorToKitchen", -6f, 7.75f, 1.9f, .9f,',
     'new DoorSpec("DoorToKitchen", -6f, 8.0f, 1.9f, .9f,', 1),

    # 3) 卧室（Bedroom）：北/南墙整幅，西墙带门
    ("new WallSpec(0f, 7.75f, RoomW, 2.0f),",
     f"new WallSpec(0f, 8.0f, RoomW, {WALL}f),", 3),        # 卧室 + 书房 + 厨房
    ("new WallSpec(0f, -7.75f, RoomW, 2.0f),",
     f"new WallSpec(0f, -8.0f, RoomW, {WALL}f),", 2),       # 卧室 + 书房
    ("new WallSpec(13.75f, 0f, 2.0f, RoomH),",
     f"new WallSpec(14.305f, 0f, {WALL}f, RoomH),", 2),     # 卧室东墙 + 厨房东墙
    ('new DoorSpec("DoorToLiving", -13.75f, 0f, .9f, 1.9f,',
     'new DoorSpec("DoorToLiving", -14.305f, 0f, .9f, 1.9f,', 1),

    # 4) 书房（Study）：东墙带门
    ("new WallSpec(-13.75f, 0f, 2.0f, RoomH),",
     f"new WallSpec(-14.305f, 0f, {WALL}f, RoomH),", 2),    # 书房西墙 + 厨房西墙
    ('new DoorSpec("DoorToLiving", 13.75f, 0f, .9f, 1.9f,',
     'new DoorSpec("DoorToLiving", 14.305f, 0f, .9f, 1.9f,', 1),

    # 5) 厨房（Kitchen）：南墙带门
    ('new DoorSpec("DoorToLiving", 0f, -7.75f, 1.9f, .9f,',
     'new DoorSpec("DoorToLiving", 0f, -8.0f, 1.9f, .9f,', 1),

    # 6) 贴墙家具/装饰：可走区只变宽（x 内沿 12.75→13.055），
    #    因此**只有贴东西墙的物件**要外移 0.305；贴南北墙的 y 不变。
    ('new DecorSpec("small_cabinet", -12.1f, 3.2f, true, 1f),',
     'new DecorSpec("small_cabinet", -12.405f, 3.2f, true, 1f),', 1),
    ('new DecorSpec("small_cabinet", 12.1f, 3.2f, true, 1f),',
     'new DecorSpec("small_cabinet", 12.405f, 3.2f, true, 1f),', 1),
    ('new DecorSpec("small_cabinet", 12.1f, -3.6f, true, 1f),',
     'new DecorSpec("small_cabinet", 12.405f, -3.6f, true, 1f),', 1),
    ('new DecorSpec("small_plant", -12.2f, -6.0f, false, 1f),',
     'new DecorSpec("small_plant", -12.505f, -6.0f, false, 1f),', 3),
    ('new DecorSpec("tall_plant", 12.2f, -6.0f, true, 1f),',
     'new DecorSpec("tall_plant", 12.505f, -6.0f, true, 1f),', 2),
    ('new DecorSpec("tall_plant", 12.2f, 4.7f, true, 1f),',
     'new DecorSpec("tall_plant", 12.505f, 4.7f, true, 1f),', 1),
    ('new PropSpec("StudyTallPlant", -12.2f, 4.7f),',
     'new PropSpec("StudyTallPlant", -12.505f, 4.7f),', 1),
]


def main() -> None:
    write = "--write" in sys.argv
    src = open(PATH, encoding="utf-8").read()
    out = src
    total = 0
    problems: list[str] = []

    for old, new, expect in REPLACEMENTS:
        actual = out.count(old)
        if actual != expect:
            if expect == 0 and actual == 0:
                continue                       # 显式标注"已被上方消费"的占位
            problems.append(
                f"命中数不符: 期望 {expect} 实际 {actual}  [{old[:70]}]")
            continue
        out = out.replace(old, new)
        total += actual

    if problems:
        print("=== 中止：以下条目未按预期命中，未写入 ===")
        for p in problems:
            print("  " + p)
        return

    print(f"=== 共替换 {total} 处 ===")
    changed = out != src
    print(f"  文件是否发生变化: {changed}")

    if write and changed:
        open(PATH, "w", encoding="utf-8", newline="\n").write(out)
        print(f"  已写入 {PATH}")
    elif not write:
        print("  （预演模式，未写入；加 --write 落盘）")


if __name__ == "__main__":
    main()