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

        public BattleState CurrentBattle { get; private set; } = new();

        public bool IsBattleActive => _phase == Phase.InBattle;

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

            StartBattleInternal(playerCards, enemyCards, player?.CurrentWeather ?? WeatherType.WarmWind,
                player?.XRayCount ?? 1, vsBoss);
        }

        public void StartBattle(IReadOnlyList<Card> playerDeck, IReadOnlyList<Card> enemyDeck,
            WeatherType weather, int xRayCount, bool isBossBattle = false)
        {
            StartBattleInternal(playerDeck, enemyDeck, weather, xRayCount, isBossBattle);
        }

        private void StartBattleInternal(IReadOnlyList<Card> playerDeck, IReadOnlyList<Card> enemyDeck,
            WeatherType weather, int xRayCount, bool isBossBattle)
        {
            ResetRuntime();
            _isBossBattle = isBossBattle;

            foreach (var c in playerDeck)
                _playerHand.Add(CloneForBattle(c));
            foreach (var c in enemyDeck)
                _enemyHand.Add(CloneForBattle(c));

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

        /// <summary>玩家点击手牌下标出牌。</summary>
        public bool TrySubmitPlayerCard(int playerHandIndex, out string error)
        {
            error = null;
            if (_phase != Phase.InBattle)
            {
                error = "当前不在战斗中";
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

            var enemyCard = PickEnemyCard();
            if (enemyCard == null)
            {
                error = "敌方无牌可出";
                return false;
            }

            ResolveRound(playerHandIndex, playerCard, enemyCard);
            return true;
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

        private static Card CardFromConfig(CardConfig cc)
        {
            return new Card
            {
                Id = cc.Id,
                Name = cc.DisplayName,
                Level = cc.Level,
                Type = cc.Type,
                IsUnique = cc.IsUnique
            };
        }

        private Card PickEnemyCard()
        {
            if (_enemyHand.Count == 0)
                return null;

            var candidates = new List<int>();
            for (var i = 0; i < _enemyHand.Count; i++)
            {
                var c = _enemyHand[i];
                if (IsLegalPlay(c, _enemyHand, _lastEnemyInstanceId))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                // 仅一张且与上次相同实例时仍须出牌
                var idx = Random.Range(0, _enemyHand.Count);
                return _enemyHand[idx];
            }

            var pick = candidates[Random.Range(0, candidates.Count)];
            return _enemyHand[pick];
        }

        private void ResolveRound(int playerHandIndex, Card playerCard, Card enemyCard)
        {
            var enemyIdx = _enemyHand.IndexOf(enemyCard);
            if (enemyIdx < 0)
                enemyIdx = 0;

            var result = CardBattleRules.Compare(playerCard, enemyCard, _weather);
            var summary =
                $"{playerCard.Name} vs {enemyCard.Name} → {result} (天气 {_weather})";

            // Doc §2.2.3：败者入弃牌；平局双方入弃牌；胜者该回合牌留在手中。
            switch (result)
            {
                case BattleCompareResult.Draw:
                    MoveCardAtToDiscard(_playerHand, playerHandIndex, _playerDiscard);
                    MoveCardAtToDiscard(_enemyHand, enemyIdx, _enemyDiscard);
                    summary += " [平局入弃牌]";
                    break;
                case BattleCompareResult.FirstWins:
                    MoveCardAtToDiscard(_enemyHand, enemyIdx, _enemyDiscard);
                    summary += " [己方胜·敌方牌入弃]";
                    break;
                case BattleCompareResult.SecondWins:
                    MoveCardAtToDiscard(_playerHand, playerHandIndex, _playerDiscard);
                    summary += " [敌方胜·己方牌入弃]";
                    break;
            }

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
            var boss = _isBossBattle;
            Debug.Log(
                $"[BattleManager] 战斗结束 己方{(playerVictory ? "胜" : "败")} 原因={reason} BOSS战={boss}");
            SyncBattleState();
            EventManager.Instance?.Publish(new BattleEndedEvent(playerVictory, reason, boss));
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
            if (count <= 0 || _enemyHand.Count == 0)
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
            CurrentBattle = new BattleState();
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
