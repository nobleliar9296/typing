using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class TypingSessionTests
{
    [TestMethod]
    public void CorrectCharacterAdvancesCursorAndMarksPositionCorrect()
    {
        var session = new TypingSession("ab");

        var result = session.ProcessCharacter('a', timestampTicks: 0);

        Assert.IsTrue(result.WasAccepted);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Correct, result.State.Characters[0].State);
        Assert.AreEqual(CharacterState.Current, result.State.Characters[1].State);
        Assert.AreEqual(1, result.State.TypedCharacterKeypresses);
        Assert.AreEqual(1, result.State.CorrectCharacterKeypresses);
    }

    [TestMethod]
    public void IncorrectCharacterAdvancesCursorAndMarksPositionIncorrect()
    {
        var session = new TypingSession("ab");

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.IsTrue(result.WasAccepted);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('x', result.State.Characters[0].ActualChar);
        Assert.AreEqual(1, result.State.CurrentErrors);
        Assert.AreEqual(1, result.State.IncorrectCharacterKeypresses);
    }

    [TestMethod]
    public void RequireCorrectKey_WrongCharacterShowsRejectedInputWithoutAdvancing()
    {
        var session = CreateRequireCorrectSession("ab");

        var result = session.ProcessCharacter('x', timestampTicks: 0);

        Assert.IsFalse(result.WasAccepted);
        Assert.IsTrue(result.WasRejected);
        Assert.AreEqual(0, result.State.CursorIndex);
        Assert.AreEqual('a', result.State.CurrentExpectedCharacter);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('x', result.State.Characters[0].ActualChar);
        Assert.IsTrue(result.State.Characters[0].HadRejectedInput);
        Assert.AreEqual(1, result.State.CurrentErrors);
        Assert.AreEqual(1, result.State.TypedCharacterKeypresses);
        Assert.AreEqual(0, result.State.CorrectCharacterKeypresses);
        Assert.AreEqual(1, result.State.IncorrectCharacterKeypresses);
    }

    [TestMethod]
    public void RequireCorrectKey_BackspaceClearsRejectedInputAndRestoresCurrentCharacter()
    {
        var session = CreateRequireCorrectSession("ab");

        session.ProcessCharacter('x', timestampTicks: 0);
        var result = session.ProcessBackspace(timestampTicks: Stopwatch.Frequency / 2);

        Assert.IsTrue(result.WasAccepted);
        Assert.AreEqual(0, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Current, result.State.Characters[0].State);
        Assert.IsNull(result.State.Characters[0].ActualChar);
        Assert.IsTrue(result.State.Characters[0].HadRejectedInput);
        Assert.AreEqual(0, result.State.CurrentErrors);
        Assert.AreEqual(1, result.State.BackspaceCount);
        Assert.IsNotNull(result.Event);
        Assert.AreEqual(InputEventKind.Backspace, result.Event.Kind);
        Assert.AreEqual(0, result.Event.Position);
        Assert.AreEqual('a', result.Event.ExpectedChar);
        Assert.AreEqual('x', result.Event.ActualChar);
        Assert.IsTrue(result.Event.WasCorrection);
    }

    [TestMethod]
    public void RequireCorrectKey_CorrectCharacterAfterRejectedInputAdvancesAndRemainsError()
    {
        var session = CreateRequireCorrectSession("ab");

        session.ProcessCharacter('x', timestampTicks: 0);
        var result = session.ProcessCharacter('a', timestampTicks: Stopwatch.Frequency);

        Assert.IsTrue(result.WasAccepted);
        Assert.IsFalse(result.WasRejected);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('a', result.State.Characters[0].ActualChar);
        Assert.IsTrue(result.State.Characters[0].HadRejectedInput);
        Assert.AreEqual(1, result.State.CurrentErrors);
        Assert.AreEqual(2, result.State.TypedCharacterKeypresses);
        Assert.AreEqual(1, result.State.CorrectCharacterKeypresses);
        Assert.AreEqual(1, result.State.IncorrectCharacterKeypresses);
        Assert.AreEqual(0.5, result.State.Accuracy, 0.0001);
        Assert.IsNotNull(result.Event);
        Assert.IsTrue(result.Event.IsCorrect);
        Assert.IsTrue(result.Event.WasCorrection);
    }

    [TestMethod]
    public void RequireCorrectKey_CorrectCharacterAfterBackspaceRemainsError()
    {
        var session = CreateRequireCorrectSession("ab");

        session.ProcessCharacter('x', timestampTicks: 0);
        session.ProcessBackspace(timestampTicks: Stopwatch.Frequency / 2);
        var result = session.ProcessCharacter('a', timestampTicks: Stopwatch.Frequency);

        Assert.IsTrue(result.WasAccepted);
        Assert.AreEqual(1, result.State.CursorIndex);
        Assert.AreEqual(CharacterState.Incorrect, result.State.Characters[0].State);
        Assert.AreEqual('a', result.State.Characters[0].ActualChar);
        Assert.IsTrue(result.State.Characters[0].HadRejectedInput);
        Assert.AreEqual(1, result.State.CurrentErrors);
        Assert.AreEqual(1, result.State.BackspaceCount);
    }

    [TestMethod]
    public void BackspaceRemovesPreviousPositionAndAllowsCorrection()
    {
        var session = new TypingSession("ab");

        session.ProcessCharacter('x', timestampTicks: 0);
        var backspace = session.ProcessBackspace(timestampTicks: Stopwatch.Frequency / 2);
        var correction = session.ProcessCharacter('a', timestampTicks: Stopwatch.Frequency);

        Assert.IsTrue(backspace.WasAccepted);
        Assert.IsTrue(correction.WasAccepted);
        Assert.AreEqual(1, correction.State.CursorIndex);
        Assert.AreEqual(CharacterState.Correct, correction.State.Characters[0].State);
        Assert.AreEqual(0, correction.State.CurrentErrors);
        Assert.AreEqual(2, correction.State.TypedCharacterKeypresses);
        Assert.AreEqual(1, correction.State.CorrectCharacterKeypresses);
        Assert.AreEqual(1, correction.State.IncorrectCharacterKeypresses);
        Assert.AreEqual(0.5, correction.State.Accuracy, 0.0001);
        Assert.IsNotNull(correction.Event);
        Assert.IsTrue(correction.Event.WasCorrection);
    }

    [TestMethod]
    public void RawWpmUsesFiveCharacterWordFormula()
    {
        var session = new TypingSession("abcde");

        session.ProcessCharacter('a', timestampTicks: 0);
        session.ProcessCharacter('b', timestampTicks: 15 * Stopwatch.Frequency);
        session.ProcessCharacter('c', timestampTicks: 30 * Stopwatch.Frequency);
        session.ProcessCharacter('d', timestampTicks: 45 * Stopwatch.Frequency);
        var result = session.ProcessCharacter('e', timestampTicks: 60 * Stopwatch.Frequency);

        Assert.AreEqual(1.0, result.State.RawWpm, 0.0001);
    }

    [TestMethod]
    public void AccuracyUsesCorrectCharacterKeypressesOverTotalCharacterKeypresses()
    {
        var session = new TypingSession("abc");

        session.ProcessCharacter('a', timestampTicks: 0);
        session.ProcessCharacter('x', timestampTicks: Stopwatch.Frequency);
        var result = session.ProcessCharacter('c', timestampTicks: 2 * Stopwatch.Frequency);

        Assert.AreEqual(2 / 3.0, result.State.Accuracy, 0.0001);
    }

    [TestMethod]
    public void CompleteSessionReturnsExpectedSummaryValues()
    {
        var session = new TypingSession("ab");

        session.ProcessCharacter('a', timestampTicks: 0);
        session.ProcessCharacter('x', timestampTicks: Stopwatch.Frequency);
        var summary = session.Complete(timestampTicks: Stopwatch.Frequency);

        Assert.IsTrue(summary.IsComplete);
        Assert.AreEqual(2, summary.TypedCharacterKeypresses);
        Assert.AreEqual(1, summary.CorrectCharacterKeypresses);
        Assert.AreEqual(1, summary.IncorrectCharacterKeypresses);
        Assert.AreEqual(1, summary.CurrentErrors);
        Assert.AreEqual(0.5, summary.Accuracy, 0.0001);
    }

    [TestMethod]
    public void GetEventsReturnsDefensiveSnapshot()
    {
        var session = new TypingSession("ab");

        session.ProcessCharacter('a', timestampTicks: 0);
        var firstSnapshot = session.GetEvents();
        session.ProcessCharacter('b', timestampTicks: Stopwatch.Frequency);
        var secondSnapshot = session.GetEvents();

        Assert.AreEqual(1, firstSnapshot.Length);
        Assert.AreEqual(2, secondSnapshot.Length);
        Assert.AreNotSame(firstSnapshot, secondSnapshot);
    }

    [TestMethod]
    public void TypingStateSnapshot_CurrentExpectedCharacter_ReturnsCurrentTargetCharacter()
    {
        var session = new TypingSession("ab");

        var initial = session.GetSnapshot(timestampTicks: 0);
        var afterFirstCharacter = session.ProcessCharacter('a', timestampTicks: Stopwatch.Frequency).State;

        Assert.AreEqual('a', initial.CurrentExpectedCharacter);
        Assert.AreEqual('b', afterFirstCharacter.CurrentExpectedCharacter);
    }

    [TestMethod]
    public void TypingStateSnapshot_CurrentExpectedCharacter_ReturnsNullWhenComplete()
    {
        var session = new TypingSession("a");

        var result = session.ProcessCharacter('a', timestampTicks: 0);

        Assert.IsNull(result.State.CurrentExpectedCharacter);
    }

    private static TypingSession CreateRequireCorrectSession(string targetText)
    {
        return new TypingSession(
            targetText,
            new TypingSessionOptions(
                ErrorAdvanceMode.RequireCorrectKey,
                AllowBackspace: true));
    }
}
