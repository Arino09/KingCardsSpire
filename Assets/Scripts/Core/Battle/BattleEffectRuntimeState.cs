using KingCardsSpire.Models;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 单局对战内与卡牌效果相关的可变状态（Phase 5 内容填充）。
    /// </summary>
    public sealed class BattleEffectRuntimeState
    {
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

        public bool PlayerForgeBladeActive;

        public bool PlayerStrikeBladeActive;

        public bool EnemyForgeBladeActive;

        public bool EnemyStrikeBladeActive;

        public bool PlayerGoldenNecklacePlayed;

        /// <summary>异能「指数形态」：每次己方出牌额外弃1张自选牌，且本张参与比大小的等级按平方计。</summary>
        public bool PlayerExponentialFormActive;

        /// <summary>敌方打出指数形态异能后，同上（弃牌由 AI 随机，比大小等级平方）。</summary>
        public bool EnemyExponentialFormActive;

        public bool PlayerMustWinThisRound;

        public bool PlayerMustLoseThisRound;

        /// <summary>双方下一回合仅能出国王/平民/大臣（出自「最终时刻」）。</summary>
        public bool FinalMomentRestrictionActive;

        public float ConsumablePlayerLevelBonus;

        public float ConsumableEnemyLevelBonus;

        public bool DisableEnemyFunctionEffects;

        public bool DisableEnemyAbilityEffects;

        /// <summary>续命牌：己方本回合若败，己方出的牌不进弃牌堆。</summary>
        public bool PlayerSurviveLossToHand;

        public void ClearRoundConsumableFlags()
        {
            ConsumablePlayerLevelBonus = 0f;
            ConsumableEnemyLevelBonus = 0f;
            DisableEnemyFunctionEffects = false;
            DisableEnemyAbilityEffects = false;
            PlayerSurviveLossToHand = false;
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
            PlayerForgeBladeActive = false;
            PlayerStrikeBladeActive = false;
            EnemyForgeBladeActive = false;
            EnemyStrikeBladeActive = false;
            PlayerGoldenNecklacePlayed = false;
            PlayerExponentialFormActive = false;
            EnemyExponentialFormActive = false;
            PlayerMustWinThisRound = false;
            PlayerMustLoseThisRound = false;
            FinalMomentRestrictionActive = false;
            ClearRoundConsumableFlags();
        }
    }
}
