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

        public static string BuildNpcStoryStartId(string npcId, int storyIndex)
        {
            return $"npc_{npcId}_{storyIndex}";
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

        public static bool TryParseNpcStoryId(string id, out string npcId, out int storyIndex)
        {
            npcId = null;
            storyIndex = 0;
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("npc_", StringComparison.Ordinal))
                return false;

            var lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore <= "npc_".Length || lastUnderscore >= id.Length - 1)
                return false;

            if (!int.TryParse(id.Substring(lastUnderscore + 1), out storyIndex) || storyIndex < 1)
                return false;

            npcId = id.Substring("npc_".Length, lastUnderscore - "npc_".Length);
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
