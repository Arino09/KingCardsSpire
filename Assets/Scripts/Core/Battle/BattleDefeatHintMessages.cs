namespace KingCardsSpire.Core.Battle
{
    /// <summary>战败时 HintTip 展示用中文说明（与 <see cref="BattleEndReason"/> 对齐）。</summary>
    public static class BattleDefeatHintMessages
    {
        public static string GetMessage(BattleEndReason reason)
        {
            return reason switch
            {
                BattleEndReason.PlayerHandEmpty => "己方无手牌，本局判负。",
                BattleEndReason.FourSymbolsEnemyComplete => "敌方集齐四象，本局判负。",
                BattleEndReason.RoundLimitDraw => "已达回合上限且为平局，按规则判负。",
                BattleEndReason.RoundLimitByTotalHandLevel => "已达回合上限，己方总手牌等级低于对方，判负。",
                BattleEndReason.FinalMomentPlayerHandEmpty => "「最终时刻」结算后己方无手牌，判负。",
                BattleEndReason.FinalMomentBothHandsEmpty => "「最终时刻」结算后双方均无手牌，按规则判负。",
                _ => "本局战败。"
            };
        }
    }
}
