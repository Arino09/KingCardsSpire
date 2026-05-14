using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI;
using UnityEngine;
using Random = UnityEngine.Random;

[assembly: InternalsVisibleTo("KingCardsSpire.Tests")]

namespace KingCardsSpire.Managers
{
    public sealed class GameManager : PersistentMonoSingleton<GameManager>
    {
        private bool _gameOver;
        private bool _runVictory;
        private EventManager _events;

        /// <summary>击败驻守者后待玩家在界面中确认的奖励选项（与 <see cref="BossRewardOfferedEvent"/> 同步）。</summary>
        private BossRewardOption[] _pendingBossRewards;

        /// <summary>上一场玩家胜利且打出金色项链时，下一次驻守金币结算翻倍（领取后清除）。</summary>
        private bool _pendingBossVictoryGoldenNecklaceDoubleGold;

        public PlayerData PlayerState { get; private set; } = new();
        public FloorState FloorState { get; private set; } = new();

        /// <summary>当前层、当前日的商店货架（文档 §4）。</summary>
        public ShopState ShopState { get; private set; } = new();
        
        public GameConfig GameConfig => ResolveGameConfig();

        private readonly List<ShopSlotState> _shopSlotsBuilder = new();
        private readonly List<CardConfigEntry> _shopCandidatesScratch = new();
        private readonly HashSet<string> _shopPickedProductIds = new();
        private readonly HashSet<string> _ownedCardIdsScratch = new();
        private readonly List<string> _npcNewEncounterScratch = new();
        private readonly HashSet<string> _npcTowerIdScratch = new();

        private static readonly BuffId[] DraftableBuffIds =
        {
            BuffId.RichSecondGen,
            BuffId.UnlimitedSupply,
            BuffId.RandomCommoner,
            BuffId.RandomKing,
            BuffId.SurprisePack,
            BuffId.XRayBoost,
            BuffId.ChaoticBattlefield
        };

        private readonly BuffId[] _buffDraftOfferBuffer = new BuffId[3];
        private readonly List<BuffId> _buffDraftPoolScratch = new();
        private readonly List<Card> _surpriseRuntimePoolScratch = new();

        public bool IsGameOver => _gameOver;
        public bool IsRunVictory => _runVictory;

        /// <summary>出战卡组上限（文档 §3.2 / §2.2.1）。</summary>
        public const int MaxBattleDeckCards = 10;

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
                _events.Unsubscribe<BattleStartedEvent>(OnBattleStartedClearNecklaceGoldFlag);
                _events.Unsubscribe<GameOverEvent>(OnGameOverNavToTitle);
            }

