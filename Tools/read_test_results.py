# -*- coding: utf-8 -*-
"""读取 Unity 测试结果 XML，打印失败用例与断言消息（UTF-8 安全）。

用法: python Tools/read_test_results.py [xml路径]
"""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET

path = sys.argv[1] if len(sys.argv) > 1 else "Logs/editmode-fade-results.xml"

root = ET.parse(path).getroot()
run = root if root.tag == "test-run" else root.find("test-run")
if run is not None:
    print(
        f"总计={run.get('total')} 通过={run.get('passed')} "
        f"失败={run.get('failed')} 跳过={run.get('skipped')}"
    )
print()

for tc in root.iter("test-case"):
    result = tc.get("result")
    print(f"[{result}] {tc.get('name')}  ({tc.get('duration')}s)")
    if result == "Passed":
        print()
        continue
    for child in tc.iter():
        if child.tag in ("message", "stack-trace"):
            text = (child.text or "").strip().replace("\n", " | ")
            if text:
                print(f"    {child.tag}: {text[:400]}")
    print()