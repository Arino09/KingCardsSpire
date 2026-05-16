using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using NUnit.Framework;

namespace KingCardsSpire.Tests.EditMode
{
    public sealed class GameManagerWeatherRollTests
    {
        [Test]
        public void IsFinalFloorForFixedEndingWeather_true_only_on_last_floor()
        {
            Assert.IsTrue(GameManager.IsFinalFloorForFixedEndingWeather(7, 7));
            Assert.IsFalse(GameManager.IsFinalFloorForFixedEndingWeather(6, 7));
            Assert.IsFalse(GameManager.IsFinalFloorForFixedEndingWeather(8, 7));
        }

        [Test]
        public void IsFinalFloorForFixedEndingWeather_false_when_towerFloors_invalid()
        {
            Assert.IsFalse(GameManager.IsFinalFloorForFixedEndingWeather(1, 0));
            Assert.IsFalse(GameManager.IsFinalFloorForFixedEndingWeather(0, 7));
        }

        [Test]
        public void Daily_random_pool_excludes_Ending_and_Clear_and_matches_enum_order()
        {
            var n = GameManager.GetNonEndingWeatherKindCountForTests();
            Assert.Greater(n, 0);
            for (var i = 0; i < n; i++)
            {
                var w = GameManager.GetNonEndingWeatherByOrdinalForTests(i);
                Assert.AreNotEqual(WeatherType.Ending, w);
                Assert.AreNotEqual(WeatherType.Clear, w);
            }
        }
    }
}
