# -*- coding: utf-8 -*-
"""核验候选 Unity 插件的 GitHub 仓库元数据（精简输出，只读网络）。

用法: python Tools/gh_repo_check.py
"""
from __future__ import annotations

import json
import urllib.request

REPOS = [
    # 对话 / 剧情 / 事件
    "YarnSpinnerTool/YarnSpinner",
    "inkle/ink",
    "inkle/ink-unity-integration",
    "Siccity/xNode",
    "DrFrugal/Fungus",
    # 寻路 / AI
    "h8man/NavMeshPlus",
    "SunnyValleyStudio/Unity-2D-A-star-pathfinding",
    "Cr34t3r/Unity-2D-Pathfinding",
    "AronGranberg/AStarPathfindingProject",
    # 存档 / 序列化
    "BayatGames/SaveGameFree",
    "Cysharp/UniTask",
    "JamesNK/Newtonsoft.Json",
    # 卡牌 / 战斗 / 背包
    "kubakrzysiek/Unity-Card-Game",
    "Ferdowsi/Unity-Deck-Builder",
    "Daimaou/Unity-RPG-Battle-System",
    "GameDevBox/Unity-Inventory-System",
    # 像素 / 相机 / 渲染
    "RyanNielson/PixelCamera2D",
    "Unity-Technologies/2d-pixel-perfect",
    # 输入 / UI / 交互
    "Unity-Technologies/InputSystem",
    "Unity-Technologies/UnityCsReference",
    # Tiled / 地图
    "Seanba/SuperTiled2Unity",
    # 状态机 / 事件总线
    "Unity-Technologies/Unity-Robotics-Hub",
    "Real-Serious-Games/Unity-Event-Bus",
    # 本地化
    "Unity-Technologies/UnityLocalization",
    # 通用框架
    "QianMo/Game-Programmer-Study-Notes",
    "EllanJiang/GameFramework",
    "mob-sakai/UIEffect",
    "mob-sakai/ParticleEffectForUGUI",
    "AnnulusGames/LitMotion",
    "Cysharp/R3",
    "neuecc/UniRx",
    "UnityCommunity/UnityLibrary",
    "Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo",
    "keijiro/TestCards",
    "keijiro/Rsvfx",
]


def fetch(full_name: str) -> dict | None:
    req = urllib.request.Request(
        f"https://api.github.com/repos/{full_name}",
        headers={
            "Accept": "application/vnd.github+json",
            "User-Agent": "repo-check",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except Exception as exc:  # noqa: BLE001
        print(f"!! {full_name:55s} 查询失败: {exc}")
        return None


def main() -> None:
    for name in REPOS:
        data = fetch(name)
        if not data:
            continue
        lic = (data.get("license") or {}).get("spdx_id") or "无"
        desc = (data.get("description") or "")[:110]
        print(
            f"{data['full_name']:52s} | {data['stargazers_count']:>6}★ | "
            f"{str(data.get('language'))[:12]:12s} | {lic:12s} | "
            f"pushed {(data.get('pushed_at') or '')[:10]} | "
            f"archived={data.get('archived')} | {desc}"
        )


if __name__ == "__main__":
    main()