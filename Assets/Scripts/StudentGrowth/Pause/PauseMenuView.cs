using System;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        public event Action ContinueRequested, SaveRequested, LoadRequested, SettingsRequested, TitleRequested, QuitRequested, ConfirmRequested, CancelRequested;
        public RectTransform Root => (RectTransform)transform;
        private GameObject main, confirmation, busy;
        private Text title, prompt, notice, busyText;
        private Button resume, save, load, settings, toTitle, quit, yes, no, saveFirst;
        private PauseConfirmation previousConfirmation;
        private MenuTextService text;
        private PauseMenuCoordinator model;
        public void Bind(PauseMenuCoordinator model,MenuTextService text)
        { this.model = model; this.text = text; text.Changed += Refresh; Refresh(); }
        private void OnDestroy() { if (text != null) text.Changed -= Refresh; }
        public void Refresh()
        {
            if (model == null) return;
            bool rootVisible = model.State != PauseMenuState.Gameplay && model.State != PauseMenuState.Settings;
            main.SetActive(model.State == PauseMenuState.Paused);
            var canvas = GetComponent<Canvas>(); canvas.enabled = rootVisible;
            GetComponent<GraphicRaycaster>().enabled = rootVisible;
            busy.SetActive(model.State == PauseMenuState.Busy);
            busyText.text = text.Pick("正在处理，请稍候…","Please wait…");
            title.text = model.IsTitleContext ? text.Pick("学生成长","Student Growth") : text.Pick("暂停","Paused");
            resume.gameObject.SetActive(!model.IsTitleContext);
            Set(resume,text.Pick("继续游戏","Continue"));
            float offset = model.IsTitleContext ? -100f : 0f;
            MenuUi.Place((RectTransform)save.transform,650,280 + offset,620,76);
            MenuUi.Place((RectTransform)load.transform,650,380 + offset,620,76);
            MenuUi.Place((RectTransform)settings.transform,650,480 + offset,620,76);
            MenuUi.Place((RectTransform)toTitle.transform,650,580 + offset,620,76);
            MenuUi.Place((RectTransform)quit.transform,650,680 + offset,620,76);
            Set(save,text.Pick("保存游戏","Save game")); Set(load,text.Pick("读取游戏","Load game")); Set(settings,text.Pick("设置","Settings"));
            Set(toTitle,text.Pick("返回主菜单","Return to title")); Set(quit,text.Pick("退出游戏","Quit game"));
            save.interactable = !model.IsTitleContext; toTitle.interactable = !model.IsTitleContext;
            Set(yes,text.Pick("确认","Confirm")); Set(no,text.Pick("取消","Cancel"));
            notice.text = text.Notice(model.Notice);
            confirmation.SetActive(model.Confirmation != PauseConfirmation.None);
            saveFirst.gameObject.SetActive(model.HasUnsavedChoice);
            if(model.HasUnsavedChoice) Set(yes,"不保存并继续");
            if(model.Confirmation!=previousConfirmation && model.Confirmation!=PauseConfirmation.None) no.Select();
            previousConfirmation=model.Confirmation;
            confirmation.transform.SetAsLastSibling();
            string action = model.Confirmation == PauseConfirmation.Save ? text.Pick("保存到此槽位？已有存档将被替换。","Save to this slot? Its current save will be replaced.") :
                model.Confirmation == PauseConfirmation.Load ? text.Pick("读取此存档？未保存的进度将丢失。","Load this save? Unsaved progress will be lost.") :
                model.Confirmation == PauseConfirmation.ReturnToTitle ? text.Pick("返回主菜单？请先保存当前进度。","Return to title? Save your progress first.") :
                model.Confirmation == PauseConfirmation.NewGame ? text.Pick("开始新游戏？当前未保存进度将丢失。","Start a new game? Unsaved progress will be lost.") :
                text.Pick("退出游戏？请先保存当前进度。","Quit the game? Save your progress first.");
            if(model.Confirmation==PauseConfirmation.ReplaceAutomatic) action="替换自动档并启用\n确认后立即用当前进度替换自动档，启用本局跨天自动保存。其他手动档不会改变。";
            if(model.HasUnsavedChoice) action="当前进度尚未保存。\n保存后继续、不保存并继续，或取消返回游戏。";
            if((model.Confirmation==PauseConfirmation.Save || model.Confirmation==PauseConfirmation.Load || model.Confirmation==PauseConfirmation.ReplaceAutomatic) && model.Slots!=null)
                action+="\n"+text.Slot(model.Slots[model.SelectedSlot.Value]);
            prompt.text = action;
        }
        private static void Set(Button b,string label) => b.GetComponentInChildren<Text>().text = label;
        public static PauseMenuView Create(Transform owner,Font font)
        {
            var root = new GameObject("PauseCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.transform.SetParent(owner,false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = 6000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            root.AddComponent<SettingsCanvasFitView>(); var view = root.AddComponent<PauseMenuView>();
            var main = MenuUi.Panel("PausePanel",view.Root,new Color(0.05f,0.08f,0.13f,0.97f)); view.main = main.gameObject;
            view.title = MenuUi.Label(main,font,600,55,720,90,44);
            view.resume = MenuUi.Button(main,font,650,180,620,76,() => view.ContinueRequested?.Invoke());
            view.save = MenuUi.Button(main,font,650,280,620,76,() => view.SaveRequested?.Invoke());
            view.load = MenuUi.Button(main,font,650,380,620,76,() => view.LoadRequested?.Invoke());
            view.settings = MenuUi.Button(main,font,650,480,620,76,() => view.SettingsRequested?.Invoke());
            view.toTitle = MenuUi.Button(main,font,650,580,620,76,() => view.TitleRequested?.Invoke());
            view.quit = MenuUi.Button(main,font,650,680,620,76,() => view.QuitRequested?.Invoke());
            view.notice = MenuUi.Label(main,font,200,820,1520,100,25);
            var busyPanel = MenuUi.Panel("BusyPanel",view.Root,new Color(0.05f,0.08f,0.13f,0.97f));
            view.busy = busyPanel.gameObject; view.busyText = MenuUi.Label(busyPanel,font,300,430,1320,180,34);
            view.busy.SetActive(false);
            var confirm = MenuUi.Panel("PauseConfirmation",view.Root,new Color(0.06f,0.09f,0.15f,0.99f)); view.confirmation = confirm.gameObject;
            view.prompt = MenuUi.Label(confirm,font,300,300,1320,180,34);
            view.yes = MenuUi.Button(confirm,font,500,580,400,85,() => view.ConfirmRequested?.Invoke());
            view.no = MenuUi.Button(confirm,font,1020,580,400,85,() => view.CancelRequested?.Invoke());
            view.saveFirst=MenuUi.Button(confirm,font,700,700,520,85,() => view.model.SaveBeforeLeaving());
            Set(view.saveFirst,"保存后继续"); view.saveFirst.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false); root.GetComponent<Canvas>().enabled = false; return view;
        }
    }
    public static class MenuUi
    {
        public static RectTransform Panel(string name,RectTransform parent,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color; return r;
        }
        public static Text Label(RectTransform parent,Font font,float x,float y,float w,float h,int size)
        {
            var go = new GameObject("Text",typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            Place((RectTransform)go.transform,x,y,w,h); var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size; label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        public static Button Button(RectTransform parent,Font font,float x,float y,float w,float h,Action click)
        {
            var go = new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false); Place((RectTransform)go.transform,x,y,w,h);
            go.GetComponent<Image>().color = new Color(0.2f,0.3f,0.43f); var b = go.GetComponent<Button>(); b.targetGraphic = go.GetComponent<Image>();
            Label((RectTransform)go.transform,font,0,0,w,h,28); b.onClick.AddListener(() => click()); return b;
        }
        public static void Place(RectTransform r,float x,float y,float w,float h)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1); r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); }
    }
}
