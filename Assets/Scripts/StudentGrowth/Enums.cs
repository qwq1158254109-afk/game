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
        Training,
        // 2026-10-24 追加（扭蛋抽卡）：只允许加在末尾——存量资产按序号序列化。
        Capsule
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
        AddStageTime,
        // 2026-10-24 新增：只允许追加在末尾——存量资产按序号序列化 RewardType，
        // 插中间会把已有奖励的类型整体错位。
        AddRelationship
    }

    /// <summary>
    /// 条件判定对象（2026-10-24）。默认 PlayerStat = 判玩家属性（旧行为，存量资产不受影响）；
    /// NpcRelationship = 判与某 NPC 的好感度（npcId 字段指明是谁）。
    /// </summary>
    public enum ConditionSubject
    {
        PlayerStat,
        NpcRelationship
    }

    public enum ComparisonType
    {
        GreaterOrEqual,
        LessOrEqual,
        Equal,
        NotEqual
    }
}
