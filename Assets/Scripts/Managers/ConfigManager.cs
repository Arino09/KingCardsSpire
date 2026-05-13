using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class ConfigManager : PersistentMonoSingleton<ConfigManager>
    {
        private readonly Dictionary<string, CardConfigEntry> _cards = new();
        private readonly Dictionary<string, BuffConfig> _buffs = new();
        private readonly Dictionary<string, WeatherConfig> _weathers = new();
        private readonly List<ShopConfig> _shopConfigs = new();
        private readonly List<GameConfig> _gameConfigs = new();
        private readonly List<TowerConfig> _towerConfigs = new();
        private readonly Dictionary<string, DialogueLineEntry> _dialogueLines = new();
        private readonly Dictionary<string, HeroEntry> _heroes = new();
        private readonly List<HeroEntry> _heroSlots = new();
        private readonly Dictionary<string, NpcEntry> _npcs = new();

        public int CardCount => _cards.Count;
        public int BuffCount => _buffs.Count;
        public int WeatherCount => _weathers.Count;
        public int ShopConfigCount => _shopConfigs.Count;
        public int GameConfigCount => _gameConfigs.Count;

        public int TowerConfigCount => _towerConfigs.Count;

        public int DialogueLineCount => _dialogueLines.Count;

        public int HeroCount => _heroes.Count;

        public int NpcCount => _npcs.Count;

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

            IList<TowerConfig> towerList = null;
            yield return assets.LoadAssetsAsync<TowerConfig>(AddressableLabels.ConfigTower, l => towerList = l);
            IndexTowers(towerList);

            IList<DialogueConfig> dialogueList = null;
            yield return assets.LoadAssetsAsync<DialogueConfig>(AddressableLabels.ConfigDialogue, l => dialogueList = l);
            IndexDialogues(dialogueList);

            IList<HeroConfig> heroList = null;
            yield return assets.LoadAssetsAsync<HeroConfig>(AddressableLabels.ConfigHero, l => heroList = l);
            IndexHeroes(heroList);

            IList<NpcConfig> npcList = null;
            yield return assets.LoadAssetsAsync<NpcConfig>(AddressableLabels.ConfigNpc, l => npcList = l);
            IndexNpcs(npcList);
        }

        private void IndexCards(IList<CardConfig> list)
        {
            _cards.Clear();
            if (list == null)
                return;
            foreach (var db in list)
            {
                if (db == null)
                    continue;
                var entries = db.Cards;
                if (entries == null)
                    continue;
                for (var i = 0; i < entries.Count; i++)
                {
                    var c = entries[i];
                    if (c == null || string.IsNullOrEmpty(c.Id))
                        continue;
                    if (!_cards.ContainsKey(c.Id))
                        _cards.Add(c.Id, c);
                }
            }
        }

        private void IndexBuffs(IList<BuffConfig> list)
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

        private void IndexWeathers(IList<WeatherConfig> list)
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

        private void IndexShop(IList<ShopConfig> list)
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

        private void IndexGame(IList<GameConfig> list)
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

        private void IndexTowers(IList<TowerConfig> list)
        {
            _towerConfigs.Clear();
            if (list == null)
                return;
            foreach (var t in list)
            {
                if (t != null)
                    _towerConfigs.Add(t);
            }
        }

        private void IndexDialogues(IList<DialogueConfig> list)
        {
            _dialogueLines.Clear();
            if (list == null)
                return;

            for (var i = 0; i < list.Count; i++)
            {
                var db = list[i];
                if (db == null)
                    continue;

                var lines = db.Lines;
                if (lines == null)
                    continue;

                for (var j = 0; j < lines.Count; j++)
                {
                    var line = lines[j];
                    if (line == null || string.IsNullOrEmpty(line.Id))
                        continue;
                    _dialogueLines[line.Id] = line;
                }
            }
        }

        private void IndexHeroes(IList<HeroConfig> list)
        {
            _heroes.Clear();
            _heroSlots.Clear();
            if (list == null)
                return;

            for (var i = 0; i < list.Count; i++)
            {
                var db = list[i];
                if (db == null || db.Heroes == null)
                    continue;

                for (var j = 0; j < db.Heroes.Count; j++)
                {
                    var entry = db.Heroes[j];
                    if (entry == null || string.IsNullOrEmpty(entry.Id))
                        continue;

                    _heroes[entry.Id] = entry;
                    if (entry.Id != StoryDialogueRules.NarratorCharacterId)
                        _heroSlots.Add(entry);
                }
            }
        }

        private void IndexNpcs(IList<NpcConfig> list)
        {
            _npcs.Clear();
            if (list == null)
                return;

            for (var i = 0; i < list.Count; i++)
            {
                var db = list[i];
                if (db == null || db.Npcs == null)
                    continue;

                for (var j = 0; j < db.Npcs.Count; j++)
                {
                    var entry = db.Npcs[j];
                    if (entry == null || string.IsNullOrEmpty(entry.NpcId))
                        continue;

                    _npcs[entry.NpcId] = entry;
                }
            }
        }

        public bool TryGetDialogueLine(string id, out DialogueLineEntry line)
        {
            line = null;
            return !string.IsNullOrEmpty(id) && _dialogueLines.TryGetValue(id, out line);
        }

        public bool TryGetHero(string id, out HeroEntry entry)
        {
            entry = null;
            return !string.IsNullOrEmpty(id) && _heroes.TryGetValue(id, out entry);
        }

        public bool TryGetHeroBySlot(int slotIndex, out HeroEntry entry)
        {
            entry = null;
            if (slotIndex < 0 || slotIndex >= _heroSlots.Count)
                return false;

            entry = _heroSlots[slotIndex];
            return entry != null;
        }

        public bool TryGetNpc(string npcId, out NpcEntry entry)
        {
            entry = null;
            return !string.IsNullOrEmpty(npcId) && _npcs.TryGetValue(npcId, out entry);
        }

        public bool TryGetCard(string id, out CardConfigEntry config) => _cards.TryGetValue(id, out config);

        /// <summary>
        /// 商店进货：同一类型内不放回随机所用候选池（已持有的唯一卡排除）。
        /// </summary>
        public void CollectShopCandidates(CardType type, HashSet<string> ownedCardIds, List<CardConfigEntry> results)
        {
            results.Clear();
            foreach (var entry in _cards.Values)
            {
                if (entry == null || entry.Type != type)
                    continue;
                if (entry.IsUnique && ownedCardIds != null && ownedCardIds.Contains(entry.Id))
                    continue;
                results.Add(entry);
            }
        }

        public bool TryGetBuff(string id, out BuffConfig config) => _buffs.TryGetValue(id, out config);

        public bool TryGetWeather(string id, out WeatherConfig config) => _weathers.TryGetValue(id, out config);

        public IReadOnlyList<ShopConfig> GetShopConfigs() => _shopConfigs;

        public IReadOnlyList<GameConfig> GetGameConfigs() => _gameConfigs;

        /// <summary>按层数（1-based）取塔层条目；无配置或越界时返回 false。</summary>
        public bool TryGetTowerFloor(int floorIndex1Based, out TowerFloorEntry entry)
        {
            entry = null;
            var tower = ResolveTowerConfig();
            return tower != null && tower.TryGetFloor(floorIndex1Based, out entry);
        }

        /// <summary>汇总第 1 层至 <paramref name="floorInclusive"/> 层塔配置中的 npcId 并集（去重写入 <paramref name="dest"/>）。</summary>
        public void CollectNpcIdsForFloorsUpTo(int floorInclusive, ISet<string> dest)
        {
            if (dest == null || floorInclusive < 1)
                return;

            for (var floor = 1; floor <= floorInclusive; floor++)
            {
                if (!TryGetTowerFloor(floor, out var entry))
                    continue;

                var ids = entry.NpcIds;
                if (ids == null)
                    continue;

                for (var i = 0; i < ids.Length; i++)
                {
                    var id = ids[i];
                    if (!string.IsNullOrEmpty(id))
                        dest.Add(id);
                }
            }
        }

        private TowerConfig ResolveTowerConfig()
        {
            if (_towerConfigs.Count == 0)
                return null;
            for (var i = 0; i < _towerConfigs.Count; i++)
            {
                var t = _towerConfigs[i];
                if (t != null && t.Id == "default")
                    return t;
            }

            return _towerConfigs[0];
        }
    }
}
