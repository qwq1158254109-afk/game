using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// NPC 数据实体（2026-10-24，对话系统好感度轮）。
    ///
    /// 之前对话资产直接挂在 MapInteractionTarget.dialogue 上，NPC 没有归属：
    /// 同一个 NPC 在多个场景出现要重复配置，好感度也没有挂靠的 ID。
    /// NpcData 是 NPC 的唯一身份：npcId 进存档（好感度按它记账），头像/名字给对话 UI 兜底。
    ///
    /// 资产约定放在 Assets/Resources/StudentGrowth/NPC/ 下（NpcCatalog 按 npcId 查找）。
    /// 注意：本类必须是所在文件的主类——多类共文件的 SO 无法落资产（EndingData 的教训）。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Data/NPC Data", fileName = "Npc_")]
    public class NpcData : ScriptableObject
    {
        [Tooltip("稳定 ID。存档里的好感度按它记账，改名不迁移。建议 npc_ 前缀。")]
        public string npcId;

        [Tooltip("显示名。对话与菜单用它。")]
        public string displayName;

        [Tooltip("默认头像。对话资产行级可覆盖。")]
        public Sprite portrait;

        [TextArea(1, 4)]
        [Tooltip("一句话人设，给策划看，不进游戏逻辑。")]
        public string description;
    }
}
