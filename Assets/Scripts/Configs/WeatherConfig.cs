using System;
using System.Collections.Generic;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 单条天气的静态配置（表中的一行）。
    /// </summary>
    [Serializable]
    public sealed class WeatherConfigEntry
    {
        [SerializeField] private string id;
        [SerializeField] private WeatherType weatherType;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;

        public string Id => id;

        public WeatherType WeatherType => weatherType;

        public string DisplayName => displayName;

        public string Description => description;
    }

    /// <summary>
    /// 天气配置表：一个资源内包含全部 <see cref="WeatherConfigEntry"/>（与 <see cref="BuffConfig"/> 一致）。
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherConfig", menuName = "KingCardsSpire/Configs/Weather Database", order = 2)]
    public sealed class WeatherConfig : ScriptableObject
    {
        [SerializeField] private List<WeatherConfigEntry> weathers = new();

        public IReadOnlyList<WeatherConfigEntry> Weathers => weathers;
    }
}
