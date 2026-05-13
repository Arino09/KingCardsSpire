using System;
using System.Collections.Generic;

namespace KingCardsSpire.Models
{
    public enum NpcHubButtonKind
    {
        NewEncounter = 0,
        MetNpc = 1
    }

    /// <summary>NPC 主界面单条入口按钮的展示数据。</summary>
    public readonly struct NpcHubButtonSpec
    {
        public NpcHubButtonKind Kind { get; }
        public string NpcId { get; }
        public string LabelText { get; }
        public string AvatarId { get; }

        public NpcHubButtonSpec(NpcHubButtonKind kind, string npcId, string labelText, string avatarId = "")
        {
            Kind = kind;
            NpcId = npcId ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            AvatarId = avatarId ?? string.Empty;
        }
    }

    /// <summary>NPCView 打开时的列表视图模型（结识新原住民 + 已遇列表）。</summary>
    public sealed class NpcHubViewModel
    {
        public const string NewEncounterLabel = "结识新原住民";

        public NpcHubViewModel(IReadOnlyList<NpcHubButtonSpec> buttons, bool newEncounterPoolEmpty)
        {
            Buttons = buttons ?? Array.Empty<NpcHubButtonSpec>();
            NewEncounterPoolEmpty = newEncounterPoolEmpty;
        }

        public IReadOnlyList<NpcHubButtonSpec> Buttons { get; }

        /// <summary>为 true 时点击「结识新原住民」仅提示「暂无新原住民」，不消耗当日访问。</summary>
        public bool NewEncounterPoolEmpty { get; }

        public static NpcHubViewModel Build(IReadOnlyList<string> metNpcIdsSorted, bool newEncounterPoolEmpty)
        {
            return Build(metNpcIdsSorted, newEncounterPoolEmpty, null, null);
        }

        public static NpcHubViewModel Build(
            IReadOnlyList<string> metNpcIdsSorted,
            bool newEncounterPoolEmpty,
            Func<string, string> displayNameResolver)
        {
            return Build(metNpcIdsSorted, newEncounterPoolEmpty, displayNameResolver, null);
        }

        public static NpcHubViewModel Build(
            IReadOnlyList<string> metNpcIdsSorted,
            bool newEncounterPoolEmpty,
            Func<string, string> displayNameResolver,
            Func<string, string> avatarIdResolver)
        {
            var list = new List<NpcHubButtonSpec> { new NpcHubButtonSpec(NpcHubButtonKind.NewEncounter, string.Empty, NewEncounterLabel) };

            if (metNpcIdsSorted != null)
            {
                for (var i = 0; i < metNpcIdsSorted.Count; i++)
                {
                    var id = metNpcIdsSorted[i];
                    if (string.IsNullOrEmpty(id))
                        continue;
                    var label = displayNameResolver != null ? displayNameResolver(id) : id;
                    var avatarId = avatarIdResolver != null ? avatarIdResolver(id) : string.Empty;
                    list.Add(new NpcHubButtonSpec(NpcHubButtonKind.MetNpc, id, label, avatarId));
                }
            }

            return new NpcHubViewModel(list, newEncounterPoolEmpty);
        }
    }
}
