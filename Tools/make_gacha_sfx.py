#!/usr/bin/env python3
"""合成扭蛋机演出用的 8-bit 芯片音效（2026-10-28，T5）。

只依赖标准库（wave / struct / math / random），不需要 numpy / scipy。
输出到 Assets/Resources/Audio/SFX/Gacha/，采样率 44100、单声道、16 bit PCM。

音色约定（与项目其它 SFX 保持同一取向：短、干、不抢戏）：
- 方波做「机械 / 芯片」质感，噪声做「撞击 / 摩擦」质感；
- 单条时长都在 0.6 s 以内，稀有 fanfare 最长；
- 峰值统一归一到 0.72，避免叠加时削波。

用法：
    python Tools/make_gacha_sfx.py
    python Tools/make_gacha_sfx.py --out "D:/tmp/gacha"
"""

from __future__ import annotations

import argparse
import math
import os
import random
import struct
import wave

SAMPLE_RATE = 44100
PEAK = 0.72
random.seed(20261028)


# ── 基础波形 ────────────────────────────────────────────────────────────

def square(phase: float, duty: float = 0.5) -> float:
    """占空比可调的方波，phase 用 0..1 表示一个周期。"""
    return 1.0 if (phase % 1.0) < duty else -1.0


def triangle(phase: float) -> float:
    p = phase % 1.0
    return 4.0 * abs(p - 0.5) - 1.0


def noise() -> float:
    return random.uniform(-1.0, 1.0)


def exp_env(i: int, n: int, curve: float = 5.0) -> float:
    """指数衰减包络，curve 越大衰减越快。"""
    if n <= 1:
        return 0.0
    return math.exp(-curve * i / (n - 1))


def attack_env(i: int, n: int, attack_ratio: float = 0.08) -> float:
    """短促起音 + 指数衰减，避免爆音（直接起振会有 DC 冲击）。"""
    attack = max(1, int(n * attack_ratio))
    if i < attack:
        return i / attack
    return exp_env(i - attack, n - attack, 5.0)


# ── 音效构造 ────────────────────────────────────────────────────────────

def make_coin() -> list[float]:
    """投币：两下短促上滑，像金属币撞到滑槽。"""
    samples: list[float] = []
    for index, (f0, f1, dur) in enumerate(((1180.0, 1560.0, 0.045), (1560.0, 1980.0, 0.06))):
        n = int(SAMPLE_RATE * dur)
        phase = 0.0
        for i in range(n):
            t = i / n
            freq = f0 + (f1 - f0) * t
            phase += freq / SAMPLE_RATE
            samples.append(square(phase, 0.35) * attack_env(i, n, 0.05) * 0.9)
        if index == 0:
            samples.extend([0.0] * int(SAMPLE_RATE * 0.015))
    return samples


def make_drop() -> list[float]:
    """出蛋：塑料蛋从滑槽掉出来的「咚 + 滑」。"""
    n = int(SAMPLE_RATE * 0.16)
    samples: list[float] = []
    phase = 0.0
    for i in range(n):
        t = i / n
        freq = 760.0 * (1.0 - 0.72 * t)
        phase += freq / SAMPLE_RATE
        body = square(phase, 0.5) * 0.6
        click = noise() * 0.4 * exp_env(i, n, 22.0)
        samples.append((body + click) * attack_env(i, n, 0.03))
    return samples


def make_rattle() -> list[float]:
    """摇蛋：颗粒摩擦声，振幅按 38 Hz 起伏（像蛋在壳里滚）。"""
    n = int(SAMPLE_RATE * 0.28)
    samples: list[float] = []
    for i in range(n):
        t = i / n
        amp = 0.55 + 0.45 * math.sin(2.0 * math.pi * 38.0 * i / SAMPLE_RATE)
        grain = noise() * 0.5 + triangle(i * 180.0 / SAMPLE_RATE) * 0.2
        samples.append(grain * amp * attack_env(i, n, 0.06) * 0.85)
    return samples


def make_tick() -> list[float]:
    """悬念脉冲：一声很短的「嗒」，用于漏光阶段的节拍。"""
    n = int(SAMPLE_RATE * 0.035)
    samples: list[float] = []
    phase = 0.0
    for i in range(n):
        phase += 2100.0 / SAMPLE_RATE
        samples.append(square(phase, 0.25) * exp_env(i, n, 14.0) * 0.8)
    return samples


