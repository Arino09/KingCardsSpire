using System.Collections.Generic;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Models;
using NUnit.Framework;

namespace KingCardsSpire.Tests.EditMode
{
    public sealed class CardBattleRulesBeggarAndPerfectMatchTests
    {
        private static Card C(string id, float level) =>
            new Card { Id = id, Level = level, Type = CardType.Basic };

        [Test]
        public void Beggar_vs_king_when_enemy_has_no_commoner_counts_as_commoner()
        {
            var beggar = C(WellKnownCardIds.Beggar, 1f);
            var king = C(WellKnownCardIds.King, 3f);
            var enemyHand = new List<Card> { king };
            var playerHand = new List<Card> { beggar };
            var r = CardBattleRules.Compare(beggar, king, WeatherType.WarmWind, playerHand, enemyHand, false);
            Assert.AreEqual(BattleCompareResult.FirstWins, r);
        }

        [Test]
        public void PerfectMatch_inverts_plain_numeric_compare()
        {
            var low = C("low", 1f);
            var high = C("high", 3f);
            var w = WeatherType.WarmWind;
            Assert.AreEqual(BattleCompareResult.SecondWins,
                CardBattleRules.Compare(low, high, w, null, null, false));
            Assert.AreEqual(BattleCompareResult.FirstWins,
                CardBattleRules.Compare(low, high, w, null, null, true));
        }
    }
}
