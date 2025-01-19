namespace DiceCheck.Models;

public record DiceRoll(int Sides, int Count)
{
    private static readonly Random Random = new();

    public RollResult Roll()
    {
        var values = new List<int>();
        for (int i = 0; i < Count; i++)
        {
            values.Add(Random.Next(1, Sides + 1));
        }
        return new RollResult(values);
    }
}

public record RollResult(IReadOnlyList<int> Values)
{
    public int Sum => Values.Sum();
    public bool Contains(int value) => Values.Contains(value);
    public bool All(Func<int, bool> predicate) => Values.All(predicate);
    public int Count(Func<int, bool> predicate) => Values.Count(predicate);
}
