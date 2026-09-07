using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class DisplaySettingsView : MonoBehaviour
    {
        private SettingsService service;
        private Dropdown display, mode, resolution, refresh;
        private Text message, countdown;
        private GameObject confirmation;
        private Button confirm, reject;
        private string shownTransaction;
        private bool rendering;
        private DisplayCapabilities caps;
        private readonly List<Vector2Int> sizes = new List<Vector2Int>();
        private readonly List<ResolvedDisplayMode> rates = new List<ResolvedDisplayMode>();
        private readonly List<SettingsWindowIntent> offeredModes = new List<SettingsWindowIntent>();
        public void Bind(SettingsService value)
        {
            Unbind(); service = value;
            service.StateChanged += Render;
            service.Display.Changed += Render;
            caps = service.Display.Capabilities; Render();
        }
        private void OnEnable() { if (service != null) { caps = service.Display.Capabilities; Render(); } }
        private void OnDisable() { service?.Display?.ViewClosed(); }
        private void OnDestroy() { service?.Display?.ViewClosed(); Unbind(); }
        private void Unbind()
        { if (service != null) { service.StateChanged -= Render; service.Display.Changed -= Render; } }
        private void Edit()
        {
            if (rendering || service == null || sizes.Count == 0 || caps.Displays.Count == 0) return;
            var d = caps.Displays[Mathf.Clamp(display.value, 0, caps.Displays.Count - 1)];
            var size = sizes[Mathf.Clamp(resolution.value, 0, sizes.Count - 1)];
            var rate = rates.Count == 0 ? d.Desktop : rates[Mathf.Clamp(refresh.value, 0, rates.Count - 1)];
            service.SetPendingDisplay(new DisplayIntent(size.x, size.y, offeredModes[mode.value],
                rate.RefreshRateNumerator, rate.RefreshRateDenominator, d.Index, d.StableId));
        }
        private void Render()
        {
            if (service == null || caps == null) return;
            rendering = true;
            var coordinator = service.Display;
            var intent = service.Pending.DisplayIntent;
            bool editable = caps.CanObserve && service.State == SettingsServiceState.Editing;
            display.interactable = mode.interactable = resolution.interactable = editable;
            refresh.interactable = editable && intent.WindowMode == SettingsWindowIntent.Exclusive;
            SetOptions(display, caps.Displays.Select(d => (d.Index + 1) + ": " + d.Name).ToList(),
                Math.Max(0, caps.Displays.ToList().FindIndex(d => d.Index == intent.PreferredDisplayIndex)));
            offeredModes.Clear();
            offeredModes.AddRange(caps.WindowModes.Where(m => m != SettingsWindowIntent.Exclusive || (caps.Displays.Count > 0 && caps.Displays[display.value].Modes.Count > 0)));
            SetOptions(mode, offeredModes.Select(m => m == SettingsWindowIntent.Windowed ? "窗口" : m == SettingsWindowIntent.Borderless ? "无边框全屏" : "独占全屏").ToList(),
                Math.Max(0, offeredModes.IndexOf(intent.WindowMode)));
            sizes.Clear(); rates.Clear();
            if (caps.Displays.Count > 0)
            {
                var d = caps.Displays[display.value];
                sizes.AddRange(d.Modes.Select(m => new Vector2Int(m.Width, m.Height)).Distinct());
                sizes.Add(new Vector2Int(Math.Min(1280, d.WorkWidth), Math.Min(720, d.WorkHeight)));
                sizes.Add(new Vector2Int(intent.Width, intent.Height));
                var sorted = sizes.Distinct().OrderBy(s => s.x).ThenBy(s => s.y).ToArray(); sizes.Clear(); sizes.AddRange(sorted);
                SetOptions(resolution, sizes.Select(s => s.x + " × " + s.y).ToList(), sizes.FindIndex(s => s.x == intent.Width && s.y == intent.Height));
                rates.AddRange(d.Modes.Where(m => m.Width == intent.Width && m.Height == intent.Height));
                if (rates.Count == 0 || intent.WindowMode != SettingsWindowIntent.Exclusive) { rates.Clear(); rates.Add(d.Desktop); }
                SetOptions(refresh, rates.Select(r => r.Hertz.ToString("0.##") + " Hz").ToList(),
                    Math.Max(0, rates.FindIndex(r => Math.Abs(r.Hertz - (double)intent.RefreshRateNumerator / intent.RefreshRateDenominator) < 0.01)));
            }
            resolution.interactable = editable && intent.WindowMode != SettingsWindowIntent.Borderless;
            shownTransaction = coordinator.TransactionId;
            bool busy = coordinator.IsBusy;
            confirmation.SetActive(busy);
            confirm.gameObject.SetActive(coordinator.CanConfirm);
            reject.interactable = coordinator.Phase != DisplayTransactionPhase.Recovering;
            countdown.text = coordinator.Phase == DisplayTransactionPhase.Observing ? "正在切换并验证显示模式…" :
                coordinator.Phase == DisplayTransactionPhase.Recovering ? "正在恢复可用的显示模式…" :
                coordinator.CanConfirm ? $"是否保留此显示模式？\n{Math.Ceiling(coordinator.RemainingSeconds)} 秒后自动恢复" : "确认已暂停，返回游戏窗口后继续。";
            var actual = coordinator.Runtime.ResolvedDisplayMode;
            var result = coordinator.LastResult;
            string outcome = result == null ? "更改显示模式后需在 15 秒内确认；窗口与无边框使用桌面刷新率。" :
                $"显示：{VerificationText(result)}；恢复：{RecoveryText(result)}；保存：{(result.PersistenceOutcome == PersistenceOutcome.PersistenceFailed && !service.IsPersistenceDirty ? "已保存" : SaveText(result))}";
            message.text = !caps.CanObserve ? "显示模式需在独立运行的游戏中调整。" :
                actual != null && actual.IsFallback ? $"偏好：{service.Applied.WindowWidth}×{service.Applied.WindowHeight}；当前：{actual}\n{outcome}" : outcome;
            rendering = false;
        }
        private static string VerificationText(DisplayTransactionResult r) => r.RequestOutcome == DisplayRequestOutcome.RequestException ? "切换请求失败" :
            r.VerificationOutcome == DisplayVerificationOutcome.Acceptable ? "已验证" : r.VerificationOutcome == DisplayVerificationOutcome.ConfirmationTimedOut ? "确认超时" :
            r.VerificationOutcome == DisplayVerificationOutcome.RejectedByPlayer ? "已取消" : "未能验证";
        private static string RecoveryText(DisplayTransactionResult r) => r.RecoveryOutcome == DisplayRecoveryOutcome.NotRequired ? "无需恢复" :
            r.RecoveryOutcome == DisplayRecoveryOutcome.RollbackSucceeded ? "已恢复" : r.RecoveryOutcome == DisplayRecoveryOutcome.RollbackDegraded ? "已使用安全模式" : "失败，请重试恢复";
        private static string SaveText(DisplayTransactionResult r) => r.PersistenceOutcome == PersistenceOutcome.Persisted ? "已保存" :
            r.PersistenceOutcome == PersistenceOutcome.PersistenceFailed ? "失败，可重试保存" : "未提交显示偏好";
        private static void SetOptions(Dropdown dropdown, List<string> options, int selected)
        {
            if (!dropdown.options.Select(o => o.text).SequenceEqual(options)) { dropdown.ClearOptions(); dropdown.AddOptions(options); }
            dropdown.SetValueWithoutNotify(Math.Max(0, selected)); dropdown.RefreshShownValue();
        }
        public static DisplaySettingsView Create(RectTransform parent, Font font)
        {
            var root = new GameObject("DisplaySettings", typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var view = root.AddComponent<DisplaySettingsView>();
            view.display = DropdownAt(rect, font, "目标显示器", 100, 590, 430);
            view.mode = DropdownAt(rect, font, "窗口模式", 560, 590, 340);
            view.resolution = DropdownAt(rect, font, "分辨率", 930, 590, 380);
            view.refresh = DropdownAt(rect, font, "刷新率", 1340, 590, 350);
            view.message = Label(rect, font, "", 100, 686, 1620, 48, 22);
            var modal = new GameObject("DisplayConfirmation", typeof(RectTransform), typeof(Image)); modal.transform.SetParent(rect, false);
            var mr = (RectTransform)modal.transform; mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = mr.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0.94f, 0.96f, 0.99f, 0.99f);
            view.confirmation = modal;
            view.countdown = Label(mr, font, "", 300, 330, 1320, 160, 38); view.countdown.alignment = TextAnchor.MiddleCenter;
            view.confirm = ButtonAt(mr, font, "保留此模式", 510, 560);
            view.reject = ButtonAt(mr, font, "恢复原模式", 1010, 560);
            view.confirm.onClick.AddListener(() => view.service?.Display.Confirm(view.shownTransaction));
            view.reject.onClick.AddListener(() => view.service?.Display.Reject(view.shownTransaction));
            foreach (var control in new[] { view.display, view.mode, view.resolution, view.refresh }) control.onValueChanged.AddListener(_ => view.Edit());
            modal.SetActive(false); return view;
        }
        private static Dropdown DropdownAt(RectTransform parent, Font font, string caption, float x, float y, float width)
        {
            Label(parent, font, caption, x, y - 28, width, 28, 21);
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources()); go.name = caption; go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y + 6, width, 48);
            foreach (var text in go.GetComponentsInChildren<Text>(true)) { text.font = font; text.fontSize = 22; text.color = Color.black; }
            var dropdown = go.GetComponent<Dropdown>(); dropdown.ClearOptions(); dropdown.interactable = false;
            var arrow = Label((RectTransform)go.transform, font, "▼", width - 30, 10, 24, 28, 18);
            foreach (var option in go.GetComponentsInChildren<Toggle>(true))
            { if (option.graphic is Image tick) { tick.sprite = GameUiStyle.GetSolidSprite(); tick.color = Color.black; } }
            return go.GetComponent<Dropdown>();
        }
        private static Text Label(RectTransform parent, Font font, string value, float x, float y, float w, float h, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.text = value; t.fontSize = size; t.color = new Color(0.1f, 0.14f, 0.2f); t.raycastTarget = false;
            Place(t.rectTransform, x, y, w, h); return t;
        }
        private static Button ButtonAt(RectTransform parent, Font font, string title, float x, float y)
        {
            var go = DefaultControls.CreateButton(new DefaultControls.Resources()); go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, 400, 80);
            var text = go.GetComponentInChildren<Text>(); text.font = font; text.fontSize = 28; text.text = title;
            return go.GetComponent<Button>();
        }
        private static void Place(RectTransform r, float x, float y, float w, float h)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
    }
}
