using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StudentGrowth
{
    public sealed class SaveGameCodec
    {
        private readonly ISaveContentResolver content;
        public SaveGameCodec(ISaveContentResolver content) { this.content = content; }
        private static JsonSerializerSettings Options => new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.None, MaxDepth = 32, MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace, Culture = System.Globalization.CultureInfo.InvariantCulture };
        public SaveGameRecord Capture(GameSessionState state, string scene, double seconds, Vector2? position, Vector2 facing)
        {
            if (state == null) throw new InvalidOperationException("SAVE_NO_SESSION");
            var stage = state.stageState;
            var record = new SaveGameRecord {
                runId = state.runId, tutorialSeen = state.tutorialSeen, capsulePityUsed = state.capsulePityUsed, scenePath = scene, hasPlayerPosition = position.HasValue, playerX = position?.x ?? 0, playerY = position?.y ?? 0,
                facingX = facing.x, facingY = facing.y, playedSeconds = seconds,
                player = Copy(state.playerState), time = Copy(state.timeActionState), location = Copy(state.locationState),
                currentStageId = stage.currentStage == null ? null : RequiredId(stage.currentStage),
                remainingTime = stage.remainingTime, bossUnlocked = stage.bossUnlocked, bossDefeated = stage.bossDefeated,
                clearedStageCount = stage.clearedStageCount, defeatedBossCount = stage.defeatedBossCount,
                endingId = state.currentEnding == null ? null : RequiredId(state.currentEnding), endingReached = state.endingReached,
                cardIds = state.deckState.masterDeck.Select(RequiredId).ToList(), itemIds = state.itemInventory.ownedItems.Select(RequiredId).ToList(),
                activityIds = state.unlockedActivities.Select(RequiredId).ToList(), stageIds = state.unlockedStages.Select(RequiredId).ToList(),
                clearedStageIds = new List<string>(state.clearedStageIds), defeatedBossIds = new List<string>(state.defeatedBossIds),
                unlockedActivityIds = new List<string>(state.unlockedActivityIds), unlockedLocationIds = new List<string>(state.unlockedLocationIds),
                npcRelationships = state.npcRelationships.entries
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.npcId))
                    .Select(e => new NpcRelationshipEntry { npcId = e.npcId, value = e.value })
                    .ToList(),
                capsulePulls = state.capsulePulls
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.cardId))
                    .Select(e => new CapsulePullEntry { cardId = e.cardId, cardName = e.cardName, rarity = e.rarity, duplicate = e.duplicate })
                    .ToList() };
            Validate(record); return record;
        }
        private string RequiredId(ScriptableObject value)
        { var id = content.GetId(value); if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SAVE_CONTENT_WITHOUT_ID"); return id; }
        private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value,Options),Options);
        public string Encode(SaveGameRecord record) { Validate(record); return JsonConvert.SerializeObject(record,Options); }
        public LoadedGameState Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 2 * 1024 * 1024) throw new InvalidOperationException("SAVE_INVALID_SIZE");
            var root = JObject.Parse(json,new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (root.Value<int?>("version") != 1 && root.Value<int?>("version") != 2) throw new InvalidOperationException("SAVE_UNSUPPORTED_VERSION");
            var r = root.ToObject<SaveGameRecord>(JsonSerializer.Create(Options)); Validate(r);
            var state = new GameSessionState { runId = r.runId, tutorialSeen = r.tutorialSeen, capsulePityUsed = r.capsulePityUsed, playerState = r.player, timeActionState = r.time, locationState = r.location,
                currentEnding = content.Resolve<EndingData>(r.endingId), endingReached = r.endingReached,
                clearedStageIds = r.clearedStageIds, defeatedBossIds = r.defeatedBossIds,
                unlockedActivityIds = r.unlockedActivityIds, unlockedLocationIds = r.unlockedLocationIds };
            state.stageState = new StageState { currentStage = content.Resolve<StageData>(r.currentStageId), remainingTime = r.remainingTime,
                bossUnlocked = r.bossUnlocked, bossDefeated = r.bossDefeated, clearedStageCount = r.clearedStageCount, defeatedBossCount = r.defeatedBossCount };
            state.deckState.masterDeck = r.cardIds.Select(id => content.Resolve<CardData>(id)).ToList();
            state.itemInventory.ownedItems = r.itemIds.Select(id => content.Resolve<ItemData>(id)).ToList();
            state.unlockedActivities = r.activityIds.Select(id => content.Resolve<ActivityData>(id)).ToList();
            state.unlockedStages = r.stageIds.Select(id => content.Resolve<StageData>(id)).ToList();
            state.npcRelationships.entries = r.npcRelationships
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.npcId))
                .Select(e => new NpcRelationshipEntry
                {
                    npcId = e.npcId,
                    value = Mathf.Clamp(e.value, NpcRelationshipState.MinValue, NpcRelationshipState.MaxValue)
                })
                .ToList();
            state.capsulePulls = r.capsulePulls == null
                ? new List<CapsulePullEntry>()
                : r.capsulePulls
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.cardId))
                    .Take(200)
                    .ToList();
            return new LoadedGameState(r,state);
        }
        private static void Validate(SaveGameRecord r)
        {
            if (r == null || (r.version != 1 && r.version != 2) || r.player == null || r.time == null || r.location == null ||
                string.IsNullOrWhiteSpace(r.scenePath) || !r.scenePath.StartsWith("Assets/",StringComparison.Ordinal) || !r.scenePath.EndsWith(".unity",StringComparison.Ordinal) || r.scenePath.Contains("..") ||
                !Finite(r.playedSeconds) || r.playedSeconds < 0 || !Finite(r.playerX) || !Finite(r.playerY) || !Finite(r.facingX) || !Finite(r.facingY) ||
                Math.Abs(r.playerX) > 100000 || Math.Abs(r.playerY) > 100000 ||
                r.time.currentDay < 1 || !Enum.IsDefined(typeof(TimePeriod),r.time.currentPeriod) || r.time.currentActionPoints < 0 || r.time.currentActionPoints > 3 ||
                r.remainingTime < 0 || r.clearedStageCount < 0 || r.defeatedBossCount < 0 || r.player.maxEnergy < 1 || r.player.energy < 0 || r.player.energy > r.player.maxEnergy ||
                r.location.actionUseCount == null || r.location.locationFlags == null || r.location.actionUseCount.Count > 10000 || r.location.locationFlags.Count > 10000)
                throw new InvalidOperationException("SAVE_INVALID_DATA");
            foreach (var list in new[] { r.cardIds,r.itemIds,r.activityIds,r.stageIds,r.clearedStageIds,r.defeatedBossIds,r.unlockedActivityIds,r.unlockedLocationIds,r.location.unlockedLocationIds,r.location.visitedLocationIds })
                if (list == null || list.Count > 10000 || list.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 200)) throw new InvalidOperationException("SAVE_INVALID_IDS");
            if (r.npcRelationships == null || r.npcRelationships.Count > 1000
                || r.npcRelationships.Any(e => e == null || string.IsNullOrWhiteSpace(e.npcId) || e.npcId.Length > 200
                    || e.value < NpcRelationshipState.MinValue || e.value > NpcRelationshipState.MaxValue))
                throw new InvalidOperationException("SAVE_INVALID_DATA");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
