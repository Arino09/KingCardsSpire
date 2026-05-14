using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>对话选项：描述与跳转到的下一句 id。</summary>
    [Serializable]
    public sealed class DialogueChoiceEntry
    {
        [SerializeField] private string optionText;
        [SerializeField] private string nextDialogueId;

        public string OptionText => optionText;

        public string NextDialogueId => nextDialogueId;
    }

    /// <summary>对话表中的一行（一项）。</summary>
    [Serializable]
    public sealed class DialogueLineEntry
    {
        [Tooltip("原住民剧情链：npc_{npcId}_{访问序号}_{句序号}，入口句序号为 1，与 StoryDialogueRules.BuildNpcStoryStartId 一致。")]
        [SerializeField] private string id;
        [Tooltip("出现时机类型（策划分类，便于筛选；不改变代码入口 id 约定）。")]
        [SerializeField] private DialogueTimingKind timingKind;
        [SerializeField] private string backgroundId;
        [SerializeField] private string characterId;
        [SerializeField] private string speakerName;
        [TextArea(2, 6)] [SerializeField] private string bodyText;
        [SerializeField] private bool isChoice;
        [SerializeField] private string nextId;
        [SerializeField] private List<DialogueChoiceEntry> choices = new();

        public string Id => id;

        public DialogueTimingKind TimingKind => timingKind;

        public string BackgroundId => backgroundId;

        public string CharacterId => characterId;

        public string SpeakerName => speakerName;

        public string BodyText => bodyText;

        public bool IsChoice => isChoice;

        /// <summary>非选择行：展示结束后跳转的下一句 id；空表示结束整条对话。</summary>
        public string NextId => nextId;

        public IReadOnlyList<DialogueChoiceEntry> Choices => choices;
    }

    /// <summary>全部对话行配置（一个资源内可含多条，多个资源在 ConfigManager 中合并索引）。</summary>
    [CreateAssetMenu(fileName = "DialogueConfig", menuName = "KingCardsSpire/Configs/Dialogue Database", order = 50)]
    public sealed class DialogueConfig : ScriptableObject
    {
        [SerializeField] private List<DialogueLineEntry> lines = new();

        public IReadOnlyList<DialogueLineEntry> Lines => lines;
    }
}
