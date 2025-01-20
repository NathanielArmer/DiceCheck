using System.Text.RegularExpressions;
using Microsoft.Playwright;
using System.Web;
using Xunit;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiceCheck.Web.UITests;

[Collection("Playwright")]
public class UITests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage Page => _fixture.Page ?? throw new InvalidOperationException("Page not initialized");

    public UITests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private async Task NavigateToPage()
    {
        await Page.GotoAsync(_fixture.ServerAddress, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    }

    private async Task WaitForReactLoad()
    {
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for React to render the main components
        await Page.WaitForSelectorAsync("input#sides");
        await Page.WaitForSelectorAsync("input#numberOfDice");
    }

    private async Task WaitForPageLoad()
    {
        await NavigateToPage();
        await WaitForReactLoad();
    }

    [Fact]
    public async Task HomePage_LoadsSuccessfully()
    {
        await WaitForPageLoad();

        // Check that the title is present
        var titleText = await Page.TextContentAsync("h1");
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
        await Page.FillAsync("input#sides", sides.ToString());

        // Set the number of dice
        await Page.FillAsync("input#numberOfDice", numberOfDice.ToString());

        // Click the roll button
        await Page.ClickAsync("button:text('Roll Dice')");

        try {
            // Wait for network request to complete
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Add console log listener for debugging
            Page.Console += (_, msg) => Console.WriteLine($"Browser Console: {msg.Text}");

            // Wait for either dice values or error message
            var element = await Page.WaitForSelectorAsync("[data-testid='dice-value'], [data-testid='error-message']");
            var elementType = await element.GetAttributeAsync("data-testid");

            if (elementType == "error-message")
            {
                var errorText = await element.TextContentAsync();
                throw new Exception($"Test failed: Received error message: {errorText}");
            }

            // Verify the page content is still visible
            var title = await Page.QuerySelectorAsync("h1");
            Assert.NotNull(title);
            
            // Check that we have the correct number of dice values
            var diceValues = await Page.QuerySelectorAllAsync("[data-testid='dice-value']");
            Assert.Equal(numberOfDice, diceValues.Count);

            // Check that each value is within the correct range
            foreach (var diceValue in diceValues)
            {
                var value = await diceValue.TextContentAsync();
                Assert.NotNull(value);
                var number = int.Parse(value ?? "0");
                Assert.InRange(number, 1, sides);
            }

            // Check that the sum is correct
            var sumText = await Page.TextContentAsync("[data-testid='sum']");
            Assert.NotNull(sumText);
            var sum = int.Parse((sumText ?? "Sum: 0").Replace("Sum: ", ""));
            
            var total = 0;
            foreach (var diceValue in diceValues)
            {
                var value = await diceValue.TextContentAsync();
                total += int.Parse(value ?? "0");
            }
            Assert.Equal(total, sum);
        }
        catch (Exception ex)
        {
            // Take a screenshot on failure
            await Page.ScreenshotAsync(new() { Path = "test-failure.png" });
            throw;
        }
    }

    [Theory]
    [InlineData(0, 1, "Number of sides must be positive")]
    [InlineData(-1, 1, "Number of sides must be positive")]
    [InlineData(6, 0, "Number of dice must be positive")]
    [InlineData(6, -1, "Number of dice must be positive")]
    public async Task RollDice_WithInvalidInput_ShowsError(int sides, int numberOfDice, string expectedError)
    {
        await WaitForPageLoad();

        // Add a condition since we're testing validation
        await Page.ClickAsync("button:text('Add Condition')");
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "10");

        // Set invalid values
        await Page.FillAsync("input#sides", sides.ToString());
        await Page.FillAsync("input#numberOfDice", numberOfDice.ToString());

        // Click roll button
        await Page.ClickAsync("button:text('Roll Dice')");

        // Wait for error
        var errorElement = await Page.WaitForSelectorAsync("[data-testid='error-message']");
        var errorText = await errorElement?.TextContentAsync();
        Assert.Equal(expectedError, errorText);
    }

    [Fact]
    public async Task RollDice_WithSumGreaterThanCondition_ShowsConditionResult()
    {
        await WaitForPageLoad();

        // Set basic roll parameters
        await Page.FillAsync("input#sides", "6");
        await Page.FillAsync("input#numberOfDice", "3");

        // Add and configure the condition
        await Page.ClickAsync("button:text('Add Condition')");
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "10");

        // Click the roll button
        await Page.ClickAsync("button:text('Roll Dice')");

        // Wait for the results
        await Page.WaitForSelectorAsync("[data-testid='dice-value']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check that the condition result is shown
        var conditionResults = await Page.QuerySelectorAllAsync("[data-testid='condition-result']");
        Assert.Single(conditionResults);

        var conditionText = await conditionResults[0].TextContentAsync();
        Assert.Contains("Sum Greater Than", conditionText ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RollDice_WithMultipleConditions_ShowsAllConditionResults()
    {
        await WaitForPageLoad();

        // Add first condition
        await Page.ClickAsync("button:text('Add Condition')");
        await Page.SelectOptionAsync("[data-testid='conditionType']", "countMatching");
        await Page.FillAsync("[data-testid='conditionValue']", "4");
        await Page.FillAsync("[data-testid='conditionCount']", "2");

        // Add and configure second condition
        await Page.ClickAsync("button:text('Add Condition')");
        await Page.WaitForSelectorAsync("[data-testid='conditionType'] >> nth=1");
        
        var conditionTypes = await Page.QuerySelectorAllAsync("[data-testid='conditionType']");
        await conditionTypes[1].SelectOptionAsync("sumGreaterThan");
        
        var conditionValues = await Page.QuerySelectorAllAsync("[data-testid='conditionValue']");
        await conditionValues[1].FillAsync("8");

        // Click roll button
        await Page.ClickAsync("button:text('Roll Dice')");

        // Wait for results
        await Page.WaitForSelectorAsync("[data-testid='dice-value']");

        // Get and verify condition results
        var conditionResults = await Page.QuerySelectorAllAsync("[data-testid='condition-result']");
        Assert.Equal(2, conditionResults.Count);

        var conditionTexts = new List<string>();
        foreach (var result in conditionResults)
        {
            var text = await result.TextContentAsync();
            if (text != null)
            {
                conditionTexts.Add(text);
            }
        }

        // Check for expected condition results
        Assert.Contains(conditionTexts, t => t.Contains("Sum greater than 8", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(conditionTexts, t => t.Contains("Exactly 2 dice showing 4", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadFromQueryString_LoadsConditionType()
    {
        // Navigate with query parameters
        var queryString = "?sides=8&numberOfDice=3&conditionType=sumGreaterThan&conditionValue=15";
        await Page.GotoAsync(_fixture.ServerAddress + queryString);
        await WaitForReactLoad();

        // Get values from form inputs
        var sides = await Page.InputValueAsync("input#sides");
        var numberOfDice = await Page.InputValueAsync("input#numberOfDice");
        var conditionType = await Page.InputValueAsync("[data-testid='conditionType']");
        var conditionValue = await Page.InputValueAsync("[data-testid='conditionValue']");

        Assert.Equal("8", sides);
        Assert.Equal("3", numberOfDice);
        Assert.Equal("sumGreaterThan", conditionType);
        Assert.Equal("15", conditionValue);
    }

    [Fact]
    public async Task UrlUpdatesWithUserInput()
    {
        await WaitForPageLoad();

        // Set values
        await Page.FillAsync("input#sides", "10");
        await Page.FillAsync("input#numberOfDice", "4");

        // Add and configure condition
        await Page.ClickAsync("button:text('Add Condition')");
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "20");

        // Wait for URL to update
        await Page.WaitForURLAsync(url => 
            url.Contains("sides=10") && 
            url.Contains("numberOfDice=4") && 
            url.Contains("conditionType=sumGreaterThan") && 
            url.Contains("conditionValue=20")
        );

        var url = Page.Url;
        Assert.Contains("sides=10", url);
        Assert.Contains("numberOfDice=4", url);
        Assert.Contains("conditionType=sumGreaterThan", url);
        Assert.Contains("conditionValue=20", url);
    }

    [Fact]
    public async Task UrlUpdates_WhenConditionsRemoved()
    {
        await WaitForPageLoad();

        // Add an initial condition
        await Page.ClickAsync("button:text('Add Condition')");
        await Page.SelectOptionAsync("[data-testid='conditionType']", "sumGreaterThan");
        await Page.FillAsync("[data-testid='conditionValue']", "10");

        // Wait for URL to update
        await Page.WaitForURLAsync(url => 
            url.Contains("conditionType=sumGreaterThan") && 
            url.Contains("conditionValue=10")
        );

        // Remove the condition
        var removeButtons = await Page.QuerySelectorAllAsync("button:text('Remove')");
        await removeButtons[0].ClickAsync();

        // Wait for URL to update
        await Page.WaitForURLAsync(url => 
            !url.Contains("conditionType=sumGreaterThan") && 
            !url.Contains("conditionValue=10")
        );

        var url = Page.Url;
        Assert.DoesNotContain("conditionType=sumGreaterThan", url);
        Assert.DoesNotContain("conditionValue=10", url);
    }
}