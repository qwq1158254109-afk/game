using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 批次 0 的资产生成器：把现有卡牌按「起始 / 奖励」分成两个卡池。
    /// 幂等——重复执行只覆盖字段，不会生成重复资产。
    /// </summary>
    public static class BattleCardPoolGenerator
    {
        private const string PoolFolder = "Assets/Resources/StudentGrowth/Battle/Pools";
        private const string CardSearchRoot = "Assets/Resources/StudentGrowth/Battle";

        private static readonly string[] StarterCardIds =
        {
            "card_study_class_focus",
            "card_study_math_practice",
            "card_study_mock_exam",
            "card_study_wrong_book",
            "card_study_english_words",
            "card_rest_good_sleep"
        };

        [MenuItem("Tools/Student Growth/Battle/Generate Card Pools")]
        public static void GenerateCardPools()
        {
            EnsureFolder(PoolFolder);

            Dictionary<string, CardData> byId = CollectCards();
            List<CardData> starterCards = new List<CardData>();
            List<string> missing = new List<string>();

            for (int i = 0; i < StarterCardIds.Length; i++)
            {
                if (byId.TryGetValue(StarterCardIds[i], out CardData card) && card != null)
                {
                    starterCards.Add(card);
                }
                else
                {
                    missing.Add(StarterCardIds[i]);
                }
            }

            List<CardData> rewardCards = new List<CardData>();
            foreach (KeyValuePair<string, CardData> pair in byId)
            {
                // 升级版（_up）只能由图书馆升级获得，不进奖励池。
                if (pair.Value != null && !IsStarter(pair.Value.cardId) && !IsUpgradeVariant(pair.Value.cardId))
                {
                    rewardCards.Add(pair.Value);
                }
            }

            rewardCards.Sort((a, b) => string.CompareOrdinal(a.cardId, b.cardId));

            CardPoolData starterPool = UpsertPool(PoolFolder + "/pool_starter.asset", "pool_starter", "起始牌组", true, starterCards);
            CardPoolData rewardPool = UpsertPool(PoolFolder + "/pool_reward.asset", "pool_reward", "奖励卡池", false, rewardCards);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missing.Count > 0)
            {
                Debug.LogWarning("[CardPool] 以下起始卡 ID 在工程里找不到，已跳过：" + string.Join(", ", missing));
            }

            Debug.Log("[CardPool] 起始池 " + starterCards.Count + " 张；奖励池 " + rewardCards.Count + " 张。"
                + " 资产目录：" + PoolFolder);

            Selection.activeObject = starterPool != null ? starterPool : rewardPool;
        }

        private static bool IsStarter(string cardId)
        {
            for (int i = 0; i < StarterCardIds.Length; i++)
            {
                if (StarterCardIds[i] == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUpgradeVariant(string cardId)
        {
            return !string.IsNullOrEmpty(cardId) && cardId.EndsWith("_up");
        }

        private static Dictionary<string, CardData> CollectCards()
        {
            Dictionary<string, CardData> result = new Dictionary<string, CardData>();
            string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { CardSearchRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null || string.IsNullOrEmpty(card.cardId))
                {
                    continue;
                }

                if (!result.ContainsKey(card.cardId))
                {
                    result.Add(card.cardId, card);
                }
            }

            return result;
        }

        private static CardPoolData UpsertPool(string path, string poolId, string displayName, bool isStarter, List<CardData> cards)
        {
            CardPoolData pool = AssetDatabase.LoadAssetAtPath<CardPoolData>(path);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<CardPoolData>();
                AssetDatabase.CreateAsset(pool, path);
            }

            pool.poolId = poolId;
            pool.displayName = displayName;
            pool.isStarterPool = isStarter;
            pool.cards = new List<CardData>(cards);
            EditorUtility.SetDirty(pool);
            return pool;
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
    }
}
