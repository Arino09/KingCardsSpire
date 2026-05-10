using System;
using System.Globalization;
using KingCardsSpire.Configs;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Views.UI.Cards
{
    /// <summary>
    /// 卡牌 UI 展示用数据（与预制体字段一一对应，不含交互逻辑）。
    /// </summary>
    public sealed class CardViewModel
    {
        private CardViewModel(
            string levelDisplay,
            string typeDisplay,
            string name,
            string effect,
            Sprite art,
            string cardId,
            string battleInstanceId)
        {
            LevelDisplay = levelDisplay;
            TypeDisplay = typeDisplay;
            Name = name;
            Effect = effect;
            Art = art;
            CardId = cardId ?? string.Empty;
            BattleInstanceId = battleInstanceId ?? string.Empty;
        }

        public string LevelDisplay { get; }

        public string TypeDisplay { get; }

        public string Name { get; }

        public string Effect { get; }

        public Sprite Art { get; }

        public string CardId { get; }

        public string BattleInstanceId { get; }

        public static CardViewModel FromCard(Card card, CardConfigEntry config = null)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            var level = card.Level;
            var type = card.Type;
            if (config != null)
            {
                if (float.IsNaN(level) || float.IsInfinity(level))
                    level = config.Level;
            }

            return new CardViewModel(
                CardLevelFormat.FormatLevel(level),
                CardTypeLocalization.ToShort(type),
                ResolveName(card, config),
                ResolveEffect(card, config),
                null,
                card.Id,
                card.BattleInstanceId);
        }

        public static CardViewModel FromConfigOnly(CardConfigEntry config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new CardViewModel(
                CardLevelFormat.FormatLevel(config.Level),
                CardTypeLocalization.ToShort(config.Type),
                config.DisplayName ?? string.Empty,
                config.Description ?? string.Empty,
                null,
                config.Id,
                string.Empty);
        }

        /// <summary>
        /// 驻守奖励一条选项：金币展示为「奖励卡」样式；卡牌选项优先用配置表文案与等级。
        /// </summary>
        public static CardViewModel FromBossRewardOption(BossRewardOption option, CardConfigEntry cardConfigOrNull)
        {
            if (option == null)
                throw new ArgumentNullException(nameof(option));

            if (option.IsGold)
            {
                return new CardViewModel(
                    "—",
                    "奖励",
                    "金币",
                    $"点击领取 +{option.GoldAmount}",
                    null,
                    "__boss_reward_gold__",
                    string.Empty);
            }

            if (cardConfigOrNull != null)
                return FromConfigOnly(cardConfigOrNull);

            var name = string.IsNullOrEmpty(option.CardDisplayName) ? option.CardId : option.CardDisplayName;
            return new CardViewModel(
                "?",
                "卡牌",
                name ?? string.Empty,
                string.Empty,
                null,
                option.CardId ?? string.Empty,
                string.Empty);
        }

        private static string ResolveName(Card card, CardConfigEntry config)
        {
            if (!string.IsNullOrEmpty(card.Name))
                return card.Name;
            if (config != null && !string.IsNullOrEmpty(config.DisplayName))
                return config.DisplayName;
            return string.Empty;
        }

        private static string ResolveEffect(Card card, CardConfigEntry config)
        {
            if (!string.IsNullOrEmpty(card.EffectDesc))
                return card.EffectDesc;
            if (config != null && !string.IsNullOrEmpty(config.Description))
                return config.Description;
            return string.Empty;
        }
    }

    /// <summary>
    /// 将 <see cref="Card.Level"/> 格式化为 UI 短字符串。
    /// </summary>
    public static class CardLevelFormat
    {
        public static string FormatLevel(float level)
        {
            if (float.IsNaN(level) || float.IsInfinity(level))
                return string.Empty;

            if (Mathf.Approximately(level, Mathf.Round(level)))
                return Mathf.Round(level).ToString(CultureInfo.InvariantCulture);

            return level.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// <see cref="CardType"/> 简短中文标签（可与预制体上静态「类型」文案搭配）。
    /// </summary>
    public static class CardTypeLocalization
    {
        public static string ToShort(CardType type)
        {
            switch (type)
            {
                case CardType.Basic:
                    return "基础";
                case CardType.Function:
                    return "功能";
                case CardType.Ability:
                    return "异能";
                case CardType.Consumable:
                    return "消耗";
                default:
                    return type.ToString();
            }
        }
    }
}
