using System;
using System.Collections.Generic;
using KingCardsSpire.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// Phase 5：形态牌、功能牌优先级链、异能数值修正等的对战结算辅助。
    /// </summary>
    public static class BattleCardEffectResolver
    {
        private const float Epsilon = 0.001f;

        /// <summary>
        /// 策划案：四象须「各自在出战且战胜对手的那一回」各记一次；四类均达成后本局直接胜/败（不再要求四张同时在手）。
        /// </summary>
        public static void RecordFourSymbolRoundProgress(BattleEffectRuntimeState state,
            Card playerStaged, Card enemyStaged, BattleCompareResult result)
        {
            if (state == null)
                return;

            if (result == BattleCompareResult.FirstWins)
                MarkFourSymbolWinIfPlayed(state, playerStaged?.Id, true);
            else if (result == BattleCompareResult.SecondWins)
                MarkFourSymbolWinIfPlayed(state, enemyStaged?.Id, false);
        }

        public static bool PlayerHasAllFourSymbolRoundWins(BattleEffectRuntimeState state) =>
            state != null
            && state.PlayerFourSymbolQinglongRoundWin
            && state.PlayerFourSymbolBaihuRoundWin
            && state.PlayerFourSymbolZhuqueRoundWin
            && state.PlayerFourSymbolXuanwuRoundWin;

        public static bool EnemyHasAllFourSymbolRoundWins(BattleEffectRuntimeState state) =>
            state != null
            && state.EnemyFourSymbolQinglongRoundWin
            && state.EnemyFourSymbolBaihuRoundWin
            && state.EnemyFourSymbolZhuqueRoundWin
            && state.EnemyFourSymbolXuanwuRoundWin;

        private static void MarkFourSymbolWinIfPlayed(BattleEffectRuntimeState state, string playedId,
            bool forPlayer)
        {
            if (state == null || string.IsNullOrEmpty(playedId))
                return;

            if (forPlayer)
            {
                if (string.Equals(playedId, WellKnownCardIds.FourQinglong, StringComparison.OrdinalIgnoreCase))
                    state.PlayerFourSymbolQinglongRoundWin = true;
                else if (string.Equals(playedId, WellKnownCardIds.FourBaihu, StringComparison.OrdinalIgnoreCase))
                    state.PlayerFourSymbolBaihuRoundWin = true;
                else if (string.Equals(playedId, WellKnownCardIds.FourZhuque, StringComparison.OrdinalIgnoreCase))
                    state.PlayerFourSymbolZhuqueRoundWin = true;
                else if (string.Equals(playedId, WellKnownCardIds.FourXuanwu, StringComparison.OrdinalIgnoreCase))
                    state.PlayerFourSymbolXuanwuRoundWin = true;
            }
            else
            {
                if (string.Equals(playedId, WellKnownCardIds.FourQinglong, StringComparison.OrdinalIgnoreCase))
                    state.EnemyFourSymbolQinglongRoundWin = true;
                else if (string.Equals(playedId, WellKnownCardIds.FourBaihu, StringComparison.OrdinalIgnoreCase))
                    state.EnemyFourSymbolBaihuRoundWin = true;
                else if (string.Equals(playedId, WellKnownCardIds.FourZhuque, StringComparison.OrdinalIgnoreCase))
                    state.EnemyFourSymbolZhuqueRoundWin = true;
                else if (string.Equals(playedId, WellKnownCardIds.FourXuanwu, StringComparison.OrdinalIgnoreCase))
                    state.EnemyFourSymbolXuanwuRoundWin = true;
            }
        }

        /// <summary>
        /// 为本次比较构造「逻辑上的己方出牌」（影子复制对手上一张；命运随机参照手牌）。
        /// </summary>
        public static Card ResolvePlayerLogicalCardForCompare(Card stagedPlayerCard,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> enemyHand,
            BattleEffectRuntimeState state)
        {
            if (stagedPlayerCard == null)
                return null;

            if (string.Equals(stagedPlayerCard.Id, WellKnownCardIds.Shadow, StringComparison.OrdinalIgnoreCase))
            {
                var snap = state?.LastEnemyPlayedSnapshot;
                if (snap == null)
                    return CloneCard(stagedPlayerCard, 0f);
                return CloneCard(snap, snap.Level);
            }

            if (string.Equals(stagedPlayerCard.Id, WellKnownCardIds.Fate, StringComparison.OrdinalIgnoreCase)
                && playerHand != null && playerHand.Count > 0)
            {
                var pick = playerHand[Random.Range(0, playerHand.Count)];
                var baseLv = MorphBaseLevel(pick, true, playerHand, enemyHand);
                return CloneCard(stagedPlayerCard, baseLv + 1f);
            }

            return stagedPlayerCard;
        }

        /// <summary>
        /// 敌方影子/命运（与己方对称）。
        /// </summary>
        public static Card ResolveEnemyLogicalCardForCompare(Card stagedEnemyCard,
            IReadOnlyList<Card> enemyHand,
            IReadOnlyList<Card> playerHand,
            BattleEffectRuntimeState state)
        {
            if (stagedEnemyCard == null)
                return null;

            if (string.Equals(stagedEnemyCard.Id, WellKnownCardIds.Shadow, StringComparison.OrdinalIgnoreCase))
            {
                var snap = state?.LastPlayerPlayedSnapshot;
                if (snap == null)
                    return CloneCard(stagedEnemyCard, 0f);
                return CloneCard(snap, snap.Level);
            }

            if (string.Equals(stagedEnemyCard.Id, WellKnownCardIds.Fate, StringComparison.OrdinalIgnoreCase)
                && enemyHand != null && enemyHand.Count > 0)
            {
                var pick = enemyHand[Random.Range(0, enemyHand.Count)];
                var baseLv = MorphBaseLevel(pick, false, playerHand, enemyHand);
                return CloneCard(stagedEnemyCard, baseLv + 1f);
            }

            return stagedEnemyCard;
        }

        /// <summary>
        /// 计算用于 <see cref="CardBattleRules.Compare"/> 的出牌克隆（Level 已含异能修正，天气仍由 Compare 内处理）。
        /// </summary>
        public static Card ToCompareCard(Card logicalCard,
            bool isPlayerCard,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> enemyHand,
            BattleEffectRuntimeState state,
            int round1Based,
            int completedRoundsBeforeThisOne,
            bool applyAllInDouble)
        {
            if (logicalCard == null)
                return null;

            var lv = MorphBaseLevel(logicalCard, isPlayerCard, playerHand, enemyHand);

            if (string.Equals(logicalCard.Id, WellKnownCardIds.AllIn, StringComparison.OrdinalIgnoreCase)
                && applyAllInDouble)
                lv *= 2f;

            if (isPlayerCard)
            {
                lv += state?.ConsumablePlayerLevelBonus ?? 0f;
                ApplyAbilityLevelModifiers(ref lv, logicalCard, true, state, round1Based,
                    completedRoundsBeforeThisOne);
            }
            else
            {
                lv += state?.ConsumableEnemyLevelBonus ?? 0f;
                ApplyAbilityLevelModifiers(ref lv, logicalCard, false, state, round1Based,
                    completedRoundsBeforeThisOne);
            }

            if (state != null)
            {
                if (isPlayerCard && state.PlayerExponentialFormActive)
                    lv *= lv;
                else if (!isPlayerCard && state.EnemyExponentialFormActive)
                    lv *= lv;
            }

            return CloneCard(logicalCard, lv);
        }

        private static void ApplyAbilityLevelModifiers(ref float level, Card logicalCard,
            bool isPlayerCard, BattleEffectRuntimeState state, int round1Based,
            int completedRoundsBeforeThisOne)
        {
            if (state == null)
                return;

            var isHalfFive = IsRoughlyHalfFraction(logicalCard.Level);

            if (isPlayerCard && state.PlayerWarmDayActive && isHalfFive)
                level += 0.25f;
            if (!isPlayerCard && state.EnemyWarmDayActive && isHalfFive)
                level += 0.25f;
            if (isPlayerCard && state.PlayerSnowflakeActive && isHalfFive)
                level -= 0.25f;
            if (!isPlayerCard && state.EnemySnowflakeActive && isHalfFive)
                level -= 0.25f;

            var even = round1Based % 2 == 0;
            if (isPlayerCard)
            {
                if (state.PlayerEvenFormActive && even)
                    level += 1f;
                if (state.PlayerOddFormActive && !even)
                    level += 1f;
                if (state.PlayerForgeBladeActive)
                    level += 0.1f * completedRoundsBeforeThisOne;
                if (state.PlayerStrikeBladeActive)
                    level -= 0.1f * completedRoundsBeforeThisOne;
            }
            else
            {
                if (state.EnemyEvenFormActive && even)
                    level += 1f;
                if (state.EnemyOddFormActive && !even)
                    level += 1f;
                if (state.EnemyForgeBladeActive)
                    level += 0.1f * completedRoundsBeforeThisOne;
                if (state.EnemyStrikeBladeActive)
                    level -= 0.1f * completedRoundsBeforeThisOne;
            }
        }

        private static bool IsRoughlyHalfFraction(float configuredLevel)
        {
            if (float.IsNaN(configuredLevel) || float.IsInfinity(configuredLevel))
                return false;
            var frac = Mathf.Abs(configuredLevel - Mathf.Floor(configuredLevel));
            if (frac > 0.5f + Epsilon)
                frac = 1f - frac;
            return Mathf.Abs(frac - 0.5f) < 0.06f;
        }

        /// <param name="cardBelongsToPlayer">
        /// 当前这张「出牌」是否是玩家一侧出的（用于判定对手的手牌在哪一叠）。
        /// </param>
        private static float MorphBaseLevel(Card card, bool cardBelongsToPlayer,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> enemyHand)
        {
            if (card == null)
                return 0f;

            var opposingHand = cardBelongsToPlayer ? enemyHand : playerHand;

            if (string.Equals(card.Id, WellKnownCardIds.Regicide, StringComparison.OrdinalIgnoreCase))
                return ContainsCardId(opposingHand, WellKnownCardIds.King) ? 1f : 3f;

            if (string.Equals(card.Id, WellKnownCardIds.Rebel, StringComparison.OrdinalIgnoreCase))
                return ContainsCardId(opposingHand, WellKnownCardIds.Minister) ? 1f : 2f;

            if (string.Equals(card.Id, WellKnownCardIds.Beggar, StringComparison.OrdinalIgnoreCase))
                return ContainsCardId(opposingHand, WellKnownCardIds.Commoner) ? 1f : 1f;

            return card.Level;
        }

        private static bool ContainsCardId(IReadOnlyList<Card> hand, string id)
        {
            if (hand == null || string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (c != null && string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static Card CloneCard(Card source, float level)
        {
            return new Card
            {
                Id = source.Id,
                Name = source.Name,
                Level = level,
                Type = source.Type,
                EffectDesc = source.EffectDesc,
                IsUnique = source.IsUnique,
                BattleInstanceId = source.BattleInstanceId
            };
        }

        /// <summary>
        /// 功能牌优先级：伪神 &gt; 刺客强制双败；占卜师/伪神连锁由外部强制胜负处理。
        /// </summary>
        public static BattleCompareResult ResolveSpecialFunctionPriority(Card playerLogical, Card enemyLogical,
            BattleCompareResult normalCompare)
        {
            var pGod = IsFalseGod(playerLogical);
            var eGod = IsFalseGod(enemyLogical);
            var pAss = IsAssassin(playerLogical);
            var eAss = IsAssassin(enemyLogical);

            if (pGod || eGod)
            {
                if (pGod && eGod)
                    return BattleCompareResult.Draw;
                return pGod ? BattleCompareResult.FirstWins : BattleCompareResult.SecondWins;
            }

            if (pAss || eAss)
                return BattleCompareResult.Draw;

            return normalCompare;
        }

        public static bool IsAssassin(Card c) =>
            c != null && string.Equals(c.Id, WellKnownCardIds.Assassin, StringComparison.OrdinalIgnoreCase);

        public static bool IsFalseGod(Card c) =>
            c != null && string.Equals(c.Id, WellKnownCardIds.FalseGod, StringComparison.OrdinalIgnoreCase);

        public static bool IsFortuneTeller(Card c) =>
            c != null &&
            string.Equals(c.Id, WellKnownCardIds.FortuneTeller, StringComparison.OrdinalIgnoreCase);

        public static bool EffectsStripped(Card c, CardType typeGate)
        {
            if (c == null)
                return false;
            return c.Type == typeGate;
        }

        public static bool EnemyEffectsInactive(Card enemyLogical, BattleEffectRuntimeState state)
        {
            if (enemyLogical == null || state == null)
                return false;
            if (state.DisableEnemyFunctionEffects && enemyLogical.Type == CardType.Function)
                return true;
            if (state.DisableEnemyAbilityEffects && enemyLogical.Type == CardType.Ability)
                return true;
            return false;
        }

        /// <summary>
        /// 对手功能/异能失效：视为无特性的 0 级基础牌参与比大小。
        /// </summary>
        public static Card StripToNeutralZero(Card original)
        {
            if (original == null)
                return null;
            return new Card
            {
                Id = "__stripped__",
                Name = original.Name,
                Level = 0f,
                Type = CardType.Basic,
                EffectDesc = original.EffectDesc,
                IsUnique = false,
                BattleInstanceId = original.BattleInstanceId
            };
        }
    }
}

