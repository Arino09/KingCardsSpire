using System;

namespace KingCardsSpire.Models
{
    public static class StoryDialogueRules
    {
        public const int HeroSlotCount = 3;
        public const int MaxHeroStoryCount = 7;
        public const int MaxNpcStoryCount = 3;
        public const int NpcCreditsPerFloor = 3;
        public const string NarratorCharacterId = "0";

        public static string BuildHeroStoryStartId(int heroSlotIndex, int storyIndex)
        {
            return $"hero_{heroSlotIndex}_{storyIndex}";
        }

        /// <summary>
        /// 构建原住民剧情链入口 id（第一句）。
        /// 格式：<c>npc_{npcId}_{访问序号}_{句序号}</c>，访问序号从 1 起；入口固定为第 1 句，故句序号为 <c>1</c>。
        /// 例：NPC id 为 <c>11</c> 的第 1 次访问 → <c>npc_11_1_1</c>。
        /// </summary>
        public static string BuildNpcStoryStartId(string npcId, int storyIndex)
        {
            return $"npc_{npcId}_{storyIndex}_1";
        }

        public static bool TryParseHeroStoryId(string id, out int heroSlotIndex, out int storyIndex)
        {
            heroSlotIndex = -1;
            storyIndex = 0;
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("hero_", StringComparison.Ordinal))
                return false;

            var parts = id.Split('_');
            if (parts.Length != 3)
                return false;

            return int.TryParse(parts[1], out heroSlotIndex) &&
                   int.TryParse(parts[2], out storyIndex) &&
                   heroSlotIndex >= 0 &&
                   storyIndex >= 1;
        }

        /// <summary>
        /// 解析原住民对话行 id：<c>npc_{npcId}_{访问序号}_{句序号}</c>；<paramref name="storyIndex"/> 为访问序号（第几次访问该 NPC 的剧情段）。
        /// </summary>
        public static bool TryParseNpcStoryId(string id, out string npcId, out int storyIndex)
        {
            return TryParseNpcStoryId(id, out npcId, out storyIndex, out _);
        }

        /// <summary>
        /// 解析原住民对话行 id：<c>npc_{npcId}_{访问序号}_{句序号}</c>。
        /// </summary>
        public static bool TryParseNpcStoryId(string id, out string npcId, out int storyIndex, out int lineIndex)
        {
            npcId = null;
            storyIndex = 0;
            lineIndex = 0;
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("npc_", StringComparison.Ordinal))
                return false;

            var parts = id.Split('_');
            if (parts.Length < 4 || !string.Equals(parts[0], "npc", StringComparison.Ordinal))
                return false;

            if (!int.TryParse(parts[^2], out storyIndex) || storyIndex < 1)
                return false;

            if (!int.TryParse(parts[^1], out lineIndex) || lineIndex < 1)
                return false;

            var npcSegmentCount = parts.Length - 3;
            if (npcSegmentCount < 1)
                return false;

            npcId = string.Join("_", parts, 1, npcSegmentCount);
            return !string.IsNullOrWhiteSpace(npcId);
        }

        public static bool TryGetNextHeroStoryIndex(int completedCount, int currentFloor, out int nextStoryIndex)
        {
            nextStoryIndex = completedCount + 1;
            var maxAvailable = Math.Min(Math.Max(currentFloor, 0), MaxHeroStoryCount);
            return nextStoryIndex >= 1 && nextStoryIndex <= maxAvailable;
        }

        public static bool TryGetNextNpcStoryIndex(int completedCount, out int nextStoryIndex)
        {
            nextStoryIndex = completedCount + 1;
            return nextStoryIndex >= 1 && nextStoryIndex <= MaxNpcStoryCount;
        }
    }
}
