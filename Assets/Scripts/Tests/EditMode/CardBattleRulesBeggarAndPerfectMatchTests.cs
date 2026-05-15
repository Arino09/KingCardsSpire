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
        public void Beggar_morphLevel_when_same_side_has_commoner_is_one()
        {
            var playerHand = new List<Card>
            {
                C(WellKnownCardIds.Beggar, 1f),
                C(WellKnownCardIds.Commoner, 1f)
            };
            var beggar = playerHand[0];
            var lv = BattleMorphRules.GetMorphBaseLevel(beggar, true, playerHand, new List<Card>());
            Assert.AreEqual(1f, lv);
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

        [Test]
        public void PerfectMatch_does_not_invert_king_commoner_chain()
        {
            var king = C(WellKnownCardIds.King, 3f);
            var commoner = C(WellKnownCardIds.Commoner, 1f);
            var w = WeatherType.WarmWind;
            Assert.AreEqual(BattleCompareResult.SecondWins,
                CardBattleRules.Compare(king, commoner, w, null, null, false));
            Assert.AreEqual(BattleCompareResult.SecondWins,
                CardBattleRules.Compare(king, commoner, w, null, null, true));
            Assert.AreEqual(BattleCompareResult.FirstWins,
                CardBattleRules.Compare(commoner, king, w, null, null, true));
        }

        [Test]
        public void Regicide_base_level_is_three_when_no_king_on_same_side()
        {
            var hand = new List<Card>
            {
                C(WellKnownCardIds.Regicide, 0f)
            };
            var lv = BattleMorphRules.GetMorphBaseLevel(hand[0], true, hand, new List<Card>());
            Assert.AreEqual(3f, lv);
        }

        [Test]
        public void Rebel_base_level_is_two_when_no_minister_on_same_side()
        {
            var hand = new List<Card>
            {
                C(WellKnownCardIds.Rebel, 0f)
            };
            var lv = BattleMorphRules.GetMorphBaseLevel(hand[0], true, hand, new List<Card>());
            Assert.AreEqual(2f, lv);
        }
    }
}
