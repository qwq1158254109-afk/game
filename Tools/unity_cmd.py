#!/usr/bin/env python3
"""AgentBridge 客户端 —— 通过文件与 Unity Editor 通信，不使用任何端口。

用法：
    python tools/unity_cmd.py ping
    python tools/unity_cmd.py scene_info
    python tools/unity_cmd.py hierarchy --args '{"maxDepth": 2}'
    python tools/unity_cmd.py find --args '{"name": "Player"}'
    python tools/unity_cmd.py console --args '{"count": 20}'
    python tools/unity_cmd.py menu --args '{"path": "Tools/Capture Room Screenshot"}'
    python tools/unity_cmd.py capture_scene_view --args '{"path": "Docs/shot.png"}'
    python tools/unity_cmd.py --list          # 列出 Unity 侧注册的全部命令
    python tools/unity_cmd.py --status        # 查看桥心跳

退出码：0 成功 / 1 命令执行失败 / 2 桥未就绪或参数错误 / 3 超时
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
import uuid

BRIDGE_DIR_NAME = "agent-bridge"
DEFAULT_TIMEOUT = 30.0
POLL_INTERVAL = 0.05


def project_root() -> str:
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def bridge_root() -> str:
    return os.path.join(project_root(), "Temp", BRIDGE_DIR_NAME)


def emit(obj) -> None:
    text = json.dumps(obj, ensure_ascii=False, indent=2)
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    print(text)


def read_json(path: str):
    try:
        with open(path, "r", encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, json.JSONDecodeError):
        return None


def heartbeat() -> dict | None:
    return read_json(os.path.join(bridge_root(), "heartbeat.json"))


def heartbeat_age(data: dict) -> float | None:
    """心跳距今秒数。Unity 关闭时该值会一直增长。"""
    stamp = data.get("lastTick")
    if not stamp:
        return None
    try:
        parsed = time.mktime(time.strptime(stamp, "%Y-%m-%d %H:%M:%S"))
    except ValueError:
        return None
    return time.time() - parsed


def ensure_ready(timeout: float, quiet: bool = False) -> dict:
    """确认桥在跑；Unity 刚启动/正在编译时短暂等待。"""
    deadline = time.time() + min(timeout, 20.0)
    last = None
    while time.time() < deadline:
        last = heartbeat()
        if last:
            age = heartbeat_age(last)
            if age is not None and age < 10 and not last.get("isCompiling"):
                return last
        time.sleep(0.3)

    if last is None:
        raise BridgeNotReady(
            "找不到 Temp/agent-bridge/heartbeat.json —— Unity 没打开，或 CommandBridge.cs 还没被加载。"
        )

    age = heartbeat_age(last)
    if last.get("isCompiling"):
        raise BridgeNotReady("Unity 正在编译，等它编译完再试（心跳 isCompiling=true）。")
    raise BridgeNotReady(
        f"桥心跳已过期（{age:.0f}s 未更新）—— Unity 可能已关闭或处于暂停状态。"
    )


class BridgeNotReady(RuntimeError):
    pass


def send(command: str, args: dict, timeout: float) -> tuple[int, dict]:
    root = bridge_root()
    cmd_dir = os.path.join(root, "cmd")
    result_dir = os.path.join(root, "result")
    os.makedirs(cmd_dir, exist_ok=True)
    os.makedirs(result_dir, exist_ok=True)

    cmd_id = f"{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"
    payload = {"id": cmd_id, "command": command, "args": args}

    tmp = os.path.join(cmd_dir, cmd_id + ".json.tmp")
    final = os.path.join(cmd_dir, cmd_id + ".json")
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, ensure_ascii=False)
    os.replace(tmp, final)  # 原子落盘，Editor 侧不会读到半截文件

    result_path = os.path.join(result_dir, cmd_id + ".json")
    deadline = time.time() + timeout
    armed_before = (heartbeat() or {}).get("armedAt")

    while time.time() < deadline:
        data = read_json(result_path)
        if data is not None:
            return (0 if data.get("ok") else 1), data
        time.sleep(POLL_INTERVAL)

    # 超时：区分「domain reload 打断」与「真的没响应」
    armed_after = (heartbeat() or {}).get("armedAt")
    hint = ""
    if armed_before and armed_after and armed_before != armed_after:
        hint = "（期间发生了 domain reload，命令可能已被丢弃）"
    elif command == "recompile":
        hint = "（recompile 必然触发重载，属预期）"
    try:
        os.remove(final)
    except OSError:
        pass
    return 3, {
        "ok": False,
        "command": command,
        "error": f"等待 {timeout:.0f}s 未拿到结果{hint}",
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description="通过文件桥向 Unity Editor 发送命令（无端口、无 socket）"
    )
    parser.add_argument("command", nargs="?", help="命令名，如 ping / scene_info / hierarchy")
    parser.add_argument("--args", default="{}", help="JSON 参数，默认 {}")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT, help="超时秒数，默认 30")
    parser.add_argument("--list", action="store_true", help="列出 Unity 侧注册的命令")
    parser.add_argument("--status", action="store_true", help="打印桥心跳")
    parser.add_argument("--raw", action="store_true", help="只输出 result 字段")
    opts = parser.parse_args(argv)

    if opts.status:
        beat = heartbeat()
        if beat is None:
            emit({"ok": False, "error": "桥未就绪：没有 heartbeat.json"})
            return 2
        beat["heartbeatAgeSeconds"] = round(heartbeat_age(beat) or -1, 1)
        emit(beat)
        return 0

    if opts.list:
        manifest = read_json(os.path.join(bridge_root(), "commands.json"))
        if manifest is None:
            emit({"ok": False, "error": "桥未就绪：没有 commands.json"})
            return 2
        emit(manifest)
        return 0

    if not opts.command:
        parser.print_help()
        return 2

    try:
        args = json.loads(opts.args)
    except json.JSONDecodeError as exc:
        emit({"ok": False, "error": f"--args 不是合法 JSON: {exc}"})
        return 2
    if not isinstance(args, dict):
        emit({"ok": False, "error": "--args 必须是 JSON 对象"})
        return 2

    try:
        ensure_ready(opts.timeout)
    except BridgeNotReady as exc:
        emit({"ok": False, "error": str(exc)})
        return 2

    code, data = send(opts.command, args, opts.timeout)
    if opts.raw and data.get("ok"):
        emit(data.get("result"))
    else:
        emit(data)
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
