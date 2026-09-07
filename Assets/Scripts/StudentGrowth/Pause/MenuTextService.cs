using System;

namespace StudentGrowth
{
    // Lightweight menu text only. Does not read save records or create thumbnails.
    public sealed class MenuTextService
    {
        public string Language { get; private set; } = "zh-Hans";
        public event Action Changed;
        public void SetLanguage(string value) { if (value == Language) return; Language = value; Changed?.Invoke(); }
        public string Pick(string chinese,string english) => Language.StartsWith("en",StringComparison.OrdinalIgnoreCase) ? english : chinese;
        public string Slot(IReadOnlySaveSlotSummary slot)
        {
            string name = slot.SlotId.IsAutomatic ? Pick("自动存档","Autosave") : Pick("存档 ","Slot ") + slot.SlotId.Value;
            if (slot.IsEmpty) return name + "\n" + Pick("空槽","Empty");
            if (!slot.IsValid) return name + "\n" + Pick("存档损坏","Damaged save");
            var duration = TimeSpan.FromSeconds(Math.Min(slot.PlayedSeconds,TimeSpan.MaxValue.TotalSeconds - 1));
            string extra="";
            if(slot is SaveSlotSummary detail) {
                string location=detail.LocationId;
                switch(location) { case "location_home": location="家"; break; case "location_school": location="学校"; break; case "location_shop": location="商店"; break; case "location_park": location="公园"; break; }
                extra=" · "+TimeActionRules.GetPeriodDisplayName(detail.Period)+" · "+(string.IsNullOrEmpty(location) ? "未知地点" : location);
                extra+="\n"+(DateTimeOffset.TryParse(detail.SavedAtUtc,out var date) ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "保存时间未知");
            }
            return name + " · " + Pick("第 ","Day ") + slot.Day + Pick(" 天","") + extra;
        }
        public string Notice(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            switch (code)
            {
                case "SAVE_SUCCESS": return Pick("保存成功。","Game saved.");
                case "SAVE_AUTOMATIC_OTHER_RUN": return "自动档保留了另一局进度，本次未自动保存。可在存档面板中启用本局自动存档。";
                case "SAVE_TARGET_CHANGED": return "目标存档已变化，请重新选择并确认。";
                case "SAVE_COMMIT_UNCERTAIN": return "保存提交结果待确认，暂不能离开或再次覆盖。";
                case "SAVE_AUTOMATIC_READ_ONLY": return Pick("自动存档槽不能手动覆盖。","The autosave slot cannot be overwritten manually.");
                case "SAVE_SLOT_EMPTY": return Pick("此存档槽为空。","This slot is empty.");
                case "SAVE_BATTLE_UNSUPPORTED": return Pick("战斗中暂不能保存，请结束战斗后再保存。","Finish the battle before saving.");
                case "SAVE_INTERACTION_BUSY": return Pick("请先完成当前交互。","Finish the current interaction first.");
                case "SAVE_THUMBNAIL_UNAVAILABLE": return Pick("游戏已保存，截图暂不可用。","Game saved; preview unavailable.");
                case "PAUSE_BUSY": return Pick("请先完成当前操作或显示模式确认。","Finish the current operation or display confirmation first.");
                default: return Pick("操作未完成，请重试。","Operation failed. Please retry.") + " (" + code + ")";
            }
        }
    }
}
