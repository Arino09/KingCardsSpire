using System.Collections.Generic;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Models;
using NUnit.Framework;
using UnityEngine;

namespace KingCardsSpire.Tests
{
    public sealed class CasualVictoryRewardPickerTests
    {
        [Test]
        public void BuildOffer_ReturnsNullWhenOnlyKingAndCommoner()
        {
            var hand = new List<Card>
            {
                new Card { Id = WellKnownCardIds.King },
                new Card { Id = WellKnownCardIds.Commoner }
            };

            var result = CasualVictoryRewardPicker.BuildOfferCardIds(hand, null);

            Assert.IsNull(result);
        }

        [Test]
        public void BuildOffer_DeduplicatesByCardId()
        {
            var hand = new List<Card>
            {
                new Card { Id = "minister" },
                new Card { Id = "minister" },
                new Card { Id = WellKnownCardIds.King }
            };

            Random.InitState(123);
            var result = CasualVictoryRewardPicker.BuildOfferCardIds(hand, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("minister", result[0]);
        }

        [Test]
        public void BuildOffer_UnionsHandAndDiscard()
        {
            var hand = new List<Card> { new Card { Id = WellKnownCardIds.King } };
            var discard = new List<Card> { new Card { Id = "thief" } };

            Random.InitState(7);
            var result = CasualVictoryRewardPicker.BuildOfferCardIds(hand, discard);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("thief", result[0]);
        }

        [Test]
        public void BuildOffer_WhenMoreThanThreeEligible_ReturnsThreeDistinctIds()
        {
            var hand = new List<Card>
            {
                new Card { Id = "a" },
                new Card { Id = "b" },
                new Card { Id = "c" },
                new Card { Id = "d" }
            };

            Random.InitState(1);
            var result = CasualVictoryRewardPicker.BuildOfferCardIds(hand, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            var set = new HashSet<string>(result);
            Assert.AreEqual(3, set.Count);
            foreach (var id in result)
            {
                Assert.IsTrue(id == "a" || id == "b" || id == "c" || id == "d");
            }
        }
    }
}
