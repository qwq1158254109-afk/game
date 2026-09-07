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

    public sealed class ShopPurchaseService
    {
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

        public static int ResolvePrice(ItemData item)
        {
            return item == null ? 0 : UnityEngine.Mathf.Max(0, item.price);
        }

        private static string ResolveItemName(ItemData item)
        {
            return item == null || string.IsNullOrEmpty(item.displayName) ? "道具" : item.displayName;
        }
    }
}
