using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Controls;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class PracticeTextPresenterTests
{
    [TestMethod]
    public void GetDisplayText_NewlineShowsEnterGlyphAndLineBreak()
    {
        var character = Snapshot('\n', CharacterState.Current);

        var displayText = PracticeTextPresenter.GetDisplayText(character, showSpaceDots: false);

        Assert.AreEqual("\u21B5\n", displayText);
    }

    [TestMethod]
    public void GetDisplayText_IncorrectSpaceShowsVisibleDotWhenSpaceDotsAreOff()
    {
        var character = Snapshot(' ', CharacterState.Incorrect);

        var displayText = PracticeTextPresenter.GetDisplayText(character, showSpaceDots: false);

        Assert.AreEqual("\u00B7", displayText);
    }

    [TestMethod]
    public void GetDisplayText_CorrectedSpaceShowsVisibleDotWhenSpaceDotsAreOff()
    {
        var character = Snapshot(' ', CharacterState.Correct, hadIncorrectInput: true);

        var displayText = PracticeTextPresenter.GetDisplayText(character, showSpaceDots: false);

        Assert.AreEqual("\u00B7", displayText);
    }

    [TestMethod]
    public void GetDisplayText_PendingSpaceStaysBlankWhenSpaceDotsAreOff()
    {
        var character = Snapshot(' ', CharacterState.Pending);

        var displayText = PracticeTextPresenter.GetDisplayText(character, showSpaceDots: false);

        Assert.AreEqual(" ", displayText);
    }

    [TestMethod]
    public void GetDisplayText_PendingSpaceShowsDotWhenSpaceDotsAreOn()
    {
        var character = Snapshot(' ', CharacterState.Pending);

        var displayText = PracticeTextPresenter.GetDisplayText(character, showSpaceDots: true);

        Assert.AreEqual("\u00B7", displayText);
    }

    [TestMethod]
    public void BuildCharacterLayout_PutsNewlineAtEndOfCurrentLine()
    {
        var characters = new[]
        {
            Snapshot('a', CharacterState.Correct),
            Snapshot('b', CharacterState.Correct),
            Snapshot('\n', CharacterState.Current),
            Snapshot('c', CharacterState.Pending)
        };

        var layout = PracticeTextPresenter.BuildCharacterLayout(characters, maxColumns: 12);

        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 0), layout[0]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 1), layout[1]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 2), layout[2]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(1, 0), layout[3]);
    }

    [TestMethod]
    public void BuildCharacterLayout_PutsBoundaryNewlineAfterLastCharacter()
    {
        var characters = new[]
        {
            Snapshot('a', CharacterState.Correct),
            Snapshot('b', CharacterState.Correct),
            Snapshot('\n', CharacterState.Current),
            Snapshot('c', CharacterState.Pending)
        };

        var layout = PracticeTextPresenter.BuildCharacterLayout(characters, maxColumns: 2);

        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 0), layout[0]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 1), layout[1]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 2), layout[2]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(1, 0), layout[3]);
    }

    [TestMethod]
    public void BuildCharacterLayout_PutsBlankLineNewlineAtColumnZero()
    {
        var characters = new[]
        {
            Snapshot('a', CharacterState.Correct),
            Snapshot('\n', CharacterState.Correct),
            Snapshot('\n', CharacterState.Current),
            Snapshot('b', CharacterState.Pending)
        };

        var layout = PracticeTextPresenter.BuildCharacterLayout(characters, maxColumns: 12);

        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 0), layout[0]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(0, 1), layout[1]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(1, 0), layout[2]);
        Assert.AreEqual(new PracticeTextPresenter.CharacterLayoutPosition(2, 0), layout[3]);
    }

    private static CharacterSnapshot Snapshot(
        char expectedChar,
        CharacterState state,
        bool hadIncorrectInput = false)
    {
        return new CharacterSnapshot(
            Position: 0,
            ExpectedChar: expectedChar,
            ActualChar: null,
            State: state,
            HadRejectedInput: false,
            HadIncorrectInput: hadIncorrectInput);
    }
}
