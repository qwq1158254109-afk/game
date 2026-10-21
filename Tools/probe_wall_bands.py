"""量出源图北墙三段的精确像素边界，并挑出各段的干净取样 x 区间。"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"


def main():
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)

    print("=== 逐行：三通道均值 + 冷色标记（窗/玻璃会偏蓝）===")
    print("  y     R     G     B   R-B   备注")
    for y in list(range(30, 72)) + list(range(255, 305)):
        r, g, b = a[y, 250:450].mean(axis=0)
        note = ""
        if b > r:
            note = "冷(玻璃)"
        elif r - b > 60:
            note = "暖棕"
        elif r - b > 25:
            note = "墙面色"
        print("%4d %6.1f %6.1f %6.1f %6.1f  %s" % (y, r, g, b, r - b, note))

    print("\n=== 各材质段的干净 x 区间（要求段内无窗、无立柱）===")
    for label, y0, y1 in (("压边 trim", 40, 62), ("墙面 face", 72, 258), ("踢脚 base", 262, 292)):
        prof = a[y0:y1].mean(axis=(0, 2))          # 逐列亮度
        cold = (a[y0:y1, :, 2] > a[y0:y1, :, 0]).mean(axis=0)   # 逐列"偏冷"比例
        print("\n-- %s  (y %d..%d) --" % (label, y0, y1))
        run_start, run_len = None, 0
        runs = []
        for x in range(a.shape[1]):
            ok = cold[x] < 0.02
            if ok and run_start is None:
                run_start = x
            elif not ok and run_start is not None:
                runs.append((run_start, x, x - run_start))
                run_start = None
        if run_start is not None:
            runs.append((run_start, a.shape[1], a.shape[1] - run_start))
        for s, e, ln in sorted(runs, key=lambda r: -r[2])[:6]:
            seg = prof[s:e]
            print("   x %4d..%4d  宽 %4d  亮度 mean=%6.1f std=%5.2f" % (s, e, ln, seg.mean(), seg.std()))


if __name__ == "__main__":
    main()