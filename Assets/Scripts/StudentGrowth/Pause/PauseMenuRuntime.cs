using UnityEngine;
namespace StudentGrowth
{
    // Existing callers keep a small pause adapter; application services live independently.
    public sealed class PauseMenuRuntime : MonoBehaviour
    {
        private static PauseMenuRuntime instance;
        public static PauseMenuRuntime Instance {
            get { if(instance==null) { var root=GameApplicationRuntime.Instance.gameObject; instance=root.GetComponent<PauseMenuRuntime>(); if(instance==null) instance=root.AddComponent<PauseMenuRuntime>(); } return instance; }
        }
        public static bool OwnsPause => GameApplicationRuntime.OwnsPause;
        public static bool BlocksGameplayNavigation => GameApplicationRuntime.BlocksGameplayNavigation;
        public PauseMenuCoordinator Coordinator => GameApplicationRuntime.Instance.Coordinator;
        public SaveLoadService SaveService => GameApplicationRuntime.Instance.SaveService;
        public MenuTextService TextService => GameApplicationRuntime.Instance.TextService;
        public IGameFlowService FlowService => GameApplicationRuntime.Instance.FlowService;
        public SaveLoadPanelView SaveView => GameApplicationRuntime.Instance.SaveView;
        public PauseMenuView PauseView => GameApplicationRuntime.Instance.PauseView;
        public string SaveDirectory => GameApplicationRuntime.Instance.SaveDirectory;
        public void TogglePause() => GameApplicationRuntime.Instance.TogglePause();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() { instance=null; }
    }
}
