using System.IO;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 房间对照图工具：把当前场景的主相机离屏渲染成 PNG，不带 HUD。
    /// 用途：改摆件前后出对照图，不依赖 Game 视图缩放/Play 模式。
    /// 输出：工程根目录 _room_shot.png（每次覆盖）
    /// </summary>
    public static class RoomScreenshotCapture
    {
        private const string OutPath = "E:/大创项目/_room_shot.png";
        // 2026-10-20 第五轮（方案B）：四墙等厚 2.5 单位后画幅 31.11x18.5、相机 ortho 9.25。
        // 尺寸直接取"相机视野 x 64PPU"：ortho 9.25 → 高 18.5 单位 = 1184px；
        // 宽取与画布同宽的 1991，保证 1 素材像素 = 1 屏幕像素、房间居中且不被裁。
        private const int Width = 1991;   // = 31.109 世界单位 x 64 PPU（房间全宽）
        private const int Height = 1184;  // = 18.5 世界单位 x 64 PPU（= ortho 9.25 的视野高）

        [MenuItem("Tools/Student Growth/Dev/Capture Room Screenshot")]
        public static void Capture()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Camera[] all = Object.FindObjectsByType<Camera>();
                if (all.Length > 0)
                {
                    cam = all[0];
                }
            }

            if (cam == null)
            {
                Debug.LogError("[RoomShot] 当前场景没有相机");
                return;
            }

            RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(OutPath, tex.EncodeToPNG());

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            Debug.Log(string.Format(
                "[RoomShot] 已写出 {0}；相机 pos=({1:F2},{2:F2}) ortho={3:F2} 场景={4}",
                OutPath, cam.transform.position.x, cam.transform.position.y, cam.orthographicSize,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
        }
    }
}
