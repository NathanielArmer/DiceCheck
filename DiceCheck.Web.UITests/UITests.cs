using Microsoft.Playwright;
using System.Web;
using Xunit;

namespace DiceCheck.Web.UITests;

[Collection("UI Tests")]
public class UITests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage Page => _fixture.Page ?? throw new InvalidOperationException("Page not initialized");

    public UITests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task NavigateToPage()
    {
        await Page.GotoAsync(_fixture.ServerAddress, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task WaitForJavaScriptLoad()
    {
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for JavaScript initialization with a timeout
        await Page.WaitForFunctionAsync(@"() => {
            const sides = document.getElementById('sides');
            const numberOfDice = document.getElementById('numberOfDice');
            return sides && numberOfDice && typeof window.getQueryParams === 'function';
        }", new PageWaitForFunctionOptions { Timeout = 10000 });

        // Wait for query params to be loaded
        await Page.WaitForFunctionAsync(@"() => {
            return window._queryParamsLoaded === true;
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });
    }

    private async Task WaitForPageLoad()
    {
        await NavigateToPage();
        await WaitForJavaScriptLoad();
    }

    [Fact]
    public async Task HomePage_LoadsSuccessfully()
    {
        await WaitForPageLoad();

        // Check that the title is present
        var titleText = await Page.InnerTextAsync("h1");
        Assert.Equal("Dice Roller", titleText);
    }

    [Theory]
    [InlineData(6, 3)]
    [InlineData(20, 1)]
    [InlineData(4, 4)]
    public async Task RollDice_WithoutConditions_ShowsResults(int sides, int numberOfDice)
    {
        await WaitForPageLoad();

        // Set the number of sides
        await Page.FillAsync("#sides", sides.ToString());

        // Set the number of dice
        await Page.FillAsync("#numberOfDice", numberOfDice.ToString());

        // Remove the default condition
        await Page.ClickAsync("#conditions >> button:has-text('Remove')");

        // Click the roll button
        await Page.ClickAsync("#rollButton");

        // Wait for the results
        await Page.WaitForSelectorAsync("#results:not(.hidden)");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check that we have the correct number of dice values
        var diceValues = await Page.QuerySelectorAllAsync(".dice-value");
        Assert.Equal(numberOfDice, diceValues.Count);

        // Check that each value is within the correct range
        for (int i = 0; i < diceValues.Count; i++)
        {
            var value = await Page.InnerTextAsync($".dice-value >> nth={i}");
            Assert.NotNull(value);
            var number = int.Parse(value);
            Assert.InRange(number, 1, sides);
        }

        // Check that the sum is correct
        var sumText = await Page.InnerTextAsync("#sum");
        Assert.NotNull(sumText);
        var sum = int.Parse(sumText.Replace("Sum: ", ""));
        
        var total = 0;
        for (int i = 0; i < diceValues.Count; i++)
        {
            var value = await Page.InnerTextAsync($".dice-value >> nth={i}");
            Assert.NotNull(value);
            total += int.Parse(value);
        }
        Assert.Equal(total, sum);
    }

    [Theory]
    [InlineData(0, 1, "Number of sides must be positive")]
    [InlineData(-1, 1, "Number of sides must be positive")]
    [InlineData(6, 0, "Number of dice must be positive")]
    [InlineData(6, -1, "Number of dice must be positive")]
    public async Task RollDice_WithInvalidInput_ShowsError(int sides, int numberOfDice, string expectedError)
    {
        await WaitForPageLoad();

        // Set invalid values
        await Page.FillAsync("#sides", sides.ToString());
        await Page.FillAsync("#numberOfDice", numberOfDice.ToString());

        // Click roll button
        await Page.ClickAsync("#rollButton");

        // Wait for error
        await Page.WaitForSelectorAsync("#error:not(.hidden)");

        // Verify error message
        var errorText = await Page.InnerTextAsync("#error");
        Assert.Equal(expectedError, errorText);
    }

    [Fact]
    public async Task RollDice_WithSumGreaterThanCondition_ShowsConditionResult()
    {
        await WaitForPageLoad();

        // Set basic roll parameters
        await Page.FillAsync("#sides", "6");
        await Page.FillAsync("#numberOfDice", "3");

        // Configure the default condition
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "10");

        // Click the roll button
        await Page.ClickAsync("#rollButton");

        // Wait for the results
        await Page.WaitForSelectorAsync("#results:not(.hidden)");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check that the condition result is shown
        var conditionResults = await Page.QuerySelectorAllAsync(".condition-result");
        Assert.Single(conditionResults);

        var conditionText = await Page.InnerTextAsync(".condition-result");
        Assert.NotNull(conditionText);
        Assert.Contains("Sum Greater Than", conditionText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RollDice_WithMultipleConditions_ShowsAllConditionResults()
    {
        await WaitForPageLoad();

        // Add first condition
        await Page.SelectOptionAsync("[data-testid='conditionType']", "countMatching");
        await Page.FillAsync("[data-testid='conditionValue']", "4");
        await Page.FillAsync("[data-testid='conditionCount']", "2");

        // Add second condition
        await Page.ClickAsync("#addCondition");
        await Page.WaitForSelectorAsync("#conditions > div:nth-child(2)");
        var secondConditionType = await Page.QuerySelectorAllAsync("[data-testid='conditionType']");
        await secondConditionType[1].SelectOptionAsync("sumGreaterThan");
        var secondConditionValue = await Page.QuerySelectorAllAsync("[data-testid='conditionValue']");
        await secondConditionValue[1].FillAsync("8");

        // Click roll button
        await Page.ClickAsync("#rollButton");

        // Wait for results
        await Page.WaitForSelectorAsync("#results:not(.hidden)");

        // Get all condition text
        var conditionResults = await Page.QuerySelectorAllAsync(".condition-result");
        var allConditionText = "";
        foreach (var result in conditionResults)
        {
            var text = await result.InnerTextAsync();
            allConditionText += text + "\n";
        }

        // Verify both conditions are shown
        Assert.Contains("Exactly 2 dice showing 4", allConditionText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sum greater than 8", allConditionText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadFromQueryString_SetsCorrectValues()
    {
        // Navigate to page with query parameters
        var conditionsJson = """[{"type":"sumGreaterThan","value":15},{"type":"countMatching","value":6,"count":2}]""";
        // Use JavaScript's encodeURIComponent equivalent
        var encodedConditions = Uri.EscapeDataString(conditionsJson);
        var url = $"{_fixture.ServerAddress}?sides=20&numberOfDice=4&conditions={encodedConditions}";
        
        // Now navigate to our actual page
        var response = await Page.GotoAsync(url, new PageGotoOptions { 
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        if (response == null)
        {
            throw new Exception("Navigation failed - no response");
        }

        try {
            // Wait for basic page load
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for our elements to be present
            await Page.WaitForSelectorAsync("#sides");
            await Page.WaitForSelectorAsync("#numberOfDice");

            // Wait for query params to be loaded and values set
            await Page.WaitForFunctionAsync(@"() => {
                const sidesInput = document.getElementById('sides');
                const numberOfDiceInput = document.getElementById('numberOfDice');
                return window._queryParamsLoaded === true;
            }", new PageWaitForFunctionOptions { 
                Timeout = 10000,
                PollingInterval = 100 // Check more frequently
            });

            // Verify input values
            var sidesValue = await Page.InputValueAsync("#sides");
            var diceValue = await Page.InputValueAsync("#numberOfDice");
            
            Assert.Equal("20", sidesValue);
            Assert.Equal("4", diceValue);

            // Verify conditions
            var conditionTypes = await Page.QuerySelectorAllAsync("[data-testid='conditionType']");
            var conditionValues = await Page.QuerySelectorAllAsync("[data-testid='conditionValue']");
            var conditionCounts = await Page.QuerySelectorAllAsync("[data-testid='conditionCount']");

            Assert.Equal(2, conditionTypes.Count);
            
            // Verify first condition
            Assert.Equal("sumGreaterThan", await conditionTypes[0].InputValueAsync());
            Assert.Equal("15", await conditionValues[0].InputValueAsync());
            
            // Verify second condition
            Assert.Equal("countMatching", await conditionTypes[1].InputValueAsync());
            Assert.Equal("6", await conditionValues[1].InputValueAsync());
            Assert.Equal("2", await conditionCounts[1].InputValueAsync());
            Assert.False(await conditionCounts[1].IsHiddenAsync());
        }
        catch
        {
            throw;
        }
    }

    [Fact]
    public async Task UrlUpdatesWithUserInput()
    {
        await WaitForPageLoad();

        // Set values
        await Page.FillAsync("#sides", "12");
        await Page.FillAsync("#numberOfDice", "3");

        // Add a condition
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "10");

        // Wait for URL to update
        await Page.WaitForFunctionAsync(@"() => {
            const params = new URLSearchParams(window.location.search);
            return params.get('sides') === '12' && 
                   params.get('numberOfDice') === '3' &&
                   params.get('conditions') !== null;
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });

        // Get URL parameters
        var url = await Page.EvaluateAsync<string>("window.location.search");
        var query = System.Web.HttpUtility.ParseQueryString(url);

        // Verify URL parameters
        Assert.Equal("12", query["sides"]);
        Assert.Equal("3", query["numberOfDice"]);

        // Parse conditions from URL
        var conditionsJson = System.Web.HttpUtility.UrlDecode(query["conditions"] ?? "[]");
        var conditions = System.Text.Json.JsonSerializer.Deserialize<List<object>>(conditionsJson) ?? new List<object>();
        Assert.Single(conditions);
    }

    [Fact]
    public async Task UrlUpdates_WhenConditionsRemoved()
    {
        await WaitForPageLoad();

        // Add first condition
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "10");
        
        // Add second condition
        await Page.ClickAsync("#addCondition");
        await Page.WaitForFunctionAsync(@"() => {
            const count = document.querySelectorAll('#conditions > div').length;
            return count === 2;
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });

        // Set second condition values
        var conditions = await Page.QuerySelectorAllAsync("#conditions > div");
        var secondCondition = conditions[1];
        var typeSelect = await secondCondition.QuerySelectorAsync("[data-testid='conditionType']");
        var valueInput = await secondCondition.QuerySelectorAsync("[data-testid='conditionValue']");
        
        await typeSelect!.SelectOptionAsync("sumGreaterThan");
        await valueInput!.FillAsync("15");
        
        await Page.WaitForFunctionAsync(@"() => window._queryParamsLoaded === true");

        // Remove the first condition
        await Page.ClickAsync("#conditions >> button:has-text('Remove')");
        await Page.WaitForFunctionAsync(@"() => {
            try {
                const params = new URLSearchParams(window.location.search);
                const conditions = JSON.parse(params.get('conditions') || '[]');
                return Array.isArray(conditions) && conditions.length === 1;
            } catch (e) {
                return false;
            }
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });

        // Remove the second condition
        await Page.ClickAsync("#conditions >> button:has-text('Remove')");
        await Page.WaitForFunctionAsync(@"() => {
            try {
                const params = new URLSearchParams(window.location.search);
                const conditions = JSON.parse(params.get('conditions') || '[]');
                return Array.isArray(conditions) && conditions.length === 0;
            } catch (e) {
                return false;
            }
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });
    }
}