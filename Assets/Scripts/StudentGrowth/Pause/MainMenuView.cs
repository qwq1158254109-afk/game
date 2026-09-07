using System;
using UnityEngine;
using UnityEngine.UI;
namespace StudentGrowth
{
    public sealed class MainMenuView : MonoBehaviour
    {
        public event Action StartRequested,ContinueRequested,LoadRequested,SettingsRequested,QuitRequested;
        [SerializeField] private Button continueButton, startButton;
        [SerializeField] private Text summary,version;
        [SerializeField] private CanvasGroup controls;
        [SerializeField] private Button loadButton,settingsButton,quitButton;
        private void Awake()
        {
            if(startButton==null) return;
            startButton.onClick.AddListener(()=>StartRequested?.Invoke()); continueButton.onClick.AddListener(()=>ContinueRequested?.Invoke());
            loadButton.onClick.AddListener(()=>LoadRequested?.Invoke()); settingsButton.onClick.AddListener(()=>SettingsRequested?.Invoke()); quitButton.onClick.AddListener(()=>QuitRequested?.Invoke());
        }
        public void SetInteractive(bool value) { controls.interactable=value; controls.blocksRaycasts=value; }
        public void SetSummary(SaveSlotSummary latest,string notice,MenuTextService text)
        {
            continueButton.gameObject.SetActive(latest!=null);
            summary.text=latest==null ? notice ?? "从自己的房间出发，书写新的成长故事。" : text.Slot(latest)+(string.IsNullOrEmpty(notice) ? "" : "\n"+notice);
            version.text="v"+Application.version+"    ·    方向键 / Tab 选择    Enter 确认    Esc 返回";
            if(controls.interactable) (latest==null ? startButton : continueButton).Select();
        }
        public static MainMenuView Create(Font font,Texture2D background)
        {
            var root=new GameObject("MainMenuView",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            root.AddComponent<SettingsCanvasFitView>();
            var view=root.AddComponent<MainMenuView>(); var rect=(RectTransform)root.transform;
            var back=MenuUi.Panel("RoomBackground",rect,new Color(.12f,.16f,.17f));
            if(background!=null) { var art=new GameObject("StaticRoom",typeof(RectTransform),typeof(RawImage)); art.transform.SetParent(back,false); var image=art.GetComponent<RawImage>(); image.texture=background; image.color=Color.white; var fit=art.AddComponent<AspectRatioFitter>(); fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio=(float)background.width/background.height; }
            var panel=MenuUi.Panel("MenuControls",rect,Color.clear); view.controls=panel.gameObject.AddComponent<CanvasGroup>();
            var shade=MenuUi.Panel("ReadingBackdrop",panel,new Color(.09f,.12f,.14f,.93f)); MenuUi.Place(shade,0,0,700,1080);
            var title=MenuUi.Label(panel,font,100,125,570,110,64); title.text="学生成长物语"; title.alignment=TextAnchor.MiddleLeft; title.color=new Color(.95f,.84f,.61f);
            var subtitle=MenuUi.Label(panel,font,104,232,510,45,23); subtitle.text="每一天，都有新的可能"; subtitle.alignment=TextAnchor.MiddleLeft;
            view.summary=MenuUi.Label(panel,font,104,302,520,138,23); view.summary.alignment=TextAnchor.UpperLeft; view.summary.text="正在检查存档…";
            view.continueButton=Make(panel,font,450,"继续游戏",()=>view.ContinueRequested?.Invoke());
            view.startButton=Make(panel,font,544,"开始游戏",()=>view.StartRequested?.Invoke());
            view.loadButton=Make(panel,font,638,"读取存档",()=>view.LoadRequested?.Invoke());
            view.settingsButton=Make(panel,font,732,"设置",()=>view.SettingsRequested?.Invoke());
            view.quitButton=Make(panel,font,826,"退出游戏",()=>view.QuitRequested?.Invoke());
            view.version=MenuUi.Label(panel,font,100,995,1720,50,20); view.version.alignment=TextAnchor.MiddleLeft;
            root.AddComponent<MainMenuController>(); return view;
        }
        private static Button Make(RectTransform parent,Font font,float y,string text,Action click)
        {
            var b=MenuUi.Button(parent,font,104,y,480,76,click); b.GetComponentInChildren<Text>().text=text;
            b.GetComponent<Image>().color=new Color(.30f,.24f,.17f);
            var colors=b.colors; colors.highlightedColor=new Color(1,.88f,.59f); colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.72f,.61f,.44f); b.colors=colors;
            b.gameObject.AddComponent<MenuFocusIndicator>(); return b;
        }
    }
}
