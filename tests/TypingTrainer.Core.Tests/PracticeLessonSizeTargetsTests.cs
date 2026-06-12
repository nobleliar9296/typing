using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Lessons;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class PracticeLessonSizeTargetsTests
{
    [TestMethod]
    public void PracticeLessonSizeTargets_SmallUsesConfiguredShortLength()
    {
        Assert.AreEqual(220, PracticeLessonSizeTargets.GetTargetCharacters(PracticeLessonSize.Small, 220));
    }

    [TestMethod]
    public void PracticeLessonSizeTargets_MediumUsesAboutTwoHundredFiftyWords()
    {
        Assert.AreEqual(1_250, PracticeLessonSizeTargets.GetTargetCharacters(PracticeLessonSize.Medium, 220));
    }

    [TestMethod]
    public void PracticeLessonSizeTargets_LongUsesAboutOneThousandWords()
    {
        Assert.AreEqual(5_000, PracticeLessonSizeTargets.GetTargetCharacters(PracticeLessonSize.Long, 220));
    }
}
