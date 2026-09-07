using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class EndingController : MonoBehaviour
    {
        public GameSessionState sessionState = new GameSessionState();
        public List<EndingData> endingOptions = new List<EndingData>();

        public void SetSessionState(GameSessionState state)
        {
            sessionState = state ?? new GameSessionState();
        }

        public void SetEndingOptions(IEnumerable<EndingData> endings)
        {
            endingOptions.Clear();
            if (endings == null)
            {
                return;
            }

            foreach (EndingData ending in endings)
            {
                if (ending != null)
                {
                    endingOptions.Add(ending);
                }
            }
        }

        public EndingData SelectEnding()
        {
            EndingData selected = SelectEnding(sessionState, endingOptions);
            sessionState.currentEnding = selected;
            return selected;
        }

        public EndingData SelectEnding(GameSessionState state, IEnumerable<EndingData> endings)
        {
            if (state == null || endings == null)
            {
                return null;
            }

            EndingData bestEnding = null;
            int bestScore = int.MinValue;
            foreach (EndingData ending in endings)
            {
                if (!CanUseEnding(ending, state))
                {
                    continue;
                }

                int score = CalculateScore(ending, state);
                if (bestEnding == null || score > bestScore)
                {
                    bestEnding = ending;
                    bestScore = score;
                }
            }

            return bestEnding;
        }

        public bool CanUseEnding(EndingData ending, GameSessionState state)
        {
            if (ending == null || state == null)
            {
                return false;
            }

            if (ending.requireEndingReached && !state.endingReached)
            {
                return false;
            }

            StageState stageState = state.stageState;
            if (stageState.clearedStageCount < ending.minimumClearedStages)
            {
                return false;
            }

            if (stageState.defeatedBossCount < ending.minimumDefeatedBosses)
            {
                return false;
            }

            PlayerState playerState = state.playerState;
            if (GetSubjectAverage(playerState) < ending.minimumSubjectAverage)
            {
                return false;
            }

            if (playerState.pressure > ending.maximumPressure)
            {
                return false;
            }

            return EventConditionResolver.AreConditionsMet(ending.conditions, playerState);
        }

        public static int GetSubjectAverage(PlayerState playerState)
        {
            if (playerState == null)
            {
                return 0;
            }

            return (playerState.chinese + playerState.math + playerState.english) / 3;
        }

        private static int CalculateScore(EndingData ending, GameSessionState state)
        {
            PlayerState player = state.playerState;
            StageState stage = state.stageState;
            int score = ending.priority * 1000;
            score += stage.defeatedBossCount * 100;
            score += stage.clearedStageCount * 50;
            score += GetSubjectAverage(player);
            score += Mathf.Max(0, 100 - player.pressure) / 2;
            return score;
        }
    }
}
