"""地板无缝化验证：测木纹周期、去光照梯度、检查平铺接缝。

用法：python Tools/probe_floor.py
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
OUT = r"E:/大创项目/Tools/_floor_test.png"

# 候选采样区（干净、木纹清晰）
REGION = (520, 600, 980, 800)      # x0,y0,x1,y1 -> 460x200


def main():
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    x0, y0, x1, y1 = REGION
    reg = a[y0:y1, x0:x1]

    print("=== 1. 木纹周期：行方向自相关（找板缝间距）===")
    lum = reg.mean(axis=2)
    rowmean = lum.mean(axis=1)
    rm = rowmean - rowmean.mean()
    ac = np.correlate(rm, rm, mode="full")[len(rm) - 1:]
    ac = ac / ac[0]
    peaks = [k for k in range(6, 120) if ac[k] > ac[k - 1] and ac[k] >= ac[k + 1] and ac[k] > 0.25]
    for k in peaks[:10]:
        print("   lag=%3d px  自相关=%.3f" % (k, ac[k]))

    print("\n=== 2. 光照梯度（行均值）===")
    print("   顶 10 行 mean=%.1f   底 10 行 mean=%.1f   差=%.1f" % (
        rowmean[:10].mean(), rowmean[-10:].mean(), rowmean[-10:].mean() - rowmean[:10].mean()))

    print("\n=== 3. 去梯度：拟合低通背景后相除 ===")
    # 用大核均值估计背景（对木纹不敏感），再相除提平
    small = Image.fromarray(reg.astype(np.uint8)).resize((reg.shape[1] // 8, reg.shape[0] // 8), Image.BOX)
    for _ in range(3):
        small = small.resize((small.width, small.height), Image.BOX)
    bg = np.asarray(small.resize((reg.shape[1], reg.shape[0]), Image.BICUBIC)).astype(np.float32)
    bg = np.clip(bg, 1.0, None)
    flat = np.clip(reg / bg * bg.mean(), 0, 255).astype(np.uint8)
    flat_img = Image.fromarray(flat)

    print("   原图 行均值 顶/底 = %.1f / %.1f  (差 %.1f)" % (
        reg.mean(axis=(1, 2))[:10].mean(), reg.mean(axis=(1, 2))[-10:].mean(),
        reg.mean(axis=(1, 2))[-10:].mean() - reg.mean(axis=(1, 2))[:10].mean()))
    fl = np.asarray(flat).astype(np.float32).mean(axis=2)
    print("   提平 行均值 顶/底 = %.1f / %.1f  (差 %.1f)" % (
        fl[:10].mean(), fl[-10:].mean(), fl[-10:].mean() - fl[:10].mean()))

    print("\n=== 4. 平铺接缝检查：上下边拼接处的行差 ===")
    f = np.asarray(flat).astype(np.float32)
    edge_diff = np.abs(f[0] - f[-1]).mean()
    inner_diff = np.abs(f[1] - f[0]).mean()
    print("   接缝处行差=%.2f   内部相邻行差=%.2f   (越接近越好)" % (edge_diff, inner_diff))

    print("\n=== 5. 导出测试图（2x2 平铺 + 原图对照）===")
    h, w = flat.shape[0], flat.shape[1]
    tiled = Image.new("RGB", (w * 2, h * 2))
    for j in range(2):
        for i in range(2):
            tiled.paste(flat_img, (i * w, j * h))
    tiled = tiled.resize((w, h), Image.LANCZOS)

    sheet = Image.new("RGB", (w, h * 2 + 6), (255, 0, 0))
    sheet.paste(Image.fromarray(reg.astype(np.uint8)), (0, 0))
    sheet.paste(tiled, (0, h + 6))
    sheet.save(OUT)
    print("   saved", OUT, sheet.size, "(上=原图 下=提平后2x2平铺缩略)")


if __name__ == "__main__":
    main()