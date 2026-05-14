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

        /// <summary>己方四象（青龙/白虎/朱雀/玄武）各在至少一回「出战且比大小胜」后集齐，本局直接胜。</summary>
        FourSymbolsComplete = 5,

        /// <summary>敌方四象同上集齐，本局直接判己方败。</summary>
        FourSymbolsEnemyComplete = 6
    }
}

