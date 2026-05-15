using System;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>弑君者/叛臣/乞丐：同侧手牌检测与变形。</summary>
    public static class BattleMorphRules
    {
        public static bool IsTriCoreId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            return string.Equals(id, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(id, WellKnownCardIds.Minister, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HandContainsIdOtherThan(IEnumerable<Card> hand, string id,
            string excludeBattleInstanceId)
        {
            if (hand == null || string.IsNullOrEmpty(id))
                return false;
            foreach (var c in hand)
            {
                if (c == null)
                    continue;
                if (!string.IsNullOrEmpty(excludeBattleInstanceId) &&
                    c.BattleInstanceId == excludeBattleInstanceId)
                    continue;
                if (string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>同侧手牌（不含自身实例）是否仍有指定 Id。</summary>
        public static bool SameSideHandContainsOther(IEnumerable<Card> sameSideHand, string id,
            Card self)
        {
            if (self == null)
                return HandContainsIdOtherThan(sameSideHand, id, null);
            return HandContainsIdOtherThan(sameSideHand, id, self.BattleInstanceId);
        }

        /// <summary>参与比大小的基础等级（变形牌在同侧仍有对应基础牌时为 1）。</summary>
        public static float GetMorphBaseLevel(Card card, bool cardBelongsToPlayer,
            IEnumerable<Card> playerHand, IEnumerable<Card> enemyHand)
        {
            if (card == null)
                return 0f;

            var sameSide = cardBelongsToPlayer ? playerHand : enemyHand;

            if (string.Equals(card.Id, WellKnownCardIds.Regicide, StringComparison.OrdinalIgnoreCase))
                return SameSideHandContainsOther(sameSide, WellKnownCardIds.King, card)
                    ? 1f
                    : 3f;

            if (string.Equals(card.Id, WellKnownCardIds.Rebel, StringComparison.OrdinalIgnoreCase))
                return SameSideHandContainsOther(sameSide, WellKnownCardIds.Minister, card) ? 1f : 2f;

            if (string.Equals(card.Id, WellKnownCardIds.Beggar, StringComparison.OrdinalIgnoreCase))
                return SameSideHandContainsOther(sameSide, WellKnownCardIds.Commoner, card) ? 1f : 1f;

            return card.Level;
        }

        /// <summary>将手牌中满足条件的 morph 牌就地变为国王/大臣/平民（配置表）。</summary>
        public static void ApplyMorphTransformsOnHand(IList<Card> hand, ConfigManager cfg)
        {
            if (hand == null || cfg == null)
                return;

            for (var i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (c == null)
                    continue;

                if (string.Equals(c.Id, WellKnownCardIds.Regicide, StringComparison.OrdinalIgnoreCase))
                {
                    if (!SameSideHandContainsOther(hand, WellKnownCardIds.King, c) &&
                        cfg.TryGetCard(WellKnownCardIds.King, out var king))
                        CopyTemplateOntoCard(c, king);
                }
                else if (string.Equals(c.Id, WellKnownCardIds.Rebel, StringComparison.OrdinalIgnoreCase))
                {
                    if (!SameSideHandContainsOther(hand, WellKnownCardIds.Minister, c) &&
                        cfg.TryGetCard(WellKnownCardIds.Minister, out var minister))
                        CopyTemplateOntoCard(c, minister);
                }
                else if (string.Equals(c.Id, WellKnownCardIds.Beggar, StringComparison.OrdinalIgnoreCase))
                {
                    if (!SameSideHandContainsOther(hand, WellKnownCardIds.Commoner, c) &&
                        cfg.TryGetCard(WellKnownCardIds.Commoner, out var commoner))
                        CopyTemplateOntoCard(c, commoner);
                }
            }
        }

        private static void CopyTemplateOntoCard(Card target, CardConfigEntry template)
        {
            if (target == null || template == null)
                return;
            target.Id = template.Id;
            target.Name = string.IsNullOrEmpty(template.DisplayName) ? template.Id : template.DisplayName;
            target.Level = template.Level;
            target.Type = template.Type;
            target.EffectDesc = template.Description ?? string.Empty;
            target.IsUnique = template.IsUnique;
        }

        /// <summary>文明：该侧手牌全部变为国王（本局）。</summary>
        public static void TransformHandToKings(IList<Card> hand, ConfigManager cfg)
        {
            if (hand == null || cfg == null || !cfg.TryGetCard(WellKnownCardIds.King, out var king))
                return;
            for (var i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (c == null)
                    continue;
                CopyTemplateOntoCard(c, king);
            }
        }
    }
}
