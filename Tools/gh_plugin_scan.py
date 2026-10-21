# -*- coding: utf-8 -*-
"""从 GitHub API 批量检索 Unity 插件候选仓库，输出精简表格。

只读脚本：只调用 GitHub 公开搜索 API，不修改仓库内任何文件。
用法: python Tools/gh_plugin_scan.py
输出: Tools/gh_plugin_scan_result.txt
"""
from __future__ import annotations

import json
import os
import time
import urllib.parse
import urllib.request

API = "https://api.github.com/search/repositories"


def auth_headers() -> dict[str, str]:
    """有 GITHUB_TOKEN 环境变量就带上，可把搜索配额从 10 次/分钟提到 30 次/分钟。"""
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": "plugin-scan-script",
    }
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return headers

QUERIES = {
    "地图/寻路": [
        "unity NavMesh 2D pathfinding",
        "unity A* pathfinding tilemap 2d",
    ],
    "对话/剧情": [
        "unity dialogue system narrative tool",
        "unity visual novel dialogue graph",
    ],
    "存档/序列化": [
        "unity save game serialization system",
    ],
    "背包/卡牌UI": [
        "unity inventory system rpg 2d",
        "unity card game framework deckbuilder",
    ],
    "像素/相机": [
        "unity pixel perfect camera 2d",
    ],
    "编辑器/工作流": [
        "unity inspector editor extension open source",
        "unity scene loader transition fade",
    ],
    "战斗/状态机": [
        "unity turn based battle system rpg",
        "unity behavior tree AI 2d",
    ],
    "音频": [
        "unity audio manager sound 2d",
    ],
    "本地化": [
        "unity localization chinese text",
    ],
    "地图编辑": [
        "unity tiled map importer 2d",
    ],
}


def search(query: str, per_page: int = 8) -> list[dict]:
    params = urllib.parse.urlencode(
        {
            "q": query,
            "sort": "stars",
            "order": "desc",
            "per_page": per_page,
        }
    )
    req = urllib.request.Request(f"{API}?{params}", headers=auth_headers())
    with urllib.request.urlopen(req, timeout=45) as resp:
        return json.loads(resp.read().decode("utf-8")).get("items", [])


def main() -> None:
    seen: set[str] = set()
    lines: list[str] = []
    for group, queries in QUERIES.items():
        lines.append(f"\n===== {group} =====")
        for q in queries:
            try:
                items = search(q)
            except Exception as exc:  # noqa: BLE001
                lines.append(f"  [查询失败] {q} -> {exc}")
                continue
            for it in items:
                full = it["full_name"]
                if full in seen:
                    continue
                seen.add(full)
                lic = (it.get("license") or {}).get("spdx_id") or "无"
                lines.append(
                    "| {name} | {stars}★ | {lang} | {lic} | pushed {pushed} | {desc}".format(
                        name=full,
                        stars=it["stargazers_count"],
                        lang=it.get("language"),
                        lic=lic,
                        pushed=(it.get("pushed_at") or "")[:10],
                        desc=(it.get("description") or "").replace("\n", " ")[:150],
                    )
                )
            time.sleep(1.2)  # 避开搜索 API 频率限制

    out = "\n".join(lines)
    print(out)


if __name__ == "__main__":
    main()