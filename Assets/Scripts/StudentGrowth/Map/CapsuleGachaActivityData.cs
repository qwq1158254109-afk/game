using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 扭蛋抽卡活动数据（ActivityType.Capsule，2026-10-24）。
    ///
    /// 为什么是子类而不是直接用 ActivityData：ActivityData 定义在 DataDefinitions.cs
    /// 的多类共文件里，直接 CreateAsset 落出的资产 m_Script 解析失败、加载得 null
    /// （实测：ShopCapsuleGacha 首版如此，报「活动数据不存在」）。项目里能正常落盘的
    /// 活动（HomeMapActivityData 等）全部是独立文件的子类，照此办理。
    /// 本类必须是所在文件的主类。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Data/Map/Capsule Gacha Activity", fileName = "CapsuleGachaActivity")]
    public sealed class CapsuleGachaActivityData : ActivityData
    {
    }
}
