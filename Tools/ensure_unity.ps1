<#
.SYNOPSIS
    确保 Unity 编辑器已打开本项目，且 MCP bridge 端口（默认 8095）在监听。

.DESCRIPTION
    MCP 调用返回 "Command expired after 60000ms in queue" 时，九成是 Unity 没打开项目，
    或者正处于 assembly reload（脚本编译）窗口内——后者 server 会先停再自动重启。

    判断顺序：
      1. 端口已监听 + WebSocket 握手成功      -> READY
      2. 端口已监听 + 握手失败 + 有假死证据    -> SERVER_ZOMBIE（必须重启 Unity）
      3. 端口已监听 + 握手失败 + 无假死证据    -> PORT_ONLY（可能正在编译，稍后重试）
      4. 项目被锁 + Unity 进程在 + 端口未起    -> 宽限期轮询（覆盖 assembly reload），超时 -> NEED_MCP_START
      5. 其余（Unity 没开）                  -> 启动 Unity 打开项目，轮询端口 + 握手
      6. 全程失败                            -> 输出诊断（进程 / 端口占用 / Editor.log 尾部）

    为什么要握手而不只看端口：MCP Unity 在 assembly reload 后可能出现「旧 socket 仍占端口、
    新实例绑不上、旧实例不处理请求」的假死状态。此时 netstat 一切正常，客户端命令却全部超时。
    只有真握手一次才能区分。

.PARAMETER ProjectPath
    Unity 项目根目录。默认 E:\大创项目

.PARAMETER Port
    MCP Unity bridge 端口。默认从 ProjectSettings\McpUnitySettings.json 读取，读不到用 8095

.PARAMETER TimeoutSec
    启动 Unity 后等待端口就绪的最长秒数。默认 300

.PARAMETER GraceSec
    Unity 已开但端口未监听时，等待其自行恢复的宽限秒数（覆盖 assembly reload 窗口）。默认 60

.PARAMETER UnityRoot
    Unity 编辑器安装根目录。默认 E:\Unity

.PARAMETER Quiet
    只输出 STATUS 行，不打印过程信息。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\ensure_unity.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\ensure_unity.ps1 -GraceSec 120
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = 'E:\大创项目',
    [int]$Port = 0,
    [int]$TimeoutSec = 300,
    [int]$GraceSec = 60,
    [int]$ProbeTimeoutMs = 6000,
    [string]$UnityRoot = 'E:\Unity',
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$script:logLines = New-Object System.Collections.Generic.List[string]

function Write-Step($msg) {
    $line = "[ensure_unity] $msg"
    $script:logLines.Add($line)
    if (-not $Quiet) { Write-Host $line }
}

function Write-Result($status) {
    Write-Host "STATUS=$status"
}

# ---------- 端口探测（Get-NetTCPConnection 失败时回退 netstat） ----------
function Test-PortListening([int]$p) {
    try {
        $conns = Get-NetTCPConnection -State Listen -LocalPort $p -ErrorAction Stop
        if ($conns) { return $true }
        return $false
    } catch {
        $out = & netstat -ano 2>$null | Select-String -Pattern ":$p\s" | Select-String -Pattern 'LISTENING'
        if ($out) { return $true }
        return $false
    }
}

# ---------- 真实 WebSocket 握手探测 ----------
# 端口 LISTENING 不等于 server 可用：MCP Unity 在 assembly reload 后可能出现
# 「旧 socket 仍占端口、新实例绑不上、旧实例不处理请求」的假死状态。
# 此时 netstat 显示一切正常，但客户端命令全部超时。只有真正握手一次才能区分。
function Test-WebSocketHandshake([int]$p, [int]$timeoutMs) {
    $tokenPath = Join-Path $ProjectPath 'Library\McpUnity\bridge-token'
    if (-not (Test-Path $tokenPath)) {
        Write-Step '未找到 Library\McpUnity\bridge-token，跳过握手探测（按端口可用处理）'
        return $true
    }

    $token = (Get-Content $tokenPath -Raw).Trim()
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("mcp-unity:$token"))

    # 端口可能只绑 IPv6 ::1，也可能只绑 IPv4，两个都试。
    $hosts = @('[::1]', '127.0.0.1')
    foreach ($h in $hosts) {
        $ws = New-Object System.Net.WebSockets.ClientWebSocket
        try {
            $ws.Options.SetRequestHeader('Authorization', "Basic $auth")
            $ws.Options.SetRequestHeader('X-Client-Name', 'ensure-unity-probe')
            $cts = New-Object System.Threading.CancellationTokenSource
            $cts.CancelAfter($timeoutMs)
            $uri = [Uri]('ws://{0}:{1}/McpUnity' -f $h, $p)
            $task = $ws.ConnectAsync($uri, $cts.Token)
            $done = $task.Wait($timeoutMs + 2000)
            if ($done -and $ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                Write-Step ("握手成功: {0}" -f $uri)
                return $true
            }
            Write-Step ("握手未完成: {0}（state={1}）" -f $uri, $ws.State)
        } catch {
            $inner = ''
            if ($_.Exception.InnerException) { $inner = $_.Exception.InnerException.Message }
            Write-Step ("握手失败: {0} -> {1}" -f $h, $inner)
        } finally {
            try { $ws.Dispose() } catch { }
        }
    }
    return $false
}