            ServiceLocator.Unregister<GameManager>();
            base.OnDestroy();
        }

        public void InitializeGame()
        {
            _events = EventManager.Instance;
            _events?.Subscribe<BattleEndedEvent>(OnBattleEnded);
            _events?.Subscribe<BattleStartedEvent>(OnBattleStartedClearNecklaceGoldFlag);
            _events?.Subscribe<GameOverEvent>(OnGameOverNavToTitle);
        }

        private void OnBattleStartedClearNecklaceGoldFlag(BattleStartedEvent _)
        {
            _pendingBossVictoryGoldenNecklaceDoubleGold = false;
        }

        /// <summary>每层允许停留天数上限（配置 MaxDaysPerFloor）。</summary>
        public int GetMaxDaysPerFloor()
        {
            return ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
        }

        /// <summary>
        /// 「结束当日」已使 <see cref="PlayerData.FloorDay"/> 自增后的层内超时判定（<see cref="PlayerData.FloorDay"/> 从 1 起为层内「当前天」；超过 <paramref name="maxDaysPerFloor"/> 且未击败 BOSS 则失败）。
        /// </summary>
        internal static bool EvaluateFloorTimeoutAfterAdvanceDay(int floorDayAfterIncrement, int maxDaysPerFloor, bool bossDefeated)
        {
            return floorDayAfterIncrement > maxDaysPerFloor && !bossDefeated;
        }

        /// <summary>根据当前本层天数估算剩余可停留天数（见超时判定 FloorDay &gt; MaxDaysPerFloor）。</summary>
        public int GetEstimatedRemainingDaysOnFloor()
        {
            var max = GetMaxDaysPerFloor();
            return Mathf.Max(0, max - PlayerState.FloorDay + 1);
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
            if (data.Version < 5 && PlayerState != null)
            {
                // v4 及更早：FloorDay 为「本层已结束当日次数」从 0 起；v5 起为层内「当前天」从 1 起。
                PlayerState.FloorDay = Mathf.Max(1, PlayerState.FloorDay + 1);
            }

            if (data.Version < 6 && PlayerState != null && PlayerState.CurrentFloor == 1)
            {
                // v6：第一层整层 NPC 次数 3→6（每日上限 1→2 由运行时规则处理）。
                PlayerState.NpcDialogueCredits += StoryDialogueRules.NpcCreditsFirstFloor - StoryDialogueRules.NpcCreditsPerFloor;
            }

            NormalizeBuffPersistence(PlayerState);
            if (PlayerState != null && PlayerState.FloorDay < 1)
                PlayerState.FloorDay = 1;
                FloorState.FloorIndex = PlayerState.CurrentFloor;
            _pendingBossRewards = null;
            NormalizeNpcPersistence(PlayerState);
            NormalizePlayerDeckPartition();
            NormalizePlayerAudioSettings(PlayerState);
            ApplyAudioFromCurrentPlayerState();
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
                FloorDay = 1,
                Gold = gc?.InitialGold ?? 50,
                HandCards = Array.Empty<Card>(),
                StoredCards = Array.Empty<Card>(),
                DiscardPile = Array.Empty<Card>(),
                OwnedCards = Array.Empty<Card>(),
                SelectedBuff = BuffId.None,
                ActiveBuffs = Array.Empty<BuffId>(),
                BuffPicksCompleted = 0,
                NextBuffOfferFloor = 1,
                XRayCount = gc?.InitialXRayCount ?? 1,
                CurrentWeather = WeatherType.WarmWind,
                UnlockedDialogues = Array.Empty<string>(),
                UnlockedAchievements = Array.Empty<string>(),
                HeroDialogueProgress = new int[StoryDialogueRules.HeroSlotCount],
                NpcDialogueProgress = Array.Empty<NpcDialogueProgress>(),
                LastHeroDialogueDay = 0,
                MetNpcIds = Array.Empty<string>(),
                LastNpcInteractionDay = 0,
                NpcStoryVisitsUsedToday = 0,
                NpcDialogueCredits = StoryDialogueRules.GetNpcCreditsAwardedForFloor(1),
                NpcCreditInstallmentsRemaining = 0
            };

            NormalizePlayerAudioSettings(PlayerState);
            SeedNewRunAudioSettingsFromRuntimeMixer();
            ApplyStarterDeckFromConfig(PlayerState, gc);
            NormalizePlayerDeckPartition();

            FloorState = new FloorState { BossDefeated = false };
            ShopState = new ShopState();
            _pendingBossRewards = null;
            SyncFloorStateFromTower();
            RollDailyWeather();
            _events?.Publish(new GameStartedEvent());
            ApplyAudioFromCurrentPlayerState();
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

            PlayerState.FloorDay = 1;
            FloorState.BossDefeated = false;
            PlayerState.XRayCount++;
            // 进层时清零「本游戏日内已完成原住民剧情次数」统计（与 AdvanceDay 一致）；配额仅由 NpcDialogueCredits 限制。
            PlayerState.NpcStoryVisitsUsedToday = 0;
            if (PlayerState.CurrentFloor >= 2)
            {
                // 本层若仍有未触发的按日分期，进层时一次性折成点数，避免提前过层浪费配额。
                PlayerState.NpcDialogueCredits +=
                    PlayerState.NpcCreditInstallmentsRemaining * StoryDialogueRules.NpcCreditsOnFloorEnterSlice;
                PlayerState.NpcDialogueCredits += StoryDialogueRules.NpcCreditsOnFloorEnterSlice;
                PlayerState.NpcCreditInstallmentsRemaining = StoryDialogueRules.NpcCreditInstallmentCountAfterEnter;
            }

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
            PlayerState.NpcStoryVisitsUsedToday = 0;
            PlayerState.FloorDay++;
            // 须先于 DayChangedEvent：否则 MainHub 等订阅方在 Refresh 时读到的仍是未加本日分期前的 Credits。
            ApplyNpcCreditInstallmentAfterAdvanceDay();

            _events?.Publish(new DayChangedEvent(PlayerState.CurrentDay));

            if (PlayerState.Gold <= 0)
            {
                FailRun("gold_empty");
                return;
            }

            var maxDays = ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
            if (EvaluateFloorTimeoutAfterAdvanceDay(PlayerState.FloorDay, maxDays, FloorState.BossDefeated))
                FailRun("floor_timeout");

            if (_gameOver)
                return;

            RollDailyWeather();
        }

        private void ApplyNpcCreditInstallmentAfterAdvanceDay()
        {
            if (_gameOver || _runVictory || PlayerState == null)
                return;
            if (PlayerState.CurrentFloor < 2 || PlayerState.NpcCreditInstallmentsRemaining <= 0)
                return;

            PlayerState.NpcDialogueCredits += StoryDialogueRules.NpcCreditsOnFloorEnterSlice;
            PlayerState.NpcCreditInstallmentsRemaining--;
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

            var unlimited = HasBuff(BuffId.UnlimitedSupply);
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
            {
                var gold = opt.GoldAmount;
                if (_pendingBossVictoryGoldenNecklaceDoubleGold)
                    gold *= 2;
                AddGold(gold);
            }
            else if (!string.IsNullOrEmpty(opt.CardId))
            {
                var cfgMgr = ConfigManager.Instance;
                if (cfgMgr != null && cfgMgr.TryGetCard(opt.CardId, out var cc))
                {
                    if (HasBuff(BuffId.SurprisePack))
                        AppendRandomOwnedCardsFromSurprisePackRoll();
                    else
                    {
                        AppendOwnedCard(CardFromConfig(cc));
                        _events?.Publish(new CardAcquiredEvent(cc.Id));
                    }
                }
                else
                    Debug.LogWarning($"[GameManager] 驻守奖励卡牌配置缺失: {opt.CardId}");
            }

            _pendingBossVictoryGoldenNecklaceDoubleGold = false;

            EnterNextFloor();
            return true;
        }

        /// <summary>
        /// 常规战斗胜利：玩家从当前 <see cref="BattleManager.PendingCasualVictoryRewardCardIds"/> 中确认的一张卡写入持有卡组（惊喜卡包与驻守奖励卡牌分支一致）。
        /// </summary>
        /// <returns>是否在待选列表内且成功解析配置并发放（含惊喜卡包随机）。</returns>
        public bool TryGrantCasualVictoryRewardCard(string cardId)
        {
            if (_gameOver || _runVictory || PlayerState == null)
                return false;
            if (string.IsNullOrEmpty(cardId))
                return false;

            var bm = BattleManager.Instance;
            var pending = bm?.PendingCasualVictoryRewardCardIds;
            if (pending == null || pending.Count == 0)
                return false;

            var found = false;
            for (var i = 0; i < pending.Count; i++)
            {
                if (string.Equals(pending[i], cardId, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;

            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr == null || !cfgMgr.TryGetCard(cardId, out var cc))
            {
                Debug.LogWarning($"[GameManager] 常规战胜奖励卡牌配置缺失: {cardId}");
                return false;
            }

            if (HasBuff(BuffId.SurprisePack))
                AppendRandomOwnedCardsFromSurprisePackRoll();
            else
            {
                AppendOwnedCard(CardFromConfig(cc));
                _events?.Publish(new CardAcquiredEvent(cc.Id));
            }

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

            var goldMult = _pendingBossVictoryGoldenNecklaceDoubleGold ? 2 : 1;
            for (var i = 0; i < _pendingBossRewards.Length; i++)
            {
                var opt = _pendingBossRewards[i];
                if (opt == null)
                    continue;
                if (opt.IsGold)
                    AddGold(opt.GoldAmount * goldMult);
            }

            _pendingBossVictoryGoldenNecklaceDoubleGold = false;
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
        /// 按 <see cref="GameConfig.StarterDeckCardIds"/> 写入出战卡组与仓库（前 <see cref="MaxBattleDeckCards"/> 张出战，余下进 <see cref="PlayerData.StoredCards"/>）。
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

            if (list.Count <= 0)
                return;

            var take = Math.Min(MaxBattleDeckCards, list.Count);
            player.HandCards = list.GetRange(0, take).ToArray();
            player.StoredCards = list.Count > take
                ? list.GetRange(take, list.Count - take).ToArray()
                : Array.Empty<Card>();
            player.OwnedCards = Array.Empty<Card>();
        }

        /// <summary>
        /// 读档后统一：迁移旧 <see cref="PlayerData.OwnedCards"/>，并将出战裁至 <see cref="MaxBattleDeckCards"/> 张。
        /// </summary>
        private void NormalizePlayerDeckPartition()
        {
            var p = PlayerState;
            if (p == null)
                return;

            p.HandCards ??= Array.Empty<Card>();
            p.StoredCards ??= Array.Empty<Card>();
            p.OwnedCards ??= Array.Empty<Card>();

            if (p.OwnedCards.Length > 0 && p.HandCards.Length == 0 && p.StoredCards.Length == 0)
            {
                var list = new List<Card>(p.OwnedCards);
                var n = list.Count;
                var take = Math.Min(MaxBattleDeckCards, n);
                p.HandCards = list.GetRange(0, take).ToArray();
                p.StoredCards = n > take
                    ? list.GetRange(take, n - take).ToArray()
                    : Array.Empty<Card>();
                p.OwnedCards = Array.Empty<Card>();
            }
            else if (p.OwnedCards.Length > 0)
            {
                var store = new List<Card>(p.StoredCards);
                store.AddRange(p.OwnedCards);
                p.StoredCards = store.ToArray();
                p.OwnedCards = Array.Empty<Card>();
            }

            if (p.HandCards.Length <= MaxBattleDeckCards)
                return;

            var handList = new List<Card>(p.HandCards);
            var excess = handList.Count - MaxBattleDeckCards;
            var stay = handList.GetRange(0, MaxBattleDeckCards);
            var move = handList.GetRange(MaxBattleDeckCards, excess);
            var newStore = new List<Card>(move);
            newStore.AddRange(p.StoredCards);
            p.HandCards = stay.ToArray();
            p.StoredCards = newStore.ToArray();
        }

        private void AppendOwnedCard(Card card)
        {
            if (card == null)
                return;
            var list = new List<Card>(PlayerState.StoredCards ?? Array.Empty<Card>());
            list.Add(card);
            PlayerState.StoredCards = list.ToArray();
        }

        private bool OwnsCardId(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return false;
            if (ArrayContainsCardId(PlayerState.HandCards, cardId))
                return true;
            if (ArrayContainsCardId(PlayerState.StoredCards, cardId))
                return true;
            return ArrayContainsCardId(PlayerState.OwnedCards, cardId);
        }

        /// <summary>
        /// <paramref name="fromActiveDeck"/> 为 true 时从出战移入仓库；为 false 时从仓库移入出战（出战已达 <see cref="MaxBattleDeckCards"/> 则失败）。
        /// </summary>
        public bool TryMoveCardBetweenDeckAndStorage(bool fromActiveDeck, int index)
        {
            if (_gameOver || _runVictory || PlayerState == null)
                return false;

            PlayerState.HandCards ??= Array.Empty<Card>();
            PlayerState.StoredCards ??= Array.Empty<Card>();

            var hand = new List<Card>(PlayerState.HandCards);
            var storage = new List<Card>(PlayerState.StoredCards);

            if (fromActiveDeck)
            {
                if (index < 0 || index >= hand.Count)
                    return false;

                var card = hand[index];
                if (CardBattleRules.IsKing(card) || CardBattleRules.IsCommoner(card))
                    return false;

                hand.RemoveAt(index);
                storage.Add(card);
            }
            else
            {
                if (index < 0 || index >= storage.Count)
                    return false;
                if (hand.Count >= MaxBattleDeckCards)
                    return false;

                var card = storage[index];
                storage.RemoveAt(index);
                hand.Add(card);
            }

            PlayerState.HandCards = hand.ToArray();
            PlayerState.StoredCards = storage.ToArray();
            PersistCurrentRunToDisk();
            return true;
        }

        private static bool ArrayContainsCardId(Card[] cards, string cardId)
        {
            if (cards == null)
                return false;
            for (var i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c != null && c.Id == cardId)
                    return true;
            }

            return false;
        }

        private void RollShopStockFromConfig(ShopConfig shopCfg)
        {
            _shopPickedProductIds.Clear();
            _ownedCardIdsScratch.Clear();
            CollectOwnedCardIdsForShop(PlayerState.HandCards);
            CollectOwnedCardIdsForShop(PlayerState.StoredCards);
            CollectOwnedCardIdsForShop(PlayerState.OwnedCards);

            _shopSlotsBuilder.Clear();
            AddShopSlotsForType(CardType.Ability, shopCfg.AbilityCardSlots, shopCfg.AbilityCardPrice);
            AddShopSlotsForType(CardType.Function, shopCfg.FunctionCardSlots, shopCfg.FunctionCardPrice);
            AddShopSlotsForType(CardType.Basic, shopCfg.BasicCardSlots, shopCfg.BasicCardPrice);
            AddShopSlotsForType(CardType.Consumable, shopCfg.ConsumableCardSlots, shopCfg.ConsumableCardPrice);

            ShopState.FloorIndex = PlayerState.CurrentFloor;
            ShopState.DayIndex = PlayerState.CurrentDay;
            ShopState.Slots = _shopSlotsBuilder.ToArray();
        }

        private void CollectOwnedCardIdsForShop(Card[] cards)
        {
            if (cards == null)
                return;
            for (var i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c != null && !string.IsNullOrEmpty(c.Id))
                    _ownedCardIdsScratch.Add(c.Id);
            }
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
                _pendingBossVictoryGoldenNecklaceDoubleGold = false;
                FailRun("boss_defeat");
                return;
            }

            _pendingBossVictoryGoldenNecklaceDoubleGold =
                e.PlayerVictory && e.GoldenNecklacePlayedThisBattle && e.IsBossBattle;

            if (!e.IsBossBattle || !e.PlayerVictory)
                return;

            FloorState.BossDefeated = true;

            var cfgMgr = ConfigManager.Instance;
            var maxDays = ResolveGameConfig()?.MaxDaysPerFloor ?? 3;
            var spareDays = Mathf.Max(0, maxDays - PlayerState.FloorDay + 1);
            TowerFloorEntry entry = null;
            if (cfgMgr != null)
                cfgMgr.TryGetTowerFloor(PlayerState.CurrentFloor, out entry);
            var options = BossRewardPicker.Generate(spareDays, entry, cfgMgr, e.BossVictoryRewardCardIds);
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

        /// <summary>是否仍可访问原住民（主界面按钮与进入 NPCView 的门禁）；仅受剩余配额与可播对话是否存在影响，不限每日次数。</summary>
        public bool HasNpcVisitRemainingToday()
        {
            return HasNpcStoryVisitAvailableToday();
        }

        public bool HasHeroDialogueRemainingToday()
        {
            return PlayerState != null && PlayerState.LastHeroDialogueDay != PlayerState.CurrentDay;
        }

        public bool TryPrepareHeroDialogue(int heroSlotIndex, out string startId)
        {
            startId = null;
            if (_gameOver || _runVictory || PlayerState == null)
                return false;
            if (heroSlotIndex < 0 || heroSlotIndex >= StoryDialogueRules.HeroSlotCount)
                return false;
            if (!HasHeroDialogueRemainingToday())
                return false;

            NormalizeStoryPersistence(PlayerState);
            var completed = PlayerState.HeroDialogueProgress[heroSlotIndex];
            if (!StoryDialogueRules.TryGetNextHeroStoryIndex(completed, PlayerState.CurrentFloor, out var nextStoryIndex))
                return false;

            var id = StoryDialogueRules.BuildHeroStoryStartId(heroSlotIndex, nextStoryIndex);
            if (!HasDialogueStartLine(id))
                return false;

            startId = id;
            return true;
        }

        public void CompleteHeroDialogue(int heroSlotIndex)
        {
            if (PlayerState == null || heroSlotIndex < 0 || heroSlotIndex >= StoryDialogueRules.HeroSlotCount)
                return;

            NormalizeStoryPersistence(PlayerState);
            if (!StoryDialogueRules.TryGetNextHeroStoryIndex(
                    PlayerState.HeroDialogueProgress[heroSlotIndex],
                    PlayerState.CurrentFloor,
                    out _))
                return;

            PlayerState.HeroDialogueProgress[heroSlotIndex]++;
            PlayerState.LastHeroDialogueDay = PlayerState.CurrentDay;
        }

        public bool HasNpcStoryVisitAvailableToday()
        {
            if (PlayerState == null || PlayerState.NpcDialogueCredits <= 0)
                return false;

            RefreshNewNpcEncounterScratch();
            for (var i = 0; i < _npcNewEncounterScratch.Count; i++)
            {
                if (TryPrepareNpcDialogue(_npcNewEncounterScratch[i], out _))
                    return true;
            }

            var met = PlayerState.MetNpcIds ?? Array.Empty<string>();
            for (var i = 0; i < met.Length; i++)
            {
                if (TryPrepareNpcDialogue(met[i], out _))
                    return true;
            }

            return false;
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

        public bool TryPrepareRandomNewNpcDialogue(out string pickedNpcId, out string startId)
        {
            pickedNpcId = null;
            startId = null;
            RefreshNewNpcEncounterScratch();
            if (_npcNewEncounterScratch.Count == 0)
                return false;

            var candidates = new List<string>(_npcNewEncounterScratch);
            while (candidates.Count > 0)
            {
                var index = Random.Range(0, candidates.Count);
                var id = candidates[index];
                candidates.RemoveAt(index);
                if (!TryPrepareNpcDialogue(id, out var preparedStartId))
                    continue;

                pickedNpcId = id;
                startId = preparedStartId;
                return true;
            }

            return false;
        }

        public bool TryPrepareNpcDialogue(string npcId, out string startId)
        {
            startId = null;
            if (_gameOver || _runVictory || PlayerState == null || string.IsNullOrEmpty(npcId))
                return false;
            if (PlayerState.NpcDialogueCredits <= 0)
                return false;

            NormalizeStoryPersistence(PlayerState);
            var completed = GetNpcCompletedCount(npcId);
            if (!StoryDialogueRules.TryGetNextNpcStoryIndex(completed, out var nextStoryIndex))
                return false;

            var id = StoryDialogueRules.BuildNpcStoryStartId(npcId, nextStoryIndex);
            if (!HasDialogueStartLine(id))
                return false;

            startId = id;
            return true;
        }

        public void CompleteNpcDialogue(string npcId)
        {
            if (PlayerState == null || string.IsNullOrEmpty(npcId))
                return;

            NormalizeStoryPersistence(PlayerState);
            if (PlayerState.NpcDialogueCredits <= 0)
                return;

            var progress = GetOrCreateNpcDialogueProgress(npcId);
            if (!StoryDialogueRules.TryGetNextNpcStoryIndex(progress.CompletedCount, out _))
                return;

            progress.CompletedCount++;
            PlayerState.NpcDialogueCredits--;
            PlayerState.NpcStoryVisitsUsedToday++;
            PlayerState.LastNpcInteractionDay = PlayerState.CurrentDay;
            AppendMetNpcIfMissing(npcId, PlayerState);
            _events?.Publish(new NpcEncounterStartedEvent(npcId));
        }

        /// <summary>旧入口兼容包装：立即完成一次 NPC 剧情推进并发布事件。</summary>
        public bool TryInteractWithNpc(string npcId)
        {
            if (!TryPrepareNpcDialogue(npcId, out _))
                return false;

            CompleteNpcDialogue(npcId);
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
            if (player.NpcStoryVisitsUsedToday < 0)
                player.NpcStoryVisitsUsedToday = 0;

            if (player.CurrentFloor < 2)
                player.NpcCreditInstallmentsRemaining = 0;
            else if (player.NpcCreditInstallmentsRemaining < 0)
                player.NpcCreditInstallmentsRemaining = 0;
            else if (player.NpcCreditInstallmentsRemaining > StoryDialogueRules.NpcCreditInstallmentCountAfterEnter)
                player.NpcCreditInstallmentsRemaining = StoryDialogueRules.NpcCreditInstallmentCountAfterEnter;

            NormalizeStoryPersistence(player);
        }

        private static void NormalizeStoryPersistence(PlayerData player)
        {
            if (player == null)
                return;

            if (player.HeroDialogueProgress == null ||
                player.HeroDialogueProgress.Length != StoryDialogueRules.HeroSlotCount)
            {
                var normalized = new int[StoryDialogueRules.HeroSlotCount];
                var existing = player.HeroDialogueProgress ?? Array.Empty<int>();
                var copyCount = Math.Min(existing.Length, normalized.Length);
                for (var i = 0; i < copyCount; i++)
                    normalized[i] = Math.Max(0, existing[i]);
                player.HeroDialogueProgress = normalized;
            }

            if (player.NpcDialogueProgress == null)
                player.NpcDialogueProgress = Array.Empty<NpcDialogueProgress>();

            for (var i = 0; i < player.NpcDialogueProgress.Length; i++)
            {
                var progress = player.NpcDialogueProgress[i];
                if (progress != null && progress.CompletedCount < 0)
                    progress.CompletedCount = 0;
            }
        }

        private bool HasDialogueStartLine(string startId)
        {
            var cfg = ConfigManager.Instance;
            return cfg != null && cfg.TryGetDialogueLine(startId, out _);
        }

        private int GetNpcCompletedCount(string npcId)
        {
            var progress = PlayerState.NpcDialogueProgress ?? Array.Empty<NpcDialogueProgress>();
            for (var i = 0; i < progress.Length; i++)
            {
                var entry = progress[i];
                if (entry != null && entry.NpcId == npcId)
                    return Math.Max(0, entry.CompletedCount);
            }

            return 0;
        }

        private NpcDialogueProgress GetOrCreateNpcDialogueProgress(string npcId)
        {
            var progress = PlayerState.NpcDialogueProgress ?? Array.Empty<NpcDialogueProgress>();
            for (var i = 0; i < progress.Length; i++)
            {
                var entry = progress[i];
                if (entry != null && entry.NpcId == npcId)
                    return entry;
            }

            var created = new NpcDialogueProgress { NpcId = npcId, CompletedCount = 0 };
            var list = new List<NpcDialogueProgress>(progress) { created };
            PlayerState.NpcDialogueProgress = list.ToArray();
            return created;
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

        /// <summary>旧档补齐 <see cref="PlayerData.AudioSettings"/> 并夹紧音量到 [0,1]。</summary>
        public static void NormalizePlayerAudioSettings(PlayerData player)
        {
            if (player == null)
                return;
            if (player.AudioSettings == null)
                player.AudioSettings = new GameAudioSettings();

            var a = player.AudioSettings;
            var bgm = float.IsNaN(a.BgmVolume) ? 1f : a.BgmVolume;
            var sfx = float.IsNaN(a.SfxVolume) ? 1f : a.SfxVolume;
            a.BgmVolume = Mathf.Clamp01(bgm);
            a.SfxVolume = Mathf.Clamp01(sfx);
        }

        /// <summary>将 <see cref="PlayerState"/> 中的音量应用到 <see cref="AudioManager"/>。</summary>
        public void ApplyAudioFromCurrentPlayerState()
        {
            if (PlayerState?.AudioSettings == null)
                return;

            var am = AudioManager.Instance;
            if (am == null)
                return;

            am.ApplyBgmSfxVolumes(PlayerState.AudioSettings.BgmVolume, PlayerState.AudioSettings.SfxVolume);
        }

        private void SeedNewRunAudioSettingsFromRuntimeMixer()
        {
            var am = AudioManager.Instance;
            if (am == null || PlayerState?.AudioSettings == null)
                return;

            am.GetCurrentMixLevels(out var bgm, out var sfx);
            PlayerState.AudioSettings.BgmVolume = Mathf.Clamp01(bgm);
            PlayerState.AudioSettings.SfxVolume = Mathf.Clamp01(sfx);
        }

        /// <summary>设置界面读取滑条初值：主菜单且已有存档但未 Restore 时读盘上 Player，否则读 <see cref="PlayerState"/>。</summary>
        public (float bgm, float sfx) GetEffectiveAudioVolumesForSettingsUi()
        {
            var ui = UIManager.Instance;
            var pm = PersistenceManager.Instance;
            if (ui != null && ui.IsPanelOpen(UIPanelId.MainMenu) && pm != null && pm.HasSave())
            {
                var save = pm.Load();
                if (save?.Player != null)
                {
                    NormalizePlayerAudioSettings(save.Player);
                    return (save.Player.AudioSettings.BgmVolume, save.Player.AudioSettings.SfxVolume);
                }
            }

            NormalizePlayerAudioSettings(PlayerState);
            return (PlayerState.AudioSettings.BgmVolume, PlayerState.AudioSettings.SfxVolume);
        }

        /// <summary>设置界面拖动音量：主菜单且盘上已有存档时只合并写盘 Player 音频段；否则写内存并视情况 <see cref="PersistCurrentRunToDisk"/>。</summary>
        public void ApplyAudioVolumesFromUiAndPersist(float bgmVolume, float sfxVolume)
        {
            bgmVolume = Mathf.Clamp01(bgmVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            AudioManager.Instance?.ApplyBgmSfxVolumes(bgmVolume, sfxVolume);

            var ui = UIManager.Instance;
            var pm = PersistenceManager.Instance;
            if (ui != null && ui.IsPanelOpen(UIPanelId.MainMenu) && pm != null && pm.HasSave())
            {
                var save = pm.Load();
                if (save?.Player != null)
                {
                    NormalizePlayerAudioSettings(save.Player);
                    save.Player.AudioSettings.BgmVolume = bgmVolume;
                    save.Player.AudioSettings.SfxVolume = sfxVolume;
                    pm.Save(save);
                    return;
                }
            }

            NormalizePlayerAudioSettings(PlayerState);
            PlayerState.AudioSettings.BgmVolume = bgmVolume;
            PlayerState.AudioSettings.SfxVolume = sfxVolume;
            if (pm != null && pm.HasSave() && (ui == null || !ui.IsPanelOpen(UIPanelId.MainMenu)))
                PersistCurrentRunToDisk();
        }

        /// <summary>设置面板「保存进度」：主菜单上不可用内存 Run 覆盖整槽存档（避免误写）。</summary>
        public bool TryPersistCurrentRunFromSettingsSaveButton()
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.IsPanelOpen(UIPanelId.MainMenu))
                return false;

            PersistCurrentRunToDisk();
            return true;
        }

        /// <summary>设置中「返回主菜单」：仅主菜单+设置时只关设置；否则先存档再关全部面板并打开主菜单。</summary>
        public IEnumerator ReturnToTitleFromSettingsRoutine()
        {
            yield return null;

            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            if (ui.IsPanelOpen(UIPanelId.Settings))
                ui.Close(UIPanelId.Settings);

            var onlyMainMenu = ui.IsPanelOpen(UIPanelId.MainMenu) &&
                               !ui.IsPanelOpen(UIPanelId.MainHub) &&
                               !ui.IsPanelOpen(UIPanelId.Battle);

            if (onlyMainMenu)
                yield break;

            PersistCurrentRunToDisk();
            ui.CloseAll();
            yield return ui.OpenAsync(UIPanelId.MainMenu);
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
                Version = Mathf.Max(SaveData.CurrentSchemaVersion, existing?.Version ?? 0),
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

        public bool HasBuff(BuffId id)
        {
            if (id == BuffId.None || PlayerState?.ActiveBuffs == null)
                return false;

            var arr = PlayerState.ActiveBuffs;
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i] == id)
                    return true;
            }

            return false;
        }

        public bool ShouldOfferBuffDraft()
        {
            if (PlayerState == null)
                return false;
            if (PlayerState.BuffPicksCompleted >= 4)
                return false;
            var next = Mathf.Max(1, PlayerState.NextBuffOfferFloor);
            return PlayerState.CurrentFloor == next;
        }

        /// <summary>构建当前一次 3 选 1 的候选（不放回池）；需在打开 Buff 界面前调用。</summary>
        public void TryBuildBuffDraftOffer()
        {
            for (var i = 0; i < _buffDraftOfferBuffer.Length; i++)
                _buffDraftOfferBuffer[i] = BuffId.None;

            if (!ShouldOfferBuffDraft())
                return;

            _buffDraftPoolScratch.Clear();
            for (var i = 0; i < DraftableBuffIds.Length; i++)
            {
                var id = DraftableBuffIds[i];
                if (!HasBuff(id))
                    _buffDraftPoolScratch.Add(id);
            }

            var n = _buffDraftPoolScratch.Count;
            for (var i = 0; i < _buffDraftOfferBuffer.Length; i++)
            {
                if (n <= 0)
                    break;
                var pick = Random.Range(0, n);
                var chosen = _buffDraftPoolScratch[pick];
                _buffDraftPoolScratch[pick] = _buffDraftPoolScratch[n - 1];
                n--;
                _buffDraftOfferBuffer[i] = chosen;
            }
        }

        public bool TryGetBuffDraftOfferCopy(BuffId[] dest)
        {
            if (dest == null || dest.Length < _buffDraftOfferBuffer.Length)
                return false;

            for (var i = 0; i < _buffDraftOfferBuffer.Length; i++)
                dest[i] = _buffDraftOfferBuffer[i];

            return true;
        }

        public void ApplyBuffChoice(BuffId pick)
        {
            if (PlayerState == null || pick == BuffId.None)
                return;

            var valid = false;
            for (var i = 0; i < _buffDraftOfferBuffer.Length; i++)
            {
                if (_buffDraftOfferBuffer[i] == pick)
                {
                    valid = true;
                    break;
                }
            }

            if (!valid)
                return;

            var list = new List<BuffId>(PlayerState.ActiveBuffs ?? Array.Empty<BuffId>()) { pick };
            PlayerState.ActiveBuffs = list.ToArray();
            PlayerState.BuffPicksCompleted = Mathf.Min(4, PlayerState.BuffPicksCompleted + 1);
            PlayerState.NextBuffOfferFloor = Mathf.Min(9, PlayerState.CurrentFloor + 2);

            for (var i = 0; i < _buffDraftOfferBuffer.Length; i++)
                _buffDraftOfferBuffer[i] = BuffId.None;

            if (pick == BuffId.RichSecondGen)
                PlayerState.Gold += Mathf.RoundToInt(PlayerState.Gold * 0.5f);

            _events?.Publish(new BuffAcquiredEvent(pick));
            _events?.Publish(new GoldChangedEvent(PlayerState.Gold));
            PersistCurrentRunToDisk();
        }

        /// <summary>混乱战场：开战前从全卡池选 3 张；结果写入 <see cref="BattleManager.SetPendingChaoticBattleExtras"/>。</summary>
        public IEnumerator RunChaoticBattlefieldPreBattlePickRoutine()
        {
            var ui = UIManager.Instance;
            var cfg = ConfigManager.Instance;
            var bm = BattleManager.Instance;
            if (ui == null || cfg == null || bm == null)
                yield break;

            bm.SetPendingChaoticBattleExtras(Array.Empty<Card>());

            _surpriseRuntimePoolScratch.Clear();
            cfg.AppendAllCardsAsRuntime(_surpriseRuntimePoolScratch);
            if (_surpriseRuntimePoolScratch.Count == 0)
                yield break;

            var done = false;
            List<Card> picked = null;

            yield return ui.OpenAsync(UIPanelId.CardList);
            if (!ui.TryGetView(UIPanelId.CardList, out CardListView listView))
                yield break;

            listView.Apply(new CardListViewModel(
                "混乱战场：选 3 张",
                _surpriseRuntimePoolScratch,
                false,
                3,
                list =>
                {
                    picked = new List<Card>(list);
                    done = true;
                },
                () =>
                {
                    done = true;
                    picked = null;
                },
                "选择 3 张牌加入你本局的手牌"));

            while (!done)
                yield return null;

            if (picked != null && picked.Count == 3)
                bm.SetPendingChaoticBattleExtras(picked);
        }

        private void AppendRandomOwnedCardsFromSurprisePackRoll()
        {
            var k = Random.Range(0, 4);
            for (var i = 0; i < k; i++)
                TryAppendOneRandomOwnedCardFromFullConfigPoolSurprise();
        }

        private void TryAppendOneRandomOwnedCardFromFullConfigPoolSurprise()
        {
            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr == null)
                return;

            _surpriseRuntimePoolScratch.Clear();
            cfgMgr.AppendAllCardsAsRuntime(_surpriseRuntimePoolScratch);
            for (var j = _surpriseRuntimePoolScratch.Count - 1; j >= 0; j--)
            {
                var c = _surpriseRuntimePoolScratch[j];
                if (c != null && c.IsUnique && OwnsCardId(c.Id))
                    _surpriseRuntimePoolScratch.RemoveAt(j);
            }

            if (_surpriseRuntimePoolScratch.Count == 0)
                return;

            var pick = _surpriseRuntimePoolScratch[Random.Range(0, _surpriseRuntimePoolScratch.Count)];
            if (cfgMgr.TryGetCard(pick.Id, out var cc))
            {
                AppendOwnedCard(CardFromConfig(cc));
                _events?.Publish(new CardAcquiredEvent(cc.Id));
            }
        }

        private static void NormalizeBuffPersistence(PlayerData player)
        {
            if (player == null)
                return;

            if (player.ActiveBuffs == null)
                player.ActiveBuffs = Array.Empty<BuffId>();

            if (player.SelectedBuff != BuffId.None)
            {
                var merged = new List<BuffId>(player.ActiveBuffs);
                if (!merged.Contains(player.SelectedBuff))
                    merged.Add(player.SelectedBuff);
                player.ActiveBuffs = merged.ToArray();
                player.SelectedBuff = BuffId.None;
            }

            player.BuffPicksCompleted = Mathf.Clamp(Mathf.Max(player.BuffPicksCompleted, player.ActiveBuffs.Length), 0, 4);
            player.NextBuffOfferFloor = Mathf.Max(player.NextBuffOfferFloor, player.BuffPicksCompleted * 2 + 1);
            if (player.NextBuffOfferFloor <= 0)
                player.NextBuffOfferFloor = Mathf.Max(1, player.BuffPicksCompleted * 2 + 1);
            if (player.NpcStoryVisitsUsedToday < 0)
                player.NpcStoryVisitsUsedToday = 0;
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
