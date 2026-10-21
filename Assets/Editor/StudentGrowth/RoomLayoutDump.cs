using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 布局排查小工具：把当前打开场景里 DecorativeObjects / InteractableObjects 下每个对象
    /// 的世界坐标与 Sprite 包围盒导成一行 CSV，方便算间距、贴墙、避让通道。
    /// 只读，不改场景。输出：工程根目录 _layout_dump.txt
    /// </summary>
    public static class RoomLayoutDump
    {
        private const string OutPath = "E:/大创项目/_layout_dump.txt";

        [MenuItem("Tools/Student Growth/Dev/Dump Room Layout")]
        public static void Dump()
        {
            Scene scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder();
            sb.AppendLine("scene," + scene.name);
            sb.AppendLine("group,name,x,y,bminx,bminy,bmaxx,bmaxy,w,h,sprite");

            DumpGroup(GameObject.Find("DecorativeObjects"), "Decor", sb);
            DumpGroup(GameObject.Find("InteractableObjects"), "Interact", sb);

            File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[LayoutDump] 已写出 " + OutPath + "（" + scene.name + "）");
        }

        private static void DumpGroup(GameObject root, string group, StringBuilder sb)
        {
            if (root == null)
            {
                sb.AppendLine(group + ",<missing>,,,,,,,,,");
                return;
            }

            foreach (Transform child in root.transform)
            {
                var renderers = child.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0)
                {
                    sb.AppendLine(string.Format("{0},{1},{2:F3},{3:F3},,,,,,,", group, child.name, child.position.x, child.position.y));
                    continue;
                }

                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }

                string spriteNames = string.Join("|", System.Array.ConvertAll(renderers, r => r.sprite != null ? r.sprite.name : "null"));
                sb.AppendLine(string.Format(
                    "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3},{10}",
                    group, child.name, child.position.x, child.position.y,
                    b.min.x, b.min.y, b.max.x, b.max.y, b.size.x, b.size.y, spriteNames));
            }
        }
    }
}
