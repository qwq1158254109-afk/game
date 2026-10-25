using UnityEditor;
using UnityEngine;

namespace StudentGrowthEditor
{
    /// <summary>
    /// 调试工具：把当前在编辑器里选中的对象（层级路径 + 坐标）打印到 Console。
    /// 用途：2026-10-25 定位商店地图美术扭蛋机的确切位置——
    /// 在 Scene 视图点中扭蛋机 → 跑本菜单 → 把 Console 里那行发回（或直接看 Editor.log）。
    /// </summary>
    public static class SelectionDump
    {
        [MenuItem("Tools/Student Growth/Dev/Dump Selected Object")]
        public static void Dump()
        {
            Transform selected = Selection.activeTransform;
            if (selected == null)
            {
                Debug.Log("[SelectionDump] 当前没有选中任何对象。在 Scene 视图里先点一下目标。");
                return;
            }

            string path = selected.name;
            Transform parent = selected.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            Debug.Log("[SelectionDump] " + path
                + " | localPos=(" + selected.localPosition.x.ToString("0.###") + ", " + selected.localPosition.y.ToString("0.###") + ")"
                + " | worldPos=(" + selected.position.x.ToString("0.###") + ", " + selected.position.y.ToString("0.###") + ")");
        }
    }
}
