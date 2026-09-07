namespace StudentGrowth
{
    public enum PlayerStatKey
    {
        Chinese,
        Math,
        English,
        Energy,
        MaxEnergy,
        Pressure,
        Mood,
        ParentRelation,
        Social,
        Interest,
        Confidence,
        Autonomy,
        Money
    }

    public enum ActivityType
    {
        Event,
        CardBattle,
        MiniGame,
        Rest,
        Shop,
        Training
    }

    public enum CardType
    {
        Study,
        Rest,
        Social,
        Family,
        Interest,
        Trait,
        Status
    }

    public enum CardTargetType
    {
        None,
        Self,
        SingleEnemy,
        AllEnemies,
        RandomEnemy
    }

    public enum CombatPhase
    {
        None,
        CombatStart,
        PlayerTurnStart,
        PlayerTurn,
        CardSelecting,
        CardResolving,
        PlayerTurnEnd,
        EnemyTurnStart,
        EnemyTurn,
        EnemyTurnEnd,
        Victory,
        Defeat
    }

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare
    }

    public enum CardEffectType
    {
        AddChallengeProgress,
        AddSubjectProgress,
        AddPlayerStat,
        ReducePlayerStat,
        DrawCards,
        GainEnergy,
        LoseEnergy,
        ReducePressure,
        AddPressure,
        AddTemporaryBuff,
        AddStatusCardToDrawPile,
        AddStatusCardToDiscardPile,
        ExhaustCard,
        ModifyNextCardEffect,
        GainBlock,
        GainBattleEnergy,
        DamageBattleHp,
        GainFocus,
        ApplyDistraction,
        ApplyWeakDefense
    }

    public enum MiniGameType
    {
        Memory,
        Timing,
        ChoiceQuiz,
        Reaction,
        SimplePuzzle
    }

    public enum ItemType
    {
        Consumable,
        Passive,
        StageOnly
    }

    public enum ItemTriggerTiming
    {
        OnAcquire,
        OnStageStart,
        OnBattleStart,
        OnCardPlayed,
        OnBossStart,
        OnUse,
        OnStageEnd
    }

    public enum RewardType
    {
        AddCard,
        UpgradeCard,
        RemoveCard,
        AddPlayerStat,
        ReducePlayerStat,
        AddMoney,
        ReducePressure,
        IncreaseMaxEnergy,
        AddItem,
        RemoveItem,
        UnlockActivity,
        UnlockStage,
        AddStageTime
    }

    public enum ComparisonType
    {
        GreaterOrEqual,
        LessOrEqual,
        Equal,
        NotEqual
    }
}
