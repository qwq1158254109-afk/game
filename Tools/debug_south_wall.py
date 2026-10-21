"""最小复现：定位南墙带在 x≈976..1199 被什么覆盖。

逐步累加，每步都打印 (y=980, x=1050) 的像素值，看是哪一步把墙面色变成地板色。
"""
import sys

sys.path.insert(0, r"E:/大创项目/Tools")
import numpy as np
from PIL import Image

import make_rooms as M

steps = []


def snap(canvas, label):
    px = canvas.convert("RGB").getpixel((1050, 980))
    steps.append((label, px))
    print("  %-28s (1050,980) = %s" % (label, px))


def main():
    canvas = Image.new("RGBA", (M.CANVAS_W, M.CANVAS_H))
    snap(canvas, "0 空画布")

    M.paint_floor(canvas)
    snap(canvas, "1 铺地板")

    M.apply_floor_light(canvas)
    snap(canvas, "2 光照")

    # 南墙带（living 的 gaps）
    horiz_bottom = M.resolve_gaps(M.ROOMS["living"]["gaps"], "bottom", M.CANVAS_W)
    horiz_top = M.resolve_gaps(M.ROOMS["living"]["gaps"], "top", M.CANVAS_W)
    print("  bottom gaps =", horiz_bottom)
    print("  top    gaps =", horiz_top)

    M.wall_band_h(canvas, M.CANVAS_H - M.W_TOP, M.W_TOP, horiz_bottom)
    snap(canvas, "3 南墙带")

    M.wall_band_h(canvas, 0, M.W_TOP, horiz_top)
    snap(canvas, "4 北墙带")

    # 门洞
    box = M.door_hole_box("bottom", horiz_bottom[0][0], horiz_bottom[0][1])
    print("  bottom hole box =", box)
    x0, y0, x1, y1 = box
    canvas.paste(Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0)), (x0, y0))
    snap(canvas, "5 抠洞")

    M.paint_hole_floor(canvas, box)
    snap(canvas, "6 洞里补地板")

    M.draw_doorway(canvas, box, with_threshold=False)
    snap(canvas, "7 门洞压暗+框")

    M.draw_door_in_wall(canvas, box, "bottom")
    snap(canvas, "8 贴门")

    print()
    print("=== 变化点 ===")
    for i in range(1, len(steps)):
        if steps[i][1] != steps[i - 1][1]:
            print("  %s -> %s   (%s 改写了这里)" % (steps[i - 1][1], steps[i][1], steps[i][0]))


if __name__ == "__main__":
    main()