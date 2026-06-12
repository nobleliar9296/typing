namespace TypingTrainer.Core.Lessons;

public sealed class WeightedSampler<T>
{
    private readonly Random _random;

    public WeightedSampler(int? randomSeed = null)
    {
        _random = randomSeed is int seed ? new Random(seed) : new Random();
    }

    public T Sample(IReadOnlyList<(T Item, double Weight)> weightedItems)
    {
        if (weightedItems.Count == 0)
        {
            throw new ArgumentException("At least one item is required.", nameof(weightedItems));
        }

        var totalWeight = weightedItems.Sum(item => Math.Max(0, item.Weight));
        if (totalWeight <= 0)
        {
            return weightedItems[0].Item;
        }

        var target = _random.NextDouble() * totalWeight;
        var running = 0.0;

        foreach (var (item, weight) in weightedItems)
        {
            running += Math.Max(0, weight);
            if (running >= target)
            {
                return item;
            }
        }

        return weightedItems[^1].Item;
    }

    public int Next(int minValue, int maxValue)
    {
        return _random.Next(minValue, maxValue);
    }

    public bool Chance(double probability)
    {
        return _random.NextDouble() < probability;
    }
}
