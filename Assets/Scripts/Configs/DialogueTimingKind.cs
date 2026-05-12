namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 对话行所属出现时机（策划在表里分类；运行时仍由代码传入的入口 id 决定从哪一句开始播）。
    /// </summary>
    public enum DialogueTimingKind
    {
        None = 0,

        /// <summary>新游戏开场教程（如 MainMenu 新档 → tutorial_opening）。</summary>
        OpeningTutorial = 1,

        /// <summary>挑战本层 BOSS 战前（MainHub）。</summary>
        BossPreFight = 2,

        /// <summary>访问参赛者 / 主角房交谈（HeroView）。</summary>
        HeroVisit = 3,

        /// <summary>访问原住民（NpcView）。</summary>
        NpcVisit = 4,

        /// <summary>通关等大结局（如第 7 层后 ending_final）。</summary>
        StoryEnding = 5,

        /// <summary>其它或未归类。</summary>
        Other = 6
    }
}
