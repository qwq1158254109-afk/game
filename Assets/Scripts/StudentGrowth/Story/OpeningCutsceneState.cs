namespace StudentGrowth
{
    /// <summary>
    /// 新游戏开场只播一次：主菜单点「新游戏」时 Request，卧室场景启动时 Consume。
    /// 读档、从客厅再进卧室都不会再播。
    /// </summary>
    public static class OpeningCutsceneState
    {
        private static bool pending;

        public static bool Pending => pending;

        public static void Request()
        {
            pending = true;
        }

        public static bool Consume()
        {
            if (!pending)
            {
                return false;
            }

            pending = false;
            return true;
        }

        public static void Clear()
        {
            pending = false;
        }
    }
}
