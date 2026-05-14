using KingCardsSpire.Models;
using NUnit.Framework;

namespace KingCardsSpire.Tests
{
    public sealed class StoryDialogueRulesTests
    {
        [Test]
        public void NpcCreditsAwardedPerFloorMatchesDesign()
        {
            Assert.AreEqual(6, StoryDialogueRules.GetNpcCreditsAwardedForFloor(1));
            Assert.AreEqual(3, StoryDialogueRules.GetNpcCreditsAwardedForFloor(2));
            Assert.AreEqual(3, StoryDialogueRules.GetNpcCreditsAwardedForFloor(7));
            Assert.AreEqual(
                StoryDialogueRules.NpcCreditsPerFloor,
                StoryDialogueRules.NpcCreditsOnFloorEnterSlice + StoryDialogueRules.NpcCreditInstallmentCountAfterEnter);
            Assert.AreEqual(
                StoryDialogueRules.NpcCreditsFirstFloor,
                StoryDialogueRules.NpcCreditsFirstFloorDaySlice *
                (1 + StoryDialogueRules.NpcCreditInstallmentCountAfterEnter));
            Assert.AreEqual(2, StoryDialogueRules.GetNpcInstallmentCreditSlice(1));
            Assert.AreEqual(1, StoryDialogueRules.GetNpcInstallmentCreditSlice(2));
        }

        [Test]
        public void BuildsAndParsesHeroStoryIds()
        {
            var id = StoryDialogueRules.BuildHeroStoryStartId(2, 7);

            Assert.AreEqual("hero_2_7", id);
            Assert.IsTrue(StoryDialogueRules.TryParseHeroStoryId(id, out var slot, out var storyIndex));
            Assert.AreEqual(2, slot);
            Assert.AreEqual(7, storyIndex);
        }

        [Test]
        public void BuildsAndParsesNpcStoryIdsWithUnderscoreNpcIds()
        {
            var id = StoryDialogueRules.BuildNpcStoryStartId("1_2", 3);

            Assert.AreEqual("npc_1_2_3_1", id);
            Assert.IsTrue(StoryDialogueRules.TryParseNpcStoryId(id, out var npcId, out var storyIndex, out var lineIndex));
            Assert.AreEqual("1_2", npcId);
            Assert.AreEqual(3, storyIndex);
            Assert.AreEqual(1, lineIndex);

            Assert.IsTrue(StoryDialogueRules.TryParseNpcStoryId("npc_11_1_2", out var npc11, out var visit, out var line));
            Assert.AreEqual("11", npc11);
            Assert.AreEqual(1, visit);
            Assert.AreEqual(2, line);
        }

        [Test]
        public void ClampsHeroStoryAvailabilityByFloorAndCap()
        {
            Assert.IsTrue(StoryDialogueRules.TryGetNextHeroStoryIndex(2, 3, out var nextIndex));
            Assert.AreEqual(3, nextIndex);

            Assert.IsFalse(StoryDialogueRules.TryGetNextHeroStoryIndex(3, 3, out _));
            Assert.IsFalse(StoryDialogueRules.TryGetNextHeroStoryIndex(7, 8, out _));
        }

        [Test]
        public void NpcHubUsesConfiguredDisplayNameForMetNpc()
        {
            var vm = NpcHubViewModel.Build(
                new[] { "1_1" },
                newEncounterPoolEmpty: false,
                id => id == "1_1" ? "林中商人" : id);

            Assert.AreEqual(NpcHubButtonKind.NewEncounter, vm.Buttons[0].Kind);
            Assert.AreEqual("林中商人", vm.Buttons[1].LabelText);
        }

        [Test]
        public void NpcHubHidesNewEncounterWhenEightMetOrPoolEmpty()
        {
            var eight = new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
            var vmFull = NpcHubViewModel.Build(eight, true, id => id);
            Assert.AreEqual(8, vmFull.Buttons.Count);
            for (var i = 0; i < vmFull.Buttons.Count; i++)
                Assert.AreEqual(NpcHubButtonKind.MetNpc, vmFull.Buttons[i].Kind, $"index {i}");

            var seven = new[] { "1", "2", "3", "4", "5", "6", "7" };
            var vmLast = NpcHubViewModel.Build(seven, false, id => id);
            Assert.AreEqual(8, vmLast.Buttons.Count);
            Assert.AreEqual(NpcHubButtonKind.NewEncounter, vmLast.Buttons[0].Kind);

            var vmNoNew = NpcHubViewModel.Build(seven, true, id => id);
            Assert.AreEqual(7, vmNoNew.Buttons.Count);
            Assert.AreEqual(NpcHubButtonKind.MetNpc, vmNoNew.Buttons[0].Kind);
        }
    }
}
