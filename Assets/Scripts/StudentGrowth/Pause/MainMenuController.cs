using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace StudentGrowth
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        private GameApplicationRuntime app;
        private SaveSlotSummary latest;
        public bool IsReady => view!=null && app!=null && app.Coordinator!=null;
        private void Start()
        {
            GameFlowDiagnostics.Trace("MainMenu.Start begin");
            app=GameApplicationRuntime.Instance;
            if(view==null) view=GetComponentInChildren<MainMenuView>();
            if(view==null) return;
            view.StartRequested+=StartGame; view.ContinueRequested+=ContinueSaved;
            view.LoadRequested+=OpenLoad; view.SettingsRequested+=OpenSettings; view.QuitRequested+=Quit;
            if(app==null || app.Coordinator==null)
            {
                // 启动链断在中途（常见原因：SaveContentCatalog 里有已删除资产的条目，
                // 导致 GameApplicationRuntime.Awake 抛异常、Coordinator 没建起来）。
                // 这里必须停住并说清楚，否则每次点按钮都是一条 NRE，看不出病因。
                Debug.LogError("MainMenu: 启动链未就绪（GameApplicationRuntime.Coordinator 为空），开始/继续/读取均不可用。先查 SaveContentCatalog 是否有已删除资产的条目。");
                return;
            }
            app.Coordinator.StateChanged+=Changed;
            app.Coordinator.EnterTitle(); SettingsMenuController.Instance.EnsureMenuInput();
            GameFlowDiagnostics.Trace("MainMenu.Start complete");
            StartCoroutine(Scan());
        }
        private IEnumerator Scan()
        {
            yield return null;
            latest=app.SaveService.Latest(out string notice);
            view.SetSummary(latest,notice,app.TextService);
        }
        private void StartGame() { if(!IsReady || app.Coordinator.State!=PauseMenuState.Title || app.Backend.IsBusy) return; app.Coordinator.BeginNewGame(); app.StartCoroutine(app.Coordinator.ConfirmAsync()); }
        private void ContinueSaved() {
            if(!IsReady || latest==null || !app.Coordinator.OpenLoad().Success) return;
            if((app.Coordinator.Slots[latest.SlotId.Value] as SaveSlotSummary)?.Revision!=latest.Revision) { app.Coordinator.SetNotice("SAVE_TARGET_CHANGED"); return; }
            app.Coordinator.SelectSlot(latest.SlotId); app.StartCoroutine(app.Coordinator.ConfirmAsync());
        }
        private void OpenLoad() { if(IsReady) app.Coordinator.OpenLoad(); }
        private void OpenSettings() { if(IsReady) app.Coordinator.OpenSettings(); }
        private void Quit() { if(IsReady) app.Coordinator.RequestConfirmation(PauseConfirmation.Quit); }
        private void Changed(PauseMenuStateChangedEventArgs e) { if(view!=null) view.SetInteractive(e.State==PauseMenuState.Title && app.Coordinator.Confirmation==PauseConfirmation.None); }
        private void OnDestroy() { if(app!=null) app.Coordinator.StateChanged-=Changed; }
    }
}
