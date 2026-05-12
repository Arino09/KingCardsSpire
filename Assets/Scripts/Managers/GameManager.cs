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

        /// <summary>当前层、当前日的商店货架（文档 §4）。</summary>
        public ShopState ShopState { get; private set; } = new();

        private readonly List<ShopSlotState> _shopSlotsBuilder = new();
        private readonly List<CardConfigEntry> _shopCandidatesScratch = new();
        private readonly HashSet<string> _shopPickedProductIds = new();
        private readonly HashSet<string> _ownedCardIdsScratch = new();
        private readonly List<string> _npcNewEncounterScratch = new();
        private readonly HashSet<string> _npcTowerIdScratch = new();

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
            ShopState = data.Shop ?? new ShopState();
            if (data.Version < 2 && PlayerState != null)
                PlayerState.HasCompletedOpeningTutorial = true;
            if (FloorState.FloorIndex <= 0)
                FloorState.FloorIndex = PlayerState.CurrentFloor;
            _pendingBossRewards = null;
            NormalizeNpcPersistence(PlayerState);
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
                UnlockedAchievements = Array.Empty<string>(),
                MetNpcIds = Array.Empty<string>(),
                LastNpcInteractionDay = 0
            };

            ApplyStarterDeckFromConfig(PlayerState, gc);

            FloorState = new FloorState { BossDefeated = false };
            ShopState = new ShopState();
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

        /// <summary>扣除金币（支出不受雨季加成）。</summary>
        public bool TrySpendGold(int amount)
        {
            if (_gameOver || _runVictory || amount <= 0)
                return false;
            if (PlayerState.Gold < amount)
                return false;

            PlayerState.Gold -= amount;
            _events?.Publish(new GoldChangedEvent(PlayerState.Gold));
            return true;
        }

        /// <summary>
        /// 若当前层/日与货架一致则保留；否则按 <see cref="ShopConfig"/> 重新进货（文档 §4.2）。
        /// </summary>
        public void EnsureShopStock()
        {
            if (_gameOver || _runVictory)
                return;

            if (ShopState.FloorIndex == PlayerState.CurrentFloor &&
                ShopState.DayIndex == PlayerState.CurrentDay &&
                ShopState.Slots != null &&
                ShopState.Slots.Length > 0)
                return;

            var shopCfg = ResolveShopConfig();
            if (shopCfg == null)
            {
                Debug.LogWarning("[GameManager] 未找到 ShopConfig，商店货架为空。");
                ShopState.FloorIndex = PlayerState.CurrentFloor;
                ShopState.DayIndex = PlayerState.CurrentDay;
                ShopState.Slots = Array.Empty<ShopSlotState>();
                return;
            }

            RollShopStockFromConfig(shopCfg);
        }

        /// <summary>购买指定槽位；无限供应 Buff 下不买断货架。</summary>
        public bool TryPurchaseShopSlot(int index)
        {
            if (_gameOver || _runVictory)
                return false;

            EnsureShopStock();

            var slots = ShopState.Slots;
            if (slots == null || index < 0 || index >= slots.Length)
                return false;

            var slot = slots[index];
            if (slot.SoldOut)
                return false;

            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr == null || !cfgMgr.TryGetCard(slot.ProductId, out var cc))
                return false;

            if (cc.IsUnique && OwnsCardId(cc.Id))
                return false;

            if (!TrySpendGold(slot.BasePrice))
                return false;

            AppendOwnedCard(CardFromConfig(cc));
            _events?.Publish(new CardAcquiredEvent(cc.Id));

            var unlimited = PlayerState.SelectedBuff == BuffId.UnlimitedSupply;
            if (!unlimited)
            {
                slot.SoldOut = true;
                slots[index] = slot;
            }

            return true;
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

        /// <summary>
        /// 按 <see cref="GameConfig.StarterDeckCardIds"/> 把卡牌写入「持有卡组」；列表为空或未配置则保持空数组。
        /// </summary>
        private void ApplyStarterDeckFromConfig(PlayerData player, GameConfig gc)
        {
            if (player == null)
                return;
            var ids = gc?.StarterDeckCardIds;
            if (ids == null || ids.Length == 0)
                return;

            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr == null)
                return;

            var list = new List<Card>();
            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id))
                    continue;
                if (cfgMgr.TryGetCard(id, out var cc))
                    list.Add(CardFromConfig(cc));
                else
                    Debug.LogWarning($"[GameManager] 开局卡组配置未知卡牌 Id: {id}");
            }

            if (list.Count > 0)
                player.OwnedCards = list.ToArray();
        }

        private void AppendOwnedCard(Card card)
        {
            if (card == null)
                return;
            var list = new List<Card>(PlayerState.OwnedCards ?? Array.Empty<Card>());
            list.Add(card);
            PlayerState.OwnedCards = list.ToArray();
        }

        private bool OwnsCardId(string cardId)
        {
            foreach (var c in PlayerState.OwnedCards ?? Array.Empty<Card>())
            {
                if (c != null && c.Id == cardId)
                    return true;
            }

            return false;
        }

        private void RollShopStockFromConfig(ShopConfig shopCfg)
        {
            _shopPickedProductIds.Clear();
            _ownedCardIdsScratch.Clear();
            foreach (var c in PlayerState.OwnedCards ?? Array.Empty<Card>())
            {
                if (c != null && !string.IsNullOrEmpty(c.Id))
                    _ownedCardIdsScratch.Add(c.Id);
            }

            _shopSlotsBuilder.Clear();
            AddShopSlotsForType(CardType.Ability, shopCfg.AbilityCardSlots, shopCfg.AbilityCardPrice);
            AddShopSlotsForType(CardType.Function, shopCfg.FunctionCardSlots, shopCfg.FunctionCardPrice);
            AddShopSlotsForType(CardType.Basic, shopCfg.BasicCardSlots, shopCfg.BasicCardPrice);
            AddShopSlotsForType(CardType.Consumable, shopCfg.ConsumableCardSlots, shopCfg.ConsumableCardPrice);

            ShopState.FloorIndex = PlayerState.CurrentFloor;
            ShopState.DayIndex = PlayerState.CurrentDay;
            ShopState.Slots = _shopSlotsBuilder.ToArray();
        }

        private void AddShopSlotsForType(CardType type, int count, int unitPrice)
        {
            if (count <= 0)
                return;

            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr == null)
                return;

            for (var i = 0; i < count; i++)
            {
                cfgMgr.CollectShopCandidates(type, _ownedCardIdsScratch, _shopCandidatesScratch);
                for (var j = _shopCandidatesScratch.Count - 1; j >= 0; j--)
                {
                    var id = _shopCandidatesScratch[j].Id;
                    if (_shopPickedProductIds.Contains(id))
                        _shopCandidatesScratch.RemoveAt(j);
                }

                if (_shopCandidatesScratch.Count == 0)
                {
                    Debug.LogWarning($"[GameManager] 商店 {type} 类型无可用卡牌，跳过一格。");
                    continue;
                }

                var pick = _shopCandidatesScratch[Random.Range(0, _shopCandidatesScratch.Count)];
                _shopPickedProductIds.Add(pick.Id);
                _shopSlotsBuilder.Add(new ShopSlotState
                {
                    ProductId = pick.Id,
                    BasePrice = unitPrice,
                    SoldOut = false
                });
            }
        }

        private ShopConfig ResolveShopConfig()
        {
            var list = ConfigManager.Instance?.GetShopConfigs();
            if (list == null || list.Count == 0)
                return null;
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s != null && s.Id == "default")
                    return s;
            }

            return list[0];
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

        /// <summary>今日是否仍可访问 NPC（主界面按钮与进入 NPCView 的门禁）。</summary>
        public bool HasNpcVisitRemainingToday()
        {
            return PlayerState.LastNpcInteractionDay != PlayerState.CurrentDay;
        }

        /// <summary>当前层及以下塔层中，是否存在尚未结识的 npcId。</summary>
        public bool IsNewNpcEncounterPoolEmpty()
        {
            RefreshNewNpcEncounterScratch();
            return _npcNewEncounterScratch.Count == 0;
        }

        /// <summary>从塔配置可结识池中随机一个未在 <see cref="PlayerData.MetNpcIds"/> 中的 npcId。</summary>
        public bool TryPickRandomNewNpcFromTower(out string pickedNpcId)
        {
            pickedNpcId = null;
            RefreshNewNpcEncounterScratch();
            if (_npcNewEncounterScratch.Count == 0)
                return false;

            pickedNpcId = _npcNewEncounterScratch[Random.Range(0, _npcNewEncounterScratch.Count)];
            return true;
        }

        /// <summary>消耗当日唯一一次 NPC 访问并记录已遇；对话系统未接入前为占位日志/事件。</summary>
        public bool TryInteractWithNpc(string npcId)
        {
            if (_gameOver || _runVictory || string.IsNullOrEmpty(npcId))
                return false;

            if (PlayerState.LastNpcInteractionDay == PlayerState.CurrentDay)
            {
                Debug.LogWarning("[GameManager] TryInteractWithNpc: 当日访问已消耗，不应从 UI 再次触发。");
                return false;
            }

            PlayerState.LastNpcInteractionDay = PlayerState.CurrentDay;
            AppendMetNpcIfMissing(npcId, PlayerState);
            _events?.Publish(new NpcEncounterStartedEvent(npcId));
            Debug.Log($"[GameManager] NPC 遭遇占位: {npcId}");
            return true;
        }

        /// <summary>供 NPC 列表展示：已遇 Id 按字典序排序的副本。</summary>
        public string[] GetMetNpcIdsSortedCopy()
        {
            var met = PlayerState.MetNpcIds ?? Array.Empty<string>();
            if (met.Length <= 1)
                return (string[])met.Clone();

            var copy = (string[])met.Clone();
            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }

        private void RefreshNewNpcEncounterScratch()
        {
            _npcNewEncounterScratch.Clear();
            var cfg = ConfigManager.Instance;
            if (cfg == null)
                return;

            _npcTowerIdScratch.Clear();
            cfg.CollectNpcIdsForFloorsUpTo(PlayerState.CurrentFloor, _npcTowerIdScratch);

            var met = PlayerState.MetNpcIds ?? Array.Empty<string>();
            foreach (var id in _npcTowerIdScratch)
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                if (Array.IndexOf(met, id) >= 0)
                    continue;
                _npcNewEncounterScratch.Add(id);
            }
        }

        private static void AppendMetNpcIfMissing(string npcId, PlayerData player)
        {
            var met = player.MetNpcIds;
            if (met == null || met.Length == 0)
            {
                player.MetNpcIds = new[] { npcId };
                return;
            }

            if (Array.IndexOf(met, npcId) >= 0)
                return;

            var list = new List<string>(met) { npcId };
            player.MetNpcIds = list.ToArray();
        }

        private static void NormalizeNpcPersistence(PlayerData player)
        {
            if (player == null)
                return;

            if (player.MetNpcIds == null)
                player.MetNpcIds = Array.Empty<string>();
        }

        /// <summary>将对话行 id 记入已解锁列表（去重）。</summary>
        public void RegisterDialogueLineSeen(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId) || PlayerState == null)
                return;

            var arr = PlayerState.UnlockedDialogues ?? Array.Empty<string>();
            if (Array.IndexOf(arr, dialogueId) >= 0)
                return;

            var next = new List<string>(arr) { dialogueId };
            PlayerState.UnlockedDialogues = next.ToArray();
        }

        /// <summary>将当前 Run 写入默认存档位，保留盘上历史记录条目。</summary>
        public void PersistCurrentRunToDisk()
        {
            var pm = PersistenceManager.Instance;
            if (pm == null)
                return;

            var existing = pm.Load();
            var history = existing != null && existing.History != null
                ? existing.History
                : Array.Empty<HistoryRecord>();

            var data = new SaveData
            {
                Version = 2,
                Player = PlayerState,
                Floor = FloorState,
                Shop = ShopState,
                History = history
            };

            pm.Save(data);
        }

        /// <summary>标记开场教程对话已完成并写盘。</summary>
        public void SetOpeningTutorialCompletedAndSave()
        {
            if (PlayerState == null)
                return;
            PlayerState.HasCompletedOpeningTutorial = true;
            PersistCurrentRunToDisk();
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
