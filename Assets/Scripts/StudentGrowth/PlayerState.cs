using System;

namespace StudentGrowth
{
    [Serializable]
    public class PlayerState
    {
        public int chinese;
        public int math;
        public int english;

        public int energy;
        public int maxEnergy;
        public int pressure;
        public int mood;

        public int parentRelation;
        public int social;
        public int interest;
        public int confidence;
        public int autonomy;

        public int money;

        public PlayerState()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            chinese = 35;
            math = 35;
            english = 35;
            energy = 60;
            maxEnergy = 60;
            pressure = 20;
            mood = 60;
            parentRelation = 50;
            social = 40;
            interest = 40;
            confidence = 45;
            autonomy = 35;
            money = 100;
            ClampAll();
        }

        public int GetStat(PlayerStatKey key)
        {
            switch (key)
            {
                case PlayerStatKey.Chinese:
                    return chinese;
                case PlayerStatKey.Math:
                    return math;
                case PlayerStatKey.English:
                    return english;
                case PlayerStatKey.Energy:
                    return energy;
                case PlayerStatKey.MaxEnergy:
                    return maxEnergy;
                case PlayerStatKey.Pressure:
                    return pressure;
                case PlayerStatKey.Mood:
                    return mood;
                case PlayerStatKey.ParentRelation:
                    return parentRelation;
                case PlayerStatKey.Social:
                    return social;
                case PlayerStatKey.Interest:
                    return interest;
                case PlayerStatKey.Confidence:
                    return confidence;
                case PlayerStatKey.Autonomy:
                    return autonomy;
                case PlayerStatKey.Money:
                    return money;
                default:
                    throw new ArgumentOutOfRangeException("key", key, "Unsupported player stat key.");
            }
        }

        public void SetStat(PlayerStatKey key, int value)
        {
            switch (key)
            {
                case PlayerStatKey.Chinese:
                    chinese = value;
                    break;
                case PlayerStatKey.Math:
                    math = value;
                    break;
                case PlayerStatKey.English:
                    english = value;
                    break;
                case PlayerStatKey.Energy:
                    energy = value;
                    break;
                case PlayerStatKey.MaxEnergy:
                    maxEnergy = value;
                    break;
                case PlayerStatKey.Pressure:
                    pressure = value;
                    break;
                case PlayerStatKey.Mood:
                    mood = value;
                    break;
                case PlayerStatKey.ParentRelation:
                    parentRelation = value;
                    break;
                case PlayerStatKey.Social:
                    social = value;
                    break;
                case PlayerStatKey.Interest:
                    interest = value;
                    break;
                case PlayerStatKey.Confidence:
                    confidence = value;
                    break;
                case PlayerStatKey.Autonomy:
                    autonomy = value;
                    break;
                case PlayerStatKey.Money:
                    money = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("key", key, "Unsupported player stat key.");
            }

            ClampAll();
        }

        public void AddStat(PlayerStatKey key, int delta)
        {
            SetStat(key, GetStat(key) + delta);
        }

        public void ClampAll()
        {
            ValueClampUtility.ClampPlayerState(this);
        }
    }
}
