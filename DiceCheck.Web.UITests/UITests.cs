using System.Text.RegularExpressions;
using Microsoft.Playwright;
using System.Web;
using Xunit;

namespace DiceCheck.Web.UITests;

[Collection("Sequential")]
public class UITests : IClassFixture<PlaywrightFixture>
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

    public record ConditionTestData(string Type, string Value, string? Count = null);

    public static TheoryData<ConditionTestData> ConditionTypes => new()
    {
        new ConditionTestData("sumEquals", "10"),      // 3d6 sum = 10
        new ConditionTestData("sumGreaterThan", "15"), // 3d6 sum > 15
        new ConditionTestData("sumLessThan", "5"),     // 3d6 sum < 5
        new ConditionTestData("atLeastOne", "6"),      // 3d6 at least one 6
        new ConditionTestData("all", "6"),             // 3d6 all 6's
        new ConditionTestData("countMatching", "6", "2")  // 3d6 exactly 2 sixes
    };

    [Theory]
    [MemberData(nameof(ConditionTypes))]
    public async Task RollDice_WithConditionType_ShowsResults(ConditionTestData testData)
    {
        await NavigateToPage();
        await WaitForJavaScriptLoad();

        // Set basic dice parameters
        await Page.FillAsync("#sides", "6");
        await Page.FillAsync("#numberOfDice", "3");

        // Get the existing condition container
        var container = await Page.QuerySelectorAsync("#conditions > div");
        Assert.NotNull(container);

        var typeSelect = await container.QuerySelectorAsync("[data-testid='conditionType']");
        Assert.NotNull(typeSelect);
        await typeSelect.SelectOptionAsync(testData.Type);

        var valueInput = await container.QuerySelectorAsync("[data-testid='conditionValue']");
        Assert.NotNull(valueInput);
        await valueInput.FillAsync(testData.Value);
        // Trigger input change event
        await Page.EvaluateAsync(@"() => {
            const input = document.querySelector('[data-testid=""conditionValue""]');
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        }");
        await Page.WaitForTimeoutAsync(100); // Wait for event handlers

        if (testData.Count != null)
        {
            var countInput = await container.QuerySelectorAsync("[data-testid='conditionCount']");
            Assert.NotNull(countInput);

            // Wait for count input to be visible and fill it
            await Page.WaitForSelectorAsync("[data-testid='conditionCount']:not(.hidden)");
            await countInput.FillAsync(testData.Count);
            // Trigger input change event
            await Page.EvaluateAsync(@"() => {
                const input = document.querySelector('[data-testid=""conditionCount""]');
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }");
            await Page.WaitForTimeoutAsync(100); // Wait for event handlers
        }

        // Roll the dice
        await Page.ClickAsync("#rollButton");

        // Wait for either results or error
        var resultsOrError = await Page.WaitForSelectorAsync("#results:not(.hidden), #error:not(.hidden)");
        Assert.NotNull(resultsOrError);

        // Check if we got an error
        var error = await Page.QuerySelectorAsync("#error:not(.hidden)");
        if (error != null)
        {
            var errorText = await error.TextContentAsync();
            Assert.Fail($"Got error: {errorText}");
        }

        // Verify URL contains the condition
        var url = new Uri(await Page.EvaluateAsync<string>("window.location.href"));
        var query = HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("6", query.Get("sides"));
        Assert.Equal("3", query.Get("numberOfDice"));

        var conditionTypes = query.GetValues("conditionType");
        Assert.NotNull(conditionTypes);
        Assert.Single(conditionTypes);
        Assert.Equal(testData.Type, conditionTypes[0]);

        var conditionValues = query.GetValues("conditionValue");
        Assert.NotNull(conditionValues);
        Assert.Single(conditionValues);
        Assert.Equal(testData.Value, conditionValues[0]);

        if (testData.Count != null)
        {
            var conditionCounts = query.GetValues("conditionCount");
            Assert.NotNull(conditionCounts);
            Assert.Single(conditionCounts);
            Assert.Equal(testData.Count, conditionCounts[0]);
        }

        // Verify dice values are displayed
        var diceValues = await Page.QuerySelectorAllAsync(".dice-value");
        Assert.Equal(3, diceValues.Count); // We rolled 3 dice

        // Verify sum is shown
        var sum = await Page.QuerySelectorAsync("#sum");
        Assert.NotNull(sum);
        var sumText = await sum.TextContentAsync();
        Assert.Contains("Sum:", sumText ?? string.Empty);

        // Verify condition results are shown
        var conditionResults = await Page.QuerySelectorAsync("#conditionResults");
        Assert.NotNull(conditionResults);
        var conditionResultsText = await conditionResults.TextContentAsync();
        Assert.NotNull(conditionResultsText);
        Assert.Contains("Satisfied", conditionResultsText);
    }

    public static TheoryData<ConditionTestData> QueryStringTestData => new()
    {
        new ConditionTestData("sumEquals", "10"),
        new ConditionTestData("sumGreaterThan", "15"),
        new ConditionTestData("sumLessThan", "5"),
        new ConditionTestData("atLeastOne", "6"),
        new ConditionTestData("all", "6"),
        new ConditionTestData("countMatching", "6", "2")
    };

    [Theory]
    [MemberData(nameof(QueryStringTestData))]
    public async Task LoadFromQueryString_LoadsConditionType(ConditionTestData testData)
    {
        // Construct condition JSON
        var conditionsJson = testData.Count != null 
            ? $"[{{\"type\":\"{testData.Type}\",\"value\":\"{testData.Value}\",\"count\":\"{testData.Count}\"}}]"
            : $"[{{\"type\":\"{testData.Type}\",\"value\":\"{testData.Value}\"}}]";
        
        // URL encode the conditions JSON
        var encodedConditions = Uri.EscapeDataString(conditionsJson);
        var queryString = $"?sides=6&numberOfDice=3&conditions={encodedConditions}";

        await Page.GotoAsync($"{_fixture.ServerAddress}{queryString}");
        await WaitForJavaScriptLoad();

        // Verify form values
        Assert.Equal("6", await Page.InputValueAsync("#sides"));
        Assert.Equal("3", await Page.InputValueAsync("#numberOfDice"));

        // Verify condition
        var container = await Page.QuerySelectorAsync("#conditions > div");
        Assert.NotNull(container);

        var typeSelect = await container.QuerySelectorAsync("[data-testid='conditionType']");
        var valueInput = await container.QuerySelectorAsync("[data-testid='conditionValue']");
        Assert.NotNull(typeSelect);
        Assert.NotNull(valueInput);

        var type = await typeSelect.EvaluateAsync<string>("el => el.value");
        var value = await valueInput.EvaluateAsync<string>("el => el.value");

        Assert.Equal(testData.Type, type);
        Assert.Equal(testData.Value, value);

        if (testData.Count != null)
        {
            var countInput = await container.QuerySelectorAsync("[data-testid='conditionCount']");
            Assert.NotNull(countInput);
            var count = await countInput.EvaluateAsync<string>("el => el.value");
            Assert.Equal(testData.Count, count);
            
            // Verify count input is visible
            var isHidden = await countInput.EvaluateAsync<bool>("el => el.classList.contains('hidden')");
            Assert.False(isHidden);
        }
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
                   params.get('conditionType') === 'sumGreaterThan' &&
                   params.get('conditionValue') === '10';
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });

        // Get URL parameters
        var url = await Page.EvaluateAsync<string>("window.location.search");
        var query = HttpUtility.ParseQueryString(url);

        // Verify URL parameters
        Assert.Equal("12", query["sides"]);
        Assert.Equal("3", query["numberOfDice"]);
        Assert.Equal("sumGreaterThan", query["conditionType"]);
        Assert.Equal("10", query["conditionValue"]);
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

        var secondConditionType = await Page.QuerySelectorAllAsync("[data-testid='conditionType']");
        await secondConditionType[1].SelectOptionAsync("countMatching");
        var secondConditionValue = await Page.QuerySelectorAllAsync("[data-testid='conditionValue']");
        await secondConditionValue[1].FillAsync("6");
        var countInput = await Page.QuerySelectorAllAsync("[data-testid='conditionCount']");
        await countInput[1].FillAsync("2");

        // Wait for URL to update with both conditions
        await Page.WaitForFunctionAsync(@"() => {
            const params = new URLSearchParams(window.location.search);
            const types = params.getAll('conditionType');
            const values = params.getAll('conditionValue');
            return types.length === 2 && values.length === 2;
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });

        // Get URL before removing condition
        var urlBefore = await Page.EvaluateAsync<string>("window.location.search");
        var queryBefore = HttpUtility.ParseQueryString(urlBefore);
        var typesBefore = queryBefore.GetValues("conditionType");
        var valuesBefore = queryBefore.GetValues("conditionValue");
        Assert.NotNull(typesBefore);
        Assert.NotNull(valuesBefore);
        Assert.Equal(2, typesBefore.Length);
        Assert.Equal(2, valuesBefore.Length);

        // Remove second condition
        var removeButtons = await Page.QuerySelectorAllAsync("#conditions button:has-text('Remove')");
        await removeButtons[1].ClickAsync();

        // Wait for URL to update with only one condition
        await Page.WaitForFunctionAsync(@"() => {
            const params = new URLSearchParams(window.location.search);
            const types = params.getAll('conditionType');
            const values = params.getAll('conditionValue');
            return types.length === 1 && values.length === 1;
        }", new PageWaitForFunctionOptions { 
            Timeout = 10000,
            PollingInterval = 100
        });

        // Get URL after removing condition
        var urlAfter = await Page.EvaluateAsync<string>("window.location.search");
        var queryAfter = HttpUtility.ParseQueryString(urlAfter);
        var typesAfter = queryAfter.GetValues("conditionType");
        var valuesAfter = queryAfter.GetValues("conditionValue");
        Assert.NotNull(typesAfter);
        Assert.NotNull(valuesAfter);
        Assert.Single(typesAfter);
        Assert.Single(valuesAfter);
        Assert.Equal("sumGreaterThan", typesAfter[0]);
        Assert.Equal("10", valuesAfter[0]);
    }
}