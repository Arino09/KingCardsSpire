using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>击败驻守者后待玩家在界面中确认的奖励选项（与 <see cref="BossRewardOfferedEvent"/> 同步）。</summary>
        private BossRewardOption[] _pendingBossRewards;

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
            {
                _events.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
                _events.Unsubscribe<GameOverEvent>(OnGameOverNavToTitle);
            }

            ServiceLocator.Unregister<GameManager>();
            base.OnDestroy();
        }

        public void InitializeGame()
        {
            _events = EventManager.Instance;
            _events?.Subscribe<BattleEndedEvent>(OnBattleEnded);
            _events?.Subscribe<GameOverEvent>(OnGameOverNavToTitle);
        }

        /// <summary>每层允许停留天数上限（配置 MaxDaysPerFloor）。</summary>
        public int GetMaxDaysPerFloor()
        {
            return ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
        }

        /// <summary>根据当前本层已过天数估算剩余可停留天数（见超时判定 FloorDay &gt; MaxDaysPerFloor）。</summary>
        public int GetEstimatedRemainingDaysOnFloor()
        {
            var max = GetMaxDaysPerFloor();
            return Mathf.Max(0, max - PlayerState.FloorDay);
        }

        /// <summary>从存档恢复当前 Run（Boot 主菜单「继续游戏」）。</summary>
        public void RestoreRunFromSave(SaveData data)
        {
            if (data == null)
                return;

            _gameOver = false;
            _runVictory = false;
            PlayerState = data.Player ?? new PlayerData();
            FloorState = data.Floor ?? new FloorState();
            if (FloorState.FloorIndex <= 0)
                FloorState.FloorIndex = PlayerState.CurrentFloor;
            _pendingBossRewards = null;
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
            _pendingBossRewards = null;
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

        /// <summary>供驻守奖励界面读取的待选列表（与最近一次 <see cref="BossRewardOfferedEvent"/> 一致）。</summary>
        public IReadOnlyList<BossRewardOption> PendingBossRewards => _pendingBossRewards;

        /// <summary>玩家确认一项驻守奖励后应用金币/卡牌并 <see cref="EnterNextFloor"/>。</summary>
        /// <returns>是否成功应用并进层（含通关触发）。</returns>
        public bool TryApplyBossRewardChoice(int optionIndex)
        {
            if (_gameOver || _runVictory)
                return false;
            if (!FloorState.BossDefeated)
                return false;
            if (_pendingBossRewards == null || optionIndex < 0 ||
                optionIndex >= _pendingBossRewards.Length)
                return false;

            var opt = _pendingBossRewards[optionIndex];
            _pendingBossRewards = null;

            if (opt.IsGold)
                AddGold(opt.GoldAmount);
            else if (!string.IsNullOrEmpty(opt.CardId))
            {
                var cfgMgr = ConfigManager.Instance;
                if (cfgMgr != null && cfgMgr.TryGetCard(opt.CardId, out var cc))
                {
                    AppendOwnedCard(CardFromConfig(cc));
                    _events?.Publish(new CardAcquiredEvent(cc.Id));
                }
                else
                    Debug.LogWarning($"[GameManager] 驻守奖励卡牌配置缺失: {opt.CardId}");
            }

            EnterNextFloor();
            return true;
        }

        /// <summary>
        /// 跳过卡牌奖励：发放本次待选列表中<strong>全部金币项</strong>（每笔单独调用 <see cref="AddGold"/>，雨季增收仍生效），
        /// 不领取任何卡牌；随后清空待选并进层。
        /// </summary>
        public bool TrySkipBossCardRewardsCollectGoldAndAdvance()
        {
            if (_gameOver || _runVictory)
                return false;
            if (!FloorState.BossDefeated)
                return false;
            if (_pendingBossRewards == null || _pendingBossRewards.Length == 0)
                return false;

            for (var i = 0; i < _pendingBossRewards.Length; i++)
            {
                var opt = _pendingBossRewards[i];
                if (opt == null)
                    continue;
                if (opt.IsGold)
                    AddGold(opt.GoldAmount);
            }

            _pendingBossRewards = null;
            EnterNextFloor();
            return true;
        }

        private static Card CardFromConfig(CardConfigEntry cc)
        {
            return new Card
            {
                Id = cc.Id,
                Name = string.IsNullOrEmpty(cc.DisplayName) ? cc.Id : cc.DisplayName,
                Level = cc.Level,
                Type = cc.Type,
                EffectDesc = cc.Description,
                IsUnique = cc.IsUnique
            };
        }

        private void AppendOwnedCard(Card card)
        {
            if (card == null)
                return;
            var list = new List<Card>(PlayerState.OwnedCards ?? Array.Empty<Card>());
            list.Add(card);
            PlayerState.OwnedCards = list.ToArray();
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

        /// <summary>
        /// BOSS 战败结束 Run 并回标题；BOSS 胜发驻守奖励。
        /// 非 BOSS 战不改变 Run，收尾仅在 <see cref="BattleView"/>（关闭战斗回到 MainHub）。
        /// </summary>
        private void OnBattleEnded(BattleEndedEvent e)
        {
            if (_gameOver || _runVictory)
                return;

            if (e.IsBossBattle && !e.PlayerVictory)
            {
                FailRun("boss_defeat");
                return;
            }

            if (!e.IsBossBattle || !e.PlayerVictory)
                return;

            FloorState.BossDefeated = true;

            var cfgMgr = ConfigManager.Instance;
            var maxDays = ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
            var spareDays = Mathf.Max(0, maxDays - PlayerState.FloorDay);
            TowerFloorEntry entry = null;
            if (cfgMgr != null)
                cfgMgr.TryGetTowerFloor(PlayerState.CurrentFloor, out entry);
            var options = BossRewardPicker.Generate(spareDays, entry, cfgMgr);
            _pendingBossRewards = options;
            _events?.Publish(new BossRewardOfferedEvent(PlayerState.CurrentFloor, options));
        }

        /// <summary>
        /// Run 结束时关闭当前全部面板并回到标题（主菜单）；例如 BOSS 战败、金币耗尽、层内超时等。
        /// </summary>
        private void OnGameOverNavToTitle(GameOverEvent _)
        {
            StartCoroutine(ReturnToTitleRoutine());
        }

        private static IEnumerator ReturnToTitleRoutine()
        {
            yield return null;

            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            ui.CloseAll();
            yield return ui.OpenAsync(UIPanelId.MainMenu);
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
