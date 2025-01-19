using DiceCheck.Models;
using Xunit;

namespace DiceCheck.Tests;

public class DiceRollTests
{
    [Fact]
    public void Roll_ReturnsCorrectNumberOfDice()
    {
        // Arrange
        var roll = new DiceRoll(6, 3);

        // Act
        var result = roll.Roll();

        // Assert
        Assert.Equal(3, result.Values.Count);
    }

    [Fact]
    public void Roll_AllValuesWithinRange()
    {
        var roll = new DiceRoll(6, 100);
        var result = roll.Roll();

        foreach (var value in result.Values)
        {
            Assert.True(value >= 1 && value <= 6);
        }
    }

    [Fact]
    public void Roll_GeneratesDifferentValues()
    {
        var roll = new DiceRoll(6, 1000);
        var result = roll.Roll();

        // With 1000 rolls of a d6, we should see at least 5 different values
        // (probability of not seeing any particular value is (5/6)^1000, which is effectively 0)
        var distinctValues = result.Values.Distinct().Count();
        Assert.True(distinctValues >= 5);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(20)]
    public void Roll_RespectsNumberOfSides(int sides)
    {
        var roll = new DiceRoll(sides, 1000);
        var result = roll.Roll();

        foreach (var value in result.Values)
        {
            Assert.True(value >= 1 && value <= sides);
        }
    }
}

public class RollResultTests
{
    [Fact]
    public void Sum_CalculatesCorrectly()
    {
        var result = new RollResult(new[] { 2, 3, 4 });
        Assert.Equal(9, result.Sum);
    }

    [Fact]
    public void Contains_FindsExistingValue()
    {
        var result = new RollResult(new[] { 2, 3, 4 });
        Assert.True(result.Contains(3));
    }

    [Fact]
    public void Contains_DoesNotFindMissingValue()
    {
        var result = new RollResult(new[] { 2, 3, 4 });
        Assert.False(result.Contains(5));
    }

    [Fact]
    public void All_WithAllMatching_ReturnsTrue()
    {
        var result = new RollResult(new[] { 6, 6, 6 });
        Assert.True(result.All(x => x == 6));
    }

    [Fact]
    public void All_WithSomeNotMatching_ReturnsFalse()
    {
        var result = new RollResult(new[] { 6, 5, 6 });
        Assert.False(result.All(x => x == 6));
    }

    [Fact]
    public void Count_CountsCorrectly()
    {
        var result = new RollResult(new[] { 6, 3, 6, 4, 6 });
        Assert.Equal(3, result.Count(x => x == 6));
    }
}