# ---------- 识别 Unity 主进程 ----------
# Unity.exe 会 fork 出 AssetImportWorker 子进程，它们也是 Unity.exe。
# 判断「项目是否已被打开」必须只看主进程，否则会把导入子进程误当成编辑器。
function Get-MainUnityProcesses {
    $all = @(Get-Process Unity -ErrorAction SilentlyContinue)
    if ($all.Count -eq 0) { return @() }
    $result = @()
    foreach ($pr in $all) {
        $cmd = ''
        try {
            $ci = Get-CimInstance Win32_Process -Filter ("ProcessId={0}" -f $pr.Id) -ErrorAction SilentlyContinue
            if ($ci -and $ci.CommandLine) { $cmd = $ci.CommandLine }
        } catch { }
        if ($cmd -match 'AssetImportWorker') { continue }
        if ($cmd -match '-batchmode') { continue }
        $result += $pr
    }
    return $result
}

# ---------- 假死判定 ----------
# 依据：CLOSE_WAIT 连接堆积 + Editor.log 近期反复报端口占用。
function Test-ServerZombie([int]$p) {
    $reasons = @()

    try {
        $cw = @(Get-NetTCPConnection -LocalPort $p -State CloseWait -ErrorAction SilentlyContinue)
        if ($cw.Count -ge 2) { $reasons += ("CLOSE_WAIT 连接 {0} 条（连接未正常关闭）" -f $cw.Count) }
    } catch { }

    $editorLog = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
    if (Test-Path $editorLog) {
        $recent = Get-Content $editorLog -Tail 200 -Encoding UTF8 -ErrorAction SilentlyContinue
        $pattern = "Port {0} is already in use" -f $p
        $hits = @($recent | Select-String -Pattern $pattern -SimpleMatch)
        if ($hits.Count -gt 0) {
            $reasons += ("Editor.log 近期出现 {0} 次「{1}」——旧监听未释放，新实例绑不上端口" -f $hits.Count, $pattern)
        }
    }

    if ($reasons.Count -gt 0) {
        foreach ($r in $reasons) { Write-Step ("假死证据: " + $r) }
        return $true
    }
    return $false
}

# ---------- 诊断输出 ----------
function Write-Diagnostics([int]$p) {
    Write-Step '---------- 诊断 ----------'

    $procs = Get-Process Unity -ErrorAction SilentlyContinue
    if ($procs) {
        $mainCount = 0
        foreach ($pr in $procs) {
            $startText = 'unknown'
            try { $startText = $pr.StartTime.ToString('HH:mm:ss') } catch { }

            # 区分主编辑器与资产导入子进程：AssetImportWorker 是主进程 fork 出来的，属正常现象，
            # 不要误判成「同一项目被多个 Unity 打开」。
            $role = '编辑器主进程'
            $cmd = ''
            try {
                $ci = Get-CimInstance Win32_Process -Filter ("ProcessId={0}" -f $pr.Id) -ErrorAction SilentlyContinue
                if ($ci -and $ci.CommandLine) { $cmd = $ci.CommandLine }
            } catch { }
            if ($cmd -match 'AssetImportWorker') {
                $role = '资产导入子进程（正常）'
            } elseif ($cmd -match '-batchmode') {
                $role = 'batch 模式进程'
            } else {
                $mainCount++
            }

            $memMb = [math]::Round($pr.WorkingSet64 / 1MB)
            $head = "Unity 进程: PID={0} 启动={1} 内存={2}MB" -f $pr.Id, $startText, $memMb
            Write-Step $head
            Write-Step ("    角色: " + $role)
        }
        if ($mainCount -gt 1) {
            Write-Step "警告: 检测到 $mainCount 个编辑器主进程，同一项目被多实例打开会损坏 Library，建议只保留一个。"
        }
    } else {
        Write-Step 'Unity 进程: 无'
    }

    $holder = $null
    try {
        $holder = Get-NetTCPConnection -LocalPort $p -ErrorAction SilentlyContinue |
                  Where-Object { $_.State -eq 'Listen' } | Select-Object -First 1
    } catch { }
    if ($holder) {
        Write-Step ("端口 {0} 已被 PID={1} 占用" -f $p, $holder.OwningProcess)
    } else {
        Write-Step ("端口 {0} 无监听" -f $p)
    }

    $lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (Test-Path $lockFile) {
        Write-Step 'Temp\UnityLockfile 存在（项目被 Unity 持有）'
    } else {
        Write-Step 'Temp\UnityLockfile 不存在（项目未被 Unity 打开）'
    }

    $editorLog = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
    if (Test-Path $editorLog) {
        Write-Step "Editor.log 尾部（$editorLog）："
        # Editor.log 是 UTF-8，PowerShell 5.1 默认按 ANSI 读会出乱码，必须显式指定编码。
        Get-Content $editorLog -Tail 15 -Encoding UTF8 -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Step ('  | ' + $_.TrimEnd())
        }
    } else {
        Write-Step 'Editor.log 不存在'
    }
    Write-Step '---------- 诊断结束 ----------'
}

