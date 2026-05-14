namespace KingCardsSpire.Core.Battle
{
    public enum BattleEndReason
    {
        None = 0,
        /// <summary>己方手牌为空，己方败。</summary>
        PlayerHandEmpty = 1,
        /// <summary>敌方手牌为空，己方胜。</summary>
        EnemyHandEmpty = 2,
        /// <summary>达回合上限，按总手牌等级低者胜（§2.2.5）。</summary>
        RoundLimitByTotalHandLevel = 3,
        /// <summary>达回合上限且双方总手牌等级和相等，平局。</summary>
        RoundLimitDraw = 4,

        /// <summary>四象齐集直接获胜（功能卡）。</summary>
        FourSymbolsComplete = 5,

        /// <summary>敌方四象齐集，己方败。</summary>
        FourSymbolsEnemyComplete = 6
    }
}

