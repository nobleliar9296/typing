using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class ParagraphLessonGeneratorTests
{
    [TestMethod]
    public void ParagraphLessonGenerator_UsesOnlyUnlockedCharacters_WhenRestricted()
    {
        var profile = SkillProfileDefaults.Empty(new DateTime(2026, 6, 11));
        var generator = CreateGenerator(new BuiltInParagraphProvider());

        var result = generator.Generate(profile, Options(randomSeed: 1));

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
        Assert.IsTrue(result.Reason.StartsWith("Paragraph practice", StringComparison.Ordinal));

        foreach (var character in result.Text)
        {
            Assert.IsTrue(
                character == ' ' || result.UnlockedCharacters.Contains(character),
                $"Generated locked character: '{character}' in lesson '{result.Text}'");
        }
    }

    [TestMethod]
    public void ParagraphLessonGenerator_ReturnsFallback_WhenNoParagraphMatches()
    {
        var profile = SkillProfileDefaults.Empty(new DateTime(2026, 6, 11));
        var generator = CreateGenerator(new LockedParagraphProvider());

        var result = generator.Generate(profile, Options(randomSeed: 2));

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
        Assert.AreNotEqual(FixedLessonGenerator.FixedLessonText, result.Text);
        Assert.IsTrue(result.Text.All(character => character == ' ' || result.UnlockedCharacters.Contains(character)));
    }

    private static ParagraphLessonGenerator CreateGenerator(IPracticeContentProvider provider)
    {
        return new ParagraphLessonGenerator(
            provider,
            new BuiltInWordListProvider(),
            new CharacterUnlockPlanner());
    }

    private static LessonGenerationOptions Options(int? randomSeed = null)
    {
        return new LessonGenerationOptions(
            LessonMode.Paragraph,
            LessonLengthKind.Characters,
            120,
            KeyboardLayoutRepository.Qwerty,
            randomSeed);
    }

    private sealed class LockedParagraphProvider : IPracticeContentProvider
    {
        public IReadOnlyList<PracticeContentItem> GetContentItems()
        {
            return
            [
                ContentAnalyzer.CreateParagraph(
                    "locked",
                    "Locked",
                    "zebra quote needs many locked keys",
                    "test",
                    "test")
            ];
        }
    }
}
