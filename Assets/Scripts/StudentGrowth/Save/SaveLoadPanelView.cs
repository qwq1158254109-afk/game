using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class SaveLoadPanelView : MonoBehaviour
    {
        public event Action<SaveSlotId> SlotSelected;
        public event Action BackRequested, PanelClosed;
        private readonly RawImage[] images = new RawImage[10];
        private readonly Text[] labels = new Text[10];
        private readonly Button[] buttons = new Button[10];
        private Text title, status;
        private Button back;
        private MenuTextService text;
        private IReadOnlyList<IReadOnlySaveSlotSummary> slots;
        private bool saving;
        private string notice;
        public void Bind(MenuTextService service) { text = service; text.Changed += RefreshText; }
        public void Show(IReadOnlyList<IReadOnlySaveSlotSummary> summaries,bool save,string message)
        { slots = summaries; saving = save; notice = message; gameObject.SetActive(true); RefreshText(); }
        public void SetThumbnail(SaveSlotId slot,Texture2D texture)
        { images[slot.Value].texture = texture; images[slot.Value].color = texture == null ? new Color(0.16f,0.2f,0.26f) : Color.white; }
        public void ClearThumbnails() { for(int i=0;i<10;i++) SetThumbnail(new SaveSlotId(i),null); }
        private void RefreshText()
        {
            if (text == null || slots == null) return;
            title.text = saving ? text.Pick("保存游戏","Save game") : text.Pick("读取游戏","Load game");
            back.GetComponentInChildren<Text>().text = text.Pick("返回","Back"); status.text = text.Notice(notice);
            for(int i=0;i<10;i++)
            { labels[i].text = text.Slot(slots[i]) + (saving && i==0 ? "\n替换自动档并启用" : ""); buttons[i].interactable = slots[i].IsValid && (saving || !slots[i].IsEmpty); }
        }
        private void OnDisable() { PanelClosed?.Invoke(); }
        private void OnDestroy() { if (text != null) text.Changed -= RefreshText; PanelClosed?.Invoke(); }
        public static SaveLoadPanelView Create(RectTransform parent,Font font)
        {
            var panel = MenuUi.Panel("SaveLoadPanel",parent,new Color(0.05f,0.08f,0.13f,1)); var view = panel.gameObject.AddComponent<SaveLoadPanelView>();
            view.title = MenuUi.Label(panel,font,100,35,1720,70,38);
            for(int i=0;i<10;i++)
            {
                int index = i; float x = 100 + (i % 2) * 880; float y = 135 + (i / 2) * 155;
                var button = MenuUi.Button(panel,font,x,y,840,140,() => view.SlotSelected?.Invoke(new SaveSlotId(index))); view.buttons[i] = button;
                var label = button.GetComponentInChildren<Text>(); MenuUi.Place(label.rectTransform,270,5,550,130); label.fontSize = 25; view.labels[i] = label;
                var image = new GameObject("Thumbnail",typeof(RectTransform),typeof(RawImage)); image.transform.SetParent(button.transform,false);
                MenuUi.Place((RectTransform)image.transform,4,2,240,135); view.images[i] = image.GetComponent<RawImage>(); view.images[i].raycastTarget = false;
                view.SetThumbnail(new SaveSlotId(i),null);
            }
            view.status = MenuUi.Label(panel,font,100,920,1300,100,23);
            view.back = MenuUi.Button(panel,font,1490,950,330,70,() => view.BackRequested?.Invoke());
            panel.gameObject.SetActive(false); return view;
        }
    }
}
