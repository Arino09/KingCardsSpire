using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;

namespace KingCardsSpire.Core
{
    /// <summary>对话行展示用说话者与立绘 id 解析（与 <see cref="Controllers.DialogueController"/> 运行时一致）。</summary>
    public static class DialogueLineSpeaker
    {
        public static void Resolve(ConfigManager cfg, DialogueLineEntry line, out string speakerName, out string portraitId)
        {
            speakerName = line != null ? line.SpeakerName : string.Empty;
            portraitId = line != null ? line.CharacterId : string.Empty;
            var characterId = line != null ? line.CharacterId : string.Empty;
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            if (characterId == StoryDialogueRules.NarratorCharacterId)
            {
                speakerName = string.Empty;
                portraitId = string.Empty;
                return;
            }

            if (cfg != null && cfg.TryGetHero(characterId, out var hero))
            {
                speakerName = hero.DisplayName;
                portraitId = hero.PortraitId;
                return;
            }

            if (cfg != null && cfg.TryGetNpc(characterId, out var npc))
            {
                speakerName = npc.DisplayName;
                portraitId = npc.PortraitId;
            }
        }
    }
}
