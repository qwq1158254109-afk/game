using NUnit.Framework;
using StudentGrowth;

namespace StudentGrowth.Tests
{
    public class MapInteractionPromptFormatterTests
    {
        [Test]
        public void DefaultPromptIncludesDebugToggleWhenDebugHidden()
        {
            string prompt = MapInteractionPromptFormatter.BuildDefaultPrompt(false);

            Assert.AreEqual("靠近可互动位置后按 E 交互。", prompt);
        }

        [Test]
        public void ExitPromptReturnsToStageMapWithoutTimeCost()
        {
            Assert.AreEqual("按 E：返回阶段地图｜不消耗时间", MapInteractionPromptFormatter.BuildExitPrompt());
        }

        [Test]
        public void UnavailablePromptIncludesTargetName()
        {
            Assert.AreEqual("按 E：书桌｜当前没有可执行行动", MapInteractionPromptFormatter.BuildUnavailablePrompt("书桌"));
        }

        [Test]
        public void ActivityPromptIncludesCostAndCompactedRewardPreview()
        {
            string prompt = MapInteractionPromptFormatter.BuildActivityPrompt("早点休息", 1, "\n效果：精力 +2，压力 -1");

            Assert.AreEqual("按 E：早点休息｜消耗 1 小时｜效果：精力 +2，压力 -1", prompt);
        }

        [Test]
        public void ActivityPromptClampsNegativeTimeCost()
        {
            string prompt = MapInteractionPromptFormatter.BuildActivityPrompt("返回", -1, string.Empty);

            Assert.AreEqual("按 E：返回｜不消耗时间", prompt);
        }
    }
}
