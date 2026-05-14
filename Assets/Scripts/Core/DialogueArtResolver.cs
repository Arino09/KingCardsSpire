using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingCardsSpire.Core
{
    /// <summary>
    /// 将配置表中的背景 id / 人物 id 解析为 Addressables 地址（与 MainHub、NpcButton 约定一致）。
    /// </summary>
    public static class DialogueArtResolver
    {
        /// <summary>
        /// 背景图候选地址：已带 <c>.jpg</c>/<c>.png</c> 时仅一项；否则依次尝试 <c>.jpg</c>、<c>.png</c>（与 Addressables 主地址一致即可命中）。</summary>
        public static IReadOnlyList<string> GetBackgroundAddressCandidates(string backgroundId)
        {
            if (string.IsNullOrWhiteSpace(backgroundId))
                return Array.Empty<string>();

            var id = backgroundId.Trim().Replace('\\', '/');
            var baseAddress = id.Contains("/")
                ? "Sprites/Character/NPCbg"
                : "Sprites/UI/FloorBg";
            if (id.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return new[] { $"{baseAddress}/{id}" };

            return new[]
            {
                $"{baseAddress}/{id}.jpg",
                $"{baseAddress}/{id}.png"
            };
        }

        /// <summary>兼容旧调用：返回首个候选（无扩展名时为 <c>.jpg</c> 路径）。</summary>
        public static string ResolveBackgroundAddress(string backgroundId)
        {
            var list = GetBackgroundAddressCandidates(backgroundId);
            return list.Count > 0 ? list[0] : null;
        }

        public static string ResolveCharacterAddress(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return null;
            var id = characterId.Trim().Replace('\\', '/');
            if (id.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                return $"Sprites/Character/{id}";
            return $"Sprites/Character/{id}.png";
        }
    }
}
