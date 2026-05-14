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

        /// <summary>主界面原住民入口总栏位数（「?」新遇 + 已遇头像）；达到已遇人数后不再显示新遇入口，避免超出布局。</summary>
        public const int MaxNpcHubSlots = 8;

        public NpcHubViewModel(IReadOnlyList<NpcHubButtonSpec> buttons, bool newEncounterPoolEmpty)
        {
            Buttons = buttons ?? Array.Empty<NpcHubButtonSpec>();
            NewEncounterPoolEmpty = newEncounterPoolEmpty;
        }

        public IReadOnlyList<NpcHubButtonSpec> Buttons { get; }

        /// <summary>为 true 时点击「结识新原住民」仅提示「暂无新原住民」，不消耗剧情配额；为 true 时若列表未满也会隐藏「?」入口。</summary>
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
            var list = new List<NpcHubButtonSpec>();
            var metCount = metNpcIdsSorted != null ? metNpcIdsSorted.Count : 0;
            var showNewEncounter = metCount < MaxNpcHubSlots && !newEncounterPoolEmpty;
            if (showNewEncounter)
                list.Add(new NpcHubButtonSpec(NpcHubButtonKind.NewEncounter, string.Empty, NewEncounterLabel));

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
