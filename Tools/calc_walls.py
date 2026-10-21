"""算加厚后各房间的墙/门碰撞体数值（不写文件，只打印）。"""
W = 2.0          # 墙厚
DOOR = 2.0       # 门洞净宽（= 128px）

ROOMS = {
    "living": dict(W=29.5, H=17.5, IH=12.75, IT=6.75,
                   gaps={"left": [(0, DOOR)], "right": [(0, DOOR)],
                         "top": [(-6.0, DOOR)], "bottom": [(0.0, DOOR)]}),
    "bedroom": dict(W=29.5, H=17.5, IH=12.75, IT=6.75, gaps={"left": [(0, DOOR)]}),
    "study": dict(W=29.5, H=17.5, IH=12.75, IT=6.75, gaps={"right": [(0, DOOR)]}),
    "kitchen": dict(W=29.5, H=17.5, IH=12.75, IT=6.75, gaps={"bottom": [(0, DOOR)]}),
}


def segs(lo, hi, gaps):
    """把 [lo,hi] 挖掉 gaps 后剩下的墙段 [(center, size), ...]。"""
    out, x = [], lo
    for g0, gl in sorted(gaps):
        g1 = g0 + gl
        if g0 > x:
            out.append((round((x + g0) / 2, 3), round(g0 - x, 3)))
        x = max(x, g1)
    if x < hi:
        out.append((round((x + hi) / 2, 3), round(hi - x, 3)))
    return out


for name, r in ROOMS.items():
    iw, ih, it = r["W"] / 2, r["H"] / 2, r["IT"]
    ib = it - (r["H"] - 2 * W)          # 内下边界
    print("=== %s  画布 %.1fx%.1f  内沿 x±%.2f  y %.2f..%.2f ===" % (
        name, r["W"], r["H"], iw, ib, it))
    g = r["gaps"]

    # 南墙（贴画布下沿，y -H/2 .. -H/2+W），门洞沿 x
    y_c = -ih + W / 2
    for c, s in segs(-iw, iw, g.get("bottom", [])):
        print("  南墙 new WallSpec(%.3ff, %.3ff, %.3ff, %.1ff)" % (c, y_c, s, W))
    # 北墙
    y_c = ih - W / 2
    for c, s in segs(-iw, iw, g.get("top", [])):
        print("  北墙 new WallSpec(%.3ff, %.3ff, %.3ff, %.1ff)" % (c, y_c, s, W))
    # 西墙
    x_c = -iw + W / 2
    for c, s in segs(ib, it, g.get("left", [])):
        print("  西墙 new WallSpec(%.3ff, %.3ff, %.1ff, %.3ff)" % (x_c, c, W, s))
    # 东墙
    x_c = iw - W / 2
    for c, s in segs(ib, it, g.get("right", [])):
        print("  东墙 new WallSpec(%.3ff, %.3ff, %.1ff, %.3ff)" % (x_c, c, W, s))
    print("  内沿: InnerHalfW=%.2f InnerTop=%.2f InnerBottom=%.2f" % (iw, it, ib))
    print()