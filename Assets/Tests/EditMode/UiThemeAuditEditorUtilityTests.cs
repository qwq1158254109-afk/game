using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using StudentGrowth.Editor;

namespace StudentGrowth.Tests
{
    public sealed class UiThemeAuditEditorUtilityTests
    {
        [Test]
        public void CreateThemeAuditSummary_PassesConfiguredUiSurfaces()
        {
            UiThemeAuditSummary summary = UiThemeAuditEditorUtility.CreateThemeAuditSummary();

            Assert.IsTrue(summary.themeFound, "Expected StudentGrowthPixelTheme asset to be available.");
            Assert.AreEqual(0, summary.Issues.Count, BuildIssueMessage(summary.Issues));
            Assert.GreaterOrEqual(summary.buttonCount, 100);
            Assert.GreaterOrEqual(summary.panelCandidateCount, 75);
            Assert.GreaterOrEqual(summary.textCount, 300);
            Assert.AreEqual(summary.textCount, summary.pixelTextCount);
        }

        private static string BuildIssueMessage(IReadOnlyList<string> issues)
        {
            return issues == null || issues.Count == 0
                ? "No UI theme audit issues."
                : string.Join("\n", issues);
        }
    }
}
