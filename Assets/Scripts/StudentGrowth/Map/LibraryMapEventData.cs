namespace StudentGrowth
{
    /// <summary>
    /// 图书馆事件资产的落盘子类。
    /// Unity 只能把「与文件名同名的类」作为 MonoScript 主类；GameEventData 定义在
    /// DataDefinitions.cs 里，该文件主类是 StageData，资产会静默反序列化成 StageData。
    /// 拆出独立文件后资产才能正常加载为事件数据。
    /// </summary>
    public sealed class LibraryMapEventData : GameEventData
    {
    }
}
