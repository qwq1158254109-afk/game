using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 扭蛋卡包配置（设计文档：Docs/CapsuleGachaDesign-2026-10-24.md）。
    /// 资产由 CapsuleMachineBuilder 生成（capsule_pack_default），改数值不用动代码。
    /// 注意：本类必须是所在文件的主类——共文件 SO 无法落资产。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Data/Capsule Pack Data", fileName = "capsule_pack_")]
    public class CapsulePackData : ScriptableObject
    {
        [Tooltip("稳定 ID。")]
        public string packId;

        [Tooltip("单抽价格（金）。")]
        public int price = 40;

        [Tooltip("普通品质权重。")]
        public int commonWeight = 65;

        [Tooltip("非凡品质权重。")]
        public int uncommonWeight = 27;

        [Tooltip("稀有品质权重。")]
        public int rareWeight = 8;

        [Tooltip("每局首抽保底的最低品质。")]
        public CardRarity pityMinRarity = CardRarity.Uncommon;

        [Tooltip("抽到主卡组已有的卡时返还的金币。")]
        public int duplicateRefund = 15;
    }
}
