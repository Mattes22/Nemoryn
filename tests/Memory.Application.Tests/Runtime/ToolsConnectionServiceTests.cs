namespace Memory.Application.Tests.Runtime;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Runtime;
using Memory.Application.Tests.Fakes;

public sealed class ToolsConnectionServiceTests
{
    [Fact]
    public async Task Set_normalizes_searxng_url_and_applies_immediately()
    {
        var runtime = new ToolsConnectionRuntime();
        var store = new FakeToolsConnectionStore();
        var probe = new FakeSearXngReachabilityProbe { Reachable = true };
        var service = CreateService(runtime, store, probe);

        var response = await service.SetAsync(new ToolsConnectionRequest("http://192.168.1.2:8080/search/"));

        Assert.Equal("http://192.168.1.2:8080", response.SearXngBaseUrl);
        Assert.Equal("http://192.168.1.2:8080", runtime.SearXngBaseUrl);
        Assert.True(runtime.HasSearXngBaseUrlOverride);
        Assert.True(response.Configured);
        Assert.True(response.Reachable);
        Assert.True(response.Persisted);
        Assert.Equal("http://192.168.1.2:8080", store.Settings?.SearXngBaseUrl);
        Assert.Equal("http://192.168.1.2:8080", probe.LastBaseUrl);
    }

    [Fact]
    public async Task Set_allows_empty_url_to_disable_search()
    {
        var runtime = new ToolsConnectionRuntime();
        var service = CreateService(runtime, new FakeToolsConnectionStore(), new FakeSearXngReachabilityProbe());

        var response = await service.SetAsync(new ToolsConnectionRequest("  "));

        Assert.Equal(string.Empty, response.SearXngBaseUrl);
        Assert.False(response.Configured);
        Assert.False(response.Reachable);
        Assert.True(runtime.HasSearXngBaseUrlOverride);
    }

    [Fact]
    public async Task Set_applies_even_when_persist_fails()
    {
        var runtime = new ToolsConnectionRuntime();
        var store = new FakeToolsConnectionStore { SaveSucceeds = false, SaveError = "disk full" };
        var service = CreateService(runtime, store, new FakeSearXngReachabilityProbe { Reachable = true });

        var response = await service.SetAsync(new ToolsConnectionRequest("http://127.0.0.1:8080"));

        Assert.Equal("http://127.0.0.1:8080", runtime.SearXngBaseUrl);
        Assert.False(response.Persisted);
        Assert.Equal("disk full", response.PersistError);
    }

    [Fact]
    public void FromStore_applies_saved_settings_on_startup()
    {
        var store = new FakeToolsConnectionStore
        {
            Settings = new ToolsConnectionSettings("http://host.docker.internal:8080")
        };

        var runtime = ToolsConnectionRuntime.FromStore(store);

        Assert.True(runtime.HasSearXngBaseUrlOverride);
        Assert.Equal("http://host.docker.internal:8080", runtime.SearXngBaseUrl);
    }

    private static ToolsConnectionService CreateService(
        ToolsConnectionRuntime runtime,
        FakeToolsConnectionStore store,
        FakeSearXngReachabilityProbe probe)
    {
        var seed = new ToolsOptions();
        seed.Web.SearXNG.BaseUrl = "http://127.0.0.1:8080";
        var monitor = new ApplyingOptionsMonitor(seed, runtime);
        return new ToolsConnectionService(
            monitor,
            new OptionsCache<ToolsOptions>(),
            runtime,
            store,
            probe);
    }

    private sealed class ApplyingOptionsMonitor(
        ToolsOptions seed,
        ToolsConnectionRuntime runtime) : IOptionsMonitor<ToolsOptions>
    {
        public ToolsOptions CurrentValue
        {
            get
            {
                var copy = new ToolsOptions
                {
                    DefaultCapabilities = seed.DefaultCapabilities.ToList(),
                    Web = new ToolsWebOptions
                    {
                        SearchProvider = seed.Web.SearchProvider,
                        SearXNG = new SearXngOptions { BaseUrl = seed.Web.SearXNG.BaseUrl },
                        Fetch = seed.Web.Fetch
                    }
                };
                runtime.ApplyTo(copy);
                return copy;
            }
        }

        public ToolsOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<ToolsOptions, string?> listener) => FakeDisposable.Instance;

        private sealed class FakeDisposable : IDisposable
        {
            public static readonly FakeDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
