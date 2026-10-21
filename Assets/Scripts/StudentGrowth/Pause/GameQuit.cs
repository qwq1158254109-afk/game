using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StudentGrowth
{
    /// <summary>
    /// 「退出游戏」的唯一入口。
    ///
    /// 为什么需要它：直接调 <c>Application.Quit()</c> 在 Unity 编辑器里是**空操作**——
    /// 暂停菜单点「退出游戏 → 确认」后什么都不发生（2026-10-20 用户实测反馈）。
    /// 编辑器下改为停止 Play Mode，打包后才真正退出进程。
    /// </summary>
    public static class GameQuit
    {
        public static void Request()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}