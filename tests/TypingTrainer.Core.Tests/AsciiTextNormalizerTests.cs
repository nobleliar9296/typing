using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class AsciiTextNormalizerTests
{
    [TestMethod]
    public void AsciiTextNormalizer_ConvertsSmartQuotesAndAccents()
    {
        var text = "\u201cCaf\u00E9 notes\u201d don\u2019t stop\u2014they continue\u2026";

        var normalized = AsciiTextNormalizer.ToAscii(text);

        Assert.AreEqual("\"Cafe notes\" don't stop-they continue...", normalized);
        Assert.IsTrue(normalized.All(character => character <= 0x7F));
    }
}
