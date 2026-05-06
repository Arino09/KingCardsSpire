using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "WeatherConfig", menuName = "KingCardsSpire/Configs/Weather", order = 2)]
    public class WeatherConfig : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] WeatherType weatherType;
        [SerializeField] string displayName;
        [TextArea] [SerializeField] string description;
        [SerializeField] float modifier;

        public string Id => id;
        public WeatherType WeatherType => weatherType;
        public string DisplayName => displayName;
        public string Description => description;
        public float Modifier => modifier;
    }
}
