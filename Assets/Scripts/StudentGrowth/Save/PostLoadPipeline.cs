using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace StudentGrowth
{
    public sealed class LoadedGameContext
    { public LoadedGameState State { get; } public LoadedGameContext(LoadedGameState state) { State = state; } }
    public sealed class PostLoadHandlerResult : CommandResult
    { public string HandlerName { get; } public PostLoadHandlerResult(string name,bool success,string error = null) : base(success,error) { HandlerName = name; } }
    public interface IPostLoadHandler
    { int Order { get; } IEnumerator ExecuteAsync(LoadedGameContext context,Action<PostLoadHandlerResult> completed); }
    public interface IAchievementSyncService : IPostLoadHandler { }
    public interface IAchievementPlatform
    {
        bool IsAvailable { get; }
        bool IsUnlocked(string id);
        bool Unlock(string id);
    }
    public sealed class AchievementRule
    {
        public string PlatformId { get; }
        public Func<GameSessionState,bool> Earned { get; }
        public AchievementRule(string id,Func<GameSessionState,bool> earned) { PlatformId = id; Earned = earned; }
    }
    public sealed class AchievementSyncService : IAchievementSyncService
    {
        private readonly IAchievementPlatform platform;
        private readonly IReadOnlyList<AchievementRule> rules;
        public int Order => 100;
        public AchievementSyncService(IAchievementPlatform platform,IReadOnlyList<AchievementRule> rules) { this.platform = platform; this.rules = rules; }
        public IEnumerator ExecuteAsync(LoadedGameContext context,Action<PostLoadHandlerResult> completed)
        {
            bool success = true; string error = null;
            try
            {
                if (platform == null || !platform.IsAvailable) { success = false; error = "ACHIEVEMENT_PLATFORM_UNAVAILABLE"; }
                else foreach (var rule in rules)
                    if (rule.Earned(context.State.Session) && !platform.IsUnlocked(rule.PlatformId) && !platform.Unlock(rule.PlatformId))
                    { success = false; error = "ACHIEVEMENT_SYNC_FAILED"; }
            }
            catch { success = false; error = "ACHIEVEMENT_PLATFORM_UNAVAILABLE"; }
            completed?.Invoke(new PostLoadHandlerResult(nameof(AchievementSyncService),success,error)); yield break;
        }
    }
    public sealed class PostLoadPipeline
    {
        private readonly List<IPostLoadHandler> handlers;
        public PostLoadPipeline(IEnumerable<IPostLoadHandler> handlers) { this.handlers = handlers.OrderBy(h => h.Order).ToList(); }
        public IEnumerator ExecuteAsync(LoadedGameContext context,Action<IReadOnlyList<PostLoadHandlerResult>> completed)
        {
            var results = new List<PostLoadHandlerResult>();
            foreach (var handler in handlers)
            {
                PostLoadHandlerResult result = null; IEnumerator routine = null;
                try { routine = handler.ExecuteAsync(context,r => result = r); } catch { }
                if (routine != null) yield return SaveCoroutineGuard.Run(routine,_ => result = null);
                results.Add(result ?? new PostLoadHandlerResult(handler.GetType().Name,false,"POST_LOAD_HANDLER_FAILED"));
            }
            completed?.Invoke(results.AsReadOnly());
        }
    }
    public static class SaveCoroutineGuard
    {
        public static IEnumerator Run(IEnumerator routine,Action<Exception> failed)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    bool moved = false; object current = null; Exception error = null;
                    try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; } catch (Exception ex) { error = ex; }
                    if (error != null) { failed?.Invoke(error); yield break; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) stack.Push(nested); else yield return current;
                }
            }
            finally { while(stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
        }
    }
}
