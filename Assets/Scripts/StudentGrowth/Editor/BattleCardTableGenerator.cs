using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 批次 3：按《卡牌系统完整设计方案》§2.5 的 36 张卡表生成/更新卡资产。
    /// 幂等：按 cardId 在工程里找已有资产（P2/P4 里的会被原地更新，不会重复建）。
    /// 数值来源＝设计稿表格，未做二次调整。
    /// </summary>
    public static class BattleCardTableGenerator
    {
        private const string TargetFolder = "Assets/Resources/StudentGrowth/Battle/Cards";
        private const string ArtRoot = "Assets/Art/CardReferences/FanworkPixel/Generated/PNG/";

        private sealed class Spec
        {
            public string id;
            public string name;
            public CardType type;
            public CardRarity rarity;
            public int cost;
            public string description;
            public bool hasArt;
            public bool exhaust;
            public CardEffectType[] effectTypes;
            public int[] values;
            public PlayerStatKey[] stats;   // 与 effectTypes 等长，非属性效果传 PlayerStatKey.Chinese 占位
            public string upgradeTo;
        }

        [MenuItem("Tools/Student Growth/Battle/Generate Card Table (36)")]
        public static void Generate()
        {
            EnsureFolder(TargetFolder);
            List<Spec> specs = BuildSpecs();
            Dictionary<string, CardData> existing = CollectExisting();

            int created = 0, updated = 0;
            Dictionary<string, CardData> resolved = new Dictionary<string, CardData>();

            for (int i = 0; i < specs.Count; i++)
            {
                Spec s = specs[i];
                CardData card;
                if (existing.TryGetValue(s.id, out CardData found) && found != null)
                {
                    card = found;
                    updated++;
                }
                else
                {
                    card = ScriptableObject.CreateInstance<BattleCardAsset>();
                    string path = TargetFolder + "/" + s.id + ".asset";
                    AssetDatabase.CreateAsset(card, path);
                    created++;
                }

                Apply(card, s);
                EditorUtility.SetDirty(card);
                resolved[s.id] = card;
            }

            // 升级链：第二遍解析，避免顺序依赖
            for (int i = 0; i < specs.Count; i++)
            {
                Spec s = specs[i];
                if (string.IsNullOrEmpty(s.upgradeTo) || !resolved.ContainsKey(s.id))
                {
                    continue;
                }

                if (resolved.TryGetValue(s.upgradeTo, out CardData up) && up != null)
                {
                    CardData card = resolved[s.id];
                    card.isUpgradeable = true;
                    card.upgradedVersion = up;
                    EditorUtility.SetDirty(card);
                }
                else
                {
                    Debug.LogWarning("[CardTable] 缺升级版资产：" + s.upgradeTo + "（来源 " + s.id + "）");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardTable] 完成：新建 " + created + "，更新 " + updated + "，表内共 " + specs.Count + " 张。目录 " + TargetFolder);
        }

        private static void Apply(CardData card, Spec s)
        {
            card.effectSchemaVersion = CardEffectSchemaVersion.Unified;
            card.requireSupportedBattleContent = true;
            card.cardId = s.id;
            card.displayName = s.name;
            card.cardType = s.type;
            card.rarity = s.rarity;
            card.cost = s.cost;
            card.description = s.description;
            card.targetType = s.type == CardType.Rest || s.type == CardType.Trait
                ? CardTargetType.Self
                : CardTargetType.SingleEnemy;

            // 文档 §1 第 8 条：Unified 下 damage / block / drawCount / exhaustAfterUse 一律填 0，效果全写 effects
            card.damage = 0;
            card.block = 0;
            card.drawCount = 0;
            card.discardCount = 0;
            card.exhaustAfterUse = false;
            card.exhaust = s.exhaust;
            card.ethereal = false;
            card.retain = false;
            card.isUpgradeable = false;
            card.upgradedVersion = null;

            card.effects = new List<CardEffectData>();
            if (s.effectTypes != null)
            {
                for (int i = 0; i < s.effectTypes.Length; i++)
                {
                    card.effects.Add(new CardEffectData
                    {
                        effectType = s.effectTypes[i],
                        targetStat = s.stats != null && i < s.stats.Length ? s.stats[i] : PlayerStatKey.Chinese,
                        value = s.values != null && i < s.values.Length ? s.values[i] : 0
                    });
                }
            }

            // 有图就以文件为准（新画的卡图落盘后自动绑定，不用再改表里的 hasArt）
            string artPath = ArtRoot + s.id + ".png";
            bool hasFile = System.IO.File.Exists(artPath);
            if (s.hasArt || hasFile)
            {
                card.imagePath = artPath;
                card.cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            }
            else
            {
                card.imagePath = string.Empty;
                card.cardSprite = null;
            }
        }

        private static Dictionary<string, CardData> CollectExisting()
        {
            Dictionary<string, CardData> map = new Dictionary<string, CardData>();
            string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Resources/StudentGrowth" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card != null && !string.IsNullOrEmpty(card.cardId) && !map.ContainsKey(card.cardId))
                {
                    map.Add(card.cardId, card);
                }
            }

            return map;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        // ---------- 卡表（§2.5，逐条照录） ----------

        private static Spec C(string id, string name, CardType type, CardRarity rarity, int cost,
            string desc, bool art, bool exhaust, string upgradeTo, CardEffectType[] effects, int[] values, PlayerStatKey[] stats = null)
        {
            return new Spec
            {
                id = id,
                name = name,
                type = type,
                rarity = rarity,
                cost = cost,
                description = desc,
                hasArt = art,
                exhaust = exhaust,
                upgradeTo = upgradeTo,
                effectTypes = effects,
                values = values,
                stats = stats
            };
        }

        private static CardEffectType[] Fx(params CardEffectType[] items) => items;

        private static List<Spec> BuildSpecs()
        {
            List<Spec> list = new List<Spec>();

            CardEffectType P = CardEffectType.AddChallengeProgress;
            CardEffectType B = CardEffectType.GainBlock;
            CardEffectType D = CardEffectType.DrawCards;
            CardEffectType EN = CardEffectType.GainEnergy;      // 长期精力，战后回写
            CardEffectType RP = CardEffectType.ReducePressure;
            CardEffectType AP = CardEffectType.AddPressure;
            CardEffectType S = CardEffectType.AddPlayerStat;
            CardEffectType H = CardEffectType.DamageBattleHp;   // 自伤
            CardEffectType FO = CardEffectType.GainFocus;

            PlayerStatKey CH = PlayerStatKey.Chinese, MA = PlayerStatKey.Math, ENG = PlayerStatKey.English;

            // A. 起始牌组（6）
            list.Add(C("card_study_class_focus", "课堂专注", CardType.Study, CardRarity.Common, 1, "推进 6。", true, false, null, Fx(P), new[] { 6 }));
            list.Add(C("card_study_math_practice", "数学练习", CardType.Study, CardRarity.Common, 1, "推进 8。", true, false, null, Fx(P), new[] { 8 }));
            list.Add(C("card_study_english_words", "背单词", CardType.Study, CardRarity.Common, 1, "推进 7。", true, false, null, Fx(P), new[] { 7 }));
            list.Add(C("card_study_wrong_book", "整理错题", CardType.Study, CardRarity.Common, 1, "推进 10。", true, false, null, Fx(P), new[] { 10 }));
            list.Add(C("card_study_mock_exam", "模拟考试", CardType.Study, CardRarity.Uncommon, 2, "推进 18。", true, false, null, Fx(P), new[] { 18 }));
            list.Add(C("card_rest_good_sleep", "好好睡觉", CardType.Rest, CardRarity.Common, 1,
                "格挡 5；压力 -3；精力 +6（战后回写）。", true, false, null, Fx(B, RP, EN), new[] { 5, 3, 6 }));

            // B. Common 获取池（14）
            list.Add(C("card_study_note_taking", "课堂笔记", CardType.Study, CardRarity.Common, 1, "推进 7；语文 +1。", false, false, null, Fx(P, S), new[] { 7, 1 }, new[] { CH, CH }));
            list.Add(C("card_study_word_dictation", "默写单词", CardType.Study, CardRarity.Common, 1, "推进 6；英语 +1。", false, false, null, Fx(P, S), new[] { 6, 1 }, new[] { CH, ENG }));
            list.Add(C("card_study_correction_book", "错题重做", CardType.Study, CardRarity.Common, 1, "推进 6；数学 +1。", false, false, null, Fx(P, S), new[] { 6, 1 }, new[] { CH, MA }));
            list.Add(C("card_study_after_class_review", "课后复盘", CardType.Study, CardRarity.Common, 1, "推进 8。", true, false, null, Fx(P), new[] { 8 }));
            list.Add(C("card_rest_daydream", "发会儿呆", CardType.Rest, CardRarity.Common, 1, "格挡 5；压力 -3。", true, false, null, Fx(B, RP), new[] { 5, 3 }));
            list.Add(C("card_rest_music_relax", "听音乐放松", CardType.Rest, CardRarity.Common, 1, "压力 -4（战后回写）。", true, false, null, Fx(RP), new[] { 4 }));
            list.Add(C("card_rest_walk_outside", "出去走走", CardType.Rest, CardRarity.Common, 1, "精力 +5；压力 -2（战后回写）。", true, false, null, Fx(EN, RP), new[] { 5, 2 }));
            list.Add(C("card_social_group_discussion", "小组讨论", CardType.Social, CardRarity.Common, 1, "推进 6；社交 +2。", true, false, null, Fx(P, S), new[] { 6, 2 }, new[] { CH, PlayerStatKey.Social }));
            list.Add(C("card_social_borrow_notes", "借阅笔记", CardType.Social, CardRarity.Common, 1, "推进 5；抽 1 张。", true, false, null, Fx(P, D), new[] { 5, 1 }));
            list.Add(C("card_social_help_classmate", "帮助同学", CardType.Social, CardRarity.Common, 1, "推进 4；自信 +2。", true, false, null, Fx(P, S), new[] { 4, 2 }, new[] { CH, PlayerStatKey.Confidence }));
            list.Add(C("card_family_dinner_talk", "晚饭聊天", CardType.Family, CardRarity.Common, 1, "推进 4；亲子 +2；压力 -2。", true, false, null, Fx(P, S, RP), new[] { 4, 2, 2 }, new[] { CH, PlayerStatKey.ParentRelation, CH }));
            list.Add(C("card_family_do_housework", "做点家务", CardType.Family, CardRarity.Common, 1, "推进 4；自主 +2。", true, false, null, Fx(P, S), new[] { 4, 2 }, new[] { CH, PlayerStatKey.Autonomy }));
            list.Add(C("card_interest_sketch", "随手涂鸦", CardType.Interest, CardRarity.Common, 1, "推进 4；兴趣 +2。", false, false, null, Fx(P, S), new[] { 4, 2 }, new[] { CH, PlayerStatKey.Interest }));
            list.Add(C("card_interest_read_extra", "课外阅读", CardType.Interest, CardRarity.Common, 1, "推进 5；兴趣 +1。", false, false, null, Fx(P, S), new[] { 5, 1 }, new[] { CH, PlayerStatKey.Interest }));

            // C. Uncommon 池（10，每张带代价）
            list.Add(C("card_study_ask_teacher", "请教老师", CardType.Study, CardRarity.Uncommon, 2, "推进 16；自信 +2。", false, false, "card_study_ask_teacher_up", Fx(P, S), new[] { 16, 2 }, new[] { CH, PlayerStatKey.Confidence }));
            list.Add(C("card_study_full_mock", "全真模拟", CardType.Study, CardRarity.Uncommon, 2, "推进 20；压力 +3。", false, false, null, Fx(P, AP), new[] { 20, 3 }));
            list.Add(C("card_study_deep_review", "深度复盘", CardType.Study, CardRarity.Uncommon, 2, "推进 18；自伤 3。", false, false, null, Fx(P, H), new[] { 18, 3 }));
            list.Add(C("card_study_topic_summary", "专题总结", CardType.Study, CardRarity.Uncommon, 2, "推进 19；自伤 2。", false, false, null, Fx(P, H), new[] { 19, 2 }));
            list.Add(C("card_rest_full_sleep", "睡个好觉", CardType.Rest, CardRarity.Uncommon, 1, "格挡 12；压力 -6；消耗。", false, true, null, Fx(B, RP), new[] { 12, 6 }));
            list.Add(C("card_social_club_activity", "社团活动", CardType.Social, CardRarity.Uncommon, 2, "推进 12；社交 +3。", true, false, null, Fx(P, S), new[] { 12, 3 }, new[] { CH, PlayerStatKey.Social }));
            list.Add(C("card_family_parent_support", "父母的支持", CardType.Family, CardRarity.Uncommon, 1, "推进 6；亲子 +2；压力 -5。", true, false, null, Fx(P, S, RP), new[] { 6, 2, 5 }, new[] { CH, PlayerStatKey.ParentRelation, CH }));
            list.Add(C("card_family_talk_parent", "和家长谈谈", CardType.Family, CardRarity.Uncommon, 1, "推进 5；亲子 +4。", true, false, null, Fx(P, S), new[] { 5, 4 }, new[] { CH, PlayerStatKey.ParentRelation }));
            list.Add(C("card_interest_competition", "参加竞赛", CardType.Interest, CardRarity.Uncommon, 2, "推进 14；兴趣 +3。", false, false, null, Fx(P, S), new[] { 14, 3 }, new[] { CH, PlayerStatKey.Interest }));
            list.Add(C("card_trait_focus_moment", "专注时刻", CardType.Trait, CardRarity.Uncommon, 0, "获得专注 3（此后每张学业牌 +3 推进）；消耗。", false, true, null, Fx(FO), new[] { 3 }));

            // D. Rare 池（6，全部消耗）
            list.Add(C("card_rare_all_nighter", "通宵复习", CardType.Study, CardRarity.Rare, 3, "推进 40；自伤 12；消耗。", false, true, "card_rare_all_nighter_up", Fx(P, H), new[] { 40, 12 }));
            list.Add(C("card_rare_mock_ranking", "模考冲刺", CardType.Study, CardRarity.Rare, 3, "推进 38；消耗。", false, true, null, Fx(P), new[] { 38 }));
            list.Add(C("card_rare_perfect_note", "完美笔记", CardType.Study, CardRarity.Rare, 2, "推进 26；抽 1 张；消耗。", false, true, null, Fx(P, D), new[] { 26, 1 }));
            list.Add(C("card_rare_showcase", "成果展示", CardType.Social, CardRarity.Rare, 2, "推进 22；格挡 10；消耗。", false, true, null, Fx(P, B), new[] { 22, 10 }));
            list.Add(C("card_rare_deep_focus", "心流", CardType.Trait, CardRarity.Rare, 1, "获得专注 6；消耗。", false, true, null, Fx(FO), new[] { 6 }));
            list.Add(C("card_rare_parent_letter", "家长的一封信", CardType.Family, CardRarity.Rare, 1, "格挡 18；压力 -10；消耗。", false, true, null, Fx(B, RP), new[] { 18, 10 }));

            // E. 升级版（§2.5-E：Uncommon / Rare 各 1 张，效果 +50%、费不变）
            list.Add(C("card_study_ask_teacher_up", "请教老师+", CardType.Study, CardRarity.Uncommon, 2, "推进 24；自信 +3。", false, false, null, Fx(P, S), new[] { 24, 3 }, new[] { CH, PlayerStatKey.Confidence }));
            list.Add(C("card_rare_all_nighter_up", "通宵复习+", CardType.Study, CardRarity.Rare, 3, "推进 60；自伤 12；消耗。", false, true, null, Fx(P, H), new[] { 60, 12 }));

            return list;
        }
    }
}
