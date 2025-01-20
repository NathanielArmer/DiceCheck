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
    private static IHost? _staticHost;
    private static IPlaywright? _staticPlaywright;
    private static IBrowser? _staticBrowser;
    private static readonly object _lock = new object();
    private static bool _initialized;
    private static string? _serverAddress;

    private IBrowserContext? _context;
    public IPage? Page { get; private set; }

    public string ServerAddress
    {
        get
        {
            EnsureServer();
            return _serverAddress ?? throw new InvalidOperationException("Server address not initialized");
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        if (_staticHost == null)
        {
            lock (_lock)
            {
                if (_staticHost == null)
                {
                    builder.ConfigureWebHost(webHostBuilder => webHostBuilder
                        .UseKestrel()
                        .UseUrls("http://127.0.0.1:0"));

                    _staticHost = builder.Build();
                    _staticHost.Start();

                    var server = _staticHost.Services.GetRequiredService<IServer>();
                    var addresses = server.Features.Get<IServerAddressesFeature>();
                    
                    if (addresses == null || !addresses.Addresses.Any())
                        throw new InvalidOperationException("No server addresses available.");

                    _serverAddress = addresses.Addresses
                        .Select(x => new Uri(x))
                        .Last()
                        .ToString();
                }
            }
        }

        return testHost;
    }

    private void EnsureServer()
    {
        if (_staticHost == null)
        {
            using var _ = CreateDefaultClient();
        }
    }

    private async Task EnsureServerReady()
    {
        EnsureServer();
        
        using var client = new HttpClient();
        var maxAttempts = 3;
        var currentAttempt = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (currentAttempt < maxAttempts)
        {
            try
            {
                var response = await client.GetAsync(_serverAddress);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Ignore and retry
            }

            currentAttempt++;
            if (currentAttempt < maxAttempts)
                await Task.Delay(delay);
        }

        throw new Exception("Server failed to start after multiple attempts");
    }

    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    _staticPlaywright = Playwright.CreateAsync().GetAwaiter().GetResult();
                    _staticBrowser = _staticPlaywright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = !System.Diagnostics.Debugger.IsAttached,
                        Args = new[] { "--no-sandbox" }
                    }).GetAwaiter().GetResult();
                    _initialized = true;
                }
            }
        }

        await EnsureServerReady();

        // Create a new context for each test
        _context = await _staticBrowser!.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            JavaScriptEnabled = true,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });
        
        Page = await _context.NewPageAsync();
        
        // Set longer timeouts for tests
        Page.SetDefaultTimeout(5000);
        Page.SetDefaultNavigationTimeout(5000);
    }

    public new async Task DisposeAsync()
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

        await base.DisposeAsync();
    }

    public static async Task GlobalTeardown()
    {
        if (_staticBrowser != null)
        {
            await _staticBrowser.CloseAsync();
            await _staticBrowser.DisposeAsync();
            _staticBrowser = null;
        }

        _staticPlaywright?.Dispose();
        _staticPlaywright = null;

        if (_staticHost != null)
        {
            await _staticHost.StopAsync();
            _staticHost.Dispose();
            _staticHost = null;
        }

        _initialized = false;
    }
}
