using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public sealed class PauseSaveTests
    {
        private string directory;
        private SaveGameCodec codec;
        private SaveLoadService saves;
        private SaveSlotRepository repository;
        private GameSessionState session;
        private SaveGameRecord record;
        private SettingsService settings;
        private PauseMenuCoordinator pause;
        private FlowBackend backend;
        private bool paused;
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        [SetUp] public void Setup()
        {
            directory = Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/PauseSaveTests",Guid.NewGuid().ToString("N")));
            codec = new SaveGameCodec(new SaveContentResolver()); repository = new SaveSlotRepository(directory);
            session = new GameSessionState(); session.timeActionState.currentDay = 4; session.timeActionState.currentActionPoints = 2;
            session.playerState.money = 678; session.locationState.locationFlags["met_librarian"] = true;
            record = codec.Capture(session,"Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity",3661,new Vector2(2,3),Vector2.left);
            saves = new SaveLoadService(repository,codec,() => record,() => Png());
            settings = new SettingsService(new SettingsRepo(),new ISettingsDomainApplier[0]); settings.Initialize();
            backend = new FlowBackend();
            pause = new PauseMenuCoordinator(settings,saves,new GameFlowService(backend,new PostLoadPipeline(new IPostLoadHandler[0]),() => false),
                () => false,b => paused = b,() => { },() => { },() => { },() => Empty());
        }
        [TearDown] public void Cleanup()
        {
            foreach(var obj in objects) if(obj != null) UnityEngine.Object.DestroyImmediate(obj); objects.Clear();
            // This unique test-owned directory is inside the workspace; no player preferences are used.
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        private static IEnumerator Empty() { yield break; }
        private static void Run(IEnumerator e)
        { int count=0; while(e.MoveNext()) { if(++count>1000) Assert.Fail("Unexpected coroutine wait"); if(e.Current is IEnumerator nested) Run(nested); } }
        private static byte[] Png()
        { var t = new Texture2D(288,162,TextureFormat.RGB24,false); try { return t.EncodeToPNG(); } finally { UnityEngine.Object.DestroyImmediate(t); } }
        private void Save(int slot = 1)
        { SaveOperationResult result=null; Run(saves.SaveAsync(new SaveSlotId(slot),r=>result=r)); Assert.IsTrue(result.Success,result.ErrorCode); }
        [Test] public void TenSlotSummariesValidateCommittedRecords()
        {
            Save(); var summaries = saves.GetSlotSummaries(); Assert.AreEqual(10,summaries.Count);
            Assert.AreEqual(4,summaries[1].Day); Assert.AreEqual(3661,summaries[1].PlayedSeconds); Assert.IsTrue(summaries[0].IsEmpty);
            string generation = File.ReadAllText(Path.Combine(directory,"Slot01/Current.dat"));
            File.WriteAllText(Path.Combine(directory,"Slot01",generation,"Record.dat"),"bad payload");
            Assert.IsFalse(saves.GetSlotSummaries()[1].IsValid); // V1.1 validates the complete committed chain during scanning.
            LoadOperationResult result=null; Run(saves.LoadAsync(new SaveSlotId(1),r=>result=r)); Assert.IsFalse(result.Success);
        }
        [Test] public void AutomaticSlotCannotBeManuallyOverwritten()
        {
            SaveOperationResult result=null; Run(saves.SaveAsync(new SaveSlotId(0),r=>result=r));
            Assert.IsFalse(result.Success); Assert.AreEqual("SAVE_AUTOMATIC_READ_ONLY",result.ErrorCode);
            Run(saves.AutoSaveAsync(r=>result=r)); Assert.IsTrue(result.Success); Assert.IsFalse(saves.GetSlotSummaries()[0].IsEmpty);
            Run(saves.SaveAsync(new SaveSlotId(0),r=>result=r)); Assert.IsFalse(result.Success);
        }
        [Test] public void SaveAndOverwriteRequireConfirmationAndCancelDoesNotWrite()
        {
            pause.OpenPause(); pause.OpenSave(); pause.SelectSlot(new SaveSlotId(1));
            Assert.AreEqual(PauseConfirmation.Save,pause.Confirmation); Assert.IsTrue(saves.GetSlotSummaries()[1].IsEmpty);
            pause.Back(); Assert.IsTrue(saves.GetSlotSummaries()[1].IsEmpty);
            pause.SelectSlot(new SaveSlotId(1)); Run(pause.ConfirmAsync()); Assert.IsFalse(saves.GetSlotSummaries()[1].IsEmpty);
            record.time.currentDay=5; pause.SelectSlot(new SaveSlotId(1)); pause.Back(); Assert.AreEqual(4,saves.GetSlotSummaries()[1].Day);
        }
        [Test] public void SnapshotRoundtripRestoresTimeStatsLocationAndPosition()
        {
            Save(); LoadOperationResult result=null; Run(saves.LoadAsync(new SaveSlotId(1),r=>result=r));
            Assert.IsTrue(result.Success,result.ErrorCode); Assert.AreEqual(678,result.State.Session.playerState.money);
            Assert.AreEqual(2,result.State.Session.timeActionState.currentActionPoints); Assert.IsTrue(result.State.Session.locationState.locationFlags["met_librarian"]);
            Assert.AreEqual(2,result.State.Record.playerX); Assert.AreEqual(-1,result.State.Record.facingX);
            session.playerState.money=0; Assert.AreEqual(678,result.State.Session.playerState.money);
        }
        [Test] public void StableContentIdsPreserveDuplicateCardsAndRejectMissingDefinitions()
        {
            var resolver=new SaveContentResolver(); var card=ScriptableObject.CreateInstance<CardData>(); objects.Add(card); card.cardId="test_card"; resolver.Register(card);
            session.deckState.masterDeck.Add(card); session.deckState.masterDeck.Add(card);
            var goodCodec=new SaveGameCodec(resolver); var saved=goodCodec.Capture(session,record.scenePath,0,null,Vector2.down); string json=goodCodec.Encode(saved);
            var loaded=goodCodec.Decode(json); Assert.AreEqual(2,loaded.Session.deckState.masterDeck.Count); Assert.AreSame(card,loaded.Session.deckState.masterDeck[0]);
            Assert.Throws<InvalidOperationException>(()=>codec.Decode(json));
        }
        [Test] public void MalformedOrFutureSaveIsRejectedBeforeFlow()
        {
            Assert.Throws<InvalidOperationException>(()=>codec.Decode("{\"version\":2}"));
            Assert.Throws<InvalidOperationException>(()=>codec.Decode("{\"version\":1}"));
            record.playerX=float.NaN; Assert.Throws<InvalidOperationException>(()=>codec.Encode(record));
            Assert.AreEqual(0,backend.Calls);
        }
        [Test] public void AuthoredAssetsWithDuplicateBusinessIdsRestoreExactDefinition()
        {
            var a=ScriptableObject.CreateInstance<ActivityData>(); var b=ScriptableObject.CreateInstance<ActivityData>(); objects.Add(a); objects.Add(b);
            a.activityId=b.activityId="same_business_id";
            var resolver=new SaveContentResolver(); resolver.RegisterAsset(a,"asset:first"); resolver.RegisterAsset(b,"asset:second");
            var exact=new SaveGameCodec(resolver); session.unlockedActivities.Add(b);
            var saved=exact.Capture(session,record.scenePath,0,null,Vector2.down);
            Assert.AreEqual("asset:second",saved.activityIds[0]); Assert.AreSame(b,exact.Decode(exact.Encode(saved)).Session.unlockedActivities[0]);
        }
        [Test] public void FailedLoadNeverCallsGameFlowAndSuccessfulLoadDoes()
        {
            Save(); pause.OpenPause(); pause.OpenLoad(); pause.SelectSlot(new SaveSlotId(1));
            string generation=File.ReadAllText(Path.Combine(directory,"Slot01/Current.dat")); string path=Path.Combine(directory,"Slot01",generation,"Record.dat");
            string original=File.ReadAllText(path); File.WriteAllText(path,"corrupt"); Run(pause.ConfirmAsync()); Assert.AreEqual(0,backend.Calls); Assert.IsTrue(paused);
            File.WriteAllText(path,original); pause.SelectSlot(new SaveSlotId(1)); Run(pause.ConfirmAsync()); Assert.AreEqual(1,backend.Calls); Assert.AreEqual(PauseMenuState.Gameplay,pause.State); Assert.IsFalse(paused);
        }
        [Test] public void InterruptedGenerationDoesNotReplaceVisibleSave()
        {
            Save(); string orphan=Path.Combine(directory,"Slot01",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(orphan); File.WriteAllText(Path.Combine(orphan,"Record.dat"),"partial");
            Assert.AreEqual(4,saves.GetSlotSummaries()[1].Day); Assert.IsNotNull(codec.Decode(repository.ReadRecord(new SaveSlotId(1))));
        }
        [Test] public void ThumbnailCacheUsesOneTextureAndReleasesAtSessionEnd()
        {
            var store=new ThumbnailStore { Bytes=Png() }; var service=new SaveThumbnailService(store); ThumbnailLoadResult first=null,second=null;
            Run(service.LoadAsync(new SaveSlotId(1),r=>first=r)); Run(service.LoadAsync(new SaveSlotId(1),r=>second=r));
            Assert.AreEqual(1,store.Reads); Assert.AreSame(first.Texture,second.Texture); Assert.AreEqual(288,first.Texture.width); Assert.AreEqual(162,first.Texture.height);
            service.ReleaseAllForPanelSession(); Assert.IsTrue(first.Texture==null); Assert.AreEqual(0,service.CachedCount);
        }
        [Test] public void CorruptOrMissingThumbnailReturnsCachedPlaceholder()
        {
            var store=new ThumbnailStore { Bytes=new byte[] {1,2,3} }; var service=new SaveThumbnailService(store); ThumbnailLoadResult result=null;
            Run(service.LoadAsync(new SaveSlotId(1),r=>result=r)); Assert.IsTrue(result.IsPlaceholder);
            Run(service.LoadAsync(new SaveSlotId(1),r=>result=r)); Assert.AreEqual(1,store.Reads); service.ReleaseAllForPanelSession();
        }
        [Test] public void LanguageChangesRefreshTextWithoutReadingOrReplacingThumbnails()
        {
            Save(); var store=new ThumbnailStore { Bytes=Png() }; var thumbnails=new SaveThumbnailService(store);
            var root=new GameObject("SaveViewTest",typeof(RectTransform)); objects.Add(root); var font=GameUiStyle.ResolveTheme(null).ResolveFont();
            var view=SaveLoadPanelView.Create((RectTransform)root.transform,font); var text=new MenuTextService(); view.Bind(text); view.Show(saves.GetSlotSummaries(),false,null);
            ThumbnailLoadResult loaded=null; Run(thumbnails.LoadAsync(new SaveSlotId(1),r=>loaded=r)); view.SetThumbnail(new SaveSlotId(1),loaded.Texture);
            for(int i=0;i<10;i++) text.SetLanguage(i%2==0 ? "en" : "zh-Hans");
            Assert.AreEqual(1,store.Reads); Assert.IsTrue(view.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(image=>image.texture==loaded.Texture));
            // Verify the actual unsubscription handler even in EditMode's reduced lifecycle.
            typeof(SaveLoadPanelView).GetMethod("OnDestroy",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(view,null);
            UnityEngine.Object.DestroyImmediate(root); Assert.DoesNotThrow(()=>text.SetLanguage("en")); thumbnails.ReleaseAllForPanelSession();
        }
        [Test] public void AchievementOfflineIsDiagnosticAndDoesNotBlockRestoredGame()
        {
            var pipeline=new PostLoadPipeline(new[] { new AchievementSyncService(null,new AchievementRule[0]) });
            var flow=new GameFlowService(backend,pipeline,()=>false); GameFlowRestoreResult result=null;
            Run(flow.RestoreLoadedGameAsync(codec.Decode(codec.Encode(record)),r=>result=r));
            Assert.IsTrue(result.Success); Assert.IsFalse(result.PostLoadResults[0].Success); Assert.AreEqual("ACHIEVEMENT_PLATFORM_UNAVAILABLE",result.PostLoadResults[0].ErrorCode);
        }
        [Test] public void AchievementSyncOnlyIssuesMissingAwardsAndIsIdempotent()
        {
            var platform=new AchievementPlatform(); var sync=new AchievementSyncService(platform,new[] {new AchievementRule("earned",s=>s.playerState.money==678)});
            var context=new LoadedGameContext(codec.Decode(codec.Encode(record))); Run(sync.ExecuteAsync(context,null)); Run(sync.ExecuteAsync(context,null)); Assert.AreEqual(1,platform.Unlocks);
        }
        [Test] public void TitleContextDisablesSaveAndSettingsReturnsToCorrectParent()
        {
            pause.OpenPause(); Run(pause.ReturnToTitleAsync(null)); Assert.AreEqual(PauseMenuState.Title,pause.State); Assert.IsFalse(paused);
            Assert.IsFalse(pause.OpenSave().Success); Assert.IsTrue(pause.OpenSettings().Success); pause.SettingsClosed(); Assert.AreEqual(PauseMenuState.Title,pause.State);
            Assert.IsTrue(pause.OpenLoad().Success); pause.Back(); Assert.AreEqual(PauseMenuState.Title,pause.State);
        }
        [Test] public void InvalidSlotIdsCannotEscapeSaveRoot()
        { Assert.Throws<ArgumentOutOfRangeException>(()=>new SaveSlotId(-1)); Assert.Throws<ArgumentOutOfRangeException>(()=>new SaveSlotId(10)); }
        private sealed class SettingsRepo : ISettingsRepository
        { public SettingsLoadResult Load()=>new SettingsLoadResult(new SettingsSnapshot(),true); public SettingsSaveResult Save(SettingsSnapshot s)=>new SettingsSaveResult(SettingsPersistenceOutcome.Committed); }
        private sealed class FlowBackend : IGameFlowBackend
        { public int Calls; public IEnumerator RestoreAsync(LoadedGameState state,Action<CommandResult> completed) { Calls++; completed(new CommandResult(true)); yield break; } }
        private sealed class AchievementPlatform : IAchievementPlatform
        { public int Unlocks; public bool IsAvailable=>true; public bool IsUnlocked(string id)=>Unlocks>0; public bool Unlock(string id) { Unlocks++; return true; } }
        private sealed class ThumbnailStore : ISaveSlotRepository
        {
            public byte[] Bytes; public int Reads;
            public byte[] ReadThumbnail(SaveSlotId slot) { Reads++; return Bytes; }
            public SaveSlotSummary ReadSummary(SaveSlotId slot)=>new SaveSlotSummary(slot,false,true,1);
            public string ReadRecord(SaveSlotId slot)=>throw new InvalidOperationException();
            public void Write(SaveSlotId slot,string record,int day,double seconds,byte[] thumbnail) { }
        }
    }
}
