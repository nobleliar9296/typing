using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class TypingSessionErrorAdvanceModeTests
{
    [TestMethod]
    public void TypingSession_DefaultOptions_WrongKeyAdvancesCursor()
    {
        var session = new TypingSession("ab");

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.IsTrue(result.WasAccepted);
        Assert.IsTrue(result.DidAdvance);
        Assert.IsFalse(result.WasRejected);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual('x', result.State.Characters[0].ActualChar);
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_WrongKeyDoesNotAdvanceCursor()
    {
        var session = CreateStrictSession("abc");

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.IsFalse(result.WasAccepted);
        Assert.IsFalse(result.WasCorrect);
        Assert.IsTrue(result.WasRejected);
        Assert.IsFalse(result.DidAdvance);
        Assert.AreEqual(0, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('x', result.State.Characters[0].ActualChar);
        Assert.IsTrue(result.State.Characters[0].HadRejectedInput);
        Assert.AreEqual("Wrong key. Try again.", result.FeedbackMessage);
        Assert.IsNotNull(result.Event);
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_CorrectKeyAfterWrongKeyAdvancesCursor()
    {
        var session = CreateStrictSession("abc");

        session.ProcessCharacter('x', timestampTicks: 0);
        var result = session.ProcessCharacter('a', timestampTicks: 1);

        Assert.IsTrue(result.WasAccepted);
        Assert.IsTrue(result.WasCorrect);
        Assert.IsFalse(result.WasRejected);
        Assert.IsTrue(result.DidAdvance);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('a', result.State.Characters[0].ActualChar);
        Assert.IsTrue(result.State.Characters[0].HadRejectedInput);
        Assert.AreEqual(CharacterState.Current, result.State.Characters[1].State);
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_WrongAttemptCountsAsIncorrectKeypress()
    {
        var session = CreateStrictSession("ab");

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.AreEqual(1, result.State.TypedCharacterKeypresses);
        Assert.AreEqual(0, result.State.CorrectCharacterKeypresses);
        Assert.AreEqual(1, result.State.IncorrectCharacterKeypresses);
        Assert.AreEqual(0, result.State.Accuracy);
        Assert.AreEqual(1, result.State.CurrentErrors);
        Assert.IsNotNull(result.Event);
        Assert.AreEqual(0, result.Event.Position);
        Assert.AreEqual('a', result.Event.ExpectedChar);
        Assert.AreEqual('x', result.Event.ActualChar);
        Assert.IsFalse(result.Event.IsCorrect);
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_WrongAttemptDoesNotCompletePosition()
    {
        var session = CreateStrictSession("a");

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.IsFalse(result.State.IsComplete);
        Assert.AreEqual(0, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('x', result.State.Characters[0].ActualChar);
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_MultipleWrongAttemptsStayOnSameCharacter()
    {
        var session = CreateStrictSession("ab");

        session.ProcessCharacter('x', timestampTicks: 0);
        var result = session.ProcessCharacter('y', timestampTicks: 1);

        Assert.AreEqual(0, result.State.CursorIndex);
        Assert.AreEqual(2, result.State.TypedCharacterKeypresses);
        Assert.AreEqual(2, result.State.IncorrectCharacterKeypresses);
        Assert.AreEqual(2, session.GetEvents().Length);
        Assert.IsTrue(session.GetEvents().All(inputEvent => inputEvent.Position == 0));
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_BackspaceAfterRejectedWrongKeyClearsRejectedKey()
    {
        var session = CreateStrictSession("ab");

        session.ProcessCharacter('a', timestampTicks: 0);
        session.ProcessCharacter('x', timestampTicks: 1);
        var result = session.ProcessBackspace(timestampTicks: 2);

        Assert.IsTrue(result.WasAccepted);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual('a', result.State.Characters[0].ActualChar);
        Assert.AreEqual(CharacterState.Correct, result.State.Characters[0].State);
        Assert.IsNull(result.State.Characters[1].ActualChar);
        Assert.AreEqual(CharacterState.Current, result.State.Characters[1].State);
        Assert.IsTrue(result.State.Characters[1].HadRejectedInput);
        Assert.AreEqual(1, result.State.IncorrectCharacterKeypresses);
        Assert.AreEqual(1, result.State.BackspaceCount);
    }

    [TestMethod]
    public void TypingSession_RequireCorrectKey_CompletesOnlyAfterCorrectCharacters()
    {
        var session = CreateStrictSession("ab");

        session.ProcessCharacter('a', timestampTicks: 0);
        session.ProcessCharacter('x', timestampTicks: 1);
        var wrong = session.ProcessCharacter('y', timestampTicks: 2);
        var complete = session.ProcessCharacter('b', timestampTicks: 3);

        Assert.IsFalse(wrong.State.IsComplete);
        Assert.IsTrue(complete.State.IsComplete);
        Assert.AreEqual(4, complete.State.TypedCharacterKeypresses);
        Assert.AreEqual(2, complete.State.CorrectCharacterKeypresses);
        Assert.AreEqual(2, complete.State.IncorrectCharacterKeypresses);
        Assert.AreEqual(0.5, complete.State.Accuracy, 0.0001);
    }

    [TestMethod]
    public void TypingSession_AdvanceOnError_ExistingBehaviorStillWorks()
    {
        var session = new TypingSession(
            "ab",
            new TypingSessionOptions(ErrorAdvanceMode.AdvanceOnError, AllowBackspace: true));

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.IsTrue(result.WasAccepted);
        Assert.IsTrue(result.DidAdvance);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('x', result.State.Characters[0].ActualChar);
        Assert.AreEqual(1, result.State.CurrentErrors);
    }

    private static TypingSession CreateStrictSession(string targetText)
    {
        return new TypingSession(
            targetText,
            new TypingSessionOptions(ErrorAdvanceMode.RequireCorrectKey, AllowBackspace: true));
    }
}
