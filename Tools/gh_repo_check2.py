# -*- coding: utf-8 -*-
"""第二轮核验：修正 owner 名 + 补充方向（本地化 / Tween / 场景过渡 / 2D 光照 / 像素工具）。

只读 GitHub 公开 API。
用法: python Tools/gh_repo_check2.py
"""
from __future__ import annotations

import json
import urllib.parse
import urllib.request

REPOS = [
    # 对话 / 剧情 / 事件（修正 owner）
    "snozbot/fungus",
    "Chizaruu/Fungus",
    "Unity-Technologies/UnityLocalization",
    "Unity-Technologies/com.unity.localization",
    "andrew-raphael-lukasik/UnityLocalization",
    "Unity-Technologies/EndlessRunnerSampleGame",
    # 2D 寻路（修正 owner）
    "SunnyValleyStudio/Unity-2D-A-star-pathfinding-tutorial",
    "SunnyValleyStudio/Pathfinding-2D",
    "Wafflus/Unity-2D-Pathfinding",
    "pixelfac/2D-Astar-Pathfinding-in-Unity",
    "RonenNess/Unity-2d-pathfinding",
    "seanwyong/AStarPathfinding2D",
    # 背包 / 卡牌 / 战斗
    "Reddit-Dev/SimpleInventory",
    "Unity-Technologies/com.unity.2d.animation",
    "ZacharyPatten/UnityCardGame",
    "MinaPecheux/UnityTutorials-RTS",
    # UI 动效 / Tween
    "DigitalRuby/Tween",
    "Jeffrey-Lacher/UnityTween",
    "kyubuns/AnimeTask",
    "mob-sakai/SoftMaskForUGUI",
    "mob-sakai/ShinyEffectForUGUI",
    # 2D 光照
    "SmartCop/2D-Light-of-Sight-Unity",
    "nubick/unity-2d-lighting",
    "Unity-Technologies/2d-extras",
    "Unity-Technologies/2d-techdemos",
    # 场景 / 流程
    "Unity-Technologies/Addressables-Sample",
    "Unity-Technologies/UnitySceneLoader",
    # 像素美术工具
    "mob-sakai/UnityUIShader",
    "keijiro/Retro3DPipeline",
    "keijiro/PixelCamera",
    "SHAHBAZ-ALAM/Unity-Pixel-Art",
    # 通用工具库
    "Cysharp/MagicOnion",
    "adamsingle/Unity-Helper",
    "yasirkula/UnityIngameDebugConsole",
    "yasirkula/UnityNativeGallery",
    "yasirkula/UnityRuntimeInspector",
    "yasirkula/UnityToolbag",
]


def fetch(full_name: str) -> dict | None:
    req = urllib.request.Request(
        f"https://api.github.com/repos/{full_name}",
        headers={"Accept": "application/vnd.github+json", "User-Agent": "repo-check2"},
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except Exception as exc:  # noqa: BLE001
        print(f"!! {full_name:55s} 查询失败: {exc}")
        return None


def search(query: str, per_page: int = 5) -> None:
    params = urllib.parse.urlencode(
        {"q": query, "sort": "stars", "order": "desc", "per_page": per_page}
    )
    req = urllib.request.Request(
        f"https://api.github.com/search/repositories?{params}",
        headers={"Accept": "application/vnd.github+json", "User-Agent": "repo-check2"},
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            items = json.loads(resp.read().decode("utf-8")).get("items", [])
    except Exception as exc:  # noqa: BLE001
        print(f"~~ 搜索失败 [{query}]: {exc}")
        return
    print(f"~~ 搜索 [{query}] 命中 {len(items)} 条:")
    for it in items:
        lic = (it.get("license") or {}).get("spdx_id") or "无"
        print(
            f"   {it['full_name']:48s} {it['stargazers_count']:>6}★ {lic:12s} "
            f"pushed {(it.get('pushed_at') or '')[:10]} | {(it.get('description') or '')[:90]}"
        )


def main() -> None:
    for name in REPOS:
        data = fetch(name)
        if not data:
            continue
        lic = (data.get("license") or {}).get("spdx_id") or "无"
        desc = (data.get("description") or "")[:100]
        print(
            f"{data['full_name']:52s} | {data['stargazers_count']:>6}★ | "
            f"{str(data.get('language'))[:12]:12s} | {lic:12s} | "
            f"pushed {(data.get('pushed_at') or '')[:10]} | archived={data.get('archived')} | {desc}"
        )
    print()
    for q in [
        "unity localization open source",
        "unity tween library",
        "unity 2d lighting urp",
        "unity deckbuilder card game open source",
    ]:
        search(q)


if __name__ == "__main__":
    main()