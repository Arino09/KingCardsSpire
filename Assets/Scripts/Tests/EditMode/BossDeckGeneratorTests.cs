using System;
using System.Collections.Generic;
using System.Linq;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using NUnit.Framework;

namespace KingCardsSpire.Tests
{
    public sealed class BossDeckGeneratorTests
    {
        private const float FloatEpsilon = 1e-3f;

        [Test]
        public void GetDeckTargetSize_Floors1To7_MatchesTable()
        {
            Assert.AreEqual(6, BossDeckGenerator.GetDeckTargetSize(1));
            Assert.AreEqual(6, BossDeckGenerator.GetDeckTargetSize(2));
            Assert.AreEqual(6, BossDeckGenerator.GetDeckTargetSize(3));
            Assert.AreEqual(7, BossDeckGenerator.GetDeckTargetSize(4));
            Assert.AreEqual(8, BossDeckGenerator.GetDeckTargetSize(5));
            Assert.AreEqual(9, BossDeckGenerator.GetDeckTargetSize(6));
            Assert.AreEqual(10, BossDeckGenerator.GetDeckTargetSize(7));
        }

        [Test]
        public void GetDeckTargetSize_ClampedToSevenFloors()
        {
            Assert.AreEqual(10, BossDeckGenerator.GetDeckTargetSize(99));
            Assert.AreEqual(6, BossDeckGenerator.GetDeckTargetSize(0));
            Assert.AreEqual(6, BossDeckGenerator.GetDeckTargetSize(-3));
        }

        [Test]
        public void ApplyMeanLevelShift_AdjustsToTargetMean()
        {
            var deck = new List<Card>
            {
                new Card { Id = "a", Level = 1f, Type = CardType.Basic },
                new Card { Id = "b", Level = 2f, Type = CardType.Basic },
                new Card { Id = "c", Level = 3f, Type = CardType.Basic }
            };

            BossDeckGenerator.ApplyMeanLevelShift(deck, 3f);

            var mean = deck.Sum(c => c.Level) / deck.Count;
            Assert.AreEqual(3f, mean, FloatEpsilon);
        }

        [Test]
        public void BuildDeck_NullConfig_ReturnsEmptyList()
        {
            var deck = BossDeckGenerator.BuildDeck(1, null, new Random(1));
            Assert.IsNotNull(deck);
            Assert.AreEqual(0, deck.Count);
        }

        [Test]
        public void BuildDeck_WhenConfigAvailable_FixedSeed_SizeCoreAndUniqueAbilities()
        {
            var cfg = ConfigManager.Instance;
            if (cfg == null)
                Assert.Ignore("ConfigManager 未初始化，跳过集成断言。");

            var rng = new Random(42);
            var deck = BossDeckGenerator.BuildDeck(5, cfg, rng);

            Assert.AreEqual(BossDeckGenerator.GetDeckTargetSize(5), deck.Count);
            Assert.IsTrue(deck.Any(c =>
                string.Equals(c.Id, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(deck.Any(c =>
                string.Equals(c.Id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase)));

            var abilityIds = deck.Where(c => c.Type == CardType.Ability).Select(c => c.Id).ToList();
            Assert.AreEqual(abilityIds.Count, abilityIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
