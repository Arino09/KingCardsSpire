using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class ConfigManager : PersistentMonoSingleton<ConfigManager>
    {
        readonly Dictionary<string, CardConfig> _cards = new();
        readonly Dictionary<string, BuffConfig> _buffs = new();
        readonly Dictionary<string, WeatherConfig> _weathers = new();
        readonly List<ShopConfig> _shopConfigs = new();
        readonly List<GameConfig> _gameConfigs = new();

        public int CardCount => _cards.Count;
        public int BuffCount => _buffs.Count;
        public int WeatherCount => _weathers.Count;
        public int ShopConfigCount => _shopConfigs.Count;
        public int GameConfigCount => _gameConfigs.Count;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<ConfigManager>();
            base.OnDestroy();
        }

        public IEnumerator InitializeConfigsAsync()
        {
            var assets = AssetManager.Instance;
            yield return assets.InitializeAsync();

            IList<CardConfig> cardList = null;
            yield return assets.LoadAssetsAsync<CardConfig>(AddressableLabels.ConfigCard, l => cardList = l);
            IndexCards(cardList);

            IList<BuffConfig> buffList = null;
            yield return assets.LoadAssetsAsync<BuffConfig>(AddressableLabels.ConfigBuff, l => buffList = l);
            IndexBuffs(buffList);

            IList<WeatherConfig> weatherList = null;
            yield return assets.LoadAssetsAsync<WeatherConfig>(AddressableLabels.ConfigWeather, l => weatherList = l);
            IndexWeathers(weatherList);

            IList<ShopConfig> shopList = null;
            yield return assets.LoadAssetsAsync<ShopConfig>(AddressableLabels.ConfigShop, l => shopList = l);
            IndexShop(shopList);

            IList<GameConfig> gameList = null;
            yield return assets.LoadAssetsAsync<GameConfig>(AddressableLabels.ConfigGame, l => gameList = l);
            IndexGame(gameList);
        }

        void IndexCards(IList<CardConfig> list)
        {
            _cards.Clear();
            if (list == null)
                return;
            foreach (var c in list)
            {
                if (c == null || string.IsNullOrEmpty(c.Id))
                    continue;
                if (!_cards.ContainsKey(c.Id))
                    _cards.Add(c.Id, c);
            }
        }

        void IndexBuffs(IList<BuffConfig> list)
        {
            _buffs.Clear();
            if (list == null)
                return;
            foreach (var b in list)
            {
                if (b == null || string.IsNullOrEmpty(b.Id))
                    continue;
                if (!_buffs.ContainsKey(b.Id))
                    _buffs.Add(b.Id, b);
            }
        }

        void IndexWeathers(IList<WeatherConfig> list)
        {
            _weathers.Clear();
            if (list == null)
                return;
            foreach (var w in list)
            {
                if (w == null || string.IsNullOrEmpty(w.Id))
                    continue;
                if (!_weathers.ContainsKey(w.Id))
                    _weathers.Add(w.Id, w);
            }
        }

        void IndexShop(IList<ShopConfig> list)
        {
            _shopConfigs.Clear();
            if (list == null)
                return;
            foreach (var s in list)
            {
                if (s != null)
                    _shopConfigs.Add(s);
            }
        }

        void IndexGame(IList<GameConfig> list)
        {
            _gameConfigs.Clear();
            if (list == null)
                return;
            foreach (var g in list)
            {
                if (g != null)
                    _gameConfigs.Add(g);
            }
        }

        public bool TryGetCard(string id, out CardConfig config) => _cards.TryGetValue(id, out config);

        public bool TryGetBuff(string id, out BuffConfig config) => _buffs.TryGetValue(id, out config);

        public bool TryGetWeather(string id, out WeatherConfig config) => _weathers.TryGetValue(id, out config);

        public IReadOnlyList<ShopConfig> GetShopConfigs() => _shopConfigs;

        public IReadOnlyList<GameConfig> GetGameConfigs() => _gameConfigs;
    }
}
