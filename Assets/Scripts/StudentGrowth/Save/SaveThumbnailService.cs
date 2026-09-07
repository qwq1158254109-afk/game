using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public sealed class ThumbnailLoadResult
    {
        public Texture2D Texture { get; }
        public bool IsPlaceholder => Texture == null;
        public string ErrorCode { get; }
        public ThumbnailLoadResult(Texture2D texture, string error = null) { Texture = texture; ErrorCode = error; }
    }
    public interface ISaveThumbnailService
    {
        IEnumerator LoadAsync(SaveSlotId slot,Action<ThumbnailLoadResult> completed);
        void Release(SaveSlotId slot);
        void ReleaseAllForPanelSession();
    }
    public sealed class SaveThumbnailService : ISaveThumbnailService
    {
        public const int Width = 288, Height = 162;
        private readonly ISaveSlotRepository repository;
        private readonly Dictionary<int,ThumbnailLoadResult> cache = new Dictionary<int,ThumbnailLoadResult>();
        public int CachedCount => cache.Count;
        public SaveThumbnailService(ISaveSlotRepository repository) { this.repository = repository; }
        public IEnumerator LoadAsync(SaveSlotId slot,Action<ThumbnailLoadResult> completed)
        {
            if (!cache.TryGetValue(slot.Value,out var result))
            {
                Texture2D texture = null;
                try
                {
                    var bytes = repository.ReadThumbnail(slot);
                    if (!IsExpectedPng(bytes)) throw new InvalidOperationException();
                    texture = new Texture2D(Width,Height,TextureFormat.RGB24,false);
                    if (!ImageConversion.LoadImage(texture,bytes,true) || texture.width != Width || texture.height != Height) throw new InvalidOperationException();
                    result = new ThumbnailLoadResult(texture);
                }
                catch { DestroyTexture(texture); result = new ThumbnailLoadResult(null,"SAVE_THUMBNAIL_UNAVAILABLE"); }
                cache.Add(slot.Value,result);
            }
            completed?.Invoke(result); yield break;
        }
        private static bool IsExpectedPng(byte[] bytes) => bytes != null && bytes.Length >= 24 && bytes.Length <= 512 * 1024 &&
            bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
            bytes[16] == 0 && bytes[17] == 0 && bytes[18] == 1 && bytes[19] == 32 &&
            bytes[20] == 0 && bytes[21] == 0 && bytes[22] == 0 && bytes[23] == 162;
        public void Release(SaveSlotId slot)
        { if (cache.TryGetValue(slot.Value,out var item)) { DestroyTexture(item.Texture); cache.Remove(slot.Value); } }
        public void ReleaseAllForPanelSession()
        { foreach (var item in cache.Values) DestroyTexture(item.Texture); cache.Clear(); }
        private static void DestroyTexture(Texture2D texture)
        { if (texture == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture); }
    }
}
