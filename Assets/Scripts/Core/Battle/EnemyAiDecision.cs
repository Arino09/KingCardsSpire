using System;
using System.Collections.Generic;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 单一 Boss AI 强度映射出的实际行为参数。
    /// </summary>
    public readonly struct BossAiStrengthRule
    {
        public readonly int Strength;
        public readonly int RewriteLeadRounds;
        public readonly int SearchDepth;
        public readonly int BeliefLevel;
        public readonly float MistakeRate;
        public readonly bool OptimizeEndgame;

        public BossAiStrengthRule(int strength, int rewriteLeadRounds, int searchDepth,
            int beliefLevel, float mistakeRate, bool optimizeEndgame)
        {
            Strength = strength;
            RewriteLeadRounds = rewriteLeadRounds;
            SearchDepth = searchDepth;
            BeliefLevel = beliefLevel;
            MistakeRate = mistakeRate;
            OptimizeEndgame = optimizeEndgame;
        }

        public static BossAiStrengthRule ForStrength(int strength)
        {
            var clamped = Mathf.Clamp(strength, 0, 4);
            return clamped switch
            {
                0 => new BossAiStrengthRule(0, 0, 0, 0, 1f, false),
                1 => new BossAiStrengthRule(1, 1, 1, 1, 0.25f, false),
                2 => new BossAiStrengthRule(2, 2, 2, 2, 0.1f, false),
                3 => new BossAiStrengthRule(3, 3, 3, 3, 0f, false),
                _ => new BossAiStrengthRule(4, 4, 4, 3, 0f, true)
            };
        }
    }

    public sealed class PlayerHandBelief
    {
        public IReadOnlyList<Card> KnownCards { get; }
        public IReadOnlyList<Card> PossibleCards { get; }
        public int UnknownSlots { get; }

        private PlayerHandBelief(IReadOnlyList<Card> knownCards, IReadOnlyList<Card> possibleCards,
            int unknownSlots)
        {
            KnownCards = knownCards ?? Array.Empty<Card>();
            PossibleCards = possibleCards ?? Array.Empty<Card>();
            UnknownSlots = Mathf.Max(0, unknownSlots);
        }

        public static PlayerHandBelief Build(IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> playerDiscard, int beliefLevel)
        {
            var handCount = playerHand?.Count ?? 0;
            if (handCount <= 0)
                return new PlayerHandBelief(Array.Empty<Card>(), Array.Empty<Card>(), 0);

            if (beliefLevel <= 0)
                return new PlayerHandBelief(Array.Empty<Card>(), Array.Empty<Card>(), handCount);

            var known = new List<Card>();
            if (beliefLevel >= 2)
            {
                AddRequiredCardIfStillInHand(known, playerHand, playerDiscard, WellKnownCardIds.King);
                AddRequiredCardIfStillInHand(known, playerHand, playerDiscard, WellKnownCardIds.Commoner);
            }

            var possible = new List<Card>();
            for (var i = 0; i < handCount; i++)
            {
                var card = playerHand[i];
                if (card == null || ContainsInstance(known, card))
                    continue;
                possible.Add(card);
            }

            return new PlayerHandBelief(known, possible, handCount - known.Count);
        }

        private static void AddRequiredCardIfStillInHand(List<Card> known, IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> playerDiscard, string id)
        {
            if (ContainsCardId(playerDiscard, id))
                return;

            for (var i = 0; i < playerHand.Count; i++)
            {
                var card = playerHand[i];
                if (card == null || !string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!ContainsInstance(known, card))
                    known.Add(card);
                return;
            }
        }

        private static bool ContainsCardId(IReadOnlyList<Card> cards, string id)
        {
            if (cards == null || string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card != null && string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsInstance(IReadOnlyList<Card> cards, Card target)
        {
            if (cards == null || target == null)
                return false;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;
                if (!string.IsNullOrEmpty(target.BattleInstanceId) &&
                    card.BattleInstanceId == target.BattleInstanceId)
                    return true;
                if (ReferenceEquals(card, target))
                    return true;
            }

            return false;
        }
    }

    public static class EnemyAiDecisionService
    {
        private const float EnemyWinScore = 1000f;
        private const float DrawScore = 0f;
        private const float EnemyLoseScore = -1000f;

        public static bool TryChooseBestEnemyHandIndex(Card playerCard,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> playerDiscard,
            IReadOnlyList<Card> enemyHand,
            IReadOnlyList<Card> enemyDiscard,
            BossAiStrengthRule rule,
            WeatherType weather,
            int completedRoundsBeforeThisOne,
            bool finalMomentRestrictionActive,
            string lastEnemyInstanceId,
            out int chosenIndex)
        {
            return TryChooseBestEnemyHandIndex(playerCard, playerHand, playerDiscard, enemyHand,
                enemyDiscard, rule, weather, completedRoundsBeforeThisOne, finalMomentRestrictionActive,
                lastEnemyInstanceId, null, out chosenIndex);
        }

        public static bool TryChooseBestEnemyHandIndex(Card playerCard,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> playerDiscard,
            IReadOnlyList<Card> enemyHand,
            IReadOnlyList<Card> enemyDiscard,
            BossAiStrengthRule rule,
            WeatherType weather,
            int completedRoundsBeforeThisOne,
            bool finalMomentRestrictionActive,
            string lastEnemyInstanceId,
            BattleEffectRuntimeState effectState,
            out int chosenIndex)
        {
            chosenIndex = -1;
            if (playerCard == null || enemyHand == null || enemyHand.Count == 0)
                return false;

            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < enemyHand.Count; i++)
            {
                var enemyCard = enemyHand[i];
                if (!IsLegalEnemyCandidate(enemyCard, enemyHand, lastEnemyInstanceId,
                        finalMomentRestrictionActive))
                    continue;

                var score = ScoreCandidate(playerCard, enemyCard, playerHand, playerDiscard, enemyHand,
                    enemyDiscard, rule, weather, completedRoundsBeforeThisOne, effectState);
                if (chosenIndex >= 0 && !(score > bestScore))
                    continue;

                bestScore = score;
                chosenIndex = i;
            }

            return chosenIndex >= 0;
        }

        public static bool IsLegalEnemyCandidate(Card card, IReadOnlyList<Card> enemyHand,
            string lastEnemyInstanceId, bool finalMomentRestrictionActive)
        {
            if (card == null)
                return false;
            if (enemyHand != null && enemyHand.Count > 1 && !string.IsNullOrEmpty(lastEnemyInstanceId) &&
                card.BattleInstanceId == lastEnemyInstanceId)
                return false;
            if (finalMomentRestrictionActive && !IsAllowedUnderFinalMoment(card))
                return false;
            return true;
        }

        private static float ScoreCandidate(Card playerCard, Card enemyCard,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> playerDiscard,
            IReadOnlyList<Card> enemyHand,
            IReadOnlyList<Card> enemyDiscard,
            BossAiStrengthRule rule,
            WeatherType weather,
            int completedRoundsBeforeThisOne,
            BattleEffectRuntimeState effectState)
        {
            var result = Compare(playerCard, enemyCard, playerHand, enemyHand, weather,
                completedRoundsBeforeThisOne, effectState);
            var score = ScoreRoundResult(result, playerCard, enemyCard);
            if (rule.SearchDepth <= 1)
                return score;

            score += ScoreFuture(playerCard, enemyCard, result, playerHand, playerDiscard, enemyHand,
                enemyDiscard, rule, weather, completedRoundsBeforeThisOne + 1, effectState);
            if (rule.OptimizeEndgame)
                score += ScoreEndgamePressure(playerHand, enemyHand, weather);
            return score;
        }

        private static float ScoreFuture(Card playerCard, Card enemyCard, BattleCompareResult result,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> playerDiscard,
            IReadOnlyList<Card> enemyHand,
            IReadOnlyList<Card> enemyDiscard,
            BossAiStrengthRule rule,
            WeatherType weather,
            int completedRounds,
            BattleEffectRuntimeState effectState)
        {
            var nextPlayerHand = CloneList(playerHand);
            var nextEnemyHand = CloneList(enemyHand);
            var nextPlayerDiscard = CloneList(playerDiscard);
            var nextEnemyDiscard = CloneList(enemyDiscard);
            ResolveSimulatedRound(playerCard, enemyCard, result, nextPlayerHand, nextPlayerDiscard,
                nextEnemyHand, nextEnemyDiscard);

            var remainingDepth = rule.SearchDepth - 1;
            if (remainingDepth <= 0 || nextPlayerHand.Count == 0 || nextEnemyHand.Count == 0)
                return 0f;

            var belief = PlayerHandBelief.Build(nextPlayerHand, nextPlayerDiscard, rule.BeliefLevel);
            var possiblePlayerCards = BuildPossiblePlayerCards(belief, nextPlayerHand);
            if (possiblePlayerCards.Count == 0)
                return 0f;

            var total = 0f;
            for (var i = 0; i < possiblePlayerCards.Count; i++)
            {
                if (!TryChooseBestEnemyHandIndex(possiblePlayerCards[i], nextPlayerHand, nextPlayerDiscard,
                        nextEnemyHand, nextEnemyDiscard,
                        new BossAiStrengthRuleShim(rule, remainingDepth).ToRule(), weather,
                        completedRounds, false, null, effectState, out var futureEnemyIndex))
                    continue;

                total += ScoreCandidate(possiblePlayerCards[i], nextEnemyHand[futureEnemyIndex],
                    nextPlayerHand, nextPlayerDiscard, nextEnemyHand, nextEnemyDiscard,
                    new BossAiStrengthRuleShim(rule, remainingDepth).ToRule(), weather, completedRounds,
                    effectState);
            }

            return total / possiblePlayerCards.Count * 0.35f;
        }

        private static BattleCompareResult Compare(Card playerCard, Card enemyCard,
            IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> enemyHand,
            WeatherType weather,
            int completedRoundsBeforeThisOne,
            BattleEffectRuntimeState effectState)
        {
            if (effectState != null && effectState.PlayerMustWinThisRound)
                return BattleCompareResult.FirstWins;
            if (effectState != null && effectState.PlayerMustLoseThisRound)
                return BattleCompareResult.SecondWins;

            var round1Based = completedRoundsBeforeThisOne + 1;
            var playerLogical = BattleCardEffectResolver.ResolvePlayerLogicalCardForCompare(playerCard,
                playerHand, enemyHand, effectState);
            var enemyLogical = BattleCardEffectResolver.ResolveEnemyLogicalCardForCompare(enemyCard,
                enemyHand, playerHand, effectState);
            if (BattleCardEffectResolver.EnemyEffectsInactive(enemyLogical, effectState))
                enemyLogical = BattleCardEffectResolver.StripToNeutralZero(enemyLogical);

            var playerAllIn = IsCardId(playerCard, WellKnownCardIds.AllIn);
            var enemyAllIn = IsCardId(enemyCard, WellKnownCardIds.AllIn);
            var playerCompare = BattleCardEffectResolver.ToCompareCard(playerLogical, true, playerHand,
                enemyHand, effectState, round1Based, completedRoundsBeforeThisOne, playerAllIn);
            var enemyCompare = BattleCardEffectResolver.ToCompareCard(enemyLogical, false, playerHand,
                enemyHand, effectState, round1Based, completedRoundsBeforeThisOne, enemyAllIn);
            var normal = CardBattleRules.Compare(playerCompare, enemyCompare, weather, playerHand, enemyHand,
                invertNumericRanking: (effectState?.PlayerPerfectMatchActive == true) ^
                                      (effectState?.EnemyPerfectMatchActive == true));
            var special = BattleCardEffectResolver.ResolveSpecialFunctionPriority(playerLogical,
                enemyLogical, normal);
            if (special != normal)
                return special;
            if (BattleCardEffectResolver.IsFortuneTeller(playerLogical))
                return BattleCompareResult.SecondWins;
            if (BattleCardEffectResolver.IsFortuneTeller(enemyLogical))
                return BattleCompareResult.FirstWins;
            return normal;
        }

        private static float ScoreRoundResult(BattleCompareResult result, Card playerCard, Card enemyCard)
        {
            var playerLevel = playerCard?.Level ?? 0f;
            var enemyLevel = enemyCard?.Level ?? 0f;
            return result switch
            {
                BattleCompareResult.SecondWins => EnemyWinScore + playerLevel * 10f,
                BattleCompareResult.Draw => DrawScore + playerLevel - enemyLevel,
                BattleCompareResult.FirstWins => EnemyLoseScore - enemyLevel * 10f,
                _ => 0f
            };
        }

        private static void ResolveSimulatedRound(Card playerCard, Card enemyCard,
            BattleCompareResult result, List<Card> playerHand, List<Card> playerDiscard,
            List<Card> enemyHand, List<Card> enemyDiscard)
        {
            switch (result)
            {
                case BattleCompareResult.Draw:
                    MoveCardToDiscard(playerHand, playerDiscard, playerCard);
                    MoveCardToDiscard(enemyHand, enemyDiscard, enemyCard);
                    break;
                case BattleCompareResult.FirstWins:
                    MoveCardToDiscard(enemyHand, enemyDiscard, enemyCard);
                    break;
                case BattleCompareResult.SecondWins:
                    MoveCardToDiscard(playerHand, playerDiscard, playerCard);
                    break;
            }
        }

        private static List<Card> BuildPossiblePlayerCards(PlayerHandBelief belief,
            IReadOnlyList<Card> fallbackPlayerHand)
        {
            var cards = new List<Card>();
            if (belief.KnownCards.Count > 0)
            {
                cards.AddRange(belief.KnownCards);
                if (belief.UnknownSlots <= 0)
                    return cards;
            }

            if (belief.PossibleCards.Count > 0)
                cards.AddRange(belief.PossibleCards);
            else if (fallbackPlayerHand != null)
                cards.AddRange(fallbackPlayerHand);
            return cards;
        }

        private static float ScoreEndgamePressure(IReadOnlyList<Card> playerHand,
            IReadOnlyList<Card> enemyHand, WeatherType weather)
        {
            var playerTotal = CardBattleRules.SumTotalHandLevel(playerHand, weather);
            var enemyTotal = CardBattleRules.SumTotalHandLevel(enemyHand, weather);
            return playerTotal - enemyTotal;
        }

        private static List<Card> CloneList(IReadOnlyList<Card> cards)
        {
            var list = new List<Card>();
            if (cards == null)
                return list;
            for (var i = 0; i < cards.Count; i++)
                list.Add(cards[i]);
            return list;
        }

        private static void MoveCardToDiscard(List<Card> hand, List<Card> discard, Card card)
        {
            var index = FindCardIndex(hand, card);
            if (index < 0)
                return;
            discard.Add(hand[index]);
            hand.RemoveAt(index);
        }

        private static int FindCardIndex(IReadOnlyList<Card> cards, Card target)
        {
            if (cards == null || target == null)
                return -1;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;
                if (!string.IsNullOrEmpty(target.BattleInstanceId) &&
                    card.BattleInstanceId == target.BattleInstanceId)
                    return i;
                if (ReferenceEquals(card, target))
                    return i;
            }

            return -1;
        }

        private static bool IsAllowedUnderFinalMoment(Card card)
        {
            return IsCardId(card, WellKnownCardIds.King)
                   || IsCardId(card, WellKnownCardIds.Commoner)
                   || IsCardId(card, WellKnownCardIds.Minister);
        }

        private static bool IsCardId(Card card, string id)
        {
            return card != null && string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct BossAiStrengthRuleShim
        {
            private readonly BossAiStrengthRule _source;
            private readonly int _searchDepth;

            public BossAiStrengthRuleShim(BossAiStrengthRule source, int searchDepth)
            {
                _source = source;
                _searchDepth = searchDepth;
            }

            public BossAiStrengthRule ToRule()
            {
                return new BossAiStrengthRule(_source.Strength, _source.RewriteLeadRounds,
                    _searchDepth, _source.BeliefLevel, _source.MistakeRate, _source.OptimizeEndgame);
            }
        }
    }
}
