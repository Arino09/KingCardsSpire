using KingCardsSpire.Managers;
using NUnit.Framework;

namespace KingCardsSpire.Tests
{
    /// <summary>与 <see cref="GameManager.AdvanceDay"/> 层内超时规则一致（通过 <see cref="GameManager.EvaluateFloorTimeoutAfterAdvanceDay"/>）。</summary>
    public sealed class FloorDayAdvanceTimeoutTests
    {
        [Test]
        public void FourthDayAfterAdvance_triggersTimeout_whenBossUndefeated()
        {
            const int max = 3;
            Assert.IsFalse(GameManager.EvaluateFloorTimeoutAfterAdvanceDay(1, max, false));
            Assert.IsFalse(GameManager.EvaluateFloorTimeoutAfterAdvanceDay(2, max, false));
            Assert.IsFalse(GameManager.EvaluateFloorTimeoutAfterAdvanceDay(3, max, false));
            Assert.IsTrue(GameManager.EvaluateFloorTimeoutAfterAdvanceDay(4, max, false));
        }

        [Test]
        public void BossDefeated_doesNotTimeoutForHighFloorDay()
        {
            Assert.IsFalse(GameManager.EvaluateFloorTimeoutAfterAdvanceDay(99, 3, true));
        }
    }
}
