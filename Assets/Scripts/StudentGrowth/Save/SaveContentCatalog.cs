using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    [CreateAssetMenu(menuName = "Game Data/Save Content Catalog")]
    public sealed class SaveContentCatalog : ScriptableObject
    {
        public const string ResourcePath = "StudentGrowth/SaveContentCatalog";
        public List<ScriptableObject> entries = new List<ScriptableObject>();
        public List<string> saveKeys = new List<string>();
    }
    public interface ISaveContentResolver { T Resolve<T>(string id) where T : ScriptableObject; string GetId(ScriptableObject value); }
    public sealed class SaveContentResolver : ISaveContentResolver
    {
        private readonly Dictionary<string, ScriptableObject> entries = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        private readonly Dictionary<ScriptableObject,string> assetKeys = new Dictionary<ScriptableObject,string>();
        public void RegisterAsset(ScriptableObject value,string key)
        {
            if (value == null || string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("SAVE_CATALOG_INVALID");
            entries.Add(Kind(value)+":"+key,value); assetKeys.Add(value,key);
        }
        public string GetId(ScriptableObject value) => value != null && assetKeys.TryGetValue(value,out var key) ? key : Id(value);
        public void Register(ScriptableObject value)
        {
            if (value == null || assetKeys.ContainsKey(value)) return;
            string id = Id(value); if (string.IsNullOrEmpty(id)) return;
            string key = Kind(value) + ":" + id;
            // Generated defaults can share IDs with authored replacements. First registration wins.
            if (!entries.ContainsKey(key)) entries.Add(key,value);
        }
        public T Resolve<T>(string id) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (entries.TryGetValue(typeof(T).Name + ":" + id,out var value) && value is T found) return found;
            throw new InvalidOperationException("SAVE_CONTENT_MISSING:" + typeof(T).Name + ":" + id);
        }
        private static string Kind(ScriptableObject v) => v is CardData ? nameof(CardData) : v is ItemData ? nameof(ItemData) :
            v is StageData ? nameof(StageData) : v is ActivityData ? nameof(ActivityData) : v is EndingData ? nameof(EndingData) : "";
        public static string Id(ScriptableObject v) => v is CardData c ? c.cardId : v is ItemData i ? i.itemId :
            v is StageData s ? s.stageId : v is ActivityData a ? a.activityId : v is EndingData e ? e.endingId : null;
        public static SaveContentResolver CreateRuntime()
        {
            var resolver = new SaveContentResolver();
            var catalog = Resources.Load<SaveContentCatalog>(SaveContentCatalog.ResourcePath);
            if (catalog != null)
            {
                if (catalog.entries.Count != catalog.saveKeys.Count) throw new InvalidOperationException("SAVE_CATALOG_REBUILD_REQUIRED");
                for (int i=0;i<catalog.entries.Count;i++) resolver.RegisterAsset(catalog.entries[i],catalog.saveKeys[i]);
            }
            // Existing sample content is generated rather than stored as assets. Generate it once
            // per resolver, then hold references so ID-based saves also survive process restarts.
            var sample = MvpSampleDataFactory.CreateSessionWithStarterDeck();
            MvpSampleDataFactory.AddDefaultStarterItems(sample);
            MvpSampleDataFactory.CreateDefaultShopItems();
            MvpSampleDataFactory.CreateThreeStageSequence();
            MvpSampleDataFactory.CreateDefaultEndings();
            foreach (var entry in Resources.FindObjectsOfTypeAll<ScriptableObject>()) resolver.Register(entry);
            return resolver;
        }
    }
}
