namespace DiceCheck.Models;

public enum ConditionType
{
    /// <summary>
    /// Checks if the sum of all dice equals the target value
    /// Example: with 2d6, SumEquals 7 checks for a sum of 7
    /// </summary>
    SumEquals,

    /// <summary>
    /// Checks if the sum of all dice is greater than the target value
    /// Example: with 2d6, SumGreaterThan 9 checks for 10-12
    /// </summary>
    SumGreaterThan,

    /// <summary>
    /// Checks if the sum of all dice is less than the target value
    /// Example: with 2d6, SumLessThan 5 checks for 2-4
    /// </summary>
    SumLessThan,

    /// <summary>
    /// Checks if at least one die shows the target value
    /// Example: with 3d6, AtLeastOne 6 checks for any sixes
    /// </summary>
    AtLeastOne,

    /// <summary>
    /// Checks if all dice show the target value
    /// Example: with 3d6, All 6 checks for three sixes
    /// </summary>
    All,

    /// <summary>
    /// Checks if exactly N dice show the target value
    /// Example: with 5d6, CountMatching(2, 6) checks for exactly 2 sixes
    /// </summary>
    CountMatching
}

public record RollCondition(ConditionType Type, ConditionValue Value)
{
    public static RollCondition CreateCountMatching(int count, int value) =>
        new(ConditionType.CountMatching, new ConditionValue(value, count));

    public static RollCondition Create(ConditionType type, int value) =>
        new(type, new ConditionValue(value));

    public bool Evaluate(RollResult result) => Type switch
    {
        ConditionType.SumEquals => result.Sum == Value.Value,
        ConditionType.SumGreaterThan => result.Sum > Value.Value,
        ConditionType.SumLessThan => result.Sum < Value.Value,
        ConditionType.AtLeastOne => result.Contains(Value.Value),
        ConditionType.All => result.All(x => x == Value.Value),
        ConditionType.CountMatching => Value.Count.HasValue && result.Count(x => x == Value.Value) == Value.Count.Value,
        _ => throw new ArgumentException($"Unknown condition type: {Type}")
    };

    public override string ToString() => Type switch
    {
        ConditionType.SumEquals => $"Sum equals {Value.Value}",
        ConditionType.SumGreaterThan => $"Sum greater than {Value.Value}",
        ConditionType.SumLessThan => $"Sum less than {Value.Value}",
        ConditionType.AtLeastOne => $"At least one die showing {Value.Value}",
        ConditionType.All => $"All dice showing {Value.Value}",
        ConditionType.CountMatching => Value.Count.HasValue ? 
            $"Exactly {Value.Count.Value} dice showing {Value.Value}" :
            $"Count matching {Value.Value}",
        _ => base.ToString()!
    };
}
