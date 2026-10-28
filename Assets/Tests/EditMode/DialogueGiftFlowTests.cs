using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 送礼流程的收尾分支（2026-10-27 二次修订：用户要求「赠礼完之后回到对话界面」）。
    ///
    /// 锁的契约是「送礼走完一定回到菜单，而不是被踢回地图」：
    /// 1. 有收礼对话 → 先播收礼那句，播完回菜单；NPC 上下文全程不能丢
    ///    （丢了从收礼那句起好感度小心心就全灭）；
    /// 2. 送出了但选项没配 nextDialogueId → 直接回菜单；
    /// 3. nextDialogueId 指向未登记的对话 → 同样回菜单，不能停在菜单上点不动；
    /// 4. 玩家取消 → 回菜单（既有设计）；
    /// 5. 连菜单都没记住（异常数据）→ 收起面板并结束，不能留个点不动的对话框。
    ///
    /// 第 1 条顺带防一个死循环：Play 会重新登记 pendingComplete，
    /// 如果回菜单时用的不是提前抓好的原回调，菜单播完会再绕回送礼收尾。
    /// </summary>
    public class DialogueGiftFlowTests
    {
        private GameObject viewObject;
        private GameObject runnerObject;
        private GameObject sfxHost;
        private DialogueView view;
        private DialogueRunner runner;
        private NpcData npc;
        private DialogueData menu;
        private DialogueData giftLine;
        private DialogueData greetLine;
        private bool completed;
        private Action pendingAction;

        [SetUp]
        public void Setup()
        {
            InstallSilentSfx();

            viewObject = new GameObject("GiftFlowTestView");
            view = viewObject.AddComponent<DialogueView>();
            // EditMode 下 AddComponent 不会跑 Awake，panelRoot 仍是 null，
            // 于是 SetVisible 什么都做不了、面板永远“关不掉”。这里手动补上。
            view.panelRoot = viewObject;
            // 先收起一次：面板 GameObject 处于关闭状态时，Show 里的打字机协程不会启动
            // （EditMode 推进不了协程，只会留下无用警告）。
            view.Hide();

            runnerObject = new GameObject("GiftFlowTestRunner");
            runner = runnerObject.AddComponent<DialogueRunner>();
            runner.view = view;

            npc = ScriptableObject.CreateInstance<NpcData>();
            npc.npcId = "npc_test_gift";
            npc.displayName = "测试同学";

            menu = MakeDialogue("dialogue_test_menu", "要和测试同学聊点什么？");
            giftLine = MakeDialogue("dialogue_test_gift", "收下了礼物，看起来很高兴。");
            runner.Register(menu);
            runner.Register(giftLine);

            completed = false;
            pendingAction = () => completed = true;
            SetPrivate(runner, "currentDialogueData", menu);
            SetPrivate(runner, "giftReturnDialogue", menu);
            SetPrivate(runner, "pendingComplete", pendingAction);
        }

        [TearDown]
        public void Cleanup()
        {
            ReleaseSilentSfx();

            if (runnerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(runnerObject);
            }

            if (viewObject != null)
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }

            if (npc != null)
            {
                UnityEngine.Object.DestroyImmediate(npc);
            }

            if (menu != null)
            {
                UnityEngine.Object.DestroyImmediate(menu);
            }

            if (giftLine != null)
            {
                UnityEngine.Object.DestroyImmediate(giftLine);
            }

            if (greetLine != null)
            {
                UnityEngine.Object.DestroyImmediate(greetLine);
                greetLine = null;
            }
        }

        [Test]
        public void GiftPlaysReceiptLineThenReturnsToMenu()
        {
            // 真实链路里 currentNpc 是进入菜单对话时由 Play 写入的，这里照走一遍，
            // 否则测的只是「null 传 null 还是 null」，等于没测。
            runner.Play(menu, pendingAction, npc);
            SetPrivate(runner, "pendingGiftOption", GiftOption("dialogue_test_gift"));
            SetPrivate(runner, "giftReturnDialogue", menu);

            InvokeResumeAfterGift(runner, true);

            Assert.AreEqual(giftLine, CurrentDialogue(), "送出礼物后应该先播收礼对话");
            Assert.AreSame(npc, runner.currentNpc,
                "收礼对话必须继承送礼所在对话的 NPC，否则好感度小心心会消失");
            Assert.IsFalse(completed, "收礼对话还没播完，不能提前触发结束回调");

            // 点一下把收礼那句播完。
            view.Advance();

            Assert.AreEqual(menu, CurrentDialogue(), "收礼那句播完必须回到菜单，不能直接结束整段对话");
            Assert.IsTrue(viewObject.activeSelf, "回菜单时对话面板必须还开着");
            Assert.IsFalse(completed, "回菜单不等于结束整段对话（告辞才结束）");
        }

        [Test]
        public void GiftWithoutNextDialogueReturnsToMenu()
        {
            SetPrivate(runner, "pendingGiftOption", GiftOption(null));

            InvokeResumeAfterGift(runner, true);

            Assert.AreEqual(menu, CurrentDialogue(), "没配收礼对话时也应回到菜单，而不是把玩家踢回地图");
            Assert.IsTrue(viewObject.activeSelf, "回菜单时对话面板必须还开着");
            Assert.IsFalse(completed, "回菜单不等于结束整段对话");
        }

        [Test]
        public void GiftWithUnknownNextDialogueReturnsToMenu()
        {
            SetPrivate(runner, "pendingGiftOption", GiftOption("dialogue_does_not_exist"));

            InvokeResumeAfterGift(runner, true);

            Assert.AreEqual(menu, CurrentDialogue(),
                "跳转目标未登记时 PlayById 只记警告，必须由送礼收尾把菜单接回来，否则对话会停在点不动的状态");
            Assert.IsTrue(viewObject.activeSelf, "回菜单时对话面板必须还开着");
            Assert.IsFalse(completed, "回菜单不等于结束整段对话");
        }

        [Test]
        public void CancelGiftReturnsToMenu()
        {
            SetPrivate(runner, "pendingGiftOption", GiftOption("dialogue_test_gift"));

            InvokeResumeAfterGift(runner, false);

            Assert.AreEqual(menu, CurrentDialogue(), "取消送礼后应回到菜单重选");
            Assert.IsTrue(viewObject.activeSelf, "回菜单时对话面板必须还开着");
            Assert.IsFalse(completed, "回菜单不等于结束对话");
        }

        [Test]
        public void GiftWithoutMenuEndsInsteadOfStalling()
        {
            SetPrivate(runner, "giftReturnDialogue", null);
            SetPrivate(runner, "pendingGiftOption", GiftOption("dialogue_does_not_exist"));

            InvokeResumeAfterGift(runner, true);

            Assert.IsTrue(completed,
                "连菜单都没记住时必须兜底结束并放行输入，否则输入闸门永远不释放");
            Assert.IsFalse(viewObject.activeSelf, "兜底结束时同样要收起面板");
        }

        [Test]
        public void JumpReturnsToMenuAfterChildDialogue()
        {
            // 「打招呼」这类子对话：View 先发 onOptionChosen、再发 onJumpRequested，
            // 这里照同样的顺序调，别只测其中一步。
            runner.Play(menu, pendingAction, npc);
            greetLine = MakeDialogue("dialogue_test_greet", "「你也好。」");
            runner.Register(greetLine);
            DialogueOption greet = new DialogueOption
            {
                text = "打招呼",
                nextDialogueId = "dialogue_test_greet"
            };

            InvokePrivate(runner, "HandleOptionChosen", greet);
            InvokePrivate(runner, "HandleJumpRequested", "dialogue_test_greet");

            Assert.AreEqual(greetLine, CurrentDialogue(), "选了打招呼应该播打招呼那段");
            Assert.IsFalse(completed, "子对话还没播完，不能提前结束整段对话");

            view.Advance();

            Assert.AreEqual(menu, CurrentDialogue(), "打招呼播完必须回到菜单，不能把玩家丢回地图");
            Assert.IsTrue(viewObject.activeSelf, "回菜单时对话面板必须还开着");
            Assert.IsFalse(completed, "回菜单不等于结束整段对话（告辞才结束）");

            // 回菜单后再点一次打招呼，仍然要能回菜单（防自我循环 / 状态没清干净）。
            InvokePrivate(runner, "HandleOptionChosen", greet);
            InvokePrivate(runner, "HandleJumpRequested", "dialogue_test_greet");
            view.Advance();

            Assert.AreEqual(menu, CurrentDialogue(), "第二次打招呼也要回菜单");
            Assert.IsFalse(completed, "第二次回菜单同样不等于结束对话");
        }

        // ── 工具 ────────────────────────────────────────────────

        /// <summary>
        /// EditMode 下任何一次 Sfx 播放都会走 SfxPlayer.Bootstrap()，
        /// 而 Bootstrap 里的 DontDestroyOnLoad 在编辑模式下直接抛 InvalidOperationException，
        /// 会把被测流程整条打断（表现为 TargetInvocationException 包一层）。
        /// 这里塞一个「没建过声道池」的实例：PlayInternal 看到 sources == null 就 return，
        /// 既不出声也不抛异常。不能调 Build()——那会真去建 AudioSource。
        /// </summary>
        private void InstallSilentSfx()
        {
            FieldInfo field = typeof(SfxPlayer).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || field.GetValue(null) != null)
            {
                return;
            }

            sfxHost = new GameObject("[Test] SfxSilencer");
            SfxPlayer player = sfxHost.AddComponent<SfxPlayer>();
            field.SetValue(null, player);
        }

        private void ReleaseSilentSfx()
        {
            FieldInfo field = typeof(SfxPlayer).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null && field.GetValue(null) is SfxPlayer player && player != null && player.gameObject == sfxHost)
            {
                field.SetValue(null, null);
            }

            if (sfxHost != null)
            {
                UnityEngine.Object.DestroyImmediate(sfxHost);
                sfxHost = null;
            }
        }

        private static DialogueOption GiftOption(string nextDialogueId)
        {
            DialogueOption option = new DialogueOption
            {
                text = "赠送礼物（从背包选择）",
                chooseItemAsGift = true,
                nextDialogueId = nextDialogueId
            };
            return option;
        }

        private static DialogueData MakeDialogue(string dialogueId, string text)
        {
            DialogueData data = ScriptableObject.CreateInstance<DialogueData>();
            data.dialogueId = dialogueId;
            data.speakerName = "测试";
            data.lines = new List<DialogueLine>
            {
                new DialogueLine { text = text }
            };
            return data;
        }

        /// <summary>台词面板当前在播哪一段（private 字段，测试用反射读）。</summary>
        private DialogueData CurrentDialogue()
        {
            return GetPrivate<DialogueData>(view, "currentDialogue");
        }

        private static void InvokeResumeAfterGift(DialogueRunner target, bool giftGiven)
        {
            InvokePrivate(target, "ResumeAfterGift", giftGiven);
        }

        private static void InvokePrivate(DialogueRunner target, string methodName, params object[] args)
        {
            MethodInfo method = typeof(DialogueRunner).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "DialogueRunner." + methodName + " 不存在，测试已过期");
            method.Invoke(target, args);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "找不到私有字段 " + fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "找不到私有字段 " + fieldName);
            return (T)field.GetValue(target);
        }
    }
}
