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
    python tools/unity_cmd.py --probe         # 判定该走 MCP 还是桥

退出码：0 成功 / 1 命令执行失败 / 2 桥未就绪或参数错误 / 3 超时
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import socket
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


# ------------------------------------------------------------------ MCP 探活
#
# 端口 LISTENING 不等于 MCP 可用：mcp-unity 在 domain reload 后会出现
# 「旧 socket 占着端口、新实例绑不上、旧实例不处理请求」的假活状态。
# 只有真正握手一次才能区分。逻辑与 tools/ensure_unity.ps1 的 Test-WebSocketHandshake 一致。


def mcp_port() -> int:
    settings = read_json(os.path.join(project_root(), "ProjectSettings", "McpUnitySettings.json"))
    if isinstance(settings, dict) and isinstance(settings.get("Port"), int):
        return settings["Port"]
    return 8095


def mcp_token() -> str | None:
    path = os.path.join(project_root(), "Library", "McpUnity", "bridge-token")
    try:
        with open(path, "r", encoding="utf-8") as fh:
            token = fh.read().strip()
    except OSError:
        return None
    return token or None


def mcp_handshake(port: int, timeout: float = 6.0) -> dict:
    token = mcp_token()
    if token is None:
        return {"ok": False, "reason": "no-token", "detail": "缺少 Library/McpUnity/bridge-token"}

    key = base64.b64encode(os.urandom(16)).decode()
    auth = base64.b64encode(f"mcp-unity:{token}".encode()).decode()
    attempts: list[dict] = []

    # 端口可能只绑 ::1，也可能只绑 127.0.0.1，两个都试，并保留每个的结论。
    for host in ("::1", "127.0.0.1"):
        family = socket.AF_INET6 if ":" in host else socket.AF_INET
        try:
            with socket.socket(family, socket.SOCK_STREAM) as sock:
                sock.settimeout(timeout)
                sock.connect((host, port))
                request = (
                    f"GET /McpUnity HTTP/1.1\r\n"
                    f"Host: {host}:{port}\r\n"
                    "Upgrade: websocket\r\n"
                    "Connection: Upgrade\r\n"
                    f"Sec-WebSocket-Key: {key}\r\n"
                    "Sec-WebSocket-Version: 13\r\n"
                    f"Authorization: Basic {auth}\r\n"
                    "X-Client-Name: unity-channel-probe\r\n\r\n"
                )
                sock.sendall(request.encode("ascii"))
                first = sock.recv(256).decode("latin-1", "replace").split("\r\n", 1)[0]
                if " 101" in first:
                    return {"ok": True, "host": host, "status": first, "attempts": attempts + [{"host": host, "result": "ok", "status": first}]}
                attempts.append({"host": host, "result": "rejected", "status": first})
        except socket.timeout:
            attempts.append({"host": host, "result": "timeout"})
        except OSError as exc:
            attempts.append({"host": host, "result": "refused", "detail": str(exc)})

    # 判定优先级：timeout 是「假活」特征（端口在听但不响应），比 refused 更值得报警。
    results = {a["result"] for a in attempts}
    if "timeout" in results:
        reason = "timeout-zombie"
    elif "rejected" in results:
        reason = "rejected"
    else:
        reason = "refused"
    return {"ok": False, "reason": reason, "attempts": attempts}


def probe(timeout: float = 6.0) -> dict:
    """判定该用哪条通道：MCP 优先，MCP 假活则退到文件桥。"""
    port = mcp_port()
    mcp = mcp_handshake(port, timeout)

    beat = heartbeat()
    age = heartbeat_age(beat) if beat else None
    bridge_ready = bool(beat) and age is not None and age < 10 and not beat.get("isCompiling")

    if mcp.get("ok"):
        choice = "mcp"
        reason = "MCP 握手成功"
    elif bridge_ready:
        choice = "bridge"
        reason = f"MCP 不可用（{mcp.get('reason')}），文件桥就绪"
    else:
        choice = "none"
        reason = "MCP 与文件桥都不可用"

    return {
        "recommend": choice,
        "reason": reason,
        "mcp": {"port": port, **mcp},
        "bridge": {
            "ready": bridge_ready,
            "heartbeatAgeSeconds": None if age is None else round(age, 1),
            "isCompiling": None if beat is None else beat.get("isCompiling"),
            "armedAt": None if beat is None else beat.get("armedAt"),
        },
    }


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
    parser.add_argument(
        "--probe",
        action="store_true",
        help="探测 MCP 握手与桥心跳，判定该走哪条通道（MCP 优先）",
    )
    parser.add_argument(
        "--probe-timeout", type=float, default=6.0, help="--probe 的 MCP 握手超时秒数，默认 6"
    )
    parser.add_argument("--raw", action="store_true", help="只输出 result 字段")
    opts = parser.parse_args(argv)

    if opts.probe:
        report = probe(opts.probe_timeout)
        emit(report)
        return 0 if report["recommend"] != "none" else 2

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
