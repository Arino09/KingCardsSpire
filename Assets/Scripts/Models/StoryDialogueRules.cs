using System;

namespace KingCardsSpire.Models
{
    public static class StoryDialogueRules
    {
        public const int HeroSlotCount = 3;
        public const int MaxHeroStoryCount = 7;
        public const int MaxNpcStoryCount = 3;

        /// <summary>除第一层外，每层通过「进层 + 本层内结束当日」累计发放的「原住民剧情推进」总次数（分笔发放，见 <see cref="NpcCreditsOnFloorEnterSlice"/>）。</summary>
        public const int NpcCreditsPerFloor = 3;

        /// <summary>第一层「原住民剧情推进」总配额（分 3 笔发放：开局 + 本层内两次结束当日，每笔 <see cref="NpcCreditsFirstFloorDaySlice"/>）。</summary>
        public const int NpcCreditsFirstFloor = 6;

        /// <summary>第一层：每个发放日（开局或某次 <c>AdvanceDay</c>）写入的点数，共 3 日 × 每日 2 点。</summary>
        public const int NpcCreditsFirstFloorDaySlice = 2;

        /// <summary>第 2 层及以后：进入该层当日立刻发放的配额（首日合计为上一层的剩余 + 本项）。</summary>
        public const int NpcCreditsOnFloorEnterSlice = 1;

        /// <summary>在「首日已发一笔」之后，本层内每次 <c>AdvanceDay</c> 再发放的笔数；与首日笔合计为该层总配额（第一层为 <see cref="NpcCreditsFirstFloor"/> / <see cref="NpcCreditsFirstFloorDaySlice"/>，其余层为 <see cref="NpcCreditsPerFloor"/> / <see cref="NpcCreditsOnFloorEnterSlice"/>）。</summary>
        public const int NpcCreditInstallmentCountAfterEnter = 2;

        public const string NarratorCharacterId = "0";

        /// <summary>本层内每次分期应增加的 <see cref="PlayerData.NpcDialogueCredits"/> 数量（第一层 2，第 2 层起 1）。</summary>
        public static int GetNpcInstallmentCreditSlice(int floor1Based)
        {
            return floor1Based == 1 ? NpcCreditsFirstFloorDaySlice : NpcCreditsOnFloorEnterSlice;
        }

        /// <summary>
        /// 若将某层「累计应发放」的原住民次数换算为常数：第一层为 <see cref="NpcCreditsFirstFloor"/>；第 2 层起每层合计为 <see cref="NpcCreditsPerFloor"/>（实际按进层 + 按日分期写入 <see cref="PlayerData.NpcDialogueCredits"/>）。
        /// </summary>
        public static int GetNpcCreditsAwardedForFloor(int floor1Based)
        {
            return floor1Based == 1 ? NpcCreditsFirstFloor : NpcCreditsPerFloor;
        }

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
