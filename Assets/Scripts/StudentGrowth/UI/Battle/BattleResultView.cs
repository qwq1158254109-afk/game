using System;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    // Read-only presentation; the session consumer owns rewards and stage state.
    public sealed class BattleResultView : MonoBehaviour
    {
        public static void Show(RectTransform parent, BattleUIConfig config, Font font, BattleResultRecord record,
            GameSessionState session, Action continueAction)
        {
            var root = new GameObject("BattleResultView",typeof(RectTransform),typeof(Image),typeof(BattleResultView));
            var rect = (RectTransform)root.transform; rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            root.GetComponent<Image>().color = new Color(.04f,.025f,.015f,.86f);
            var panel=new GameObject("ResultWindow",typeof(RectTransform),typeof(Image),typeof(AspectRatioFitter));
            var p=(RectTransform)panel.transform;p.SetParent(rect,false);
            bool applied=record!=null && record.Consumption==BattleResultConsumption.Applied;
            bool success=applied && record.Success;
            Sprite sprite=config.GetArtwork(!applied?"window_recovery":success?"window_challenge_complete":"window_challenge_incomplete");
            var image=panel.GetComponent<Image>();image.sprite=sprite;image.color=Color.white;image.preserveAspect=true;
            var fitter=panel.GetComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=sprite==null?1.13f:sprite.rect.width/sprite.rect.height;
            TextAt(p,font,!applied?"结算未完成":success?"挑战完成":"本次未完成",new Vector2(.09f,.68f),new Vector2(.91f,.77f),28,FontStyle.Bold);
            string body=!applied ? "结算遇到问题，已停止自动重试。\n请使用暂停菜单退出或读取已有存档。\n"+(record?.Failure??"结果不可用")
                : (record.Challenge==null?"挑战":record.Challenge.displayName)+"\n\n"+(success?"阶段挑战通过，成长继续。":"本次未达到目标，成长仍会继续。")
                +"\n\n完成度："+record.FinalProgress+" / "+record.TargetProgress+"    用时："+record.TurnsUsed+" 回合"
                +"\n"+record.OutcomeReason+"\n累计通过："+session.stageState.defeatedBossCount+" 次";
            TextAt(p,font,body,new Vector2(.10f,.26f),new Vector2(.9f,.65f),20,FontStyle.Normal);
            var buttonRoot=new GameObject("ContinueButton",typeof(RectTransform),typeof(Image),typeof(Button));
            var b=(RectTransform)buttonRoot.transform;b.SetParent(p,false);b.anchorMin=new Vector2(.25f,.05f);b.anchorMax=new Vector2(.75f,.19f);b.offsetMin=Vector2.zero;b.offsetMax=Vector2.zero;
            var button=buttonRoot.GetComponent<Button>();button.targetGraphic=buttonRoot.GetComponent<Image>();button.interactable=applied;
            TextAt(b,font,!applied?"已停止":session.endingReached?"查看结局":"进入下一阶段",new Vector2(.06f,.12f),new Vector2(.94f,.88f),20,FontStyle.Bold).color=new Color(1f,.94f,.78f);
            BattleArtStyler.Button(button,config);
            button.onClick.AddListener(()=>{if(!session.BattleResults.TryRequestReturn(record))return;button.interactable=false;continueAction?.Invoke();});
        }
        private static Text TextAt(RectTransform parent,Font font,string value,Vector2 min,Vector2 max,int size,FontStyle style)
        {
            var node=new GameObject("Label",typeof(RectTransform),typeof(Text));var rect=(RectTransform)node.transform;rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            var text=node.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=size;text.fontStyle=style;
            text.color=new Color(.16f,.085f,.04f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
    }
}
