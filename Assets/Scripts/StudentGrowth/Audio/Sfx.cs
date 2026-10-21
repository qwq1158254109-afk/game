using UnityEngine;

namespace StudentGrowth
{
    /// <summary>音效入口（业务代码只碰这里，不直接读 Assets/Resources 路径）。</summary>
    public static class Sfx
    {
        public static void Play(string clipPath, float volume = 1f, float pitch = 1f)
        {
            SfxPlayer.PlayInternal(clipPath, volume, pitch);
        }

        /// <summary>从一组同名变体里随机挑一个播（脚步、翻书这类避免机械重复）。</summary>
        public static void PlayVaried(string[] clipPaths, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clipPaths == null || clipPaths.Length == 0)
            {
                return;
            }

            string pick = clipPaths[Random.Range(0, clipPaths.Length)];
            Play(pick, volume, 1f + Random.Range(-pitchJitter, pitchJitter));
        }

        // ── 常用音效的固定路径（素材在 Resources/Audio/SFX/ 下）──────────────
        public static class Ui
        {
            private static readonly string[] Clicks =
            {
                "UI/ui_click_1", "UI/ui_click_2", "UI/ui_click_3", "UI/ui_click_4"
            };

            public static void Click() => PlayVaried(Clicks, 0.55f, 0.04f);
            public static void Confirm() => Play("UI/ui_confirm_1", 0.7f);
            public static void Back() => Play("UI/ui_back", 0.6f);
            public static void Error() => Play("UI/ui_error_1", 0.7f);
            public static void Open() => Play("UI/ui_open", 0.55f);
            public static void Close() => Play("UI/ui_close", 0.55f);
        }

        public static class World
        {
            private static readonly string[] Footsteps =
            {
                "World/footstep00", "World/footstep01", "World/footstep02", "World/footstep03",
                "World/footstep04", "World/footstep05", "World/footstep06", "World/footstep07",
                "World/footstep08", "World/footstep09"
            };

            public static void Step(bool running) => PlayVaried(Footsteps, running ? 0.42f : 0.3f, 0.08f);
            public static void DoorOpen() => Play("World/doorOpen_1", 0.8f);
            public static void DoorClose() => Play("World/doorClose_2", 0.75f);
            public static void PageFlip() => PlayVaried(new[] { "World/bookFlip1", "World/bookFlip2", "World/bookFlip3" }, 0.6f);
            public static void Coin() => PlayVaried(new[] { "World/handleCoins", "World/handleCoins2" }, 0.7f);
        }
    }
}
