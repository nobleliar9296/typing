using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Lessons;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class WeightedSamplerTests
{
    [TestMethod]
    public void WeightedSampler_WithSameSeed_IsDeterministic()
    {
        var first = new WeightedSampler<string>(randomSeed: 42);
        var second = new WeightedSampler<string>(randomSeed: 42);
        var items = new[] { ("a", 1.0), ("b", 2.0), ("c", 3.0) };

        var firstSamples = Enumerable.Range(0, 8).Select(_ => first.Sample(items)).ToArray();
        var secondSamples = Enumerable.Range(0, 8).Select(_ => second.Sample(items)).ToArray();

        CollectionAssert.AreEqual(firstSamples, secondSamples);
    }
}