function Save-Log {
    $dir = Join-Path $ProjectPath 'Logs'
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $file = Join-Path $dir ('ensure_unity_{0}.log' -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
    try {
        [System.IO.File]::WriteAllLines($file, $script:logLines, (New-Object System.Text.UTF8Encoding($false)))
        if (-not $Quiet) { Write-Host "[ensure_unity] 日志: $file" }
    } catch { }
}

# ---------- 1. 解析端口 ----------
if ($Port -le 0) {
    $settingsPath = Join-Path $ProjectPath 'ProjectSettings\McpUnitySettings.json'
    $Port = 8095
    if (Test-Path $settingsPath) {
        try {
            $cfg = Get-Content $settingsPath -Raw | ConvertFrom-Json
            if ($cfg.Port) { $Port = [int]$cfg.Port }
        } catch {
            Write-Step '读取 McpUnitySettings.json 失败，回退端口 8095'
        }
    }
}
Write-Step "目标端口: $Port"

if (Test-PortListening $Port) {
    if (Test-WebSocketHandshake $Port $ProbeTimeoutMs) {
        Write-Result 'READY'
        Write-Step 'MCP bridge 已监听且握手成功，无需操作。'
        Save-Log
        exit 0
    }

    Write-Step '端口在监听，但 WebSocket 握手无响应。'
    if (Test-ServerZombie $Port) {
        Write-Result 'SERVER_ZOMBIE'
        Write-Step 'MCP Unity server 已假死：旧监听占着端口，新实例绑不上，客户端命令会全部超时。'
        Write-Step '脚本无法修复此状态，必须重启 Unity 编辑器（关闭 Unity，再重新打开项目）。'
        Write-Diagnostics $Port
        Save-Log
        exit 6
    }

    Write-Result 'PORT_ONLY'
    Write-Step '端口在监听但握手失败，且未找到假死证据。可能正在编译或刚完成重启，稍后重试。'
    Write-Diagnostics $Port
    Save-Log
    exit 7
}

# ---------- 2. 项目是否已被某个 Unity 实例打开 ----------
$lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
$projectLocked = Test-Path $lockFile
$mainUnityProcs = @(Get-MainUnityProcesses)

# 只要有主进程在，就认为「正在启动或已在运行」，绝不能另起一个实例——
# 两个实例打开同一项目会损坏 Library。锁文件只作辅助参考（刚启动时可能还没写）。
if ($mainUnityProcs.Count -gt 0) {
    Write-Step "检测到 Unity 主进程 $($mainUnityProcs.Count) 个，锁文件: $projectLocked，端口 $Port 未监听。"
    Write-Step "可能是 assembly reload（脚本编译）窗口内——server 会先停再自动重启。宽限等待 ${GraceSec}s。"

    $waited = 0
    $interval = 3
    while ($waited -lt $GraceSec) {
        Start-Sleep -Seconds $interval
        $waited += $interval
        if (Test-PortListening $Port) {
            # 端口起来还不够，要确认握手真的通，否则可能落进假死状态
            if (Test-WebSocketHandshake $Port $ProbeTimeoutMs) {
                Write-Result 'READY'
                Write-Step "端口与握手均就绪（宽限期第 ${waited}s），编译完成后自动恢复，无需手动干预。"
                Save-Log
                exit 0
            }
            Write-Step '端口已起但握手不通，继续等待...'
        }
        if (($waited % 15) -eq 0) { Write-Step "宽限等待中... ${waited}s" }
    }

    Write-Result 'NEED_MCP_START'
    Write-Step "宽限 ${GraceSec}s 后 MCP bridge 仍不可用。请在 Unity 中执行："
    Write-Step '  Tools -> MCP Unity -> Server Window -> Start Server'
    Write-Step '若 Server Window 显示已在运行却仍无响应，属假死，需重启 Unity 编辑器。'
    Write-Diagnostics $Port
    Save-Log
    exit 2
}

# ---------- 3. 定位 Unity.exe ----------
$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $versionFile)) {
    Write-Result 'FAILED'
    Write-Step '找不到 ProjectSettings\ProjectVersion.txt，无法确定 Unity 版本。'
    Save-Log
    exit 3
}
$match = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
if (-not $match) {
    Write-Result 'FAILED'
    Write-Step 'ProjectVersion.txt 中未解析到 m_EditorVersion。'
    Save-Log
    exit 3
}
$version = $match.Matches[0].Groups[1].Value.Trim()
Write-Step "项目 Unity 版本: $version"

