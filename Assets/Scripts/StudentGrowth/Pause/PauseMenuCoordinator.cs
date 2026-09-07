using System;
using System.Collections;
using System.Collections.Generic;

namespace StudentGrowth
{
    public enum PauseMenuState { Gameplay, Paused, Settings, Save, Load, Title, Busy }
    public enum PauseConfirmation { None, Save, Load, ReturnToTitle, Quit, NewGame, ReplaceAutomatic, Unsaved }
    public sealed class PauseMenuStateChangedEventArgs : EventArgs
    { public PauseMenuState State { get; } public PauseMenuStateChangedEventArgs(PauseMenuState state) { State = state; } }
    public interface IPauseMenuCoordinator
    {
        PauseMenuState State { get; }
        event Action<PauseMenuStateChangedEventArgs> StateChanged;
        CommandResult OpenSettings();
        CommandResult OpenSave();
        CommandResult OpenLoad();
        CommandResult ContinueGame();
        IEnumerator ReturnToTitleAsync(Action<CommandResult> completed);
        IEnumerator QuitGameAsync(Action<CommandResult> completed);
    }
    public sealed class PauseMenuCoordinator : IPauseMenuCoordinator
    {
        private readonly SettingsService settings;
        private readonly ISaveLoadService saves;
        private readonly IGameFlowService flow;
        private readonly Func<bool> transitionBusy;
        private readonly Action<bool> pause;
        private readonly Action openSettings, closeSettings, quit;
        private readonly Func<IEnumerator> newGame;
        private bool titleContext;
        private PauseMenuState operationOrigin;
        public Func<IEnumerator> ReturnToMenu { get; set; }
        public Func<bool> ProgressDirty { get; set; }
        public Action PrepareLeave { get; set; }
        public Action CancelLeave { get; set; }
        private PauseConfirmation pendingNavigation;
        private SaveSlotId pendingLoadSlot;
        private string selectedRevision, pendingLoadRevision;
        public bool HasUnsavedChoice => Confirmation==PauseConfirmation.Unsaved;
        public void SetNotice(string message) { Notice=message; Publish(); }
        public void EnterTitle() { titleContext=true; pause(false); Change(PauseMenuState.Title); }
        public void BeginNewGame() { if(!Blocked && titleContext) { Confirmation=PauseConfirmation.NewGame; Publish(); } }
        private bool NeedsSave() { try { return ProgressDirty?.Invoke()==true; } catch { return true; } }
        public void SaveBeforeLeaving()
        {
            if(!HasUnsavedChoice || Blocked) return;
            Confirmation=PauseConfirmation.None; Slots=saves.GetSlotSummaries(); Change(PauseMenuState.Save);
        }
        public IEnumerator DiscardAndLeaveAsync()
        {
            if(!HasUnsavedChoice || Blocked) yield break;
            var intent=pendingNavigation; pendingNavigation=PauseConfirmation.None; Confirmation=intent;
            yield return ExecuteNavigation(intent);
        }
        private IEnumerator ExecuteNavigation(PauseConfirmation intent)
        {
            if(intent==PauseConfirmation.ReturnToTitle) { yield return ReturnToTitleAsync(null); yield break; }
            if(intent==PauseConfirmation.Quit) { yield return QuitGameAsync(null); yield break; }
            if(intent==PauseConfirmation.Load) {
                SelectedSlot=pendingLoadSlot; selectedRevision=pendingLoadRevision;
                yield return LoadSelected();
            }
        }
        public PauseMenuState State { get; private set; }
        public PauseConfirmation Confirmation { get; private set; }
        public SaveSlotId SelectedSlot { get; private set; }
        public string Notice { get; private set; }
        public IReadOnlyList<IReadOnlySaveSlotSummary> Slots { get; private set; }
        public event Action<PauseMenuStateChangedEventArgs> StateChanged;
        public bool IsTitleContext => titleContext;
        public PauseMenuCoordinator(SettingsService settings,ISaveLoadService saves,IGameFlowService flow,Func<bool> transitionBusy,
            Action<bool> pause,Action openSettings,Action closeSettings,Action quit,Func<IEnumerator> newGame)
        { this.settings = settings; this.saves = saves; this.flow = flow; this.transitionBusy = transitionBusy; this.pause = pause;
            this.openSettings = openSettings; this.closeSettings = closeSettings; this.quit = quit; this.newGame = newGame; }
        private bool Blocked => State == PauseMenuState.Busy || settings.IsBusy || transitionBusy() || (saves is SaveLoadService service && (service.IsBusy || service.CommitUncertain));
        public CommandResult OpenPause()
        {
            if (Blocked) return Fail("PAUSE_BUSY");
            if (State != PauseMenuState.Gameplay) return Back();
            pause(true); Change(PauseMenuState.Paused); return Ok();
        }
        public CommandResult OpenSettings()
        {
            if (Blocked || (State != PauseMenuState.Gameplay && State != PauseMenuState.Paused && State != PauseMenuState.Title)) return Fail("PAUSE_BUSY");
            if (State == PauseMenuState.Gameplay) pause(true);
            Change(PauseMenuState.Settings); openSettings(); return Ok();
        }
        public void SettingsClosed() { if (State == PauseMenuState.Settings) Change(titleContext ? PauseMenuState.Title : PauseMenuState.Paused); }
        public CommandResult OpenSave() => OpenSlots(true);
        public CommandResult OpenLoad() => OpenSlots(false);
        private CommandResult OpenSlots(bool saving)
        {
            if (Blocked || (State != PauseMenuState.Paused && State != PauseMenuState.Title) || (saving && titleContext)) return Fail("PAUSE_BUSY");
            Slots = saves.GetSlotSummaries(); Change(saving ? PauseMenuState.Save : PauseMenuState.Load); return Ok();
        }
        public CommandResult ContinueGame()
        {
            if (Blocked || titleContext || State != PauseMenuState.Paused) return Fail("PAUSE_BUSY");
            Change(PauseMenuState.Gameplay); pause(false); return Ok();
        }
        public CommandResult Back()
        {
            if (Blocked) return Fail("PAUSE_BUSY");
            if (Confirmation != PauseConfirmation.None) { Confirmation = PauseConfirmation.None; pendingNavigation=PauseConfirmation.None; CancelLeave?.Invoke(); Publish(); return Ok(); }
            if (State == PauseMenuState.Settings) { closeSettings(); return Ok(); }
            if (State == PauseMenuState.Save || State == PauseMenuState.Load) { pendingNavigation=PauseConfirmation.None; CancelLeave?.Invoke(); Change(titleContext ? PauseMenuState.Title : PauseMenuState.Paused); return Ok(); }
            if (State == PauseMenuState.Paused) return ContinueGame();
            return Ok();
        }
        public CommandResult SelectSlot(SaveSlotId slot)
        {
            if (Blocked || (State != PauseMenuState.Save && State != PauseMenuState.Load)) return Fail("PAUSE_BUSY");
            var summary = Slots[slot.Value];
            if(State==PauseMenuState.Save && !summary.IsValid) return Fail("SAVE_SLOT_DAMAGED");
            if (State == PauseMenuState.Load && (summary.IsEmpty || !summary.IsValid)) return Fail(summary.IsEmpty ? "SAVE_SLOT_EMPTY" : "SAVE_SLOT_DAMAGED");
            SelectedSlot = slot; selectedRevision=(summary as SaveSlotSummary)?.Revision;
            Confirmation = State == PauseMenuState.Save ? (slot.IsAutomatic ? PauseConfirmation.ReplaceAutomatic : PauseConfirmation.Save) : PauseConfirmation.Load; Notice = null; Publish(); return Ok();
        }
        public CommandResult RequestConfirmation(PauseConfirmation kind)
        {
            if (Blocked || (State != PauseMenuState.Paused && State != PauseMenuState.Title) ||
                (kind != PauseConfirmation.ReturnToTitle && kind != PauseConfirmation.Quit && kind != PauseConfirmation.NewGame)) return Fail("PAUSE_BUSY");
            Confirmation = kind; Publish(); return Ok();
        }
        public IEnumerator ConfirmAsync()
        {
            if (Blocked || Confirmation == PauseConfirmation.None) yield break;
            var kind = Confirmation;
            if(kind==PauseConfirmation.Unsaved) { yield return DiscardAndLeaveAsync(); yield break; }
            if((kind==PauseConfirmation.Load || kind==PauseConfirmation.ReturnToTitle || kind==PauseConfirmation.Quit) && !titleContext && ProgressDirty!=null) {
                PrepareLeave?.Invoke();
                if(NeedsSave()) { pendingNavigation=kind; pendingLoadSlot=SelectedSlot; pendingLoadRevision=selectedRevision; Confirmation=PauseConfirmation.Unsaved; Publish(); yield break; }
            }
            if (kind == PauseConfirmation.ReturnToTitle) { yield return ReturnToTitleAsync(null); yield break; }
            if (kind == PauseConfirmation.Quit) { yield return QuitGameAsync(null); yield break; }
            if (kind == PauseConfirmation.NewGame)
            {
                Change(PauseMenuState.Busy); bool failed = false;
                yield return SaveCoroutineGuard.Run(newGame(),_ => failed = true);
                if (failed) { Change(PauseMenuState.Title); Fail("FLOW_NEW_GAME_FAILED"); }
                else { titleContext = false; Change(PauseMenuState.Gameplay); pause(false); }
                yield break;
            }
            operationOrigin = State; Change(PauseMenuState.Busy);
            if (kind == PauseConfirmation.Save || kind==PauseConfirmation.ReplaceAutomatic)
            {
                SaveOperationResult result = null;
                var saveRoutine=saves is SaveLoadService actual ? actual.SaveExpectedAsync(SelectedSlot,selectedRevision,kind==PauseConfirmation.ReplaceAutomatic,r=>result=r) : saves.SaveAsync(SelectedSlot,r=>result=r);
                yield return SaveCoroutineGuard.Run(saveRoutine,_ => result = null);
                Slots = saves.GetSlotSummaries(); Change(PauseMenuState.Save);
                Notice = result?.Success == true ? (result.ErrorCode ?? "SAVE_SUCCESS") : result?.ErrorCode ?? "SAVE_WRITE_FAILED"; Publish();
                if(result?.Success==true && pendingNavigation!=PauseConfirmation.None && !NeedsSave()) {
                    var intent=pendingNavigation; pendingNavigation=PauseConfirmation.None;
                    if(intent==PauseConfirmation.Load && SelectedSlot.Value==pendingLoadSlot.Value) {
                        SelectedSlot=pendingLoadSlot; selectedRevision=(Slots[SelectedSlot.Value] as SaveSlotSummary)?.Revision;
                        Change(PauseMenuState.Load); Confirmation=PauseConfirmation.Load; Notice="保存已更新读取目标，请重新确认。"; Publish();
                    } else yield return ExecuteNavigation(intent);
                } else if(result?.Success!=true) { pendingNavigation=PauseConfirmation.None; CancelLeave?.Invoke(); }
            }
            else
            {
                yield return LoadSelected();
            }
        }
        private IEnumerator LoadSelected()
        {
                var origin=titleContext ? PauseMenuState.Title : PauseMenuState.Load;
                Change(PauseMenuState.Busy);
                LoadOperationResult loaded = null;
                var read=saves is SaveLoadService actual ? actual.LoadExpectedAsync(SelectedSlot,selectedRevision,true,r=>loaded=r) : saves.LoadAsync(SelectedSlot,r=>loaded=r);
                yield return SaveCoroutineGuard.Run(read,_ => loaded = null);
                if (loaded?.Success != true) { CancelLeave?.Invoke(); Change(origin); Fail(loaded?.ErrorCode ?? "SAVE_LOAD_FAILED"); yield break; }
                GameFlowRestoreResult restored = null;
                yield return SaveCoroutineGuard.Run(flow.RestoreLoadedGameAsync(loaded.State,r => restored = r),_ => restored = null);
                if (restored?.Success != true) { CancelLeave?.Invoke(); Change(origin); Fail(restored?.ErrorCode ?? "FLOW_RESTORE_FAILED"); yield break; }
                titleContext = false; Change(PauseMenuState.Gameplay); pause(false);
        }
        public IEnumerator ReturnToTitleAsync(Action<CommandResult> completed)
        {
            if (Blocked || State == PauseMenuState.Settings) { completed?.Invoke(Fail("PAUSE_BUSY")); yield break; }
            pause(true);
            if(ReturnToMenu!=null) {
                Change(PauseMenuState.Busy); bool failed=false;
                yield return SaveCoroutineGuard.Run(ReturnToMenu(),_=>failed=true);
                if(failed) { Change(PauseMenuState.Paused); completed?.Invoke(Fail("FLOW_MENU_FAILED")); yield break; }
            }
            titleContext = true; Change(PauseMenuState.Title); pause(false); completed?.Invoke(Ok());
        }
        public IEnumerator QuitGameAsync(Action<CommandResult> completed)
        {
            if (Blocked) { completed?.Invoke(Fail("PAUSE_BUSY")); yield break; }
            // The display service owns candidate cancellation. Never write a pending display intent.
            settings.Display?.Quit(); quit(); Confirmation = PauseConfirmation.None; completed?.Invoke(Ok());
        }
        private void Change(PauseMenuState state) { State = state; Confirmation = PauseConfirmation.None; Notice = null; Publish(); }
        private void Publish()
        {
            if (StateChanged == null) return;
            foreach (Action<PauseMenuStateChangedEventArgs> listener in StateChanged.GetInvocationList())
                try { listener(new PauseMenuStateChangedEventArgs(State)); } catch { }
        }
        private CommandResult Fail(string code) { Notice = code; Publish(); return new CommandResult(false,code); }
        private static CommandResult Ok() => new CommandResult(true);
    }
}
