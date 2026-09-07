using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class GameBootstrapper : MonoBehaviour
    {
        public const string DefaultResourcePath = "StudentGrowth/MvpBootstrapperData";

        public GameBootstrapperData bootstrapperData;
        private static GameSessionState sharedSessionState;
        public static GameSessionState SharedSession => sharedSessionState;
        public static GameSessionState RestoringSession { get; private set; }
        public static void BeginRestore(GameSessionState state) { RestoringSession = state; sharedSessionState = state; }
        public static void EndRestore() { RestoringSession = null; }
        public static void ClearSharedSession() { sharedSessionState = null; RestoringSession = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedSession() => ClearSharedSession();

        public GameSessionState CreateSession()
        {
            if (sharedSessionState != null)
            {
                return sharedSessionState;
            }

            GameBootstrapperData data = GetData();
            if (data == null)
            {
                GameSessionState fallbackState = MvpSampleDataFactory.CreateSessionWithStarterDeck();
                MvpSampleDataFactory.AddDefaultStarterItems(fallbackState);
                sharedSessionState = fallbackState;
                return fallbackState;
            }

            GameSessionState sessionState = new GameSessionState();
            AddCards(sessionState, data.starterDeck);
            AddItems(sessionState, data.starterItems);

            if (sessionState.deckState.masterDeck.Count == 0)
            {
                GameSessionState fallbackDeckState = MvpSampleDataFactory.CreateSessionWithStarterDeck();
                AddCards(sessionState, fallbackDeckState.deckState.masterDeck);
            }

            if (sessionState.itemInventory.ownedItems.Count == 0)
            {
                MvpSampleDataFactory.AddDefaultStarterItems(sessionState);
            }

            sharedSessionState = sessionState;
            return sessionState;
        }

        public static void SetSharedSession(GameSessionState sessionState)
        {
            if (sessionState != null)
            {
                sharedSessionState = sessionState;
            }
        }

        public List<StageData> GetStageSequence()
        {
            GameBootstrapperData data = GetData();
            if (data == null || data.stages == null || data.stages.Count == 0)
            {
                return MvpSampleDataFactory.CreateThreeStageSequence();
            }

            List<StageData> stageSequence = new List<StageData>();
            foreach (StageData stage in data.stages)
            {
                if (stage != null)
                {
                    stageSequence.Add(stage);
                }
            }

            return stageSequence.Count > 0 ? stageSequence : MvpSampleDataFactory.CreateThreeStageSequence();
        }

        public StageData GetFirstStage(IReadOnlyList<StageData> stageSequence)
        {
            GameBootstrapperData data = GetData();
            if (data != null && data.firstStage != null)
            {
                return data.firstStage;
            }

            return stageSequence != null && stageSequence.Count > 0 ? stageSequence[0] : null;
        }

        public List<LocationData> GetLocations()
        {
            GameBootstrapperData data = GetData();
            if (data == null || data.locations == null || data.locations.Count == 0)
            {
                return MvpSampleDataFactory.CreateDefaultLocations();
            }

            List<LocationData> locations = new List<LocationData>();
            foreach (LocationData location in data.locations)
            {
                if (location != null)
                {
                    locations.Add(location);
                }
            }

            return locations.Count > 0 ? locations : MvpSampleDataFactory.CreateDefaultLocations();
        }

        public List<EndingData> GetEndingOptions()
        {
            GameBootstrapperData data = GetData();
            if (data == null || data.endings == null || data.endings.Count == 0)
            {
                return MvpSampleDataFactory.CreateDefaultEndings();
            }

            List<EndingData> endings = new List<EndingData>();
            foreach (EndingData ending in data.endings)
            {
                if (ending != null)
                {
                    endings.Add(ending);
                }
            }

            return endings.Count > 0 ? endings : MvpSampleDataFactory.CreateDefaultEndings();
        }

        private GameBootstrapperData GetData()
        {
            if (bootstrapperData != null)
            {
                return bootstrapperData;
            }

            bootstrapperData = Resources.Load<GameBootstrapperData>(DefaultResourcePath);
            return bootstrapperData;
        }

        private static void AddCards(GameSessionState sessionState, IEnumerable<CardData> cards)
        {
            if (cards == null)
            {
                return;
            }

            foreach (CardData card in cards)
            {
                sessionState.deckState.AddCardToMasterDeck(card);
            }
        }

        private static void AddItems(GameSessionState sessionState, IEnumerable<ItemData> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (ItemData item in items)
            {
                sessionState.itemInventory.AddItem(item);
            }
        }
    }
}
