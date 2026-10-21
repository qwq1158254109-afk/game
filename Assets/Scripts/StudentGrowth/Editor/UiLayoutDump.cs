using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Editor
{
    /// <summary>
    /// 运行时/编辑器 UI 布局 dump 工具 —— 用于在没有 MCP 的情况下核查 UI 布局。
    ///
    /// 用途：输出指定场景里某个节点子树的完整 RectTransform 数据 + 组件清单，
    /// 用于定位"元素被裁切 / 位置不对 / 缺组件"这类问题。
    ///
    /// batch 调用：
    ///   -executeMethod StudentGrowth.Editor.UiLayoutDump.DumpStageHubTimeHud
    /// </summary>
    public static class UiLayoutDump
    {
        private const string OutPath = "Docs/UI_LAYOUT_DUMP.md";

        [MenuItem("Tools/Student Growth/UI/Dump StageHub Time HUD Layout")]
        public static void DumpStageHubTimeHud()
        {
            DumpScene(
                "Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity",
                "Canvas_StageHubSelection/Layer_04_TopLeftTimeInfo",
                "StageHub 左上时间 HUD");
        }

        [MenuItem("Tools/Student Growth/UI/Dump All StageHub Layers Layout")]
        public static void DumpAllStageHubLayers()
        {
            DumpScene(
                "Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity",
                "Canvas_StageHubSelection",
                "StageHub 全部层级");
        }

        public static void DumpScene(string scenePath, string rootPath, string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# UI Layout Dump — " + title);
            sb.AppendLine();
            sb.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("场景：" + scenePath);
            sb.AppendLine("根节点路径：" + rootPath);
            sb.AppendLine();

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                sb.AppendLine("**场景打开失败**");
                WriteAndExit(sb);
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            GameObject target = null;
            for (int i = 0; i < roots.Length; i++)
            {
                // rootPath 的首段是场景根对象名，其余是子路径。
                // 不能直接用 roots[i].transform.Find(rootPath)：Find 只接受相对路径。
                string[] segs = rootPath.Split('/');
                if (roots[i].name != segs[0])
                {
                    continue;
                }

                Transform cur = roots[i].transform;
                bool ok = true;
                for (int s = 1; s < segs.Length; s++)
                {
                    cur = cur.Find(segs[s]);
                    if (cur == null)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    target = cur.gameObject;
                    break;
                }
            }

            if (target == null)
            {
                sb.AppendLine("**未找到节点：" + rootPath + "**");
                sb.AppendLine();
                sb.AppendLine("场景根对象清单：");
                for (int i = 0; i < roots.Length; i++)
                {
                    sb.AppendLine("- " + roots[i].name);
                }

                WriteAndExit(sb);
                return;
            }

            // 输出 Canvas 的缩放信息（判断是否 CanvasScaler 引起偏移）
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                sb.AppendLine("## Canvas");
                sb.AppendLine();
                sb.AppendLine("| 项 | 值 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 名称 | " + canvas.name + " |");
                sb.AppendLine("| RenderMode | " + canvas.renderMode + " |");
                RectTransform crt = canvas.transform as RectTransform;
                if (crt != null)
                {
                    sb.AppendLine("| Canvas sizeDelta | " + Fmt(crt.sizeDelta) + " |");
                    sb.AppendLine("| Canvas rect | (" + crt.rect.width.ToString("0.##") + ","
                        + crt.rect.height.ToString("0.##") + ") |");
                    sb.AppendLine("| Canvas localScale | " + Fmt(crt.localScale) + " |");
                }

                if (scaler != null)
                {
                    sb.AppendLine("| Scaler mode | " + scaler.uiScaleMode + " |");
                    sb.AppendLine("| ReferenceResolution | " + Fmt(scaler.referenceResolution) + " |");
                    sb.AppendLine("| MatchWidthOrHeight | " + scaler.matchWidthOrHeight + " |");
                }

                sb.AppendLine();
            }

            sb.AppendLine("## 节点树");
            sb.AppendLine();
            sb.AppendLine("| 深度 | 节点 | anchorMin | anchorMax | pivot | anchoredPos | sizeDelta | 世界rect | 组件 |");
            sb.AppendLine("| ---: | --- | --- | --- | --- | --- | --- | --- | --- |");
            Walk(target.transform, 0, sb);

            WriteAndExit(sb);
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            RectTransform rt = t as RectTransform;
            string indent = new string('　', depth);

            string anchorMin = "-";
            string anchorMax = "-";
            string pivot = "-";
            string anchoredPos = "-";
            string sizeDelta = "-";
            string worldRect = "-";
            if (rt != null)
            {
                anchorMin = Fmt(rt.anchorMin);
                anchorMax = Fmt(rt.anchorMax);
                pivot = Fmt(rt.pivot);
                anchoredPos = Fmt(rt.anchoredPosition);
                sizeDelta = Fmt(rt.sizeDelta);
                // 世界坐标下的实际矩形 —— 用来判断是否跑出屏幕
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                worldRect = "x[" + corners[0].x.ToString("0.#") + "," + corners[2].x.ToString("0.#")
                    + "] y[" + corners[0].y.ToString("0.#") + "," + corners[2].y.ToString("0.#") + "]";
            }

            var comps = new List<string>();
            Component[] all = t.GetComponents<Component>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                {
                    continue;
                }

                string n = all[i].GetType().Name;
                if (n == "RectTransform" || n == "CanvasRenderer" || n == "Transform")
                {
                    continue;
                }

                if (n == "Image")
                {
                    Image img = all[i] as Image;
                    string sprite = img.sprite == null ? "null" : img.sprite.name;
                    n += "(sprite=" + sprite + ",type=" + img.type + ",enabled=" + img.enabled
                        + ",color=" + Fmt(img.color) + ")";
                }
                else if (n == "Text")
                {
                    Text txt = all[i] as Text;
                    n += "(size=" + txt.fontSize + ",color=" + Fmt(txt.color)
                        + ",text=\"" + Short(txt.text, 14) + "\")";
                }
                else if (n == "PixelPanel")
                {
                    PixelPanel pp = all[i] as PixelPanel;
                    n += "(theme=" + (pp.theme == null ? "NULL" : pp.theme.name)
                        + ",variant=" + pp.variant + ",border=" + pp.createBorder + ")";
                }
                else if (n == "PixelText")
                {
                    PixelText pt = all[i] as PixelText;
                    n += "(theme=" + (pt.theme == null ? "NULL" : pt.theme.name)
                        + ",style=" + pt.textStyle + ",inverse=" + pt.inverse + ")";
                }
                else if (n == "RectMask2D" || n == "Mask")
                {
                    n += "(裁剪!)";
                }

                comps.Add(n);
            }

            sb.Append("| ").Append(depth).Append(" | ").Append(indent).Append(t.name)
              .Append(" | ").Append(anchorMin)
              .Append(" | ").Append(anchorMax)
              .Append(" | ").Append(pivot)
              .Append(" | ").Append(anchoredPos)
              .Append(" | ").Append(sizeDelta)
              .Append(" | ").Append(worldRect)
              .Append(" | ").Append(string.Join(" / ", comps.ToArray()))
              .AppendLine(" |");

            for (int i = 0; i < t.childCount; i++)
            {
                Walk(t.GetChild(i), depth + 1, sb);
            }
        }

        private static string Fmt(Vector2 v)
        {
            return "(" + v.x.ToString("0.##") + "," + v.y.ToString("0.##") + ")";
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.##") + "," + v.y.ToString("0.##") + "," + v.z.ToString("0.##") + ")";
        }

        private static string Fmt(Color c)
        {
            return "(" + c.r.ToString("0.###") + "," + c.g.ToString("0.###")
                + "," + c.b.ToString("0.###") + "," + c.a.ToString("0.##") + ")";
        }

        private static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = s.Replace("\n", "\\n");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private static void WriteAndExit(StringBuilder sb)
        {
            string dir = Path.GetDirectoryName(OutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[UiLayoutDump] 已写出：" + OutPath + "（" + sb.Length + " 字符）");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