$unityExe = Join-Path $UnityRoot "$version\Editor\Unity.exe"
if (-not (Test-Path $unityExe)) {
    Write-Step "默认路径不存在: $unityExe，在 $UnityRoot 下搜索..."
    $found = Get-ChildItem -Path $UnityRoot -Filter 'Unity.exe' -Recurse -Depth 3 -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -like "*$version*" } |
             Select-Object -First 1
    if ($found) {
        $unityExe = $found.FullName
    } else {
        Write-Result 'FAILED'
        Write-Step "未找到版本 $version 的 Unity.exe。请确认 -UnityRoot 或安装该版本。"
        Save-Log
        exit 3
    }
}
Write-Step "Unity 可执行文件: $unityExe"

# ---------- 4. 启动 ----------
# 残留锁文件会让新启动的 Unity 认为项目仍被占用而直接退出（实测踩过：
# 上一实例被强杀后 Temp\UnityLockfile 残留，之后两次启动都在几十秒内自行退出）。
# 只有在确认没有任何 Unity 进程时才清理。
$lockPath = Join-Path $ProjectPath 'Temp\UnityLockfile'
if ((Test-Path $lockPath) -and -not (Get-Process Unity -ErrorAction SilentlyContinue)) {
    try {
        Remove-Item $lockPath -Force -ErrorAction Stop
        Write-Step '清理残留的 Temp\UnityLockfile（无 Unity 进程持有）'
    } catch {
        Write-Step "清理残留锁文件失败: $($_.Exception.Message)"
    }
}

Write-Step "启动 Unity 并打开项目: $ProjectPath"
$unityArgs = '-projectPath "{0}"' -f $ProjectPath
$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru
Write-Step "已拉起进程 PID=$($proc.Id)，等待端口 + 握手（上限 ${TimeoutSec}s）"

# ---------- 5. 轮询 ----------
$elapsed = 0
$interval = 3
while ($elapsed -lt $TimeoutSec) {
    Start-Sleep -Seconds $interval
    $elapsed += $interval
    if (Test-PortListening $Port) {
        # 端口起来不算成功，握手通了才算——否则可能落进假死状态
        if (Test-WebSocketHandshake $Port $ProbeTimeoutMs) {
            Write-Result 'READY'
            Write-Step "端口 $Port 已就绪且握手通过，耗时 ${elapsed}s。"
            Save-Log
            exit 0
        }
        Write-Step "端口已起但握手不通，继续等待...（${elapsed}s）"
    }
    if ($proc.HasExited) {
        Write-Result 'FAILED'
        Write-Step "Unity 进程已退出（ExitCode=$($proc.ExitCode)），MCP bridge 始终未就绪。"
        Write-Step '常见原因：残留锁文件、许可证失效、项目被其他实例占用。见下方诊断。'
        Write-Diagnostics $Port
        Save-Log
        exit 4
    }
    if (($elapsed % 30) -eq 0) { Write-Step "等待中... ${elapsed}s" }
}

Write-Result 'TIMEOUT'
Write-Step "等待 ${TimeoutSec}s 超时，端口 $Port 仍未监听。Unity 可能仍在首次导入资源（本项目 7000+ 文件，首开可达数分钟）。"
Write-Diagnostics $Port
Save-Log
exit 5
