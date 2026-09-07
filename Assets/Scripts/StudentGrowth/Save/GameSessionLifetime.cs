using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace StudentGrowth
{
    // The comparison derives from the real save DTO: new fields are included by default.
    public static class GameSnapshotComparison
    {
        public static string Normalize(SaveGameRecord record, double pixelSize = 1.0 / 16.0)
        {
            if (record == null) throw new InvalidOperationException("SAVE_NO_SESSION");
            if(pixelSize<=0 || double.IsNaN(pixelSize) || double.IsInfinity(pixelSize)) throw new InvalidOperationException("SAVE_INVALID_PRECISION");
            var json = JObject.FromObject(record);
            foreach (string key in new[] { "version", "playedSeconds", "saveSequence", "savedAtUtc" }) json.Remove(key);
            foreach (string key in new[] { "playerX", "playerY" })
            {
                double value = json.Value<double>(key);
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidOperationException("SAVE_INVALID_DATA");
                json[key] = Math.Round(value / pixelSize, MidpointRounding.AwayFromZero);
            }
            return Sort(json).ToString(Newtonsoft.Json.Formatting.None);
        }
        private static JToken Sort(JToken token)
        {
            if (token is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name,StringComparer.Ordinal).Select(p => new JProperty(p.Name,Sort(p.Value))));
            if (token is JArray array) return new JArray(array.Select(Sort));
            return token.DeepClone();
        }
    }
    public sealed class GameSessionLifetime
    {
        public string RuntimeSessionId { get; private set; }
        public string RunId { get; private set; }
        public bool IsActive => RuntimeSessionId != null;
        public bool Leaving { get; set; }
        public double PixelSize { get; set; } = 1.0 / 16;
        private string baseline;
        private string lastEnded;
        public void Begin(string runId)
        { RuntimeSessionId=Guid.NewGuid().ToString("N"); RunId=runId; baseline=null; Leaving=false; }
        public bool Matches(string runtimeId) => IsActive && RuntimeSessionId==runtimeId;
        public void Saved(string runtimeId, SaveGameRecord snapshot)
        { if (Matches(runtimeId) && snapshot.runId==RunId) baseline=GameSnapshotComparison.Normalize(snapshot,PixelSize); }
        public bool IsDirty(SaveGameRecord current) => baseline==null || current.runId!=RunId || baseline!=GameSnapshotComparison.Normalize(current,PixelSize);
        public CommandResult End(string expected)
        {
            if (!IsActive && expected==lastEnded) return new CommandResult(true);
            if (!Matches(expected)) return new CommandResult(false,"FLOW_SESSION_EXPIRED");
            lastEnded=expected; RuntimeSessionId=null; RunId=null; baseline=null; Leaving=false; return new CommandResult(true);
        }
    }
}
