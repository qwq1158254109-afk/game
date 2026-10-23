namespace StudentGrowth
{
    public sealed class ShopPurchaseResult
    {
        public bool success;
        public string message;
        public ItemData item;
        public int price;
        public int moneyBefore;
        public int moneyAfter;
    }

    /// <summary>
    /// 买卡结果。与 <see cref="ShopPurchaseResult"/> 分开是因为两者的载体不同：
    /// 道具进背包（itemInventory），卡牌进主卡组（deckState.masterDeck）。
    /// 合用一个结果类会让调用方分不清该刷新哪个面板。
    /// </summary>
    public sealed class ShopCardPurchaseResult
    {
        public bool success;
        public string message;
        public CardData card;
        public int price;
        public int moneyBefore;
        public int moneyAfter;
        public int deckSizeAfter;
    }

    public sealed class ShopPurchaseService
    {
        /// <summary>稀有度兜底价：卡牌没显式写 price 时按此定价。</summary>
        public const int CommonCardPrice = 50;
        public const int UncommonCardPrice = 90;
        public const int RareCardPrice = 150;

        public ShopPurchaseResult TryPurchase(ItemData item, GameSessionState sessionState)
        {
            ShopPurchaseResult result = new ShopPurchaseResult
            {
                item = item,
                price = ResolvePrice(item)
            };

            if (item == null)
            {
                result.message = "商品数据缺失，暂时无法购买。";
                return result;
            }

            if (sessionState == null || sessionState.playerState == null || sessionState.itemInventory == null)
            {
                result.message = "当前存档状态未初始化，暂时无法购买。";
                return result;
            }

            result.moneyBefore = sessionState.playerState.money;
            if (result.moneyBefore < result.price)
            {
                result.message = "金钱不足，无法购买 " + ResolveItemName(item) + "。";
                result.moneyAfter = result.moneyBefore;
                return result;
            }

            if (result.price > 0)
            {
                sessionState.playerState.AddStat(PlayerStatKey.Money, -result.price);
            }

            sessionState.itemInventory.AddItem(item);
            result.moneyAfter = sessionState.playerState.money;
            result.success = true;
            result.message = "已购买 " + ResolveItemName(item) + "，花费 " + result.price + "。";
            return result;
        }

        /// <summary>
        /// 购买卡牌并写入主卡组。允许重复购买同一张卡——本作是 DBG 结构，
        /// 同名卡多张是正常构筑方式，不做唯一性限制。
        /// </summary>
        public ShopCardPurchaseResult TryPurchaseCard(CardData card, GameSessionState sessionState)
        {
            ShopCardPurchaseResult result = new ShopCardPurchaseResult
            {
                card = card,
                price = ResolveCardPrice(card)
            };

            if (card == null)
            {
                result.message = "卡牌数据缺失，暂时无法购买。";
                return result;
            }

            if (sessionState == null || sessionState.playerState == null || sessionState.deckState == null)
            {
                result.message = "当前存档状态未初始化，暂时无法购买。";
                return result;
            }

            result.moneyBefore = sessionState.playerState.money;
            result.deckSizeAfter = sessionState.deckState.masterDeck == null ? 0 : sessionState.deckState.masterDeck.Count;

            if (result.moneyBefore < result.price)
            {
                result.message = "金钱不足，无法购买 " + ResolveCardName(card) + "。";
                result.moneyAfter = result.moneyBefore;
                return result;
            }

            if (result.price > 0)
            {
                sessionState.playerState.AddStat(PlayerStatKey.Money, -result.price);
            }

            sessionState.deckState.AddCardToMasterDeck(card);
            result.moneyAfter = sessionState.playerState.money;
            result.deckSizeAfter = sessionState.deckState.masterDeck.Count;
            result.success = true;
            result.message = "已购买卡牌 " + ResolveCardName(card) + "，花费 " + result.price
                + "，卡组现有 " + result.deckSizeAfter + " 张。";
            return result;
        }

        public static int ResolvePrice(ItemData item)
        {
            return item == null ? 0 : UnityEngine.Mathf.Max(0, item.price);
        }

        /// <summary>
        /// 卡牌售价：显式 price 优先，否则按稀有度兜底。
        /// 兜底是为了让所有既有卡牌（都没有 price 字段值）能直接进商店，
        /// 不必先逐个补数据。
        /// </summary>
        public static int ResolveCardPrice(CardData card)
        {
            if (card == null)
            {
                return 0;
            }

            if (card.price > 0)
            {
                return card.price;
            }

            switch (card.rarity)
            {
                case CardRarity.Rare:
                    return RareCardPrice;
                case CardRarity.Uncommon:
                    return UncommonCardPrice;
                default:
                    return CommonCardPrice;
            }
        }

        private static string ResolveItemName(ItemData item)
        {
            return item == null || string.IsNullOrEmpty(item.displayName) ? "道具" : item.displayName;
        }

        private static string ResolveCardName(CardData card)
        {
            if (card == null)
            {
                return "卡牌";
            }

            if (!string.IsNullOrEmpty(card.displayName))
            {
                return card.displayName;
            }

            return string.IsNullOrEmpty(card.cardId) ? "卡牌" : card.cardId;
        }
    }
}
