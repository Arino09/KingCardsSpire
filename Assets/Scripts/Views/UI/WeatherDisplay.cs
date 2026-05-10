using KingCardsSpire.Models;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 天气枚举的中文展示（与塔内主界面一致）。
    /// </summary>
    public static class WeatherDisplay
    {
        public static string Format(WeatherType w)
        {
            return w switch
            {
                WeatherType.Rainy => "雨季",
                WeatherType.Sunny => "烈日",
                WeatherType.Hail => "冰雹",
                WeatherType.WarmWind => "暖风",
                WeatherType.Ending => "终焉",
                _ => w.ToString()
            };
        }
    }
}
