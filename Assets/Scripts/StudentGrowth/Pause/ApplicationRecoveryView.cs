using UnityEngine;
using UnityEngine.UI;
namespace StudentGrowth
{
    public sealed class ApplicationRecoveryView : MonoBehaviour
    {
        private Text message;
        private Button retry,quit;
        private GameApplicationRuntime app;
        public void Show(string reason) { gameObject.SetActive(true); message.text=reason; SettingsMenuController.Instance.EnsureMenuInput(); }
        public void Hide() => gameObject.SetActive(false);
        private void Update() { retry.interactable=!app.Backend.IsBusy && !app.SaveService.IsBusy && !app.SaveService.CommitUncertain; quit.interactable=!app.SaveService.IsBusy && !app.SaveService.CommitUncertain && !AudioSettingsController.DisplayTransitionBlocked; }
        public static ApplicationRecoveryView Create(Transform parent,Font font,GameApplicationRuntime app)
        {
            var go=new GameObject("ApplicationRecovery",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); go.transform.SetParent(parent,false);
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder=12000;
            var scale=go.GetComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution=new Vector2(1920,1080);
            go.AddComponent<SettingsCanvasFitView>(); var view=go.AddComponent<ApplicationRecoveryView>(); view.app=app;
            var panel=MenuUi.Panel("Recovery",(RectTransform)go.transform,new Color(.07f,.09f,.12f));
            view.message=MenuUi.Label(panel,font,260,250,1400,220,32);
            view.retry=MenuUi.Button(panel,font,430,560,480,90,()=>app.StartCoroutine(app.Backend.ReturnToMenuAsync(r=>{ if(r.Success) app.Coordinator.EnterTitle(); })));
            view.retry.GetComponentInChildren<Text>().text="重试返回主菜单";
            view.quit=MenuUi.Button(panel,font,1010,560,480,90,()=> { AudioSettingsController.Instance.Service.Display?.Quit(); GameQuit.Request(); });
            view.quit.GetComponentInChildren<Text>().text="退出游戏"; view.Hide(); return view;
        }
    }
}
