using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 送礼流程的收尾分支（2026-10-27）。
    ///
    /// 锁的是「玩家点完『赠送』之后一定走得下去」。四条分支各一条：
    /// 1. 有收礼对话 → 播它，且 NPC 上下文不能丢（丢了从收礼那句起好感度小心心全灭）；
    /// 2. 送出了但选项没配 nextDialogueId → 直接结束，不能把整段菜单从头再说一遍；
    /// 3. nextDialogueId 指向未登记的对话 → 同样直接结束，不能停在菜单上点不动；
    /// 4. 玩家取消 → 回菜单重选（既有设计，回归保护）。
    ///
    /// 背景：三种失败形态的可见表现完全不同（重播 / 卡住 / 面板不关），
    /// 所以这四条都写死结果，别只测「不抛异常」。
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
        }

        [Test]
        public void GiftWithNextDialogueKeepsNpcContext()
        {
            // 真实链路里 currentNpc 是进入菜单对话时由 Play 写入的，这里照走一遍，
            // 否则测的只是「null 传 null 还是 null」，等于没测。
            runner.Play(menu, pendingAction, npc);
            SetPrivate(runner, "pendingGiftOption", GiftOption("dialogue_test_gift"));

            InvokeResumeAfterGift(runner, true);

            Assert.AreEqual(giftLine, CurrentDialogue(),
                "送出礼物后应该播的是收礼对话，不是菜单");
            Assert.AreSame(npc, runner.currentNpc,
                "收礼对话必须继承送礼所在对话的 NPC，否则好感度小心心会消失");
            Assert.IsFalse(completed, "收礼对话还没点完，不能提前触发结束回调");
        }

        [Test]
        public void GiftWithoutNextDialogueEndsInsteadOfReplayingMenu()
        {
            SetPrivate(runner, "pendingGiftOption", GiftOption(null));

            InvokeResumeAfterGift(runner, true);

            Assert.IsTrue(completed, "没有收礼对话时必须直接结束对话，交还输入");
            Assert.IsFalse(viewObject.activeSelf, "结束时对话面板必须收起，不能留在屏幕上");
        }

        [Test]
        public void GiftWithUnknownNextDialogueDoesNotStall()
        {
            SetPrivate(runner, "pendingGiftOption", GiftOption("dialogue_does_not_exist"));

            InvokeResumeAfterGift(runner, true);

            Assert.IsTrue(completed,
                "跳转目标未登记时 PlayById 只记警告，必须由送礼收尾兜底结束，否则输入闸门永远不释放");
            Assert.IsFalse(viewObject.activeSelf, "兜底结束时同样要收起面板");
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
            MethodInfo method = typeof(DialogueRunner).GetMethod(
                "ResumeAfterGift",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "DialogueRunner.ResumeAfterGift 不存在，测试已过期");
            method.Invoke(target, new object[] { giftGiven });
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
