using System;
using System.Collections.Generic;
using System.Text;
using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;

namespace KingCardsSpire.Core
{
    /// <summary>按存档进度将某 NPC 已看过的对话正文拼接为纯文本（用于历史记录界面）。</summary>
    public static class NpcDialogueHistoryText
    {
        /// <summary>
        /// 优先根据 <see cref="PlayerData.UnlockedDialogues"/> 中该 NPC 的台词 id 排序输出；
        /// 若无匹配且 <paramref name="completedCount"/> 大于 0，则按配置链兜底遍历（选择支取首项，见实现注释）。
        /// </summary>
        public static string BuildForNpc(string npcId, ConfigManager cfg, PlayerData player, int completedCount)
        {
            if (string.IsNullOrEmpty(npcId) || cfg == null || player == null || completedCount <= 0)
                return string.Empty;

            var fromUnlocked = CollectSortedLineIdsFromUnlocked(npcId, player, completedCount);
            if (fromUnlocked.Count > 0)
                return FormatLines(cfg, fromUnlocked);

            return FormatLinesFromFallbackChains(npcId, cfg, completedCount);
        }

        private static List<string> CollectSortedLineIdsFromUnlocked(string npcId, PlayerData player, int completedCount)
        {
            var arr = player.UnlockedDialogues ?? Array.Empty<string>();
            var tuples = new List<(int StoryIndex, int LineIndex, string Id)>();
            for (var i = 0; i < arr.Length; i++)
            {
                var raw = arr[i];
                if (string.IsNullOrEmpty(raw))
                    continue;
                if (!StoryDialogueRules.TryParseNpcStoryId(raw, out var pid, out var storyIndex, out var lineIndex))
                    continue;
                if (!string.Equals(pid, npcId, StringComparison.Ordinal) || storyIndex > completedCount)
                    continue;
                tuples.Add((storyIndex, lineIndex, raw));
            }

            tuples.Sort((a, b) =>
            {
                var c = a.StoryIndex.CompareTo(b.StoryIndex);
                return c != 0 ? c : a.LineIndex.CompareTo(b.LineIndex);
            });

            var ids = new List<string>(tuples.Count);
            for (var i = 0; i < tuples.Count; i++)
                ids.Add(tuples[i].Id);
            return ids;
        }

        private static string FormatLines(ConfigManager cfg, IReadOnlyList<string> lineIdsOrdered)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < lineIdsOrdered.Count; i++)
            {
                if (!cfg.TryGetDialogueLine(lineIdsOrdered[i], out var line))
                    continue;
                AppendParagraph(sb, cfg, line);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 极旧档等无 Unlocked 记录时：按每段剧情从入口沿 NextId 遍历；遇选择支时无法还原玩家当时选项，固定走 <see cref="DialogueChoiceEntry.NextDialogueId"/> 的第一项。
        /// </summary>
        private static string FormatLinesFromFallbackChains(string npcId, ConfigManager cfg, int completedCount)
        {
            var sb = new StringBuilder();
            for (var storyIndex = 1; storyIndex <= completedCount; storyIndex++)
            {
                var startId = StoryDialogueRules.BuildNpcStoryStartId(npcId, storyIndex);
                if (!cfg.TryGetDialogueLine(startId, out _))
                    continue;

                var currentId = startId;
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (!string.IsNullOrEmpty(currentId) && visited.Add(currentId))
                {
                    if (!cfg.TryGetDialogueLine(currentId, out var line))
                        break;

                    AppendParagraph(sb, cfg, line);

                    if (line.IsChoice)
                    {
                        var choices = line.Choices;
                        if (choices == null || choices.Count == 0)
                            break;
                        currentId = choices[0].NextDialogueId;
                        continue;
                    }

                    currentId = line.NextId;
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendParagraph(StringBuilder sb, ConfigManager cfg, DialogueLineEntry line)
        {
            DialogueLineSpeaker.Resolve(cfg, line, out var speakerName, out _);
            var body = line != null ? line.BodyText : string.Empty;
            if (sb.Length > 0)
                sb.Append("\n\n");

            if (string.IsNullOrEmpty(speakerName))
                sb.Append(body);
            else
            {
                sb.Append(speakerName);
                sb.Append('\n');
                sb.Append(body);
            }
        }
    }
}
