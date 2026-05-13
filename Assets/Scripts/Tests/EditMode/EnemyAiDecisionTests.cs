using System;
using System.Collections.Generic;
using System.Linq;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Models;
using NUnit.Framework;

namespace KingCardsSpire.Tests
{
    public sealed class EnemyAiDecisionTests
    {
        [Test]
        public void StrengthTwoInfersRequiredKingAndCommonerWhenTheyAreNotDiscarded()
        {
            var playerHand = new[]
            {
                Card(WellKnownCardIds.King, 3f),
                Card(WellKnownCardIds.Commoner, 1f)
            };
            var playerDiscard = new[]
            {
                Card(WellKnownCardIds.Minister, 2f)
            };

            var belief = PlayerHandBelief.Build(playerHand, playerDiscard,
                BossAiStrengthRule.ForStrength(2).BeliefLevel);

            CollectionAssert.AreEquivalent(new[] { WellKnownCardIds.King, WellKnownCardIds.Commoner },
                belief.KnownCards.Select(c => c.Id).ToArray());
            Assert.AreEqual(0, belief.UnknownSlots);
        }

        [Test]
        public void StrengthZeroTreatsHiddenPlayerCardsAsUnknown()
        {
            var playerHand = new[]
            {
                Card(WellKnownCardIds.King, 3f),
                Card(WellKnownCardIds.Commoner, 1f)
            };

            var belief = PlayerHandBelief.Build(playerHand, Array.Empty<Card>(),
                BossAiStrengthRule.ForStrength(0).BeliefLevel);

            Assert.AreEqual(0, belief.KnownCards.Count);
            Assert.AreEqual(2, belief.UnknownSlots);
        }

        [Test]
        public void StrongAiChoosesCommonerAgainstKnownKing()
        {
            var playerHand = new[]
            {
                Card(WellKnownCardIds.King, 3f),
                Card(WellKnownCardIds.Commoner, 1f)
            };
            var enemyHand = new[]
            {
                Card(WellKnownCardIds.Commoner, 1f),
                Card(WellKnownCardIds.Minister, 2f)
            };

            var success = EnemyAiDecisionService.TryChooseBestEnemyHandIndex(
                Card(WellKnownCardIds.King, 3f),
                playerHand,
                Array.Empty<Card>(),
                enemyHand,
                Array.Empty<Card>(),
                BossAiStrengthRule.ForStrength(3),
                WeatherType.WarmWind,
                completedRoundsBeforeThisOne: 0,
                finalMomentRestrictionActive: false,
                lastEnemyInstanceId: null,
                out var chosenIndex);

            Assert.IsTrue(success);
            Assert.AreEqual(0, chosenIndex);
        }

        private static Card Card(string id, float level)
        {
            return new Card
            {
                Id = id,
                Name = id,
                Level = level,
                Type = CardType.Basic,
                BattleInstanceId = Guid.NewGuid().ToString("N")
            };
        }
    }
}
