using Microsoft.Playwright;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiceCheck.Web.UITests;

public class PlaywrightFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private IHost? _host;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    public IPage? Page { get; private set; }

    public string ServerAddress
    {
        get
        {
            EnsureServer();
            var address = ClientOptions.BaseAddress.ToString();
            return address.EndsWith("/") ? address : address + "/";
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Create the host for TestServer
        var testHost = builder.Build();

        // Modify the host builder to use Kestrel instead of TestServer
        builder.ConfigureWebHost(webHostBuilder => webHostBuilder
            .UseKestrel()
            .UseUrls("http://127.0.0.1:0")); // Use port 0 for dynamic port assignment

        // Create and start the Kestrel server
        _host = builder.Build();
        _host.Start();

        // Get the server address
        var server = _host.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        
        if (addresses == null || !addresses.Addresses.Any())
            throw new InvalidOperationException("No server addresses available.");

        ClientOptions.BaseAddress = addresses.Addresses
            .Select(x => new Uri(x))
            .Last();

        // Return the TestServer host
        return testHost;
    }

    private void EnsureServer()
    {
        if (_host is null)
        {
            using var _ = CreateDefaultClient();
        }
    }

    private async Task EnsureServerReady()
    {
        EnsureServer();
        
        // Try to connect to the server
        using var client = new HttpClient();
        for (int i = 0; i < 5; i++)
        {
            try
            {
                var response = await client.GetAsync(ServerAddress);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                await Task.Delay(1000);
            }
        }
        throw new Exception("Server failed to start");
    }

    public async Task InitializeAsync()
    {
        await EnsureServerReady();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !System.Diagnostics.Debugger.IsAttached,
                Args = new[] { "--no-sandbox" }
            });
            
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                JavaScriptEnabled = true
            });
            
            Page = await _context.NewPageAsync();
            
            // Set longer timeouts for tests
            Page.SetDefaultNavigationTimeout(3000);
            Page.SetDefaultTimeout(3000);
        }
        catch
        {
            throw;
        }
    }

    public new async Task DisposeAsync()
    {
        try
        {
            if (Page != null)
            {
                await Page.CloseAsync();
                Page = null;
            }

            if (_context != null)
            {
                await _context.CloseAsync();
                await _context.DisposeAsync();
                _context = null;
            }

            if (_browser != null)
            {
                await _browser.CloseAsync();
                await _browser.DisposeAsync();
                _browser = null;
            }

            if (_playwright != null)
            {
                _playwright.Dispose();
                _playwright = null;
            }

            if (_host != null)
            {
                try
                {
                    await _host.StopAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during host shutdown: {ex.Message}");
                }
                finally
                {
                    _host.Dispose();
                    _host = null;
                }
            }

            await base.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during disposal: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
