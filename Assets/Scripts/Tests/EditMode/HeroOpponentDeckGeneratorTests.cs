using System.Collections.Generic;
using KingCardsSpire.Core.Battle;
using NUnit.Framework;

namespace KingCardsSpire.Tests
{
    public sealed class HeroOpponentDeckGeneratorTests
    {
        [Test]
        public void PickWithReplacementIndices_EmptyPool_AddsNothing()
        {
            var dest = new List<int>();
            HeroOpponentDeckGenerator.PickWithReplacementIndices(0, 3, dest, (_, __) => 0);
            Assert.That(dest, Is.Empty);
        }

        [Test]
        public void PickWithReplacementIndices_FixedSequence()
        {
            var dest = new List<int>();
            var q = new Queue<int>(new[] { 1, 0, 1 });
            HeroOpponentDeckGenerator.PickWithReplacementIndices(2, 3, dest, (_, __) => q.Dequeue());
            Assert.That(dest, Is.EqualTo(new[] { 1, 0, 1 }).AsCollection);
        }

        [Test]
        public void PickWithReplacementIndices_SingleElementPool_AlwaysZero()
        {
            var dest = new List<int>();
            HeroOpponentDeckGenerator.PickWithReplacementIndices(1, 5, dest, (min, _) => min);
            Assert.AreEqual(5, dest.Count);
            for (var i = 0; i < dest.Count; i++)
                Assert.AreEqual(0, dest[i]);
        }
    }
}
