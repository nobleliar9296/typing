using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Keyboard;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class KeyboardVisualTests
{
    [TestMethod]
    public void QwertyVisualKeyboardLayout_Create_ReturnsFiveRows()
    {
        var layout = QwertyVisualKeyboardLayout.Create();

        Assert.AreEqual(5, layout.Rows.Count);
    }

    [TestMethod]
    public void QwertyVisualKeyboardLayout_ContainsSpaceBackspaceEnterShift()
    {
        var keyIds = QwertyVisualKeyboardLayout
            .Create()
            .Rows
            .SelectMany(row => row.Keys)
            .Select(key => key.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(keyIds.Contains("Space"));
        Assert.IsTrue(keyIds.Contains("Backspace"));
        Assert.IsTrue(keyIds.Contains("Enter"));
        Assert.IsTrue(keyIds.Contains("LeftShift"));
        Assert.IsTrue(keyIds.Contains("RightShift"));
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_MapsLowercaseLetter()
    {
        var mapping = new QwertyCharacterToKeyMapper().Map('a');

        Assert.IsNotNull(mapping);
        Assert.AreEqual("KeyA", mapping.KeyId);
        Assert.IsFalse(mapping.RequiresShift);
        Assert.IsNull(mapping.ShiftKeyId);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_MapsUppercaseLetterWithShift()
    {
        var mapping = new QwertyCharacterToKeyMapper().Map('A');

        Assert.IsNotNull(mapping);
        Assert.AreEqual("KeyA", mapping.KeyId);
        Assert.IsTrue(mapping.RequiresShift);
        Assert.AreEqual("RightShift", mapping.ShiftKeyId);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_MapsSpaceToSpaceKey()
    {
        var mapping = new QwertyCharacterToKeyMapper().Map(' ');

        Assert.IsNotNull(mapping);
        Assert.AreEqual("Space", mapping.KeyId);
        Assert.IsFalse(mapping.RequiresShift);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_MapsNewlineToEnter()
    {
        var mapping = new QwertyCharacterToKeyMapper().Map('\n');

        Assert.IsNotNull(mapping);
        Assert.AreEqual("Enter", mapping.KeyId);
        Assert.IsFalse(mapping.RequiresShift);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_MapsShiftedNumberSymbols()
    {
        var mapper = new QwertyCharacterToKeyMapper();

        var exclamation = mapper.Map('!');
        var closeParenthesis = mapper.Map(')');

        Assert.IsNotNull(exclamation);
        Assert.AreEqual("Digit1", exclamation.KeyId);
        Assert.IsTrue(exclamation.RequiresShift);
        Assert.AreEqual("RightShift", exclamation.ShiftKeyId);

        Assert.IsNotNull(closeParenthesis);
        Assert.AreEqual("Digit0", closeParenthesis.KeyId);
        Assert.IsTrue(closeParenthesis.RequiresShift);
        Assert.AreEqual("LeftShift", closeParenthesis.ShiftKeyId);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_MapsPunctuation()
    {
        var mapper = new QwertyCharacterToKeyMapper();

        var question = mapper.Map('?');
        var quote = mapper.Map('"');
        var comma = mapper.Map(',');

        Assert.IsNotNull(question);
        Assert.AreEqual("Slash", question.KeyId);
        Assert.IsTrue(question.RequiresShift);
        Assert.AreEqual("LeftShift", question.ShiftKeyId);

        Assert.IsNotNull(quote);
        Assert.AreEqual("Quote", quote.KeyId);
        Assert.IsTrue(quote.RequiresShift);
        Assert.AreEqual("LeftShift", quote.ShiftKeyId);

        Assert.IsNotNull(comma);
        Assert.AreEqual("Comma", comma.KeyId);
        Assert.IsFalse(comma.RequiresShift);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_UsesOppositeHandShiftForUppercaseLeftHandKey()
    {
        var mapping = new QwertyCharacterToKeyMapper().Map('A');

        Assert.IsNotNull(mapping);
        Assert.AreEqual("RightShift", mapping.ShiftKeyId);
    }

    [TestMethod]
    public void QwertyCharacterToKeyMapper_UsesOppositeHandShiftForUppercaseRightHandKey()
    {
        var mapping = new QwertyCharacterToKeyMapper().Map('L');

        Assert.IsNotNull(mapping);
        Assert.AreEqual("LeftShift", mapping.ShiftKeyId);
    }
}
