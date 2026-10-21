# -*- coding: utf-8 -*-
"""从 Unity Bee 编译日志里提取 CS 错误（含文件与行号）。

只读脚本。用法: python Tools/extract_compile_errors.py
"""
from __future__ import annotations

import json
import re

LOG = "Library/Bee/tundra.log.json"


def main() -> None:
    seen: set[str] = set()
    with open(LOG, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except Exception:
                continue
            text = json.dumps(event, ensure_ascii=False)
            if "error CS" not in text:
                continue
            # 把 JSON 里的 \r\n 还原成真换行，便于按行切分
            text = text.replace("\\r\\n", "\n").replace("\\n", "\n")
            for match in re.findall(r"[^\n]*error CS\d+[^\n]*", text):
                match = match.strip().strip('"').strip()
                if match and match not in seen:
                    seen.add(match)
                    print("  " + match[:240])

    if not seen:
        print("  (无 error CS)")


if __name__ == "__main__":
    main()