def make_pop() -> list[float]:
    """炸壳：噪声爆裂 + 下滑，蛋壳裂开的瞬间。"""
    n = int(SAMPLE_RATE * 0.2)
    samples: list[float] = []
    phase = 0.0
    for i in range(n):
        t = i / n
        freq = 520.0 * (1.0 - 0.85 * t)
        phase += freq / SAMPLE_RATE
        burst = noise() * exp_env(i, n, 18.0) * 0.75
        body = square(phase, 0.5) * exp_env(i, n, 9.0) * 0.5
        samples.append((burst + body) * attack_env(i, n, 0.02))
    return samples


def _fanfare(notes: list[tuple[float, float]], tail: float) -> list[float]:
    """上行琶音。notes = [(频率, 时长), ...]，末尾加一点余响。"""
    samples: list[float] = []
    for freq, dur in notes:
        n = int(SAMPLE_RATE * dur)
        phase = 0.0
        phase2 = 0.0
        for i in range(n):
            phase += freq / SAMPLE_RATE
            phase2 += (freq * 2.0) / SAMPLE_RATE
            lead = square(phase, 0.5) * 0.6
            shimmer = square(phase2, 0.25) * 0.18 * exp_env(i, n, 6.0)
            samples.append((lead + shimmer) * attack_env(i, n, 0.05))
    samples.extend([0.0] * int(SAMPLE_RATE * tail))
    return samples


def make_fanfare_rare() -> list[float]:
    """稀有：C5-E5-G5-C6 四音上行，最长最亮。"""
    return _fanfare([(523.25, 0.11), (659.25, 0.11), (783.99, 0.11), (1046.50, 0.2)], 0.08)


def make_fanfare_uncommon() -> list[float]:
    """非凡：C5-G5 两音，克制一些。"""
    return _fanfare([(523.25, 0.1), (783.99, 0.16)], 0.06)


def make_flip() -> list[float]:
    """翻卡：一张短促的高频「唰」。"""
    n = int(SAMPLE_RATE * 0.07)
    samples: list[float] = []
    for i in range(n):
        t = i / n
        freq = 900.0 + 1600.0 * t
        grain = noise() * 0.35
        tone = triangle(i * freq / SAMPLE_RATE) * 0.35
        samples.append((grain + tone) * attack_env(i, n, 0.04))
    return samples


# ── 写文件 ──────────────────────────────────────────────────────────────

CLIPS = {
    "gacha_coin": make_coin,
    "gacha_drop": make_drop,
    "gacha_rattle": make_rattle,
    "gacha_tick": make_tick,
    "gacha_pop": make_pop,
    "gacha_fanfare_rare": make_fanfare_rare,
    "gacha_fanfare_uncommon": make_fanfare_uncommon,
    "gacha_flip": make_flip,
}


def normalize(samples: list[float], peak: float = PEAK) -> list[float]:
    if not samples:
        return samples
    top = max(abs(s) for s in samples)
    if top <= 1e-9:
        return samples
    scale = peak / top
    return [s * scale for s in samples]


def write_wav(path: str, samples: list[float]) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    frames = bytearray()
    for sample in samples:
        value = int(max(-1.0, min(1.0, sample)) * 32767)
        frames += struct.pack("<h", value)
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(bytes(frames))


def main() -> int:
    parser = argparse.ArgumentParser(description="合成扭蛋演出音效")
    parser.add_argument(
        "--out",
        default=os.path.join("Assets", "Resources", "Audio", "SFX", "Gacha"),
        help="输出目录（默认 Assets/Resources/Audio/SFX/Gacha）",
    )
    args = parser.parse_args()

    total = 0
    for name, builder in CLIPS.items():
        samples = normalize(builder())
        path = os.path.join(args.out, name + ".wav")
        write_wav(path, samples)
        seconds = len(samples) / SAMPLE_RATE
        print(f"{name:24s} {seconds:5.2f}s  → {path}")
        total += 1

    print(f"\n完成：{total} 个音效 → {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
