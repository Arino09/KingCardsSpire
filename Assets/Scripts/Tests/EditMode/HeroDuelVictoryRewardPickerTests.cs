using System;
using System.Collections.Generic;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using NUnit.Framework;

namespace KingCardsSpire.Tests
{
    public sealed class HeroDuelVictoryRewardPickerTests
    {
        private const float FloatEpsilon = 1e-3f;

        [Test]
        public void BuildOfferCardIds_NullConfig_ReturnsNull()
        {
            Assert.IsNull(HeroDuelVictoryRewardPicker.BuildOfferCardIds(null, _ => false));
        }

        [Test]
        public void BuildOfferCardIds_NullOwnsPredicate_ReturnsNull()
        {
            Assert.IsNull(HeroDuelVictoryRewardPicker.BuildOfferCardIds(
                ConfigManager.Instance,
                null));
        }

        [Test]
        public void BuildOfferCardIdsFromCandidatePool_NullArgs_ReturnsNull()
        {
            var pool = new List<Card> { new Card { Id = "a", Level = 1f, Type = CardType.Basic } };
            Assert.IsNull(HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(null, (a, b) => 0,
                () => 0f));
            Assert.IsNull(HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(pool, null, () => 0f));
            Assert.IsNull(HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(pool, (a, b) => 0, null));
        }

        [Test]
        public void FromCandidatePool_AllLevelOne_LowTierRandom_ReturnsMeanAtMost1_5()
        {
            var pool = new List<Card>
            {
                new Card { Id = "a", Level = 1f, Type = CardType.Basic },
                new Card { Id = "b", Level = 1f, Type = CardType.Basic },
                new Card { Id = "c", Level = 1f, Type = CardType.Basic },
                new Card { Id = "d", Level = 1f, Type = CardType.Basic }
            };

            var rng = new Random(11);
            var result = HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(pool,
                (min, max) => rng.Next(min, max),
                () => 0f);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(3, new HashSet<string>(result).Count);
            var mean = MeanLevelById(pool, result);
            Assert.LessOrEqual(mean, 1.5f + FloatEpsilon);
        }

        [Test]
        public void FromCandidatePool_AllLevelTwo_HighTierRandom_ReturnsMeanAtLeast1_7()
        {
            var pool = new List<Card>
            {
                new Card { Id = "a", Level = 2f, Type = CardType.Basic },
                new Card { Id = "b", Level = 2f, Type = CardType.Basic },
                new Card { Id = "c", Level = 2f, Type = CardType.Basic },
                new Card { Id = "d", Level = 2f, Type = CardType.Basic }
            };

            var rng = new Random(22);
            var result = HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(pool,
                (min, max) => rng.Next(min, max),
                () => 0.99f);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            var mean = MeanLevelById(pool, result);
            Assert.GreaterOrEqual(mean, 1.7f - FloatEpsilon);
        }

        [Test]
        public void FromCandidatePool_TwoCards_ReturnsTwoDistinctIds()
        {
            var pool = new List<Card>
            {
                new Card { Id = "x", Level = 1f, Type = CardType.Basic },
                new Card { Id = "y", Level = 1f, Type = CardType.Basic }
            };

            var rng = new Random(3);
            var result = HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(pool,
                (min, max) => rng.Next(min, max),
                () => 0f);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreNotEqual(result[0], result[1]);
        }

        [Test]
        public void FromCandidatePool_MixedPool_MonteCarloRoughTierSplit()
        {
            var pool = new List<Card>
            {
                new Card { Id = "l1", Level = 1f, Type = CardType.Basic },
                new Card { Id = "l2", Level = 1f, Type = CardType.Basic },
                new Card { Id = "l3", Level = 1f, Type = CardType.Basic },
                new Card { Id = "h1", Level = 2f, Type = CardType.Basic },
                new Card { Id = "h2", Level = 2f, Type = CardType.Basic },
                new Card { Id = "h3", Level = 2f, Type = CardType.Basic }
            };

            var rng = new Random(404);
            var lowMeanCount = 0;
            var highMeanCount = 0;
            const int iterations = 4000;

            for (var i = 0; i < iterations; i++)
            {
                var result = HeroDuelVictoryRewardPicker.BuildOfferCardIdsFromCandidatePool(pool,
                    (min, max) => rng.Next(min, max),
                    () => (float)rng.NextDouble());

                Assert.IsNotNull(result);
                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(3, new HashSet<string>(result).Count);

                var mean = MeanLevelById(pool, result);
                if (mean <= 1.5f + FloatEpsilon)
                    lowMeanCount++;
                else if (mean >= 1.7f - FloatEpsilon)
                    highMeanCount++;
            }

            var lowRatio = lowMeanCount / (float)iterations;
            var highRatio = highMeanCount / (float)iterations;

            Assert.Greater(lowRatio, 0.48f, "低档均值结果比例应明显占优（目标约 70% 档位倾向 + 翻转补偿）");
            Assert.Greater(highRatio, 0.12f, "高档均值结果比例应占有显著份额（目标约 30% 档位倾向 + 翻转补偿）");
        }

        private static float MeanLevelById(IReadOnlyList<Card> pool, List<string> ids)
        {
            var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < pool.Count; i++)
            {
                var c = pool[i];
                if (c != null && !string.IsNullOrEmpty(c.Id))
                    map[c.Id] = c.Level;
            }

            var sum = 0f;
            for (var i = 0; i < ids.Count; i++)
                sum += map[ids[i]];

            return sum / ids.Count;
        }
    }
}
