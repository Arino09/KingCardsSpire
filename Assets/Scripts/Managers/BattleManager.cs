using System;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KingCardsSpire.Managers
{
    public sealed class BattleManager : PersistentMonoSingleton<BattleManager>
    {
        private enum Phase
        {
            Idle,
            InBattle
        }

        private Phase _phase = Phase.Idle;

        private readonly List<Card> _playerHand = new();
        private readonly List<Card> _enemyHand = new();
        private readonly List<Card> _playerDiscard = new();
        private readonly List<Card> _enemyDiscard = new();
        private readonly List<string> _historyLines = new();

        private WeatherType _weather;
        private bool _noRoundLimit;
        private int _maxRounds;
        private int _roundsCompleted;
        private string _lastPlayerInstanceId;
        private string _lastEnemyInstanceId;
        private bool _isBossBattle;
        private int _bossAiStrength;

        private bool _isTutorialBattle;

        private string _opponentDisplayName = string.Empty;

        /// <summary>本回合已锁定的敌方手牌下标；-1 表示未准备。</summary>
        private int _pendingEnemyHandIndex = -1;

        /// <summary>玩家已暂存的手牌下标；-1 表示未选择。</summary>
        private int _pendingPlayerHandIndex = -1;

        private readonly BattleEffectRuntimeState _battleEffects = new();

        /// <summary>「最终时刻」将在下一回合开始时生效。</summary>
        private bool _restrictedNextRound;

        /// <summary>本回合指数形态要求的额外弃牌是否已由玩家完成（或无需弃牌）。</summary>
        private bool _playerExponentialSacrificeResolved;

        public BattleState CurrentBattle { get; private set; } = new();

        public bool IsBattleActive => _phase == Phase.InBattle;

        /// <summary>当前是否为「开场教学战」（用于 UI 脚本化与第一回合敌出牌锁定）。</summary>
        public bool IsTutorialBattle => _isTutorialBattle;

        /// <summary>本回合已锁定的敌方手牌下标，未准备时为 -1。</summary>
        public int PendingEnemyHandIndex => _pendingEnemyHandIndex;

        /// <summary>玩家已暂存的手牌下标，未选择时为 -1。</summary>
        public int PendingPlayerHandIndex => _pendingPlayerHandIndex;

        /// <summary>
        /// 该实例是否因「不可连续两次打出同一张牌」规则在本回合不可再出（与 <see cref="IsLegalPlay"/> 一致，用于 UI 置灰）。
        /// </summary>
        public bool IsPlayerCardRestrictedByLastPlay(Card card)
        {
            if (_phase != Phase.InBattle || card == null)
                return false;

            return !IsLegalPlay(card, _playerHand, _lastPlayerInstanceId);
        }

        /// <summary>敌方：同上规则，用于已透视可见的敌方手牌置灰。</summary>
        public bool IsEnemyCardRestrictedByLastPlay(Card card)
        {
            if (_phase != Phase.InBattle || card == null)
                return false;

            return !IsLegalPlay(card, _enemyHand, _lastEnemyInstanceId);
        }

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<BattleManager>();
            base.OnDestroy();
        }

        public void InitializeBattle() { }

        /// <summary>使用当前玩家状态开局；手牌为空时使用内置测试卡组。</summary>
        /// <param name="vsBoss">true 时使用当前塔层配置的敌方卡组。</param>
        public void StartBattleFromPlayerState(bool vsBoss = false)
        {
            var game = GameManager.Instance;
            var player = game != null ? game.PlayerState : null;

            var playerCards = BuildPlayerDeck(player);
            var enemyCards = vsBoss ? BuildBossDeckFromTowerOrFallback() : BuildDefaultEnemyDeck();
            var bossAiStrength = vsBoss ? ResolveBossAiStrengthFromTower() : 0;

            StartBattleInternal(playerCards, enemyCards, player?.CurrentWeather ?? WeatherType.WarmWind,
                player?.XRayCount ?? 1, vsBoss, bossAiStrength, null, false);
        }

        /// <summary>主角房友谊战：敌方卡组由 <see cref="HeroOpponentDeckGenerator"/> 占位生成，非 BOSS。</summary>
        public void StartHeroDuelFromPlayerState(string heroSlotId, string opponentDisplayName)
        {
            var game = GameManager.Instance;
            var player = game != null ? game.PlayerState : null;

            var playerCards = BuildPlayerDeck(player);
            var enemyCards = HeroOpponentDeckGenerator.BuildPlaceholderDeck(heroSlotId);

            StartBattleInternal(playerCards, enemyCards, player?.CurrentWeather ?? WeatherType.WarmWind,
                player?.XRayCount ?? 1, isBossBattle: false, bossAiStrength: 0,
                opponentDisplayNameOverride: opponentDisplayName, tutorialBattle: false);
        }

        /// <summary>开场教学战：双方各 3 张国王/大臣/平民，对手显示名为「花」，暖风、固定透视平民、第一回合敌必出大臣。</summary>
        public void StartTutorialBattle()
        {
            var playerDeck = new List<Card>
            {
                NewRuntimeCard(WellKnownCardIds.King, "国王", 3f),
                NewRuntimeCard(WellKnownCardIds.Minister, "大臣", 2f),
                NewRuntimeCard(WellKnownCardIds.Commoner, "平民", 1f)
            };
            var enemyDeck = new List<Card>
            {
                NewRuntimeCard(WellKnownCardIds.King, "国王", 3f),
                NewRuntimeCard(WellKnownCardIds.Minister, "大臣", 2f),
                NewRuntimeCard(WellKnownCardIds.Commoner, "平民", 1f)
            };

            StartBattleInternal(playerDeck, enemyDeck, WeatherType.WarmWind, 1, false, 0, "花", true);
        }

        public void StartBattle(IReadOnlyList<Card> playerDeck, IReadOnlyList<Card> enemyDeck,
            WeatherType weather, int xRayCount, bool isBossBattle = false,
            string opponentDisplayNameOverride = null)
        {
            var bossAiStrength = isBossBattle ? ResolveBossAiStrengthFromTower() : 0;
            StartBattleInternal(playerDeck, enemyDeck, weather, xRayCount, isBossBattle,
                bossAiStrength, opponentDisplayNameOverride, false);
        }

        private void StartBattleInternal(IReadOnlyList<Card> playerDeck, IReadOnlyList<Card> enemyDeck,
            WeatherType weather, int xRayCount, bool isBossBattle, int bossAiStrength,
            string opponentDisplayNameOverride = null, bool tutorialBattle = false)
        {
            ResetRuntime();
            _isTutorialBattle = tutorialBattle;
            _isBossBattle = isBossBattle;
            _bossAiStrength = Mathf.Max(0, bossAiStrength);
            _opponentDisplayName = !string.IsNullOrEmpty(opponentDisplayNameOverride)
                ? opponentDisplayNameOverride
                : ResolveOpponentDisplayName(isBossBattle);

            foreach (var c in playerDeck)
                _playerHand.Add(CloneForBattle(c));
            foreach (var c in enemyDeck)
                _enemyHand.Add(CloneForBattle(c));

            ActivateOpeningEnemyAbilityCards();

            _weather = weather;
            _noRoundLimit = weather == WeatherType.WarmWind;
            _maxRounds = CardBattleRules.ComputeMaxRounds(_playerHand.Count, _enemyHand.Count);

            ApplyXRay(Mathf.Min(Mathf.Max(0, xRayCount), _enemyHand.Count));

            _phase = Phase.InBattle;
            SyncBattleState();
            EventManager.Instance?.Publish(new BattleStartedEvent());
            EventManager.Instance?.Publish(new BattleStateChangedEvent());
            Debug.Log(
                $"[BattleManager] 战斗开始 天气={_weather} 上限回合={(_noRoundLimit ? "无(暖风)" : _maxRounds.ToString())}");
        }

        /// <summary>
        /// 玩家一键出牌（无动画编排时使用）：准备敌方 → 暂存己方 → 立即结算。
        /// </summary>
        public bool TrySubmitPlayerCard(int playerHandIndex, out string error)
        {
            if (!PrepareEnemyPlay(out error))
                return false;
            if (!TryStagePlayerCard(playerHandIndex, out error))
                return false;

            if (RequiresPlayerExponentialSacrifice())
            {
                if (!TryAutoCompletePlayerExponentialSacrifice(out error))
                    return false;
            }

            return CommitPendingRound(out _, out error);
        }

        /// <summary>
        /// 为本回合随机锁定敌方将要出的牌（仍在手牌中）；已锁定且己方未选时幂等成功。
        /// </summary>
        public bool PrepareEnemyPlay(out string error)
        {
            error = null;
            if (_phase != Phase.InBattle)
            {
                error = "当前不在战斗中";
                return false;
            }

            if (_pendingEnemyHandIndex >= 0 && _pendingPlayerHandIndex >= 0)
                return true;

            if (_pendingEnemyHandIndex >= 0 && _pendingPlayerHandIndex < 0)
                return true;

            if (!TryPickEnemyHandIndex(out var enemyIdx))
            {
                error = "敌方无牌可出";
                return false;
            }

            _pendingEnemyHandIndex = enemyIdx;
            _pendingPlayerHandIndex = -1;
            return true;
        }

        /// <summary>暂存玩家本回合要出的手牌下标（不结算）。需先 <see cref="PrepareEnemyPlay"/>。</summary>
        public bool TryStagePlayerCard(int playerHandIndex, out string error)
        {
            error = null;
            if (_phase != Phase.InBattle)
            {
                error = "当前不在战斗中";
                return false;
            }

            if (_pendingEnemyHandIndex < 0)
            {
                error = "尚未准备敌方出牌";
                return false;
            }

            if (_pendingPlayerHandIndex >= 0)
            {
                error = "已选择己方出牌";
                return false;
            }

            if (playerHandIndex < 0 || playerHandIndex >= _playerHand.Count)
            {
                error = "手牌下标无效";
                return false;
            }

            var playerCard = _playerHand[playerHandIndex];
            if (!IsLegalPlay(playerCard, _playerHand, _lastPlayerInstanceId))
            {
                error = "不能连续两次出同一张牌";
                return false;
            }

            if (_battleEffects.FinalMomentRestrictionActive &&
                !IsAllowedUnderFinalMoment(playerCard))
            {
                error = "最终时刻：仅能出国王、平民或大臣";
                return false;
            }

            _pendingPlayerHandIndex = playerHandIndex;
            _playerExponentialSacrificeResolved = !RequiresPlayerExponentialSacrifice();
            if (TryRewriteEnemyPendingCardAgainstPlayer(playerHandIndex))
            {
                SyncBattleState();
                EventManager.Instance?.Publish(new BattleStateChangedEvent());
            }
            return true;
        }

        /// <summary>
        /// 指数形态激活且本回合尚有多张手牌时，需在 <see cref="CommitPendingRound"/> 前额外弃置1张非出牌张。
        /// </summary>
        public bool RequiresPlayerExponentialSacrifice() =>
            _phase == Phase.InBattle
            && _battleEffects.PlayerExponentialFormActive
            && _pendingPlayerHandIndex >= 0
            && !_playerExponentialSacrificeResolved
            && _playerHand.Count > 1;

        /// <summary>玩家点击手牌完成指数形态的额外弃置。</summary>
        public bool TryCompletePlayerExponentialSacrifice(int discardHandIndex, out string error)
        {
            error = null;
            if (!RequiresPlayerExponentialSacrifice())
            {
                error = "当前不需要弃牌";
                return false;
            }

            if (discardHandIndex == _pendingPlayerHandIndex)
            {
                error = "不能弃置本回合即将打出的牌";
                return false;
            }

            if (discardHandIndex < 0 || discardHandIndex >= _playerHand.Count)
            {
                error = "手牌下标无效";
                return false;
            }

            var pending = _pendingPlayerHandIndex;
            DiscardPlayerCardAt(discardHandIndex);
            if (discardHandIndex < pending)
                _pendingPlayerHandIndex--;

            _playerExponentialSacrificeResolved = true;
            SyncBattleState();
            EventManager.Instance?.Publish(new BattleStateChangedEvent());
            return true;
        }

        private bool TryAutoCompletePlayerExponentialSacrifice(out string error)
        {
            error = null;
            if (!RequiresPlayerExponentialSacrifice())
                return true;

            var pending = _pendingPlayerHandIndex;
            var removeAt = RandomOtherIndex(_playerHand.Count, pending);
            return TryCompletePlayerExponentialSacrifice(removeAt, out error);
        }

        public bool CommitPendingRound(out BattleCompareResult compareResult, out string error)
        {
            compareResult = default;
            error = null;
            if (_phase != Phase.InBattle)
            {
                error = "当前不在战斗中";
                return false;
            }

            if (_pendingEnemyHandIndex < 0 || _pendingPlayerHandIndex < 0)
            {
                error = "出牌未完成";
                return false;
            }

            if (_battleEffects.PlayerExponentialFormActive && _playerHand.Count > 1 &&
                !_playerExponentialSacrificeResolved)
            {
                error = "指数形态：须先弃置1张手牌";
                return false;
            }

            if (_restrictedNextRound)
            {
                _battleEffects.FinalMomentRestrictionActive = true;
                _restrictedNextRound = false;
            }

            var playerIdx = _pendingPlayerHandIndex;
            var enemyIdx = _pendingEnemyHandIndex;

            ApplyPreRoundSpecials(ref playerIdx, ref enemyIdx);

            var playerCard = _playerHand[playerIdx];
            var enemyCard = _enemyHand[enemyIdx];

            if (BattleCardEffectResolver.HandContainsAllFourSymbols(_playerHand))
            {
                compareResult = BattleCompareResult.FirstWins;
                _pendingEnemyHandIndex = -1;
                _pendingPlayerHandIndex = -1;
                FinishBattle(true, BattleEndReason.FourSymbolsComplete);
                return true;
            }

            compareResult = ComputeRoundCompareResult(playerCard, enemyCard);

            ResolveRound(playerIdx, enemyIdx, playerCard, enemyCard, compareResult);

            _battleEffects.ClearRoundConsumableFlags();

            if (_battleEffects.FinalMomentRestrictionActive)
                _battleEffects.FinalMomentRestrictionActive = false;

            _pendingEnemyHandIndex = -1;
            _pendingPlayerHandIndex = -1;

            return true;
        }

        /// <summary>
        /// 队列消耗牌效果（预见/加减等级/续命等），在下次 <see cref="CommitPendingRound"/> 结算前生效。
        /// </summary>
        public void QueueConsumableRoundModifiers(float playerLevelBonus, float enemyLevelBonus,
            bool disableEnemyFunction, bool disableEnemyAbility, bool surviveLossToHand)
        {
            _battleEffects.ConsumablePlayerLevelBonus = playerLevelBonus;
            _battleEffects.ConsumableEnemyLevelBonus = enemyLevelBonus;
            _battleEffects.DisableEnemyFunctionEffects = disableEnemyFunction;
            _battleEffects.DisableEnemyAbilityEffects = disableEnemyAbility;
            _battleEffects.PlayerSurviveLossToHand = surviveLossToHand;
        }

        public void EndBattle()
        {
            _phase = Phase.Idle;
            ResetRuntime();
            SyncBattleState();
            EventManager.Instance?.Publish(new BattleStateChangedEvent());
        }

        private List<Card> BuildBossDeckFromTowerOrFallback()
        {
            var gm = GameManager.Instance;
            var cfg = ConfigManager.Instance;
            var floor = gm != null ? gm.PlayerState.CurrentFloor : 1;
            if (cfg != null && cfg.TryGetTowerFloor(floor, out var entry))
            {
                var ids = entry.EnemyDeckCardIds;
                if (ids != null && ids.Length > 0)
                {
                    var list = new List<Card>();
                    foreach (var id in ids)
                    {
                        if (string.IsNullOrEmpty(id))
                            continue;
                        if (cfg.TryGetCard(id, out var cc))
                            list.Add(CardFromConfig(cc));
                    }

                    if (list.Count > 0)
                        return list;
                }
            }

            return BuildDefaultEnemyDeck();
        }

        private static Card CardFromConfig(CardConfigEntry cc)
        {
            return new Card
            {
                Id = cc.Id,
                Name = cc.DisplayName,
                Level = cc.Level,
                Type = cc.Type,
                EffectDesc = cc.Description ?? string.Empty,
                IsUnique = cc.IsUnique
            };
        }

        /// <summary>与原先 <see cref="PickEnemyCard"/> 相同的选取规则，返回手牌下标。</summary>
        private bool TryPickEnemyHandIndex(out int index)
        {
            index = -1;
            if (_enemyHand.Count == 0)
                return false;

            if (_isTutorialBattle && _roundsCompleted == 0)
            {
                for (var i = 0; i < _enemyHand.Count; i++)
                {
                    var c = _enemyHand[i];
                    if (!string.Equals(c.Id, WellKnownCardIds.Minister, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!IsLegalPlay(c, _enemyHand, _lastEnemyInstanceId))
                        continue;
                    if (_battleEffects.FinalMomentRestrictionActive && !IsAllowedUnderFinalMoment(c))
                        continue;
                    index = i;
                    return true;
                }
            }

            var candidates = new List<int>();
            for (var i = 0; i < _enemyHand.Count; i++)
            {
                var c = _enemyHand[i];
                if (!IsLegalPlay(c, _enemyHand, _lastEnemyInstanceId))
                    continue;
                if (_battleEffects.FinalMomentRestrictionActive && !IsAllowedUnderFinalMoment(c))
                    continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < _enemyHand.Count; i++)
                {
                    var c = _enemyHand[i];
                    if (IsLegalPlay(c, _enemyHand, _lastEnemyInstanceId))
                        candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                index = Random.Range(0, _enemyHand.Count);
                return true;
            }

            index = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private Card PickEnemyCard()
        {
            if (!TryPickEnemyHandIndex(out var idx))
                return null;
            return _enemyHand[idx];
        }

        private bool TryRewriteEnemyPendingCardAgainstPlayer(int playerHandIndex)
        {
            if (_pendingEnemyHandIndex < 0 || _pendingEnemyHandIndex >= _enemyHand.Count)
                return false;
            if (playerHandIndex < 0 || playerHandIndex >= _playerHand.Count)
                return false;

            var rule = BossAiStrengthRule.ForStrength(_bossAiStrength);
            if (rule.SearchDepth <= 0 || rule.RewriteLeadRounds <= 0)
                return false;
            if (!ShouldEnemyAiRewriteCard(rule))
                return false;
            if (IsEnemyCardVisibleToPlayer(_enemyHand[_pendingEnemyHandIndex]))
                return false;
            if (rule.MistakeRate > 0f && Random.value < rule.MistakeRate)
                return false;

            if (!EnemyAiDecisionService.TryChooseBestEnemyHandIndex(_playerHand[playerHandIndex],
                    _playerHand, _playerDiscard, _enemyHand, _enemyDiscard, rule, _weather,
                    _roundsCompleted, _battleEffects.FinalMomentRestrictionActive, _lastEnemyInstanceId,
                    _battleEffects, out var chosenIndex))
                return false;

            if (chosenIndex < 0 || chosenIndex >= _enemyHand.Count || chosenIndex == _pendingEnemyHandIndex)
                return false;

            _pendingEnemyHandIndex = chosenIndex;
            return true;
        }

        private bool ShouldEnemyAiRewriteCard(BossAiStrengthRule rule)
        {
            var predictedRoundsRemaining = EstimateRoundsUntilBattleEnd();
            return predictedRoundsRemaining > 0 &&
                   predictedRoundsRemaining <= rule.RewriteLeadRounds;
        }

        private int EstimateRoundsUntilBattleEnd()
        {
            var roundsByHandEmpty = Mathf.Min(_playerHand.Count, _enemyHand.Count);
            var roundsByLimit = int.MaxValue;
            if (!_noRoundLimit && _maxRounds > 0)
                roundsByLimit = Mathf.Max(0, _maxRounds - _roundsCompleted);

            return Mathf.Min(roundsByHandEmpty, roundsByLimit);
        }

        private bool IsEnemyCardVisibleToPlayer(Card card)
        {
            if (card == null || CurrentBattle.EnemyVisible == null)
                return false;
            for (var i = 0; i < CurrentBattle.EnemyVisible.Length; i++)
            {
                var visible = CurrentBattle.EnemyVisible[i];
                if (visible == null)
                    continue;
                if (!string.IsNullOrEmpty(card.BattleInstanceId) &&
                    visible.BattleInstanceId == card.BattleInstanceId)
                    return true;
                if (ReferenceEquals(visible, card))
                    return true;
            }

            return false;
        }

        private void ApplyPreRoundSpecials(ref int playerIdx, ref int enemyIdx)
        {
            var playerInst = _playerHand[playerIdx].BattleInstanceId;
            var enemyInst = _enemyHand[enemyIdx].BattleInstanceId;
            var pl = _playerHand[playerIdx];

            if (string.Equals(pl.Id, WellKnownCardIds.AllIn, StringComparison.OrdinalIgnoreCase)
                && _playerHand.Count > 1)
            {
                var removeAt = RandomOtherIndex(_playerHand.Count, playerIdx);
                DiscardPlayerCardAt(removeAt);
                playerIdx = FindHandIndexByInstanceId(_playerHand, playerInst);
            }

            if (string.Equals(pl.Id, WellKnownCardIds.Hope, StringComparison.OrdinalIgnoreCase)
                && _playerDiscard.Count > 0)
            {
                var di = Random.Range(0, _playerDiscard.Count);
                var recalled = _playerDiscard[di];
                _playerDiscard.RemoveAt(di);
                _playerHand.Add(recalled);
                playerIdx = FindHandIndexByInstanceId(_playerHand, playerInst);
            }

            if (string.Equals(pl.Id, WellKnownCardIds.Demon, StringComparison.OrdinalIgnoreCase)
                && _enemyHand.Count > 1)
            {
                var removeAt = RandomOtherIndex(_enemyHand.Count, enemyIdx);
                DiscardEnemyCardAt(removeAt);
                enemyIdx = FindHandIndexByInstanceId(_enemyHand, enemyInst);
                if (enemyIdx < 0)
                    enemyIdx = 0;
            }

            if (_battleEffects.EnemyExponentialFormActive && _enemyHand.Count > 1)
            {
                var removeAt = RandomOtherIndex(_enemyHand.Count, enemyIdx);
                DiscardEnemyCardAt(removeAt);
                enemyIdx = FindHandIndexByInstanceId(_enemyHand, enemyInst);
                if (enemyIdx < 0)
                    enemyIdx = 0;
            }
        }

        private BattleCompareResult ComputeRoundCompareResult(Card stagedPlayer, Card stagedEnemy)
        {
            var round1Based = _roundsCompleted + 1;
            var completedBefore = _roundsCompleted;

            var playerLogical = BattleCardEffectResolver.ResolvePlayerLogicalCardForCompare(stagedPlayer,
                _playerHand, _enemyHand, _battleEffects);
            var enemyLogical =
                BattleCardEffectResolver.ResolveEnemyLogicalCardForCompare(stagedEnemy, _enemyHand,
                    _playerHand, _battleEffects);

            if (BattleCardEffectResolver.EnemyEffectsInactive(enemyLogical, _battleEffects))
                enemyLogical = BattleCardEffectResolver.StripToNeutralZero(enemyLogical);

            var playerAllIn = string.Equals(stagedPlayer.Id, WellKnownCardIds.AllIn,
                StringComparison.OrdinalIgnoreCase);
            var enemyAllIn = string.Equals(stagedEnemy.Id, WellKnownCardIds.AllIn,
                StringComparison.OrdinalIgnoreCase);

            var pCompare = BattleCardEffectResolver.ToCompareCard(playerLogical, true, _playerHand,
                _enemyHand,
                _battleEffects, round1Based, completedBefore, playerAllIn);
            var eCompare = BattleCardEffectResolver.ToCompareCard(enemyLogical, false, _playerHand,
                _enemyHand,
                _battleEffects, round1Based, completedBefore, enemyAllIn);

            if (_battleEffects.PlayerMustWinThisRound)
            {
                _battleEffects.PlayerMustWinThisRound = false;
                return BattleCompareResult.FirstWins;
            }

            if (_battleEffects.PlayerMustLoseThisRound)
            {
                _battleEffects.PlayerMustLoseThisRound = false;
                return BattleCompareResult.SecondWins;
            }

            var normal = CardBattleRules.Compare(pCompare, eCompare, _weather);
            var special =
                BattleCardEffectResolver.ResolveSpecialFunctionPriority(playerLogical, enemyLogical, normal);
            if (special != normal)
                return special;

            if (BattleCardEffectResolver.IsFortuneTeller(playerLogical))
                return BattleCompareResult.SecondWins;
            if (BattleCardEffectResolver.IsFortuneTeller(enemyLogical))
                return BattleCompareResult.FirstWins;

            return normal;
        }

        private void ResolveRound(int playerHandIndex, int enemyIdx, Card playerCard, Card enemyCard,
            BattleCompareResult result)
        {
            var summary =
                $"{playerCard.Name} vs {enemyCard.Name} → {result} (天气 {_weather})";

            switch (result)
            {
                case BattleCompareResult.Draw:
                    DiscardPlayerCardAt(playerHandIndex);
                    DiscardEnemyCardAt(enemyIdx);
                    summary += " [平局入弃牌]";
                    break;
                case BattleCompareResult.FirstWins:
                    DiscardEnemyCardAt(enemyIdx);
                    summary += " [己方胜·敌方牌入弃]";
                    break;
                case BattleCompareResult.SecondWins:
                    if (!_battleEffects.PlayerSurviveLossToHand)
                    {
                        DiscardPlayerCardAt(playerHandIndex);
                        summary += " [敌方胜·己方牌入弃]";
                    }
                    else
                    {
                        summary += " [续命·己方败牌留在手]";
                    }

                    break;
            }

            ApplyFortuneAndFalseGodChains(playerCard, enemyCard, result);
            ApplyCivilizationIfPlayed(playerCard, enemyCard);
            ArmFinalMomentNextRound(playerCard, enemyCard, result);

            UpdateLastPlayedSnapshots(playerCard, enemyCard);

            _lastPlayerInstanceId = playerCard.BattleInstanceId;
            _lastEnemyInstanceId = enemyCard.BattleInstanceId;

            _roundsCompleted++;
            _historyLines.Add($"第{_roundsCompleted}回合: {summary}");
            CurrentBattle.TurnHistory = _historyLines.ToArray();

            EventManager.Instance?.Publish(new BattleRoundResolvedEvent(summary));
            SyncBattleState();
            EventManager.Instance?.Publish(new BattleStateChangedEvent());

            if (TryEndByHandEmpty())
                return;

            TryEndByRoundLimit();
        }

        private static bool IsAllowedUnderFinalMoment(Card c)
        {
            if (c == null)
                return false;
            return string.Equals(c.Id, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(c.Id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(c.Id, WellKnownCardIds.Minister, StringComparison.OrdinalIgnoreCase);
        }

        private static int RandomOtherIndex(int count, int except)
        {
            if (count <= 1)
                return 0;
            var idx = Random.Range(0, count - 1);
            if (idx >= except)
                idx++;
            return idx;
        }

        private static int FindHandIndexByInstanceId(List<Card> hand, string instanceId)
        {
            if (hand == null || string.IsNullOrEmpty(instanceId))
                return -1;
            for (var i = 0; i < hand.Count; i++)
            {
                if (hand[i].BattleInstanceId == instanceId)
                    return i;
            }

            return -1;
        }

        private void DiscardPlayerCardAt(int index)
        {
            if (index < 0 || index >= _playerHand.Count)
                return;
            var c = _playerHand[index];
            MoveCardAtToDiscard(_playerHand, index, _playerDiscard);
            OnCardDiscardedFromSide(c, true);
        }

        private void DiscardEnemyCardAt(int index)
        {
            if (index < 0 || index >= _enemyHand.Count)
                return;
            var c = _enemyHand[index];
            MoveCardAtToDiscard(_enemyHand, index, _enemyDiscard);
            OnCardDiscardedFromSide(c, false);
        }

        private void ActivateOpeningEnemyAbilityCards()
        {
            for (var i = _enemyHand.Count - 1; i >= 0; i--)
            {
                var card = _enemyHand[i];
                if (card == null || card.Type != CardType.Ability)
                    continue;

                MoveCardAtToDiscard(_enemyHand, i, _enemyDiscard);
                OnCardDiscardedFromSide(card, false);
                if (string.Equals(card.Id, WellKnownCardIds.FinalMoment, StringComparison.OrdinalIgnoreCase))
                    _battleEffects.FinalMomentRestrictionActive = true;
            }
        }

        private void OnCardDiscardedFromSide(Card card, bool fromPlayerPerspective)
        {
            if (card == null)
                return;
            var id = card.Id;
            if (string.Equals(id, WellKnownCardIds.WarmDay, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerWarmDayActive = true;
            if (string.Equals(id, WellKnownCardIds.WarmDay, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.EnemyWarmDayActive = true;
            if (string.Equals(id, WellKnownCardIds.Snowflake, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.EnemySnowflakeActive = true;
            if (string.Equals(id, WellKnownCardIds.Snowflake, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.PlayerSnowflakeActive = true;
            if (string.Equals(id, WellKnownCardIds.ForgeBlade, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerForgeBladeActive = true;
            if (string.Equals(id, WellKnownCardIds.StrikeBlade, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerStrikeBladeActive = true;
            if (string.Equals(id, WellKnownCardIds.ForgeBlade, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.EnemyForgeBladeActive = true;
            if (string.Equals(id, WellKnownCardIds.StrikeBlade, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.EnemyStrikeBladeActive = true;
            if (string.Equals(id, WellKnownCardIds.EvenForm, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerEvenFormActive = true;
            if (string.Equals(id, WellKnownCardIds.OddForm, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerOddFormActive = true;
            if (string.Equals(id, WellKnownCardIds.EvenForm, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.EnemyEvenFormActive = true;
            if (string.Equals(id, WellKnownCardIds.OddForm, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.EnemyOddFormActive = true;
            if (string.Equals(id, WellKnownCardIds.GoldenNecklace, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerGoldenNecklacePlayed = true;
            if (string.Equals(id, WellKnownCardIds.ExponentialForm, StringComparison.OrdinalIgnoreCase)
                && fromPlayerPerspective)
                _battleEffects.PlayerExponentialFormActive = true;
            if (string.Equals(id, WellKnownCardIds.ExponentialForm, StringComparison.OrdinalIgnoreCase)
                && !fromPlayerPerspective)
                _battleEffects.EnemyExponentialFormActive = true;
        }

        private void ApplyCivilizationIfPlayed(Card playerCard, Card enemyCard)
        {
            if (playerCard != null &&
                string.Equals(playerCard.Id, WellKnownCardIds.Civilization, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var c in _playerHand)
                    c.Level = 3f;
            }

            if (enemyCard != null &&
                string.Equals(enemyCard.Id, WellKnownCardIds.Civilization, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var c in _enemyHand)
                    c.Level = 3f;
            }
        }

        private void ArmFinalMomentNextRound(Card playerCard, Card enemyCard, BattleCompareResult result)
        {
            if (playerCard != null &&
                string.Equals(playerCard.Id, WellKnownCardIds.FinalMoment, StringComparison.OrdinalIgnoreCase)
                && result != BattleCompareResult.FirstWins)
                _restrictedNextRound = true;

            if (enemyCard != null &&
                string.Equals(enemyCard.Id, WellKnownCardIds.FinalMoment, StringComparison.OrdinalIgnoreCase)
                && result == BattleCompareResult.FirstWins)
                _restrictedNextRound = true;
        }

        private void UpdateLastPlayedSnapshots(Card playerCard, Card enemyCard)
        {
            _battleEffects.LastPlayerPlayedSnapshot = SnapshotCard(playerCard);
            _battleEffects.LastEnemyPlayedSnapshot = SnapshotCard(enemyCard);
        }

        private static Card SnapshotCard(Card c)
        {
            if (c == null)
                return null;
            return new Card
            {
                Id = c.Id,
                Name = c.Name,
                Level = c.Level,
                Type = c.Type,
                EffectDesc = c.EffectDesc,
                IsUnique = c.IsUnique,
                BattleInstanceId = c.BattleInstanceId
            };
        }

        private void ApplyFortuneAndFalseGodChains(Card playerCard, Card enemyCard,
            BattleCompareResult result)
        {
            if (BattleCardEffectResolver.IsFortuneTeller(playerCard) &&
                result == BattleCompareResult.SecondWins)
                _battleEffects.PlayerMustWinThisRound = true;
            if (BattleCardEffectResolver.IsFalseGod(playerCard) &&
                result == BattleCompareResult.FirstWins)
                _battleEffects.PlayerMustLoseThisRound = true;

            if (BattleCardEffectResolver.IsFortuneTeller(enemyCard) &&
                result == BattleCompareResult.FirstWins)
                _battleEffects.PlayerMustLoseThisRound = true;
            if (BattleCardEffectResolver.IsFalseGod(enemyCard) &&
                result == BattleCompareResult.SecondWins)
                _battleEffects.PlayerMustWinThisRound = true;
        }

        private bool TryEndByHandEmpty()
        {
            if (_playerHand.Count == 0 && _enemyHand.Count == 0)
            {
                FinishBattle(false, BattleEndReason.RoundLimitDraw);
                return true;
            }

            if (_playerHand.Count == 0)
            {
                FinishBattle(false, BattleEndReason.PlayerHandEmpty);
                return true;
            }

            if (_enemyHand.Count == 0)
            {
                FinishBattle(true, BattleEndReason.EnemyHandEmpty);
                return true;
            }

            return false;
        }

        private bool TryEndByRoundLimit()
        {
            if (_noRoundLimit)
                return false;
            if (_maxRounds <= 0)
                return false;
            if (_roundsCompleted < _maxRounds)
                return false;

            var pSum = CardBattleRules.SumTotalHandLevel(_playerHand, _weather);
            var eSum = CardBattleRules.SumTotalHandLevel(_enemyHand, _weather);

            if (Mathf.Approximately(pSum, eSum))
            {
                FinishBattle(false, BattleEndReason.RoundLimitDraw);
                return true;
            }

            // 总手牌等级低的一方获胜（§2.2.5）
            var playerWins = pSum < eSum;
            FinishBattle(playerWins, BattleEndReason.RoundLimitByTotalHandLevel);
            return true;
        }

        private void FinishBattle(bool playerVictory, BattleEndReason reason)
        {
            _phase = Phase.Idle;
            _pendingEnemyHandIndex = -1;
            _pendingPlayerHandIndex = -1;
            var boss = _isBossBattle;
            Debug.Log(
                $"[BattleManager] 战斗结束 己方{(playerVictory ? "胜" : "败")} 原因={reason} BOSS战={boss}");
            SyncBattleState();
            EventManager.Instance?.Publish(new BattleEndedEvent(playerVictory, reason, boss,
                _battleEffects.PlayerGoldenNecklacePlayed));
            EventManager.Instance?.Publish(new BattleStateChangedEvent());
        }

        private static bool IsLegalPlay(Card card, IReadOnlyList<Card> hand, string lastInstanceId)
        {
            if (hand.Count <= 1)
                return true;
            if (string.IsNullOrEmpty(lastInstanceId))
                return true;
            return card.BattleInstanceId != lastInstanceId;
        }

        private static void MoveCardAtToDiscard(List<Card> hand, int index, List<Card> discard)
        {
            if (index < 0 || index >= hand.Count)
                return;
            discard.Add(hand[index]);
            hand.RemoveAt(index);
        }

        private void ApplyXRay(int count)
        {
            if (_enemyHand.Count == 0)
            {
                CurrentBattle.EnemyVisible = Array.Empty<Card>();
                return;
            }

            if (_isTutorialBattle)
            {
                for (var i = 0; i < _enemyHand.Count; i++)
                {
                    var c = _enemyHand[i];
                    if (c != null &&
                        string.Equals(c.Id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase))
                    {
                        CurrentBattle.EnemyVisible = new[] { c };
                        return;
                    }
                }

                CurrentBattle.EnemyVisible = Array.Empty<Card>();
                return;
            }

            if (count <= 0)
            {
                CurrentBattle.EnemyVisible = Array.Empty<Card>();
                return;
            }

            var indices = new List<int>();
            for (var i = 0; i < _enemyHand.Count; i++)
                indices.Add(i);
            for (var i = 0; i < indices.Count; i++)
            {
                var j = Random.Range(i, indices.Count);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var visible = new Card[Mathf.Min(count, indices.Count)];
            for (var k = 0; k < visible.Length; k++)
                visible[k] = _enemyHand[indices[k]];
            CurrentBattle.EnemyVisible = visible;
        }

        private void SyncBattleState()
        {
            CurrentBattle.PlayerHand = _playerHand.ToArray();
            CurrentBattle.EnemyHand = _enemyHand.ToArray();
            CurrentBattle.PlayerDiscard = _playerDiscard.ToArray();
            CurrentBattle.EnemyDiscard = _enemyDiscard.ToArray();
            CurrentBattle.Round = _roundsCompleted;
            CurrentBattle.MaxRound = _noRoundLimit ? 0 : _maxRounds;
            CurrentBattle.NoRoundLimit = _noRoundLimit;
            CurrentBattle.BattleWeather = _weather;
            CurrentBattle.OpponentDisplayName = _opponentDisplayName ?? string.Empty;
            CurrentBattle.IsBossBattle = _isBossBattle;
            CurrentBattle.TurnHistory = _historyLines.ToArray();
        }

        private void ResetRuntime()
        {
            _playerHand.Clear();
            _enemyHand.Clear();
            _playerDiscard.Clear();
            _enemyDiscard.Clear();
            _historyLines.Clear();
            _roundsCompleted = 0;
            _lastPlayerInstanceId = null;
            _lastEnemyInstanceId = null;
            _isBossBattle = false;
            _bossAiStrength = 0;
            _isTutorialBattle = false;
            _opponentDisplayName = string.Empty;
            _pendingEnemyHandIndex = -1;
            _pendingPlayerHandIndex = -1;
            _battleEffects.ResetBattle();
            _restrictedNextRound = false;
            _playerExponentialSacrificeResolved = false;
            CurrentBattle = new BattleState();
        }

        private static int ResolveBossAiStrengthFromTower()
        {
            var gm = GameManager.Instance;
            var cfg = ConfigManager.Instance;
            var floor = gm != null ? gm.PlayerState.CurrentFloor : 1;
            if (cfg != null && cfg.TryGetTowerFloor(floor, out var entry))
                return entry.BossAiStrength;
            return 0;
        }

        private static string ResolveOpponentDisplayName(bool vsBoss)
        {
            if (!vsBoss)
                return "敌方";

            var gm = GameManager.Instance;
            var cfg = ConfigManager.Instance;
            var floor = gm != null ? gm.PlayerState.CurrentFloor : 1;
            if (cfg != null && cfg.TryGetTowerFloor(floor, out var entry))
            {
                var id = entry.BossId;
                if (!string.IsNullOrEmpty(id) && cfg.TryGetCard(id, out var cc))
                    return string.IsNullOrEmpty(cc.DisplayName) ? id : cc.DisplayName;
                return string.IsNullOrEmpty(id) ? "驻守者" : id;
            }

            return "驻守者";
        }

        private static Card CloneForBattle(Card template)
        {
            return new Card
            {
                Id = template.Id,
                Name = template.Name,
                Level = template.Level,
                Type = template.Type,
                EffectDesc = template.EffectDesc,
                IsUnique = template.IsUnique,
                BattleInstanceId = Guid.NewGuid().ToString("N")
            };
        }

        private static List<Card> BuildPlayerDeck(PlayerData player)
        {
            if (player?.HandCards != null && player.HandCards.Length > 0)
                return new List<Card>(player.HandCards);
            if (player?.OwnedCards != null && player.OwnedCards.Length > 0)
                return new List<Card>(player.OwnedCards);
            return BuildDefaultPlayerDeck();
        }

        private static List<Card> BuildDefaultPlayerDeck()
        {
            return new List<Card>
            {
                NewRuntimeCard(WellKnownCardIds.King, "国王", 3f),
                NewRuntimeCard(WellKnownCardIds.Commoner, "平民", 1f),
                NewRuntimeCard("guard", "护卫", 2.5f),
                NewRuntimeCard("maid", "侍女", 1.5f)
            };
        }

        private static List<Card> BuildDefaultEnemyDeck()
        {
            return new List<Card>
            {
                NewRuntimeCard(WellKnownCardIds.King, "敌国王", 3f),
                NewRuntimeCard("minister", "敌大臣", 2f),
                NewRuntimeCard("thief", "盗贼", 0.5f),
                NewRuntimeCard("noble", "贵族", 2.3f)
            };
        }

        private static Card NewRuntimeCard(string id, string name, float level) =>
            new Card
            {
                Id = id,
                Name = name,
                Level = level,
                Type = CardType.Basic
            };
    }
}
