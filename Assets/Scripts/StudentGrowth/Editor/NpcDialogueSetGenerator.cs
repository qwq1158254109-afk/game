using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Editor
{
    /// <summary>
    /// 「NPC 对话套件」生成器（2026-10-24，对话系统好感度轮）。
    ///
    /// 把图书馆管理员的菜单模式模板化：一次生成 NpcData + 菜单/打招呼/送礼三段对话，
    /// 剧情组改文案、挂场景即可上一个新 NPC。生成内容：
    ///   · Resources/StudentGrowth/NPC/{npcId}.asset          —— NpcData（好感度记账 ID）
    ///   · Resources/StudentGrowth/Dialogue/Dialogue_{npcId}_Menu.asset —— 菜单
    ///       （打招呼 +1 好感并跳 greet；送礼开背包选道具，好感按礼物价格曲线积累；告辞结束）
    ///   · Resources/StudentGrowth/Dialogue/Dialogue_{npcId}_Greet.asset —— 打招呼台词占位
    ///   · Resources/StudentGrowth/Dialogue/Dialogue_{npcId}_Gift.asset  —— 收礼台词占位
    ///
    /// 菜单：Tools/Student Growth/Content/Create NPC Dialogue Set（ScriptableWizard 填 ID 和名字）
    /// 注意：同名资产会被覆盖——手改过的对话请改名后再用同 ID 生成。
    /// </summary>
    public class NpcDialogueSetGenerator : ScriptableWizard
    {
        [Tooltip("稳定 ID，建议 npc_ 前缀。存档里的好感度按它记账，生成后不要改。")]
        public string npcId = "npc_new";

        [Tooltip("游戏里显示的名字。")]
        public string displayName = "新同学";

        [MenuItem("Tools/Student Growth/Content/Create NPC Dialogue Set")]
        public static void CreateFromMenu()
        {
            DisplayWizard<NpcDialogueSetGenerator>("创建 NPC 对话套件", "生成");
        }

        private void OnWizardCreate()
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(displayName))
            {
                Debug.LogWarning("[NpcSet] npcId 和 displayName 都不能为空。");
                return;
            }

            CreateNpcDialogueSet(npcId.Trim(), displayName.Trim());
        }

        /// <summary>
        /// 一次性补丁（批处理入口，跑过一次就不需要了）：给 10-23 已上线的图书馆管理员
        /// 补建 NpcData，使她的送礼开始积累好感度。对话资产已存在，不重建。
        /// </summary>
        [MenuItem("Tools/Student Growth/Content/Retrofit Librarian NpcData (one-shot)")]
        public static void RetrofitLibrarianBatch()
        {
            NpcData data = CreateNpcData("npc_librarian", "图书管理员");
            string path = AssetDatabase.GetAssetPath(data);
            Debug.Log("[NpcSet] 图书管理员 NpcData → " + path + "（guid=" + AssetDatabase.AssetPathToGUID(path) + "）");
        }

        /// <summary>
        /// 一次性生成（批处理入口）：母亲（客厅）与售货员（商店）两个 NPC 套件。
        /// 它们的交互点原本挂着活动（家庭晚饭 / 商店浏览），菜单第 4 个选项负责回落：
        /// 母亲选「一起吃晚饭」走原活动（含时间消耗），售货员选「看看货架」打开商店。
        /// 生成后还需把场景交互点的 dialogue / npc 指到 Menu 与 NpcData 资产。
        /// </summary>
        [MenuItem("Tools/Student Growth/Content/Create Preset Sets - Mother & Shopkeeper (one-shot)")]
        public static void CreatePresetSetsBatch()
        {
            CreateNpcDialogueSet("npc_mother", "妈妈", new DialogueOption
            {
                text = "一起吃晚饭（消耗时间）",
                useFallbackActivity = true
            });
            CreateNpcDialogueSet("npc_shop_owner", "售货员", new DialogueOption
            {
                text = "看看货架（打开商店）",
                openShop = true
            });
        }

        internal static void CreateNpcDialogueSet(string id, string name, DialogueOption extraMenuOption = null)
        {
            NpcData npc = CreateNpcData(id, name);

            DialogueData menu = GetOrCreateAsset<DialogueData>(DialogueRoot + "/Dialogue_" + id + "_Menu.asset");
            menu.dialogueId = "dialogue_" + id + "_menu";
            menu.speakerName = name;
            menu.portrait = npc.portrait;
            menu.lines = new List<DialogueLine>
            {
                new DialogueLine
                {
                    text = "要和" + name + "聊点什么？",
                    options = new List<DialogueOption>
                    {
                        new DialogueOption
                        {
                            text = "打招呼",
                            rewards = new List<RewardData>
                            {
                                new RewardData { rewardType = RewardType.AddRelationship, npcId = id, value = 1 }
                            },
                            nextDialogueId = "dialogue_" + id + "_greet"
                        },
                        new DialogueOption
                        {
                            text = "赠送礼物（从背包选择）",
                            nextDialogueId = "dialogue_" + id + "_gift",
                            chooseItemAsGift = true
                        },
                        extraMenuOption,
                        new DialogueOption { text = "告辞" }
                    }
                }
            };
            EditorUtility.SetDirty(menu);

            DialogueData greet = GetOrCreateAsset<DialogueData>(DialogueRoot + "/Dialogue_" + id + "_Greet.asset");
            greet.dialogueId = "dialogue_" + id + "_greet";
            greet.speakerName = name;
            greet.portrait = npc.portrait;
            greet.lines = new List<DialogueLine>
            {
                new DialogueLine { text = "（和" + name + "打了个招呼。）" },
                new DialogueLine { text = "「今天也辛苦啦。」" + name + "点了点头。" }
            };
            EditorUtility.SetDirty(greet);

            DialogueData gift = GetOrCreateAsset<DialogueData>(DialogueRoot + "/Dialogue_" + id + "_Gift.asset");
            gift.dialogueId = "dialogue_" + id + "_gift";
            gift.speakerName = name;
            gift.portrait = npc.portrait;
            gift.lines = new List<DialogueLine>
            {
                new DialogueLine { text = name + "收下了礼物，看起来很高兴。" }
            };
            EditorUtility.SetDirty(gift);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NpcSet] 已生成 NPC 对话套件：" + name + "（" + id + "）。"
                + "挂场景时在 MapInteractionTarget 上填 dialogue=Menu 资产、npc=NpcData 资产。");
        }

        internal static NpcData CreateNpcData(string id, string name)
        {
            EnsureFolders();
            NpcData data = GetOrCreateAsset<NpcData>(NpcRoot + "/" + id + ".asset");
            data.npcId = id;
            data.displayName = name;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private const string ResourcesStudentGrowthRoot = "Assets/Resources/StudentGrowth";
        private const string NpcRoot = ResourcesStudentGrowthRoot + "/NPC";
        private const string DialogueRoot = ResourcesStudentGrowthRoot + "/Dialogue";

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "StudentGrowth");
            EnsureFolder(ResourcesStudentGrowthRoot, "NPC");
            EnsureFolder(ResourcesStudentGrowthRoot, "Dialogue");
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
