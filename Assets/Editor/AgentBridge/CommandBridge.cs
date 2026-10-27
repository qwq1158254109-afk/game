// AgentBridge —— 零网络连接的 Editor 命令桥。
//
// 设计动机：MCP Unity（Packages/mcp-unity v1.5.0）把服务端跑在 Editor 进程内的 WebSocket 上，
// 每次 assembly reload 都要重绑端口，绑不上就进入「端口假活、请求全部超时」状态
// （见 McpUnityServer.cs L319-323 / L397-411 / L447-455，上游 issue #141）。
// 本桥不用 socket：客户端把 JSON 命令写进 Temp/agent-bridge/cmd/，
// Editor 侧在 EditorApplication.update 里轮询执行，把结果写进 Temp/agent-bridge/result/。
// domain reload 后 [InitializeOnLoad] 静态构造会重新注册轮询，因此不存在「重绑失败」这种状态。
//
// 协议
//   命令  Temp/agent-bridge/cmd/<id>.json   { "id": "...", "command": "scene_info", "args": { ... } }
//   结果  Temp/agent-bridge/result/<id>.json { "id","ok","result","error","durationMs","handledAt" }
//   清单  Temp/agent-bridge/commands.json   { "version", "commands": [ { name, description, args } ] }
//   心跳  Temp/agent-bridge/heartbeat.json  { "armedAt","lastTick","isCompiling","isPlaying","unityVersion" }
//
// 客户端见 tools/unity_cmd.py。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AgentBridge
{
    private const string BridgeVersion = "1.0.0";
    private const string RootFolderName = "agent-bridge";
    private const double PollIntervalSeconds = 0.2;
    private const double HeartbeatIntervalSeconds = 2.0;
    private const int MaxCommandsPerTick = 4;
    private const int ConsoleRingSize = 500;
    private const int StackTraceLimit = 800;
    private const string LogPrefix = "[AgentBridge]";

    // 拒绝执行的菜单项片段：防止误触退出、崩溃类入口。
    private static readonly string[] BlockedMenuFragments = { "Quit", "Exit", "Crash" };

    private static readonly Queue<JObject> ConsoleRing = new Queue<JObject>(ConsoleRingSize);
    private static readonly Dictionary<string, CommandSpec> Commands =
        new Dictionary<string, CommandSpec>(StringComparer.Ordinal);

    private static string _root;
    private static string _cmdDir;
    private static string _resultDir;
    private static string _logPath;
    private static double _nextPollAt;
    private static double _nextHeartbeatAt;
    private static string _armedAt;
    private static bool _logHooked;

    private sealed class CommandSpec
    {
        public string Name;
        public string Description;
        public string Args;
        public Func<JObject, JObject> Handler;
    }

    static AgentBridge()
    {
        try
        {
            Init();
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LogPrefix} init failed: {ex}");
        }
    }

    // ---------------------------------------------------------------- 生命周期

    private static void Init()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        _root = Path.Combine(projectRoot, "Temp", RootFolderName);
        _cmdDir = Path.Combine(_root, "cmd");
        _resultDir = Path.Combine(_root, "result");
        _logPath = Path.Combine(_root, "bridge.log");

        Directory.CreateDirectory(_cmdDir);
        Directory.CreateDirectory(_resultDir);

        _armedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        PruneStaleResults(TimeSpan.FromDays(1));

        RegisterCommands();
        WriteCommandManifest();

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        if (!_logHooked)
        {
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;
            _logHooked = true;
        }

        WriteLog($"armed (version {BridgeVersion}, {Commands.Count} commands)");
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;

        if (now < _nextPollAt)
        {
            return;
        }

        _nextPollAt = now + PollIntervalSeconds;

        // 编译 / 资产导入期间不处理命令：此时执行 Editor API 不可靠。
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        try
        {
            ProcessPending();
        }
        catch (Exception ex)
        {
            WriteLog("tick error: " + ex);
        }

        if (now >= _nextHeartbeatAt)
        {
            _nextHeartbeatAt = now + HeartbeatIntervalSeconds;
            WriteHeartbeat();
        }
    }

    // ---------------------------------------------------------------- 命令收发

    private static void ProcessPending()
    {
        string[] files = Directory.GetFiles(_cmdDir, "*.json");
        if (files.Length == 0)
        {
            return;
        }

        Array.Sort(files, StringComparer.Ordinal);
        int handled = 0;

        foreach (string file in files)
        {
            if (handled >= MaxCommandsPerTick)
            {
                break;
            }

            string raw;
            try
            {
                raw = File.ReadAllText(file, Encoding.UTF8);
            }
            catch (IOException)
            {
                continue; // 客户端仍在写，下一轮再取。
            }

            JObject envelope;
            try
            {
                envelope = JObject.Parse(raw);
            }
            catch (Exception ex)
            {
                WriteLog($"bad json in {Path.GetFileName(file)}: {ex.Message}");
                SafeDelete(file);
                continue;
            }

            string id = (string)envelope["id"] ?? Path.GetFileNameWithoutExtension(file);
            string name = (string)envelope["command"];
            JObject args = envelope["args"] as JObject ?? new JObject();

            handled++;
            JObject result = Execute(id, name, args);
            WriteResult(id, result);
            SafeDelete(file);
        }
    }

    private static JObject Execute(string id, string name, JObject args)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        JObject envelope = new JObject
        {
            ["id"] = id,
            ["command"] = name,
            ["handledAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        try
        {
            if (string.IsNullOrEmpty(name) || !Commands.TryGetValue(name, out CommandSpec spec))
            {
                throw new ArgumentException(
                    $"unknown command '{name}'. available: {string.Join(", ", Commands.Keys.OrderBy(k => k))}");
            }

            JObject payload = spec.Handler(args);
            envelope["ok"] = true;
            envelope["result"] = payload;
        }
        catch (Exception ex)
        {
            envelope["ok"] = false;
            envelope["error"] = $"{ex.GetType().Name}: {ex.Message}";
            envelope["stack"] = Trim(ex.StackTrace, StackTraceLimit);
            WriteLog($"command '{name}' failed: {ex}");
        }

        watch.Stop();
        envelope["durationMs"] = watch.ElapsedMilliseconds;
        return envelope;
    }

    private static void WriteResult(string id, JObject payload)
    {
        string path = Path.Combine(_resultDir, id + ".json");
        AtomicWrite(path, payload.ToString(Formatting.Indented));
    }

    private static void WriteHeartbeat()
    {
        JObject beat = new JObject
        {
            ["armedAt"] = _armedAt,
            ["lastTick"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["isCompiling"] = EditorApplication.isCompiling,
            ["isUpdating"] = EditorApplication.isUpdating,
            ["isPlaying"] = EditorApplication.isPlaying,
            ["unityVersion"] = Application.unityVersion,
            ["projectPath"] = Directory.GetCurrentDirectory(),
            ["bridgeVersion"] = BridgeVersion
        };
        AtomicWrite(Path.Combine(_root, "heartbeat.json"), beat.ToString(Formatting.Indented));
    }

    private static void WriteCommandManifest()
    {
        var list = new JArray();
        foreach (CommandSpec spec in Commands.Values.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            list.Add(new JObject
            {
                ["name"] = spec.Name,
                ["description"] = spec.Description,
                ["args"] = spec.Args
            });
        }

        JObject manifest = new JObject
        {
            ["version"] = BridgeVersion,
            ["generatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["commands"] = list
        };
        AtomicWrite(Path.Combine(_root, "commands.json"), manifest.ToString(Formatting.Indented));
    }

    private static void AtomicWrite(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tmp, path);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 忽略：下一轮还会尝试。
        }
    }

    private static void PruneStaleResults(TimeSpan maxAge)
    {
        try
        {
            DateTime cutoff = DateTime.UtcNow - maxAge;
            foreach (string file in Directory.GetFiles(_resultDir, "*.json"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    SafeDelete(file);
                }
            }
        }
        catch (IOException)
        {
            // 忽略。
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            File.AppendAllText(_logPath,
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch (IOException)
        {
            // 忽略：日志失败不能影响命令执行。
        }
    }

    private static string Trim(string text, int limit)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= limit ? text : text.Substring(0, limit) + "…";
    }

    // ---------------------------------------------------------------- 控制台环形缓冲

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (condition != null && condition.StartsWith(LogPrefix, StringComparison.Ordinal))
        {
            return;
        }

        lock (ConsoleRing)
        {
            if (ConsoleRing.Count >= ConsoleRingSize)
            {
                ConsoleRing.Dequeue();
            }

            ConsoleRing.Enqueue(new JObject
            {
                ["type"] = type.ToString(),
                ["message"] = Trim(condition, 2000),
                ["stack"] = type == LogType.Log ? null : Trim(stackTrace, StackTraceLimit),
                ["time"] = DateTime.Now.ToString("HH:mm:ss")
            });
        }
    }

    // ---------------------------------------------------------------- 命令注册

    private static void RegisterCommands()
    {
        Commands.Clear();

        Add("ping", "确认桥在运行：版本、项目路径、编译/播放状态", "{}", _ =>
        {
            return new JObject
            {
                ["bridgeVersion"] = BridgeVersion,
                ["unityVersion"] = Application.unityVersion,
                ["projectPath"] = Directory.GetCurrentDirectory(),
                ["armedAt"] = _armedAt,
                ["isCompiling"] = EditorApplication.isCompiling,
                ["isPlaying"] = EditorApplication.isPlaying,
                ["commandCount"] = Commands.Count
            };
        });

        Add("commands", "列出所有可用命令", "{}", _ =>
        {
            var list = new JArray();
            foreach (CommandSpec spec in Commands.Values.OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                list.Add(new JObject
                {
                    ["name"] = spec.Name,
                    ["description"] = spec.Description,
                    ["args"] = spec.Args
                });
            }

            return new JObject { ["commands"] = list };
        });

        Add("scene_info", "当前激活场景与全部已加载场景", "{}", _ =>
        {
            Scene active = SceneManager.GetActiveScene();
            var loaded = new JArray();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                loaded.Add(new JObject
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["isLoaded"] = scene.isLoaded,
                    ["isDirty"] = scene.isDirty,
                    ["rootCount"] = scene.isLoaded ? scene.GetRootGameObjects().Length : 0
                });
            }

            return new JObject
            {
                ["active"] = new JObject
                {
                    ["name"] = active.name,
                    ["path"] = active.path,
                    ["isDirty"] = active.isDirty,
                    ["isLoaded"] = active.isLoaded,
                    ["rootCount"] = active.isLoaded ? active.GetRootGameObjects().Length : 0
                },
                ["loaded"] = loaded
            };
        });

        Add("hierarchy", "场景层级树",
            "{ maxDepth?: int = 3, root?: string }", args =>
        {
            int maxDepth = args["maxDepth"]?.Value<int>() ?? 3;
            string rootPath = (string)args["root"];
            var nodes = new JArray();

            IEnumerable<GameObject> roots = rootPath == null
                ? GetRootObjects()
                : new[] { ResolveGameObject(rootPath) };

            foreach (GameObject go in roots)
            {
                nodes.Add(DescribeTree(go, maxDepth));
            }

            return new JObject { ["nodes"] = nodes };
        });

        Add("find", "按名称或层级路径查对象，返回组件列表",
            "{ name?: string, path?: string }", args =>
        {
            string path = (string)args["path"];
            string name = (string)args["name"];
            var hits = new JArray();

            if (path != null)
            {
                hits.Add(DescribeObject(ResolveGameObject(path)));
            }
            else if (name != null)
            {
                foreach (GameObject go in EnumerateAll())
                {
                    if (string.Equals(go.name, name, StringComparison.Ordinal))
                    {
                        hits.Add(DescribeObject(go));
                    }
                }
            }
            else
            {
                throw new ArgumentException("find 需要 name 或 path 之一");
            }

            return new JObject { ["count"] = hits.Count, ["hits"] = hits };
        });

        Add("console", "读取 Console 最近日志（环形缓冲，上限 500 条）",
            "{ count?: int = 50, types?: string[] = [\"Error\",\"Assert\",\"Warning\"] }", args =>
        {
            int count = args["count"]?.Value<int>() ?? 50;
            var typeFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            JToken types = args["types"];
            if (types is JArray arr)
            {
                foreach (JToken t in arr)
                {
                    typeFilter.Add((string)t);
                }
            }
            else
            {
                typeFilter.UnionWith(new[] { "Error", "Assert", "Warning" });
            }

            List<JObject> snapshot;
            lock (ConsoleRing)
            {
                snapshot = ConsoleRing.ToList();
            }

            var filtered = snapshot
                .Where(e => typeFilter.Contains((string)e["type"]))
                .ToList();

            var tail = filtered.Skip(Math.Max(0, filtered.Count - count)).ToList();
            return new JObject
            {
                ["total"] = snapshot.Count,
                ["matched"] = filtered.Count,
                ["entries"] = new JArray(tail)
            };
        });

        Add("menu", "执行 Editor 菜单项（如 \"Tools/Capture Room Screenshot\"）",
            "{ path: string }", args =>
        {
            string path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("menu 需要 path");
            }

            string blocked = BlockedMenuFragments.FirstOrDefault(
                f => path.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
            if (blocked != null)
            {
                throw new InvalidOperationException($"菜单路径含禁用片段 '{blocked}'，拒绝执行：{path}");
            }

            bool ok = EditorApplication.ExecuteMenuItem(path);
            return new JObject { ["path"] = path, ["executed"] = ok };
        });

        Add("play", "播放模式控制",
            "{ action: \"enter\" | \"exit\" | \"pause\" | \"resume\" }", args =>
        {
            string action = (string)args["action"];
            switch (action)
            {
                case "enter":
                    EditorApplication.isPlaying = true;
                    break;
                case "exit":
                    EditorApplication.isPlaying = false;
                    break;
                case "pause":
                    EditorApplication.isPaused = true;
                    break;
                case "resume":
                    EditorApplication.isPaused = false;
                    break;
                default:
                    throw new ArgumentException("action 必须是 enter/exit/pause/resume");
            }

            return new JObject { ["action"] = action, ["isPlaying"] = EditorApplication.isPlaying };
        });

        Add("open_scene", "按资产路径打开场景",
            "{ path: string }", args =>
        {
            string path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("open_scene 需要 path");
            }

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("path 必须以 Assets/ 开头");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("场景不存在", path);
            }

            bool dirty = SceneManager.GetActiveScene().isDirty;
            if (dirty && !(args["discardChanges"]?.Value<bool>() ?? false))
            {
                throw new InvalidOperationException("当前场景有未保存改动，传 discardChanges=true 才会切换");
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            return new JObject { ["opened"] = path };
        });

        Add("save_scene", "保存当前场景",
            "{ path?: string }", args =>
        {
            string path = (string)args["path"];
            Scene active = SceneManager.GetActiveScene();
            bool ok = path == null
                ? UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active)
                : UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active, path);
            return new JObject { ["saved"] = ok, ["path"] = active.path };
        });

        Add("capture_view", "把场景相机渲染成 PNG（编辑模式可用，默认取 Camera.main）",
            "{ path: string, width?: int = 1280, height?: int = 720, camera?: string }", args =>
        {
            string path = (string)args["path"];
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("capture_view 需要 path");
            }

            int width = args["width"]?.Value<int>() ?? 1280;
            int height = args["height"]?.Value<int>() ?? 720;
            Camera cam = ResolveCamera((string)args["camera"]);

            RenderTexture rt = new RenderTexture(width, height, 24);
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = cam.aspect;
            Texture2D tex = null;

            try
            {
                cam.aspect = (float)width / height;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                string full = Path.GetFullPath(path);
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(full, tex.EncodeToPNG());
                return new JObject
                {
                    ["path"] = full,
                    ["width"] = width,
                    ["height"] = height,
                    ["camera"] = cam.name
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
                cam.targetTexture = previousTarget;
                cam.aspect = previousAspect;
                if (tex != null)
                {
                    Object.DestroyImmediate(tex);
                }

                rt.Release();
                Object.DestroyImmediate(rt);
            }
        });

        Add("recompile", "刷新资产并请求脚本编译（会触发 domain reload，结果可能来不及回写）",
            "{}", _ =>
        {
            AssetDatabase.Refresh();
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            return new JObject { ["requested"] = true };
        });
    }

    private static void Add(string name, string description, string args, Func<JObject, JObject> handler)
    {
        Commands[name] = new CommandSpec
        {
            Name = name,
            Description = description,
            Args = args,
            Handler = handler
        };
    }

    // ---------------------------------------------------------------- 对象工具

    private static IEnumerable<GameObject> GetRootObjects()
    {
        Scene active = SceneManager.GetActiveScene();
        return active.isLoaded ? active.GetRootGameObjects() : Enumerable.Empty<GameObject>();
    }

    private static IEnumerable<GameObject> EnumerateAll()
    {
        foreach (GameObject root in GetRootObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                yield return t.gameObject;
            }
        }
    }

    private static GameObject ResolveGameObject(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName))
        {
            throw new ArgumentException("对象路径为空");
        }

        if (pathOrName.Contains("/"))
        {
            string trimmed = pathOrName.TrimStart('/');
            foreach (GameObject root in GetRootObjects())
            {
                if (string.Equals(root.name, trimmed.Split('/')[0], StringComparison.Ordinal))
                {
                    Transform found = root.transform.Find(trimmed);
                    if (found != null)
                    {
                        return found.gameObject;
                    }
                }
            }

            throw new ArgumentException($"按路径找不到对象：{pathOrName}");
        }

        GameObject match = EnumerateAll().FirstOrDefault(
            g => string.Equals(g.name, pathOrName, StringComparison.Ordinal));
        if (match == null)
        {
            throw new ArgumentException($"按名称找不到对象：{pathOrName}");
        }

        return match;
    }

    /// <summary>
    /// 按名称/路径取相机；未指定时依次回退到 Camera.main、场景内第一个启用的相机。
    /// Scene 视图相机不能用 Camera.Render() 出图（Unity 走自己的渲染路径），所以这里只认场景相机。
    /// </summary>
    private static Camera ResolveCamera(string nameOrPath)
    {
        if (!string.IsNullOrEmpty(nameOrPath))
        {
            GameObject go = ResolveGameObject(nameOrPath);
            Camera named = go.GetComponent<Camera>();
            if (named == null)
            {
                throw new ArgumentException($"对象上没有 Camera 组件：{nameOrPath}");
            }

            return named;
        }

        Camera main = Camera.main;
        if (main != null)
        {
            return main;
        }

        Camera fallback = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)
            .FirstOrDefault(c => c.enabled && c.cameraType != CameraType.SceneView);
        if (fallback == null)
        {
            throw new InvalidOperationException("场景里没有可用的相机（也找不到 Camera.main）");
        }

        return fallback;
    }

    private static JObject DescribeTree(GameObject go, int depthLeft)    {
        JObject node = new JObject
        {
            ["name"] = go.name,
            ["active"] = go.activeSelf,
            ["components"] = new JArray(go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name))
        };

        if (depthLeft > 0)
        {
            var children = new JArray();
            foreach (Transform child in go.transform)
            {
                children.Add(DescribeTree(child.gameObject, depthLeft - 1));
            }

            if (children.Count > 0)
            {
                node["children"] = children;
            }
        }
        else if (go.transform.childCount > 0)
        {
            node["childCount"] = go.transform.childCount;
        }

        return node;
    }

    private static JObject DescribeObject(GameObject go)
    {
        Transform t = go.transform;
        return new JObject
        {
            ["name"] = go.name,
            ["path"] = GetHierarchyPath(t),
            ["activeSelf"] = go.activeSelf,
            ["activeInHierarchy"] = go.activeInHierarchy,
            ["tag"] = go.tag,
            ["layer"] = LayerMask.LayerToName(go.layer),
            ["position"] = new JObject
            {
                ["x"] = t.position.x, ["y"] = t.position.y, ["z"] = t.position.z
            },
            ["components"] = new JArray(go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name))
        };
    }

    private static string GetHierarchyPath(Transform t)
    {
        var parts = new List<string>();
        Transform cursor = t;
        while (cursor != null)
        {
            parts.Insert(0, cursor.name);
            cursor = cursor.parent;
        }

        return string.Join("/", parts);
    }
}
