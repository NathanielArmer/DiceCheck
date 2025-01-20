using Xunit;

namespace DiceCheck.Web.UITests;

[CollectionDefinition("Sequential", DisableParallelization = true)]
public class TestCollection : ICollectionFixture<PlaywrightFixture>
{
}
