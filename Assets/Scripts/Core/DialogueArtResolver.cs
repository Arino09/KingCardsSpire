using UnityEngine;

namespace KingCardsSpire.Core
{
    /// <summary>
    /// 将配置表中的背景 id / 人物 id 解析为 Addressables 地址（与 MainHub、NpcButton 约定一致）。
    /// </summary>
    public static class DialogueArtResolver
    {
        public static string ResolveBackgroundAddress(string backgroundId)
        {
            if (string.IsNullOrWhiteSpace(backgroundId))
                return null;
            var id = backgroundId.Trim();
            if (id.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return $"Sprites/UI/FloorBg/{id}";
            return $"Sprites/UI/FloorBg/{id}.jpg";
        }

        public static string ResolveCharacterAddress(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return null;
            var id = characterId.Trim().Replace('\\', '/');
            if (id.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase))
                return $"Sprites/Character/{id}";
            return $"Sprites/Character/{id}.png";
        }
    }
}
