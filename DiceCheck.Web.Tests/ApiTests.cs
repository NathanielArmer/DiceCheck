using System.Net;
using System.Text;
using System.Text.Json;
using DiceCheck.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DiceCheck.Web.Tests;

public class ApiTests : IClassFixture<DiceCheckWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiTests(DiceCheckWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Index_ReturnsRedirect()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<!DOCTYPE html>", content); // Verify we got HTML content
    }

    [Fact]
    public async Task Get_Index_PreservesQueryString()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var queryString = "?sides=20&numberOfDice=3";

        // Act
        var response = await client.GetAsync("/" + queryString);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(queryString, response.RequestMessage.RequestUri.Query);
    }

    [Theory]
    [InlineData(6, 2)]
    [InlineData(20, 1)]
    [InlineData(4, 4)]
    public async Task Post_Roll_ReturnsValidResults(int sides, int numberOfDice)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new RollRequest
        {
            Sides = sides,
            NumberOfDice = numberOfDice
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var resultJson = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {resultJson}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = JsonSerializer.Deserialize<RollResponse>(resultJson, _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(numberOfDice, result.Values.Count);
        Assert.All(result.Values, value => Assert.InRange(value, 1, sides));
        Assert.Equal(result.Values.Sum(), result.Sum);
    }

    [Theory]
    [InlineData(0, 1)] // Invalid sides
    [InlineData(6, 0)] // Invalid number of dice
    [InlineData(-1, 1)] // Negative sides
    [InlineData(6, -1)] // Negative number of dice
    public async Task Post_Roll_WithInvalidInput_ReturnsBadRequest(int sides, int numberOfDice)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new RollRequest
        {
            Sides = sides,
            NumberOfDice = numberOfDice
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("sumEquals", 10)]
    [InlineData("sumGreaterThan", 10)]
    [InlineData("sumLessThan", 10)]
    [InlineData("atLeastOne", 6)]
    [InlineData("all", 6)]
    public async Task Post_Roll_WithSimpleConditions_ReturnsValidResults(string type, int value)
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new
        {
            Sides = 6,
            NumberOfDice = 3,
            Conditions = new object[]
            {
                new { Type = type, Value = value }
            }
        });
        Console.WriteLine($"Request: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<RollResponseWithConditions>(responseContent, _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(3, result.Values.Count);
        Assert.All(result.Values, value => Assert.InRange(value, 1, 6));
        Assert.Equal(result.Values.Sum(), result.Sum);
        Assert.Single(result.Conditions);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Post_Roll_WithCountMatchingCondition_ReturnsValidResults(int count)
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new
        {
            Sides = 6,
            NumberOfDice = 5,
            Conditions = new object[]
            {
                new { Type = "countMatching", Value = 6, Count = count }
            }
        });
        Console.WriteLine($"Request: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<RollResponseWithConditions>(responseContent, _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(5, result.Values.Count);
        Assert.All(result.Values, value => Assert.InRange(value, 1, 6));
        Assert.Equal(result.Values.Sum(), result.Sum);
        Assert.Single(result.Conditions);
    }

    [Fact]
    public async Task Post_Roll_WithMultipleConditions_ReturnsValidResults()
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new
        {
            Sides = 6,
            NumberOfDice = 4,
            Conditions = new object[]
            {
                new { Type = "sumGreaterThan", Value = 10 },
                new { Type = "atLeastOne", Value = 6 },
                new { Type = "countMatching", Value = 4, Count = 2 }
            }
        });
        Console.WriteLine($"Request: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<RollResponseWithConditions>(responseContent, _jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(4, result.Values.Count);
        Assert.All(result.Values, value => Assert.InRange(value, 1, 6));
        Assert.Equal(result.Values.Sum(), result.Sum);
        Assert.Equal(3, result.Conditions.Count);
    }

    [Theory]
    [InlineData("SUMEQUALS")] // All caps
    [InlineData("sumEquals")] // Camel case
    [InlineData("SumEquals")] // Pascal case
    public async Task Post_Roll_WithDifferentCaseConditions_ReturnsValidResults(string type)
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new
        {
            Sides = 6,
            NumberOfDice = 3,
            Conditions = new object[]
            {
                new { Type = type, Value = 10 }
            }
        });
        Console.WriteLine($"Request: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<RollResponseWithConditions>(responseContent, _jsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Conditions);
    }

    [Theory]
    [InlineData("invalidType")] // Non-existent type
    [InlineData("")] // Empty string
    [InlineData("null")] // Null string
    public async Task Post_Roll_WithInvalidConditionType_ReturnsBadRequest(string type)
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new
        {
            Sides = 6,
            NumberOfDice = 3,
            Conditions = new object[]
            {
                new { Type = type == "null" ? null : type, Value = 10 }
            }
        });
        Console.WriteLine($"Request: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Roll_WithMissingCountForCountMatching_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new
        {
            Sides = 6,
            NumberOfDice = 5,
            Conditions = new object[]
            {
                new { Type = "countMatching", Value = 6 } // Missing Count property
            }
        });
        Console.WriteLine($"Request: {json}");
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Roll_WithInvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = "{\"Sides\":6,\"NumberOfDice\":3,\"Conditions\":[{\"Type\":\"sumEquals\",\"Value\":10,";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Roll_WithMissingProperty_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var json = "{\"Sides\":6,\"Conditions\":[{\"Type\":\"sumEquals\",\"Value\":10}]}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/roll", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private class RollResponse
    {
        public List<int> Values { get; set; } = new();
        public int Sum { get; set; }
    }

    private class RollResponseWithConditions : RollResponse
    {
        public List<ConditionResult> Conditions { get; set; } = new();
    }

    private class ConditionResult
    {
        public string Condition { get; set; } = "";
        public bool Satisfied { get; set; }
    }
}
