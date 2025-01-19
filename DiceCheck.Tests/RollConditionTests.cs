using DiceCheck.Models;
using Xunit;

namespace DiceCheck.Tests;

public class RollConditionTests
{
    [Fact]
    public void SumEquals_WithMatchingSum_ReturnsTrue()
    {
        // Arrange
        var condition = RollCondition.Create(ConditionType.SumEquals, 7);
        var result = new RollResult(new[] { 3, 4 });

        // Act
        var matches = condition.Evaluate(result);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void SumEquals_WithNonMatchingSum_ReturnsFalse()
    {
        var condition = RollCondition.Create(ConditionType.SumEquals, 7);
        var result = new RollResult(new[] { 2, 4 });
        Assert.False(condition.Evaluate(result));
    }

    [Fact]
    public void SumGreaterThan_WithGreaterSum_ReturnsTrue()
    {
        var condition = RollCondition.Create(ConditionType.SumGreaterThan, 7);
        var result = new RollResult(new[] { 4, 4 });
        Assert.True(condition.Evaluate(result));
    }

    [Fact]
    public void SumGreaterThan_WithLesserSum_ReturnsFalse()
    {
        var condition = RollCondition.Create(ConditionType.SumGreaterThan, 7);
        var result = new RollResult(new[] { 3, 3 });
        Assert.False(condition.Evaluate(result));
    }

    [Fact]
    public void SumLessThan_WithLesserSum_ReturnsTrue()
    {
        var condition = RollCondition.Create(ConditionType.SumLessThan, 7);
        var result = new RollResult(new[] { 3, 3 });
        Assert.True(condition.Evaluate(result));
    }

    [Fact]
    public void SumLessThan_WithGreaterSum_ReturnsFalse()
    {
        var condition = RollCondition.Create(ConditionType.SumLessThan, 7);
        var result = new RollResult(new[] { 4, 4 });
        Assert.False(condition.Evaluate(result));
    }

    [Fact]
    public void AtLeastOne_WithMatchingValue_ReturnsTrue()
    {
        var condition = RollCondition.Create(ConditionType.AtLeastOne, 6);
        var result = new RollResult(new[] { 2, 6, 4 });
        Assert.True(condition.Evaluate(result));
    }

    [Fact]
    public void AtLeastOne_WithNoMatchingValue_ReturnsFalse()
    {
        var condition = RollCondition.Create(ConditionType.AtLeastOne, 6);
        var result = new RollResult(new[] { 2, 3, 4 });
        Assert.False(condition.Evaluate(result));
    }

    [Fact]
    public void All_WithAllMatching_ReturnsTrue()
    {
        var condition = RollCondition.Create(ConditionType.All, 6);
        var result = new RollResult(new[] { 6, 6, 6 });
        Assert.True(condition.Evaluate(result));
    }

    [Fact]
    public void All_WithSomeNotMatching_ReturnsFalse()
    {
        var condition = RollCondition.Create(ConditionType.All, 6);
        var result = new RollResult(new[] { 6, 5, 6 });
        Assert.False(condition.Evaluate(result));
    }

    [Fact]
    public void CountMatching_WithExactMatch_ReturnsTrue()
    {
        var condition = RollCondition.CreateCountMatching(2, 6);
        var result = new RollResult(new[] { 6, 3, 6, 4, 1 });
        Assert.True(condition.Evaluate(result));
    }

    [Fact]
    public void CountMatching_WithTooFewMatches_ReturnsFalse()
    {
        var condition = RollCondition.CreateCountMatching(2, 6);
        var result = new RollResult(new[] { 6, 3, 4, 4, 1 });
        Assert.False(condition.Evaluate(result));
    }

    [Fact]
    public void CountMatching_WithTooManyMatches_ReturnsFalse()
    {
        var condition = RollCondition.CreateCountMatching(2, 6);
        var result = new RollResult(new[] { 6, 6, 6, 4, 1 });
        Assert.False(condition.Evaluate(result));
    }
}
