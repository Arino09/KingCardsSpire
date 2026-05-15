using KingCardsSpire.Models;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 单局对战内与卡牌效果相关的可变状态（Phase 5 内容填充）。
    /// </summary>
    public sealed class BattleEffectRuntimeState
    {
        /// <summary>己方：青龙/白虎/朱雀/玄武各自在「本回合作为出战牌且比大小获胜」时记 true；四类均 true 后本局直接胜。</summary>
        public bool PlayerFourSymbolQinglongRoundWin;

        public bool PlayerFourSymbolBaihuRoundWin;

        public bool PlayerFourSymbolZhuqueRoundWin;

        public bool PlayerFourSymbolXuanwuRoundWin;

        /// <summary>敌方：同上；四类均 true 后本局直接判己方败。</summary>
        public bool EnemyFourSymbolQinglongRoundWin;

        public bool EnemyFourSymbolBaihuRoundWin;

        public bool EnemyFourSymbolZhuqueRoundWin;

        public bool EnemyFourSymbolXuanwuRoundWin;

        public Card LastEnemyPlayedSnapshot;

        public Card LastPlayerPlayedSnapshot;

        public bool PlayerWarmDayActive;

        public bool EnemyWarmDayActive;

        public bool PlayerSnowflakeActive;

        public bool EnemySnowflakeActive;

        public bool PlayerEvenFormActive;

        public bool PlayerOddFormActive;

        public bool EnemyEvenFormActive;

        public bool EnemyOddFormActive;

        public bool PlayerGoldenNecklacePlayed;

        /// <summary>异能「指数形态」：每次己方出牌额外弃1张自选牌，且本张参与比大小的等级按平方计。</summary>
        public bool PlayerExponentialFormActive;

        /// <summary>敌方打出指数形态异能后，同上（弃牌由 AI 随机，比大小等级平方）。</summary>
        public bool EnemyExponentialFormActive;

        public bool PlayerMustWinThisRound;

        public bool PlayerMustLoseThisRound;

        public float ConsumablePlayerLevelBonus;

        public float ConsumableEnemyLevelBonus;

        public bool DisableEnemyFunctionEffects;

        public bool DisableEnemyAbilityEffects;

        /// <summary>预见牌：本回合已向玩家揭示的敌方待出牌快照（结算后清除）。</summary>
        public Card ForesightRevealedEnemyCardSnapshot;

        /// <summary>异能「白日梦」：每回合随机替换 1 张手牌。</summary>
        public bool PlayerDaydreamActive;

        public bool EnemyDaydreamActive;

        /// <summary>本局每次「天作之合」异能进入弃牌触发后 +1；奇数时数值比大小与比和反转（国王/平民链不变）。</summary>
        public int PerfectMatchActivationCount;

        public void ClearRoundConsumableFlags()
        {
            ConsumablePlayerLevelBonus = 0f;
            ConsumableEnemyLevelBonus = 0f;
            DisableEnemyFunctionEffects = false;
            DisableEnemyAbilityEffects = false;
            ForesightRevealedEnemyCardSnapshot = null;
        }

        public void ResetBattle()
        {
            LastEnemyPlayedSnapshot = null;
            LastPlayerPlayedSnapshot = null;
            PlayerWarmDayActive = false;
            EnemyWarmDayActive = false;
            PlayerSnowflakeActive = false;
            EnemySnowflakeActive = false;
            PlayerEvenFormActive = false;
            PlayerOddFormActive = false;
            EnemyEvenFormActive = false;
            EnemyOddFormActive = false;
            PlayerGoldenNecklacePlayed = false;
            PlayerExponentialFormActive = false;
            EnemyExponentialFormActive = false;
            PlayerMustWinThisRound = false;
            PlayerMustLoseThisRound = false;
            PlayerDaydreamActive = false;
            EnemyDaydreamActive = false;
            PerfectMatchActivationCount = 0;
            PlayerFourSymbolQinglongRoundWin = false;
            PlayerFourSymbolBaihuRoundWin = false;
            PlayerFourSymbolZhuqueRoundWin = false;
            PlayerFourSymbolXuanwuRoundWin = false;
            EnemyFourSymbolQinglongRoundWin = false;
            EnemyFourSymbolBaihuRoundWin = false;
            EnemyFourSymbolZhuqueRoundWin = false;
            EnemyFourSymbolXuanwuRoundWin = false;
            ClearRoundConsumableFlags();
        }
    }
}
