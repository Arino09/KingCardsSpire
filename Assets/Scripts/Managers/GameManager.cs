using System;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KingCardsSpire.Managers
{
    public sealed class GameManager : PersistentMonoSingleton<GameManager>
    {
        private bool _gameOver;
        private bool _runVictory;
        private EventManager _events;

        public PlayerData PlayerState { get; private set; } = new();
        public FloorState FloorState { get; private set; } = new();

        public bool IsGameOver => _gameOver;
        public bool IsRunVictory => _runVictory;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (_events != null)
                _events.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
            ServiceLocator.Unregister<GameManager>();
            base.OnDestroy();
        }

        public void InitializeGame()
        {
            _events = EventManager.Instance;
            _events?.Subscribe<BattleEndedEvent>(OnBattleEnded);
        }

        /// <summary>新开局：重置玩家与第一层 FloorState，并滚动首日天气。</summary>
        public void StartNewGame()
        {
            _gameOver = false;
            _runVictory = false;

            var gc = ResolveGameConfig();
            PlayerState = new PlayerData
            {
                CurrentFloor = 1,
                CurrentDay = 1,
                FloorDay = 0,
                Gold = gc?.InitialGold ?? 50,
                HandCards = Array.Empty<Card>(),
                DiscardPile = Array.Empty<Card>(),
                OwnedCards = Array.Empty<Card>(),
                SelectedBuff = BuffId.None,
                XRayCount = gc?.InitialXRayCount ?? 1,
                CurrentWeather = WeatherType.WarmWind,
                UnlockedDialogues = Array.Empty<string>(),
                UnlockedAchievements = Array.Empty<string>()
            };

            FloorState = new FloorState { BossDefeated = false };
            SyncFloorStateFromTower();
            RollDailyWeather();
            _events?.Publish(new GameStartedEvent());
        }

        /// <summary>击败驻守者后进层；返回是否仍在本 Run 内继续（false 可能表示已通关或条件不足）。</summary>
        public bool EnterNextFloor()
        {
            if (_gameOver || _runVictory)
                return false;
            if (!FloorState.BossDefeated)
                return false;

            var gc = ResolveGameConfig();
            var maxFloors = gc?.TowerFloors ?? 7;
            PlayerState.CurrentFloor++;
            if (PlayerState.CurrentFloor > maxFloors)
            {
                _runVictory = true;
                _events?.Publish(new GameVictoryEvent());
                return true;
            }

            PlayerState.FloorDay = 0;
            FloorState.BossDefeated = false;
            PlayerState.XRayCount++;
            SyncFloorStateFromTower();
            _events?.Publish(new FloorChangedEvent(PlayerState.CurrentFloor));
            return true;
        }

        /// <summary>结束当日：天数与本层天数+1，校验失败；滚动次日天气。</summary>
        public void AdvanceDay()
        {
            if (_gameOver || _runVictory)
                return;

            PlayerState.CurrentDay++;
            PlayerState.FloorDay++;
            _events?.Publish(new DayChangedEvent(PlayerState.CurrentDay));

            if (PlayerState.Gold <= 0)
            {
                FailRun("gold_empty");
                return;
            }

            var maxDays = ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
            if (PlayerState.FloorDay > maxDays && !FloorState.BossDefeated)
                FailRun("floor_timeout");

            if (_gameOver)
                return;

            RollDailyWeather();
        }

        /// <summary>增收应用雨季 +50%（文档 §2.3）；支出不加成。</summary>
        public void AddGold(int delta)
        {
            if (delta == 0)
                return;
            var applied = delta;
            if (delta > 0 && PlayerState.CurrentWeather == WeatherType.Rainy)
                applied = Mathf.RoundToInt(delta * 1.5f);

            PlayerState.Gold += applied;
            _events?.Publish(new GoldChangedEvent(PlayerState.Gold));
        }

        public void RollDailyWeather()
        {
            var values = (WeatherType[])Enum.GetValues(typeof(WeatherType));
            var w = values[Random.Range(0, values.Length)];
            PlayerState.CurrentWeather = w;
            _events?.Publish(new WeatherChangedEvent(w));
        }

        private void SyncFloorStateFromTower()
        {
            FloorState.FloorIndex = PlayerState.CurrentFloor;
            var cfg = ConfigManager.Instance;
            if (cfg != null && cfg.TryGetTowerFloor(PlayerState.CurrentFloor, out var entry))
            {
                FloorState.BossId = entry.BossId;
                var npc = entry.NpcIds;
                FloorState.NpcIds = npc != null ? (string[])npc.Clone() : Array.Empty<string>();
            }
            else
            {
                FloorState.BossId = $"floor_{PlayerState.CurrentFloor}_boss";
                FloorState.NpcIds = Array.Empty<string>();
            }
        }

        private void FailRun(string reason)
        {
            if (_gameOver)
                return;
            _gameOver = true;
            _events?.Publish(new GameOverEvent(reason));
        }

        private void OnBattleEnded(BattleEndedEvent e)
        {
            if (!e.IsBossBattle || _gameOver || _runVictory)
                return;
            if (!e.PlayerVictory)
                return;

            FloorState.BossDefeated = true;

            var cfgMgr = ConfigManager.Instance;
            var maxDays = ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
            var spareDays = Mathf.Max(0, maxDays - PlayerState.FloorDay);
            TowerFloorEntry entry = null;
            if (cfgMgr != null)
                cfgMgr.TryGetTowerFloor(PlayerState.CurrentFloor, out entry);
            var options = BossRewardPicker.Generate(spareDays, entry, cfgMgr);
            _events?.Publish(new BossRewardOfferedEvent(PlayerState.CurrentFloor, options));
        }

        private GameConfig ResolveGameConfig()
        {
            var list = ConfigManager.Instance?.GetGameConfigs();
            if (list == null || list.Count == 0)
                return null;
            for (var i = 0; i < list.Count; i++)
            {
                var g = list[i];
                if (g != null && g.Id == "default")
                    return g;
            }

            return list[0];
        }
    }
}
