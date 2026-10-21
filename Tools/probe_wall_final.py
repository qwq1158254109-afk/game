"""墙带材质最终版 v2：用真正无窗的净面（x 660..900）重建，并自检无玻璃冷色。

源图北墙实测（见 probe_wall_clean.py）：
    窗/立柱污染区：x 120..180(暗边) 460..660(窗+框) 920..1000(立柱)
    可用净面：x 180..445 (std 9..13) 与 x 660..900 (std 6.8..10.5)
本脚本用后者（更净、更亮）。

竖向结构（x 250..450 实测，x 660..900 同构）：
    外沿暗边  y 36..38    2px
    压边      y 38..65   27px
    墙面      y 65..263 198px
    踢脚      y 263..295 32px
"""
import numpy as np
from PIL import Image

SRC = r"E:/大创项目/Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png"
OUT = r"E:/大创项目/Tools/_wall_band_final.png"
OUT_CMP = r"E:/大创项目/Tools/_wall_band_vs_source.png"

BAND_H = 96
EDGE_H, TRIM_H, BASE_H = 4, 9, 19
FACE_H = BAND_H - EDGE_H - TRIM_H - BASE_H        # 64

CLEAN_X = (660, 900)          # 真正的净面（无窗、无立柱）


def detrend(reg, strength=1.0):
    """逐通道去低通梯度（保色相）。写成 * bg.mean() 会把颜色洗灰。"""
    h, w, _ = reg.shape
    small = Image.fromarray(reg.astype(np.uint8)).resize(
        (max(1, w // 8), max(1, h // 8)), Image.BOX)
    bg = np.asarray(small.resize((w, h), Image.BICUBIC)).astype(np.float32)
    bg = np.clip(bg, 1.0, None)
    flat = reg / bg * bg.mean(axis=(0, 1))
    return np.clip(reg * (1 - strength) + flat * strength, 0, 255)


def mirror_tile_h(reg):
    """镜像半条：|左半 左半镜像| —— 尾接首天然连续，水平平铺零接缝。"""
    half = reg.shape[1] // 2
    left = reg[:, :half]
    return np.concatenate([left, left[:, ::-1]], axis=1)


def check_no_glass(reg, label):
    """自检：样本里不得出现玻璃冷色（B 明显高于 R）。"""
    cold_ratio = (reg[:, :, 2] > reg[:, :, 0] + 5).mean()
    dark_ratio = (reg.mean(axis=2) < 60).mean()
    flag = "OK" if cold_ratio < 0.005 else "!! 含玻璃/窗"
    print("  %-6s 尺寸 %-9s 偏冷率=%.4f 过暗率=%.4f  %s" % (
        label, "%dx%d" % (reg.shape[1], reg.shape[0]), cold_ratio, dark_ratio, flag))
    return cold_ratio < 0.005


def main():
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    x0, x1 = CLEAN_X

    print("=== 取样自检（净面 x %d..%d）===" % (x0, x1))
    face_raw = a[68:68 + FACE_H, x0:x1]
    trim_raw = a[40:40 + TRIM_H, x0:x1]
    base_raw = a[264:264 + BASE_H, x0:x1]
    ok = all([check_no_glass(face_raw, "face"),
              check_no_glass(trim_raw, "trim"),
              check_no_glass(base_raw, "base")])
    if not ok:
        print("!! 取样区含窗，请改 CLEAN_X —— 中止")
        return

    face = detrend(face_raw, 0.30)
    trim = detrend(trim_raw, 0.30)
    base = detrend(base_raw, 0.30)
    edge = np.tile(a[36:36 + EDGE_H, x0:x1], (1, 1, 1))

    face, trim, base, edge = (mirror_tile_h(x) for x in (face, trim, base, edge))

    wmin = min(x.shape[1] for x in (edge, trim, face, base))
    edge, trim, face, base = (x[:, :wmin] for x in (edge, trim, face, base))

    band = np.concatenate([edge, trim, face, base], axis=0)
    u8 = np.clip(band, 0, 255).astype(np.uint8)
    img = Image.fromarray(u8)

    print("\n=== 成品墙带 ===")
    print("  段高 edge=%d trim=%d face=%d base=%d  合计=%d" % (
        EDGE_H, TRIM_H, FACE_H, BASE_H, EDGE_H + TRIM_H + FACE_H + BASE_H))
    print("  尺寸 %dx%d  色数=%d" % (img.size[0], img.size[1],
                                    len(np.unique(u8.reshape(-1, 3), axis=0))))
    f = u8.astype(np.float32)
    print("  水平接缝=%.2f  内部基准=%.2f" % (
        np.abs(f[:, 0] - f[:, -1]).mean(), np.abs(f[:, 2] - f[:, 1]).mean()))
    m = u8.reshape(-1, 3).mean(axis=0)
    print("  三通道均值 R=%.1f G=%.1f B=%.1f  R-B=%.1f (源图墙面 R-B≈93)" % (
        m[0], m[1], m[2], m[0] - m[2]))

    out = Image.new("RGB", (img.size[0] * 2, img.size[1] * 3), (255, 0, 0))
    for i in range(2):
        for j in range(3):
            out.paste(img, (i * img.size[0], j * img.size[1]))
    out.save(OUT)
    print("  saved", OUT, out.size, "(2x3 平铺)")

    src_strip = Image.fromarray(np.clip(a[36:300, x0:x1], 0, 255).astype(np.uint8))
    cw = max(src_strip.size[0], img.size[0] * 2)
    cmp_img = Image.new("RGB", (cw, src_strip.size[1] + img.size[1] + 6), (255, 0, 0))
    cmp_img.paste(src_strip, (0, 0))
    cmp_img.paste(img, (0, src_strip.size[1] + 6))
    cmp_img.save(OUT_CMP)
    print("  saved", OUT_CMP, "(上=源图净面 下=成品墙带)")


if __name__ == "__main__":
    main()