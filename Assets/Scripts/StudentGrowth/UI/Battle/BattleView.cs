using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    // A snapshot-only view. Costs, effects, terminal decisions and rewards belong to the controller/session.
    public sealed class BattleView : MonoBehaviour
    {
        private ChallengeController controller;
        private BattleSessionFlow flow;
        private BattleUIConfig config;
        private Canvas canvas;
        private RectTransform content,target;
        private readonly Dictionary<string,CardData> artwork=new Dictionary<string,CardData>();
        private readonly Color ink=new Color(.22f,.14f,.08f);
        private string message="点击手牌或拖到目标；费用不足时不可出牌。";
        public static BattleView Open(ChallengeController controller,BattleSessionFlow flow,IEnumerable<CardData> cards)
        {
            var go=new GameObject("BattleView",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var view=go.AddComponent<BattleView>();view.controller=controller;view.flow=flow;
            view.canvas=go.GetComponent<Canvas>();view.canvas.renderMode=RenderMode.ScreenSpaceOverlay;view.canvas.sortingOrder=180;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            view.config=Resources.Load<BattleUIConfig>(BattleUIConfig.ResourcePath)??BattleUIConfig.CreateRuntimeDefault();
            foreach(var card in cards)view.artwork[card.cardId]=card;
            try{view.Refresh();return view;}catch{Destroy(go);throw;}
        }
        public void Refresh()
        {
            if(controller==null||controller.SandboxEngine==null)
            {
                if(content!=null && flow.BlocksSave)
                    foreach(var label in content.GetComponentsInChildren<Text>())if(label.name=="ResultDetail")label.text=flow.Failure;
                return;
            }
            if(content!=null){content.gameObject.SetActive(false);Destroy(content.gameObject);}
            var engine=controller.SandboxEngine;var state=engine.Snapshot();
            content=Rect("BattleContent",transform,new Vector2(0,0),new Vector2(1920,1080));
            var backdrop=content.gameObject.AddComponent<Image>();backdrop.color=new Color(.065f,.055f,.04f,.99f);
            Panel("Header",content,new Vector2(0,448),new Vector2(1770,115),"hud_notice_plate");
            Label("Title",content,state.Definition.Name,new Vector2(0,455),new Vector2(1300,56),36,Color.white);
            Label("Turns",content,"回合 "+state.Turn+" / "+state.Definition.MaxTurns+"    行动能量 "+state.Energy,
                new Vector2(0,400),new Vector2(1200,45),25,new Color(1,.82f,.36f));
            var player=Panel("Player",content,new Vector2(-535,150),new Vector2(650,370),"hud_tooltip_panel");
            Label("PlayerStats",player,"学生\n\n耐力 "+state.Hp+" / "+state.MaxHp+"    格挡 "+state.Block
                +"\n\n体力 "+state.LegacyStats[PlayerStatKey.Energy]+" / "+state.LegacyStats[PlayerStatKey.MaxEnergy],
                new Vector2(0,40),new Vector2(570,230),30,ink);
            target=Panel("BattleTarget",content,new Vector2(490,150),new Vector2(780,370),"hud_tooltip_panel");
            var predicted=engine.GetIntentPreview();
            string intent=state.Turn>=state.Definition.MaxTurns?"最后一回合，结束后直接判定结果。":
                (state.Intent?.Name??"等待")+"\n"+(state.Intent==null?"":BattlePresentationText.Effects(state.Intent.Effects))
                +"\n预计耐力损失 "+(state.Hp-predicted.Hp);
            if(engine.Result!=null)intent="本场已结束。";
            Label("Intent",target,"目标进度 "+state.Progress+" / "+state.Definition.Target+"\n\n"+intent,
                Vector2.zero,new Vector2(685,290),28,ink);
            Label("Message",content,message,new Vector2(0,-65),new Vector2(1500,70),22,new Color(.94f,.84f,.64f));
            if(engine.Result!=null || flow.Phase==BattleFlowPhase.RecoveryRequired)
            {
                BuildResult(state);return;
            }
            var hand=Rect("BattleHand",content,new Vector2(0,-350),new Vector2(1320,280));
            var drag=Rect("DragLayer",content,Vector2.zero,new Vector2(1920,1080));
            var tooltipRoot=Panel("Tooltip",content,Vector2.zero,new Vector2(340,240),"hud_tooltip_panel");
            var tooltipTitle=Label("TooltipTitle",tooltipRoot,"",new Vector2(0,82),new Vector2(300,42),24,ink);
            var tooltipBody=Label("TooltipBody",tooltipRoot,"",new Vector2(0,-28),new Vector2(300,160),19,ink);
            var tooltip=tooltipRoot.gameObject.AddComponent<BattleTooltipView>();tooltip.Initialize(tooltipRoot,tooltipTitle,tooltipBody,canvas,config);
            BuildStatuses(player,state,tooltip);
            var layout=hand.gameObject.AddComponent<HandLayoutController>();layout.Initialize(hand,drag,canvas,config,tooltip);
            layout.TryPlayDraggedCard=Play;
            foreach(var card in state.InPile(BattlePile.Hand))CreateCard(hand,layout,artwork[card.DefinitionId],engine.GetCardPreview(card.InstanceId));
            layout.RefreshLayout(true);
            Button("EndTurn","结束回合",content,new Vector2(785,-400),new Vector2(240,85),()=>Handle(controller.EndEngineTurn(Request(state))));
            Label("Piles",content,"抽牌 "+state.InPile(BattlePile.Draw).Count+"\n弃牌 "+state.InPile(BattlePile.Discard).Count
                +"\n消耗 "+state.InPile(BattlePile.Exhaust).Count,new Vector2(-785,-385),new Vector2(250,120),23,Color.white);
        }
        private void BuildResult(BattleSnapshot state)
        {
            var result=flow.Result;
            bool applied=result?.Consumption==BattleResultConsumption.Applied;
            var panel=Panel("BattleResult",content,new Vector2(0,-315),new Vector2(1300,350),"hud_tooltip_panel");
            string title=applied?(result.Success?"挑战完成":"本次未完成"):"战斗恢复";
            string detail=applied?"结果已结算。"+(result.Challenge.isBoss&&!result.Success?"可以重新挑战，本次没有推进阶段。":"")
                +"\n"+string.Join("  ·  ",result.Plan?.Rewards??Array.Empty<string>()):flow.Failure??"正在结算……";
            Label("ResultTitle",panel,title,new Vector2(0,120),new Vector2(1150,55),32,ink);
            Label("ResultDetail",panel,detail,new Vector2(0,30),new Vector2(1170,110),23,ink);
            if(applied)Button("Return","继续 / 返回地图",panel,new Vector2(0,-110),new Vector2(330,65),()=>{flow.TryReturn();Refresh();});
            else
            {
                Button("RetrySettlement","重试结算",panel,new Vector2(-210,-110),new Vector2(300,65),()=>{flow.RetrySettlement();Refresh();});
                Button("ExitFault","放弃未提交结果并返回",panel,new Vector2(200,-110),new Vector2(370,65),()=>{flow.ExitFault();Refresh();});
            }
        }
        private void CreateCard(RectTransform parent,HandLayoutController layout,CardData art,BattleCardPreview binding)
        {
            var rect=Panel("Card_"+binding.Card.InstanceId,parent,Vector2.zero,new Vector2(188,228),null);
            var background=rect.GetComponent<Image>();var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=background;
            BattleUiStyler.ApplyCardButton(button,background,config);
            var sprite=config.GetCardArtwork(art);
            if(sprite!=null){var image=Rect("Artwork",rect,new Vector2(0,63),new Vector2(150,85)).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;}
            Label("Name",rect,binding.Card.Definition.Name,new Vector2(0,4),new Vector2(172,45),20,Color.white);
            Label("Effects",rect,binding.Summary,new Vector2(0,-48),new Vector2(172,65),17,Color.white);
            var cost=Label("Cost",rect,"费用 "+binding.Cost,new Vector2(0,-96),new Vector2(172,28),18,Color.yellow);
            string keywords=(binding.Card.Definition.Exhaust?"消耗 ":"")+(binding.Card.Definition.Retain?"保留 ":"")+(binding.Card.Definition.Ethereal?"虚无":"");
            if(keywords.Length>0)Label("Keywords",rect,keywords,new Vector2(0,96),new Vector2(172,24),17,new Color(1,.84f,.4f));
            var view=rect.gameObject.AddComponent<BattleCardView>();view.Initialize(art,button,background,cost,binding.Summary,binding.Playable,config);view.BindInstance(binding);
            button.onClick.AddListener(()=>Play(view,null));layout.RegisterCard(view);
        }
        private bool Play(BattleCardView view,PointerEventData pointer)
        {
            var binding=view.InstancePreview;if(binding?.Card==null)return false;
            string id=binding.Card.Definition.Target==CardTargetType.SingleEnemy
                ?(pointer==null||RectTransformUtility.RectangleContainsScreenPoint(target,pointer.position,pointer.pressEventCamera)?"challenge:"+binding.State.Definition.Id:null)
                :binding.Card.Definition.Target==CardTargetType.Self?BattleEngine.SelfTargetId:null;
            var receipt=controller.PlayEngineCard(Request(binding.State,binding.Card.InstanceId,id));Handle(receipt);return receipt.Accepted;
        }
        private void Handle(BattleActionReceipt receipt)
        {message=receipt.Accepted?"操作完成。":BattlePresentationText.Failure(receipt.Failure);flow.ProcessResult();Refresh();}
        private void BuildStatuses(RectTransform player,BattleSnapshot state,BattleTooltipView tooltip)
        {
            if(state.Statuses.Count==0){Label("NoStatuses",player,"暂无状态",new Vector2(0,-120),new Vector2(570,36),21,ink);return;}
            for(int i=0;i<state.Statuses.Count;i++)
            {
                var status=state.Statuses[i];
                var row=Rect("Status_"+status.Kind,player,new Vector2(0,-72-i*30),new Vector2(560,30));
                var hit=row.gameObject.AddComponent<Image>();hit.color=Color.clear;
                var icon=Rect("Icon",row,new Vector2(-232,0),new Vector2(30,30)).gameObject.AddComponent<Image>();
                string key=status.Kind==BattleStatusKind.Focus?"icon_status_focus":status.Kind==BattleStatusKind.Distraction?"icon_status_distraction":"icon_status_weak_defense";
                icon.sprite=config.GetArtwork(key);icon.preserveAspect=true;icon.raycastTarget=false;
                Label("StatusText",row,BattlePresentationText.Status(status,state.Turn),new Vector2(20,0),new Vector2(470,32),21,ink);
                row.gameObject.AddComponent<BattleTooltipTarget>().Initialize(tooltip,BattlePresentationText.StatusName(status.Kind),BattlePresentationText.StatusDetail(status,state.Turn));
            }
        }
        private static BattleRequest Request(BattleSnapshot state,string card=null,string target=null)
            =>new BattleRequest(state.RuntimeSessionId,state.BattleId,state.Revision,Guid.NewGuid().ToString("N"),card,target);
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;}
        private RectTransform Panel(string name,Transform parent,Vector2 position,Vector2 size,string key)
        {
            var rect=Rect(name,parent,position,size);var image=rect.gameObject.AddComponent<Image>();
            image.sprite=key==null?null:config.GetArtwork(key);image.type=Image.Type.Sliced;
            image.color=image.sprite!=null?Color.white:key==null?new Color(.18f,.23f,.18f):new Color(.94f,.90f,.77f);return rect;
        }
        private Text Label(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color)
        {var label=Rect(name,parent,position,size).gameObject.AddComponent<Text>();label.text=value;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            GameUiStyle.ApplyTextFromFontSpec(label,null,fontSize,FontStyle.Normal);label.GetComponent<PixelText>().applyOnAwake=false;
            label.fontSize=fontSize;label.color=color;return label;}
        private Button Button(string name,string label,Transform parent,Vector2 position,Vector2 size,Action action)
        {var rect=Panel(name,parent,position,size,null);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            BattleArtStyler.Button(button,config);Label("Label",rect,label,Vector2.zero,size-Vector2.one*12,24,Color.white);
            string battle=controller.SandboxEngine.Snapshot().BattleId;
            button.onClick.AddListener(()=>{if(flow.IsCurrent&&flow.Result?.BattleId==battle)action();});return button;}
    }
}
