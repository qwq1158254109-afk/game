using StudentGrowth;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Editor
{
    public static class GameArtBindingEditorUtility
    {
        [MenuItem("Tools/Student Growth/Art/Bind Selected Art References")]
        public static void BindSelectedArtReferences()
        {
            int changedCount = 0;
            foreach (Object selectedObject in Selection.objects)
            {
                if (BindObjectArt(selectedObject))
                {
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Student Growth art binding updated selected assets: " + changedCount);
        }

        [MenuItem("Tools/Student Growth/Art/Bind All Known Data Assets")]
        public static void BindAllKnownDataAssets()
        {
            int changedCount = 0;
            changedCount += BindAssetsOfType<CardData>();
            changedCount += BindAssetsOfType<ItemData>();
            changedCount += BindAssetsOfType<StageData>();
            changedCount += BindAssetsOfType<ChallengeData>();
            changedCount += BindAssetsOfType<ActivityData>();
            changedCount += BindAssetsOfType<MiniGameData>();
            changedCount += BindAssetsOfType<GameEventData>();

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Student Growth art binding updated known data assets: " + changedCount);
        }

        private static int BindAssetsOfType<T>() where T : ScriptableObject
        {
            int changedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (BindObjectArt(asset))
                {
                    changedCount++;
                }
            }

            return changedCount;
        }

        private static bool BindObjectArt(Object target)
        {
            if (target is CardData cardData)
            {
                return BindCardArt(cardData);
            }

            if (target is ItemData itemData)
            {
                return BindItemArt(itemData);
            }

            if (target is StageData stageData)
            {
                return BindStageArt(stageData);
            }

            if (target is ChallengeData challengeData)
            {
                return BindChallengeArt(challengeData);
            }

            if (target is ActivityData activityData)
            {
                return BindActivityArt(activityData);
            }

            if (target is MiniGameData miniGameData)
            {
                return BindMiniGameArt(miniGameData);
            }

            if (target is GameEventData eventData)
            {
                return BindEventArt(eventData);
            }

            return false;
        }

        private static bool BindCardArt(CardData cardData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(cardData))
            {
                return false;
            }

            Sprite sprite = LoadSprite(cardData.imagePath);
            if (sprite == null)
            {
                return false;
            }

            cardData.cardSprite = sprite;
            cardData.icon = sprite;
            MarkDirty(cardData);
            return true;
        }

        private static bool BindItemArt(ItemData itemData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(itemData))
            {
                return false;
            }

            itemData.icon = LoadSprite(itemData.imagePath);
            MarkDirty(itemData);
            return itemData.icon != null;
        }

        private static bool BindStageArt(StageData stageData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(stageData))
            {
                return false;
            }

            stageData.backgroundSprite = LoadSprite(stageData.imagePath);
            stageData.icon = LoadSprite(GameArtAssetCatalog.UiSpritePaths["stage_timer_panel"]);
            MarkDirty(stageData);
            return stageData.backgroundSprite != null;
        }

        private static bool BindChallengeArt(ChallengeData challengeData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(challengeData))
            {
                return false;
            }

            challengeData.backgroundSprite = LoadSprite(challengeData.imagePath);
            if (GameArtAssetCatalog.TryGetChallengeIconPath(challengeData.challengeId, out string iconPath))
            {
                challengeData.icon = LoadSprite(iconPath);
            }

            MarkDirty(challengeData);
            return challengeData.backgroundSprite != null;
        }

        private static bool BindActivityArt(ActivityData activityData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(activityData))
            {
                return false;
            }

            activityData.backgroundSprite = LoadSprite(activityData.imagePath);
            activityData.icon = LoadSprite(GetActivityIconPath(activityData.activityType));
            MarkDirty(activityData);
            return activityData.backgroundSprite != null;
        }

        private static bool BindMiniGameArt(MiniGameData miniGameData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(miniGameData))
            {
                return false;
            }

            miniGameData.backgroundSprite = LoadSprite(miniGameData.imagePath);
            miniGameData.icon = LoadSprite(GameArtAssetCatalog.UiSpritePaths["minigame_window"]);
            MarkDirty(miniGameData);
            return miniGameData.backgroundSprite != null;
        }

        private static bool BindEventArt(GameEventData eventData)
        {
            if (!GameArtBindingUtility.ApplyKnownArtPath(eventData))
            {
                return false;
            }

            eventData.backgroundSprite = LoadSprite(eventData.imagePath);
            eventData.icon = LoadSprite(GameArtAssetCatalog.UiSpritePaths["event_window"]);
            MarkDirty(eventData);
            return eventData.backgroundSprite != null;
        }

        private static string GetActivityIconPath(ActivityType activityType)
        {
            switch (activityType)
            {
                case ActivityType.CardBattle:
                    return GameArtAssetCatalog.UiSpritePaths["boss_warning_slot"];
                case ActivityType.MiniGame:
                    return GameArtAssetCatalog.UiSpritePaths["minigame_window"];
                case ActivityType.Event:
                    return GameArtAssetCatalog.UiSpritePaths["event_window"];
                case ActivityType.Rest:
                    return GameArtAssetCatalog.StatIconPaths[PlayerStatKey.Energy];
                case ActivityType.Shop:
                    return GameArtAssetCatalog.UiSpritePaths["shop_window"];
                case ActivityType.Training:
                    return GameArtAssetCatalog.StatIconPaths[PlayerStatKey.Confidence];
                default:
                    return GameArtAssetCatalog.UiSpritePaths["activity_card_frame"];
            }
        }

        private static Sprite LoadSprite(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void MarkDirty(Object target)
        {
            EditorUtility.SetDirty(target);
        }
    }
}
