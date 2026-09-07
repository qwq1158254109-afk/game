using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    public partial class StudentGrowthMvpRuntimeUi
    {
        private readonly Dictionary<string,CardData> engineArtwork=new Dictionary<string,CardData>();
        public bool OpenEngineSandbox(ChallengeData challenge,IEnumerable<CardData> cards,uint seed)
        {
            var deck=cards.ToArray();
            var preparation=challengeController.StartEngineSandbox(challenge,deck,sessionState.playerState,seed);
            if(!preparation.Success){SetLogMessage("P2 配置",string.Join("\n",preparation.Errors));return false;}
            engineArtwork.Clear();foreach(var card in deck)engineArtwork[card.cardId]=card;
            RefreshEngineSandbox();return true;
        }
        public void RefreshEngineSandbox(string message="独立规则演练；长期属性和奖励不会提交。",bool reverseHand=false)
        {
            var engine=challengeController.SandboxEngine;if(engine==null)return;
            var state=engine.Snapshot();ClearContent();SetTopChallengeControlsVisible(false);
            titleText.text="P2 演练 · "+state.Definition.Name;
            statusText.text="回合 "+state.Turn+" / "+state.Definition.MaxTurns+"    行动能量 "+state.Energy;
            SetLogMessage("演练记录",message);
            var board=CreatePanel("EngineSandbox",contentRoot,panel,green);Stretch(board);
            var subtitle=CreateText("EngineRuleLabel",board,"规则 "+state.Definition.Rules+"  ·  进度 "+state.Progress+" / "+state.Definition.Target
                +"  ·  手牌上限 "+(state.Definition.HandLimit?.ToString()??"无限"),22,FontStyle.Bold,TextAnchor.MiddleCenter,text);
            SetTop(subtitle.rectTransform,24,24,16,38);
            var player=CreatePanel("EnginePlayer",board,panel,green);
            SetStretch(player,new Vector2(.05f,.39f),new Vector2(.38f,.86f),Vector2.zero,Vector2.zero);
            var hp=CreateText("EnginePlayerStats",player,"学生\n\n耐力 "+state.Hp+" / "+state.MaxHp+"\n格挡 "+state.Block
                +"\n长期体力副本 "+state.LegacyStats[PlayerStatKey.Energy]+" / "+state.LegacyStats[PlayerStatKey.MaxEnergy],24,FontStyle.Bold,TextAnchor.MiddleCenter,text);
            Stretch(hp.rectTransform,20);
            var target=CreatePanel("EngineTarget",board,panel,amber);battleEnemyTargetRect=target;
            SetStretch(target,new Vector2(.60f,.39f),new Vector2(.95f,.86f),Vector2.zero,Vector2.zero);
            var predicted=engine.GetIntentPreview();
            string intentText=state.Turn>=state.Definition.MaxTurns?"最后一回合\n结束后直接判定结果，敌方不再行动。":(state.Intent?.Name??"已结束")
                +"\n"+(state.Intent==null?"":BattlePresentationText.Effects(state.Intent.Effects))
                +"\n预计耐力损失 "+(state.Hp-predicted.Hp);
            var intentLabel=CreateText("EngineIntent",target,state.Definition.Name+"\n\n"+intentText,22,FontStyle.Bold,TextAnchor.MiddleCenter,text);
            Stretch(intentLabel.rectTransform,20);
            var end=CreateButton(board,"结束回合",Vector2.zero,new Vector2(210,70));end.name="EngineEndTurn";
            SetBottomRight((RectTransform)end.transform,new Vector2(-30,290),new Vector2(210,70));
            end.interactable=state.Phase==BattlePhase.PlayerAction;BattleArtStyler.Button(end,GetBattleUiConfig());
            end.onClick.AddListener(()=>HandleEngineReceipt(challengeController.EndEngineTurn(Request(state))));
            var close=CreateButton(board,"退出演练",Vector2.zero,new Vector2(170,54));close.name="EngineClose";
            SetTopLeft((RectTransform)close.transform,new Vector2(20,-18),new Vector2(170,54));
            close.onClick.AddListener(()=>{challengeController.DisposeEngineSandbox();ShowStageScreen("已退出演练；未提交长期属性。");});
            if(engine.Result!=null)
            {
                var result=CreateText("EngineResult",board,(engine.Result.Outcome==BattleOutcome.Victory?"挑战完成":engine.Result.Outcome==BattleOutcome.Faulted?"演练异常":"本次未完成")
                    +"\n"+engine.Result.Reason+"\n本场结果仅供核对，未发放奖励。",28,FontStyle.Bold,TextAnchor.MiddleCenter,text);
                SetStretch(result.rectTransform,new Vector2(.15f,.05f),new Vector2(.85f,.32f),Vector2.zero,Vector2.zero);return;
            }
            var hand=CreateEmpty("EngineHand",board);SetBottom(hand,210,210,28,235);
            var drag=CreateEmpty("EngineDrag",board);Stretch(drag);
            var tooltipLayer=CreateEmpty("EngineTooltips",board);Stretch(tooltipLayer);
            var tooltip=BuildBattleTooltipView(tooltipLayer);
            var layout=hand.gameObject.AddComponent<HandLayoutController>();
            layout.Initialize(hand,drag,GetComponent<Canvas>(),GetBattleUiConfig(),tooltip);
            layout.TryPlayDraggedCard=(view,pointer)=>PlayEngineView(view,pointer);
            var handCards=state.InPile(BattlePile.Hand).AsEnumerable();if(reverseHand)handCards=handCards.Reverse();
            foreach(var instance in handCards)
                CreateCardButton(hand,engineArtwork[instance.DefinitionId],layout,engine.GetCardPreview(instance.InstanceId),view=>PlayEngineView(view,null));
            layout.RefreshLayout(true);
            var reduce=CreateButton(board,"首张费用 -1",Vector2.zero,new Vector2(180,60));reduce.name="EngineReduceCost";
            SetBottomLeft((RectTransform)reduce.transform,new Vector2(20,40),new Vector2(180,60));
            string first=state.InPile(BattlePile.Hand).FirstOrDefault()?.InstanceId;
            reduce.interactable=first!=null;
            reduce.onClick.AddListener(()=>HandleEngineReceipt(challengeController.SetEngineTurnCost(Request(state,first),
                state.Cards.First(c=>c.InstanceId==first).TurnCostDelta-1)));
        }
        private bool PlayEngineView(BattleCardView view,PointerEventData pointer)
        {
            var binding=view.InstancePreview;if(binding?.Card==null)return false;
            string target=binding.Card.Definition.Target==CardTargetType.SingleEnemy
                ? pointer==null||IsPointerOverEnemyTarget(pointer)?"challenge:"+binding.State.Definition.Id:null
                :binding.Card.Definition.Target==CardTargetType.Self?BattleEngine.SelfTargetId:null;
            var receipt=challengeController.PlayEngineCard(Request(binding.State,binding.Card.InstanceId,target));
            HandleEngineReceipt(receipt);return receipt.Accepted;
        }
        private void HandleEngineReceipt(BattleActionReceipt receipt)
        {
            if(receipt.Accepted)RefreshEngineSandbox(receipt.Failure==BattleFailure.None?"操作完成。":BattlePresentationText.Failure(receipt.Failure));
            else SetLogMessage("演练记录",BattlePresentationText.Failure(receipt.Failure));
        }
        private static BattleRequest Request(BattleSnapshot state,string instance=null,string target=null)
            =>new BattleRequest(state.RuntimeSessionId,state.BattleId,state.Revision,Guid.NewGuid().ToString("N"),instance,target);
    }
}
