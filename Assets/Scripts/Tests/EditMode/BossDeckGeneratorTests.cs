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
        public void ComputeDeckMeanLevel_matchesArithmeticAverage()
        {
            var deck = new List<Card>
            {
                new Card { Id = "a", Level = 1f, Type = CardType.Basic },
                new Card { Id = "b", Level = 3f, Type = CardType.Basic }
            };
            Assert.AreEqual(2f, BossDeckGenerator.ComputeDeckMeanLevel(deck), FloatEpsilon);
        }

        [Test]
        public void BuildDeck_floor1_whenConfigAvailable_meanNearDeterministicTarget()
        {
            var cfg = ConfigManager.Instance;
            if (cfg == null)
                Assert.Ignore("ConfigManager 未初始化，跳过集成断言。");

            var deck = BossDeckGenerator.BuildDeck(1, cfg, new Random(123));
            var mean = BossDeckGenerator.ComputeDeckMeanLevel(deck);
            Assert.Less(Math.Abs(mean - 2f), 0.65f,
                "第1层目标均值为2（无随机步长），由卡池贪心选牌逼近，允许离散卡池带来的误差。");
        }

        [Test]
        public void BuildDeck_eachCardLevelMatchesConfigTemplate()
        {
            var cfg = ConfigManager.Instance;
            if (cfg == null)
                Assert.Ignore("ConfigManager 未初始化，跳过集成断言。");

            var deck = BossDeckGenerator.BuildDeck(5, cfg, new Random(7));
            foreach (var c in deck)
            {
                Assert.IsTrue(cfg.TryGetCard(c.Id, out var row), $"卡表缺少 Id={c.Id}");
                Assert.AreEqual(row.Level, c.Level, FloatEpsilon);
            }
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
