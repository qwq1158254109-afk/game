using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    // Composition root on the existing persistent settings owner. Services outlive panel views.
    public sealed class GameApplicationRuntime : MonoBehaviour
    {
        public const string MainMenuPath="Assets/Scenes/StudentGrowth/MainMenuScene.unity";
        public const string HomePath="Assets/Scenes/StudentGrowth/Maps/HomeLivingRoomScene.unity";
        public GameSessionLifetime Lifetime { get; } = new GameSessionLifetime();
        public UnityGameSaveSource Source => source;
        public UnityGameFlowBackend Backend { get; private set; }
        public ApplicationRecoveryView Recovery { get; private set; }
        private static GameApplicationRuntime instance;
        public static string CurrentRuntimeSessionId => instance == null ? null : instance.Lifetime.RuntimeSessionId;
        public static bool OwnsPause => instance != null && instance.ownsPause;
        public static bool BlocksGameplayNavigation => instance != null && instance.Coordinator != null && instance.Coordinator.State != PauseMenuState.Gameplay;
        public static GameApplicationRuntime Instance
        {
            get
            {
                if (instance == null)
                {
                    var root = SettingsMenuController.Instance.gameObject;
                    instance = root.GetComponent<GameApplicationRuntime>();
                    if (instance == null) instance = root.AddComponent<GameApplicationRuntime>();
                }
                return instance;
            }
        }
        [SerializeField] private string newGameScenePath = HomePath;
        public PauseMenuCoordinator Coordinator { get; private set; }
        public SaveLoadService SaveService { get; private set; }
        public IGameFlowService FlowService { get; private set; }
        public MenuTextService TextService { get; private set; }
        public SaveLoadPanelView SaveView { get; private set; }
        public PauseMenuView PauseView { get; private set; }
        public string SaveDirectory { get; private set; }
        private SettingsMenuController settingsMenu;
        private SettingsService settings;
        private UnityGameSaveSource source;
        private SaveThumbnailService thumbnails;
        private TimeActionController timeController;
        private bool ownsPause, showingSlots, autoSavePending;
        private float previousScale;
        private int panelSession;
        private SaveGameRecord pendingSnapshot;
        private string pendingSession;
        private int pendingDay;
        private string automaticNotice;
        private UnityEngine.UI.Text toast;
        private float toastUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetRuntimeInstance() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Ensure() { _ = Instance; }
        private void Awake()
        {
            if(instance!=null && instance!=this) { Destroy(this); return; }
            instance = this; settingsMenu = SettingsMenuController.Instance; settings = AudioSettingsController.Instance.Service;
            SaveDirectory = Path.Combine(Application.persistentDataPath,"Saves");
#if UNITY_EDITOR
            string marker = Path.Combine(Application.dataPath,"../Temp/SettingsPlayVerification.active");
            if (File.Exists(marker) && Guid.TryParseExact(File.ReadAllText(marker).Trim(),"N",out var id))
                SaveDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/PausePlayTests",id.ToString("N")));
            else if (Application.isBatchMode) SaveDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/PauseBatchTests",Guid.NewGuid().ToString("N")));
#endif
            if(BattleP3Smoke.Requested)SaveDirectory=Path.Combine(BattleP3Smoke.PlayerOutput,"Saves",Guid.NewGuid().ToString("N"));
            if(BattleP4Smoke.Requested)SaveDirectory=Path.Combine(BattleP4Smoke.PlayerOutput,"Saves",Guid.NewGuid().ToString("N"));
            var codec = new SaveGameCodec(SaveContentResolver.CreateRuntime());
            var repository = new SaveSlotRepository(SaveDirectory); source = new UnityGameSaveSource(codec);
            SaveService = new SaveLoadService(repository,codec,source.Capture,source.CaptureThumbnail);
            SaveService.Lifetime=Lifetime;
            SaveService.SaveBlockReason=()=>UnityGameSaveSource.CurrentSession()?.BattleFlow.BlocksSave==true?"SAVE_BATTLE_FLOW_BUSY":null;
            thumbnails = new SaveThumbnailService(repository);
            Backend = new UnityGameFlowBackend(source,this);
            FlowService = new GameFlowService(Backend,
                new PostLoadPipeline(new IPostLoadHandler[] { new AchievementSyncService(null,new AchievementRule[0]) }),
                () => settings.IsBusy || SceneTransitionManager.HasActiveTransition);
            Coordinator = new PauseMenuCoordinator(settings,SaveService,FlowService,() => SceneTransitionManager.HasActiveTransition,
                SetPaused,settingsMenu.Open,settingsMenu.Close,GameQuit.Request,StartNewGameAsync);
            Coordinator.ReturnToMenu=ReturnToMenuAsync;
            Coordinator.ProgressDirty=() => Lifetime.IsActive && Lifetime.IsDirty(source.Capture());
            Coordinator.PrepareLeave=() => Lifetime.Leaving=true;
            Coordinator.CancelLeave=() => Lifetime.Leaving=false;
            TextService = new MenuTextService();
            var font = GameUiStyle.ResolveTheme(null).ResolveFont();
            PauseView = PauseMenuView.Create(transform,font); PauseView.Bind(Coordinator,TextService);
            SaveView = SaveLoadPanelView.Create(PauseView.Root,font); SaveView.Bind(TextService);
            Recovery=ApplicationRecoveryView.Create(transform,font,this);
            var toastRoot=new GameObject("SaveFeedback",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler)); toastRoot.transform.SetParent(transform,false);
            toastRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; toastRoot.GetComponent<Canvas>().sortingOrder=11000;
            var toastScale=toastRoot.GetComponent<UnityEngine.UI.CanvasScaler>(); toastScale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; toastScale.referenceResolution=new Vector2(1920,1080);
            toast=MenuUi.Label((RectTransform)toastRoot.transform,font,200,880,1520,150,25); toast.gameObject.SetActive(false);
            PauseView.ContinueRequested += ContinueGame;
            PauseView.SaveRequested += OpenSave; PauseView.LoadRequested += OpenLoad; PauseView.SettingsRequested += OpenSettings;
            PauseView.TitleRequested += RequestTitle; PauseView.QuitRequested += RequestQuit;
            PauseView.ConfirmRequested += Confirm; PauseView.CancelRequested += Back;
            SaveView.SlotSelected += SelectSlot; SaveView.BackRequested += Back; SaveView.PanelClosed += ReleaseThumbnails;
            Coordinator.StateChanged += OnStateChanged; settingsMenu.Closed += OnSettingsClosed; settings.StateChanged += RefreshLanguage;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshLanguage(); Render(); StartCoroutine(BindDayEvents());
        }
        public void TogglePause()
        {
            if (settingsMenu.IsOpen && Coordinator.State != PauseMenuState.Settings) { settingsMenu.Close(); return; }
            if (Coordinator.State == PauseMenuState.Gameplay) Coordinator.OpenPause(); else Coordinator.Back();
        }
        private void ContinueGame() => Coordinator.ContinueGame();
        private void OpenSave() => Coordinator.OpenSave();
        private void OpenLoad() => Coordinator.OpenLoad();
        private void OpenSettings() => Coordinator.OpenSettings();
        private void RequestTitle() => Coordinator.RequestConfirmation(PauseConfirmation.ReturnToTitle);
        private void RequestQuit() => Coordinator.RequestConfirmation(PauseConfirmation.Quit);
        private void Confirm() => StartCoroutine(Coordinator.ConfirmAsync());
        private void Back() => Coordinator.Back();
        private void SelectSlot(SaveSlotId slot) => Coordinator.SelectSlot(slot);
        private void OnSettingsClosed() => Coordinator.SettingsClosed();
        private void OnStateChanged(PauseMenuStateChangedEventArgs args) => Render();
        private void Render()
        {
            if (PauseView == null || SaveView == null) { ReleaseThumbnails(); return; }
            bool visible = Coordinator.State == PauseMenuState.Save || Coordinator.State == PauseMenuState.Load;
            if (visible)
            {
                SaveView.Show(Coordinator.Slots,Coordinator.State == PauseMenuState.Save,Coordinator.Notice ?? automaticNotice);
                if (!showingSlots && !Coordinator.IsTitleContext) { showingSlots = true; int token = ++panelSession; StartCoroutine(LoadThumbnails(token)); }
            }
            else { SaveView.gameObject.SetActive(false); showingSlots = false; }
            PauseView.Refresh();
        }
        private IEnumerator LoadThumbnails(int token)
        {
            foreach (var slot in Coordinator.Slots)
            {
                if (token != panelSession || SaveView == null) yield break;
                if (slot.IsEmpty) continue;
                yield return thumbnails.LoadAsync(slot.SlotId,r => { if (token == panelSession && SaveView != null) SaveView.SetThumbnail(slot.SlotId,r.Texture); });
            }
        }
        private void ReleaseThumbnails() { panelSession++; thumbnails?.ReleaseAllForPanelSession(); if (SaveView != null) SaveView.ClearThumbnails(); showingSlots = false; }
        private void RefreshLanguage() => TextService.SetLanguage(settings.Pending.LanguageCode);
        private void SetPaused(bool value)
        {
            if (value && !ownsPause) { previousScale = Time.timeScale; ownsPause = true; Time.timeScale = 0; GameplayInputGate.Acquire(this); settingsMenu.EnsureMenuInput(); }
            else if (!value && ownsPause) { ownsPause = false; Time.timeScale = previousScale; GameplayInputGate.Release(this,true); settingsMenu?.ReleaseMenuInput(); }
        }
        private void Update()
        {
            if (Coordinator == null) return;
            if(toast!=null && toast.gameObject.activeSelf && Time.realtimeSinceStartup>toastUntil) toast.gameObject.SetActive(false);
            if (Lifetime.IsActive && Coordinator.State == PauseMenuState.Gameplay && !settingsMenu.IsOpen && !GameplayInputGate.IsBlocked && Time.timeScale > 0)
                source.PlayedSeconds += Time.unscaledDeltaTime;
            if(autoSavePending && UnityGameSaveSource.CurrentSession()?.BattleFlow.BlocksSave==true)
            {autoSavePending=false;pendingSnapshot=null;StartCoroutine(CaptureDay(Lifetime.RuntimeSessionId,pendingDay));}
            if (autoSavePending && Lifetime.IsActive && !Lifetime.Leaving && Coordinator.State == PauseMenuState.Gameplay && !SaveService.IsBusy && !settings.IsBusy && !SceneTransitionManager.HasActiveTransition)
            { autoSavePending = false; StartCoroutine(SaveService.SaveSnapshotAsync(pendingSnapshot,pendingSession,r => {
                automaticNotice=r.Success ? null : r.ErrorCode; toast.text=TextService.Notice(r.Success ? "SAVE_SUCCESS" : r.ErrorCode);
                toast.gameObject.SetActive(true); toastUntil=Time.realtimeSinceStartup+7;
            })); }
        }
        private void OnSceneLoaded(Scene scene,LoadSceneMode mode) => StartCoroutine(BindDayEvents());
        private IEnumerator BindDayEvents()
        {
            if (timeController != null) timeController.DayStarted -= OnDayStarted;
            yield return null;
            if (timeController != null) timeController.DayStarted -= OnDayStarted;
            timeController = UnityEngine.Object.FindAnyObjectByType<TimeActionController>();
            if(SceneManager.GetActiveScene().path==MainMenuPath) { Coordinator.EnterTitle(); settingsMenu.EnsureMenuInput(); yield break; }
            if(!Backend.IsBusy && !Lifetime.IsActive && UnityGameSaveSource.CurrentSession()!=null) BindSession(UnityGameSaveSource.CurrentSession());
            if (timeController != null) timeController.DayStarted += OnDayStarted;
        }
        private void OnDayStarted(int day)
        {
            if(!Lifetime.IsActive || Lifetime.Leaving || pendingDay==day) return;
            pendingDay=day; string token=Lifetime.RuntimeSessionId;
            StartCoroutine(CaptureDay(token,day));
        }
        private IEnumerator CaptureDay(string token,int day)
        {
            // DayStarted occurs inside Sleep; capture only after the synchronous activity finishes.
            yield return null;
            if(!Lifetime.Matches(token)) yield break;
            while(Lifetime.Matches(token) && UnityGameSaveSource.CurrentSession()?.BattleFlow.BlocksSave==true)yield return null;
            if(!Lifetime.Matches(token))yield break;
            try { pendingSnapshot=source.Capture(); pendingSession=token; autoSavePending=true; }
            catch(Exception ex) { Coordinator.SetNotice(ex.Message); }
        }
        public void BindSession(GameSessionState state)
        {
            GameBootstrapper.SharedSession?.BattleFlow.CancelSession();
            autoSavePending=false;pendingSnapshot=null;pendingSession=null;
            GameBootstrapper.SetSharedSession(state);
            if(string.IsNullOrEmpty(state.runId)) state.runId=Guid.NewGuid().ToString("N");
            Lifetime.Begin(state.runId); pendingDay=0; automaticNotice=null;
            var player=UnityEngine.Object.FindAnyObjectByType<RpgPlayerController>();
            var sprite=player==null ? null : player.GetComponentInChildren<SpriteRenderer>();
            if(sprite?.sprite!=null) Lifetime.PixelSize=1.0/sprite.sprite.pixelsPerUnit;
        }
        public CommandResult EndSession(string expected)
        {
            GameFlowDiagnostics.Trace("Application.EndSession begin");
            if(SaveService.IsBusy || SaveService.CommitUncertain) return new CommandResult(false,"SAVE_COMMIT_UNCERTAIN");
            if(!Lifetime.IsActive) return Lifetime.End(expected);
            if(!Lifetime.Matches(expected)) return new CommandResult(false,"FLOW_SESSION_EXPIRED");
            UnityGameSaveSource.CurrentSession()?.BattleFlow.CancelSession();
            autoSavePending=false; pendingSnapshot=null; pendingSession=null;
            if(timeController!=null) timeController.DayStarted-=OnDayStarted; timeController=null;
            PlayerManager.Instance.EndSession(); GameBootstrapper.ClearSharedSession(); source.PlayedSeconds=0;
            GameFlowDiagnostics.Trace("Application.EndSession complete");
            return Lifetime.End(expected);
        }
        public IEnumerator ReturnToMenuAsync()
        { CommandResult result=null; yield return Backend.ReturnToMenuAsync(r=>result=r); if(result?.Success!=true) throw new InvalidOperationException(result?.ErrorCode ?? "FLOW_MENU_FAILED"); }
        private IEnumerator StartNewGameAsync()
        {
            CommandResult result=null; yield return Backend.StartNewAsync(newGameScenePath,r=>result=r);
            if(result?.Success!=true) throw new InvalidOperationException(result?.ErrorCode ?? "FLOW_NEW_GAME_FAILED");
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (timeController != null) timeController.DayStarted -= OnDayStarted;
            if (settings != null) settings.StateChanged -= RefreshLanguage;
            if (settingsMenu != null) settingsMenu.Closed -= OnSettingsClosed;
            if (Coordinator != null) Coordinator.StateChanged -= OnStateChanged;
            if (PauseView != null)
            {
                PauseView.ContinueRequested -= ContinueGame; PauseView.SaveRequested -= OpenSave;
                PauseView.LoadRequested -= OpenLoad; PauseView.SettingsRequested -= OpenSettings;
                PauseView.TitleRequested -= RequestTitle; PauseView.QuitRequested -= RequestQuit;
                PauseView.ConfirmRequested -= Confirm; PauseView.CancelRequested -= Back;
            }
            if (SaveView != null)
            { SaveView.SlotSelected -= SelectSlot; SaveView.BackRequested -= Back; SaveView.PanelClosed -= ReleaseThumbnails; }
            ReleaseThumbnails(); SetPaused(false);
            if (instance == this) instance = null;
        }
    }
}

