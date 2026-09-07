namespace StudentGrowth
{
    public static class GameArtBindingUtility
    {
        public static bool ApplyKnownArtPath(CardData cardData)
        {
            if (cardData == null || string.IsNullOrEmpty(cardData.cardId))
            {
                return false;
            }

            if (!GameArtAssetCatalog.TryGetCardImagePath(cardData.cardId, out string path))
            {
                return false;
            }

            cardData.imagePath = path;
            return true;
        }

        public static bool ApplyKnownArtPath(ItemData itemData)
        {
            if (itemData == null || string.IsNullOrEmpty(itemData.itemId))
            {
                return false;
            }

            if (!GameArtAssetCatalog.TryGetItemIconPath(itemData.itemId, out string path))
            {
                return false;
            }

            itemData.imagePath = path;
            return true;
        }

        public static bool ApplyKnownArtPath(StageData stageData)
        {
            if (stageData == null || string.IsNullOrEmpty(stageData.stageId))
            {
                return false;
            }

            if (!GameArtAssetCatalog.TryGetStageBackgroundPath(stageData.stageId, out string path))
            {
                return false;
            }

            stageData.imagePath = path;
            return true;
        }

        public static bool ApplyKnownArtPath(ChallengeData challengeData)
        {
            if (challengeData == null || string.IsNullOrEmpty(challengeData.challengeId))
            {
                return false;
            }

            if (!GameArtAssetCatalog.TryGetChallengeImagePath(challengeData.challengeId, out string path))
            {
                return false;
            }

            challengeData.imagePath = path;
            return true;
        }

        public static bool ApplyKnownArtPath(ActivityData activityData)
        {
            if (activityData == null)
            {
                return false;
            }

            string path = GetActivityImagePath(activityData);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            activityData.imagePath = path;
            return true;
        }

        public static bool ApplyKnownArtPath(MiniGameData miniGameData)
        {
            if (miniGameData == null)
            {
                return false;
            }

            string path = GetMiniGameImagePath(miniGameData);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            miniGameData.imagePath = path;
            return true;
        }

        public static bool ApplyKnownArtPath(GameEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            string path = GetEventImagePath(eventData);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            eventData.imagePath = path;
            return true;
        }

        private static string GetActivityImagePath(ActivityData activityData)
        {
            switch (activityData.activityType)
            {
                case ActivityType.CardBattle:
                    return GameArtAssetCatalog.BackgroundPaths["school_classroom"];
                case ActivityType.MiniGame:
                    return activityData.linkedMiniGame != null
                        ? GetMiniGameImagePath(activityData.linkedMiniGame)
                        : GameArtAssetCatalog.BackgroundPaths["study_desk"];
                case ActivityType.Event:
                    return activityData.linkedEvent != null
                        ? GetEventImagePath(activityData.linkedEvent)
                        : GameArtAssetCatalog.BackgroundPaths["home_dining_room"];
                case ActivityType.Rest:
                    return GameArtAssetCatalog.BackgroundPaths["student_bedroom"];
                case ActivityType.Shop:
                    return GameArtAssetCatalog.BackgroundPaths["school_shop"];
                case ActivityType.Training:
                    return GameArtAssetCatalog.BackgroundPaths["study_desk"];
                default:
                    return string.Empty;
            }
        }

        private static string GetMiniGameImagePath(MiniGameData miniGameData)
        {
            switch (miniGameData.miniGameType)
            {
                case MiniGameType.Memory:
                case MiniGameType.ChoiceQuiz:
                case MiniGameType.SimplePuzzle:
                    return GameArtAssetCatalog.BackgroundPaths["study_desk"];
                case MiniGameType.Timing:
                case MiniGameType.Reaction:
                    return GameArtAssetCatalog.BackgroundPaths["school_classroom"];
                default:
                    return GameArtAssetCatalog.BackgroundPaths["stage_dashboard"];
            }
        }

        private static string GetEventImagePath(GameEventData eventData)
        {
            string id = eventData.eventId == null ? string.Empty : eventData.eventId.ToLowerInvariant();
            if (id.Contains("parent") || id.Contains("family") || id.Contains("dinner"))
            {
                return GameArtAssetCatalog.BackgroundPaths["home_dining_room"];
            }

            if (id.Contains("club") || id.Contains("classmate") || id.Contains("teacher"))
            {
                return GameArtAssetCatalog.BackgroundPaths["school_classroom"];
            }

            if (id.Contains("rest") || id.Contains("weekend"))
            {
                return GameArtAssetCatalog.BackgroundPaths["student_bedroom"];
            }

            return GameArtAssetCatalog.BackgroundPaths["stage_dashboard"];
        }
    }
}
