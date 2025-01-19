namespace DiceCheck.Models;

public record ConditionValue
{
    public int Value { get; init; }
    public int? Count { get; init; }

    public ConditionValue(int value, int? count = null)
    {
        Value = value;
        Count = count;
    }

    public override string ToString() => Count.HasValue 
        ? $"{Count} {(Count == 1 ? "time" : "times")} the value {Value}"
        : Value.ToString();
}
