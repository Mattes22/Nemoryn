namespace Memory.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Memory.Application.Abstractions.AI;
using Memory.Application.Abstractions.Persistence;
using Memory.Application.Configuration;
using Memory.Application.Runtime;
using Memory.Application.Tools;
using Memory.Application.ToolsGateway.Web;
using Memory.Infrastructure.AI;
using Memory.Infrastructure.Ingestion;
using Memory.Infrastructure.Persistence;
using Memory.Infrastructure.Retention;
using Memory.Infrastructure.ToolsGateway;
using Pgvector.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IServiceCollection AddMemoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MemoryDatabase")
            ?? throw new InvalidOperationException("Connection string 'MemoryDatabase' is not configured.");

        services.AddSingleton<IMemoryAiConnectionStore>(serviceProvider =>
            new FileMemoryAiConnectionStore(
                Path.Combine(ResolveDataDirectory(serviceProvider, configuration), FileMemoryAiConnectionStore.FileName)));
        services.AddSingleton<IMemoryDatabaseConnectionStore>(serviceProvider =>
            new FileMemoryDatabaseConnectionStore(
                Path.Combine(ResolveDataDirectory(serviceProvider, configuration), FileMemoryDatabaseConnectionStore.FileName)));
        services.AddSingleton<IToolsConnectionStore>(serviceProvider =>
            new FileToolsConnectionStore(
                Path.Combine(ResolveDataDirectory(serviceProvider, configuration), FileToolsConnectionStore.FileName)));
        services.AddSingleton(serviceProvider =>
            MemoryAiConnectionRuntime.FromStore(serviceProvider.GetRequiredService<IMemoryAiConnectionStore>()));
        services.AddSingleton(serviceProvider =>
            ToolsConnectionRuntime.FromStore(serviceProvider.GetRequiredService<IToolsConnectionStore>()));
        services.AddSingleton(serviceProvider =>
            MemoryDatabaseConnectionRuntime.Create(
                serviceProvider.GetRequiredService<IMemoryDatabaseConnectionStore>(),
                connectionString));
        services.AddSingleton<IMemoryDatabaseGateway, MemoryDatabaseGateway>();

        services.AddOptions<MemoryAiOptions>()
            .Bind(configuration.GetSection(MemoryAiOptions.SectionName))
            .PostConfigure<MemoryPolicyRuntime, MemoryAiConnectionRuntime>(ConfigureMemoryAiOptions)
            .Validate(options =>
            {
                return options.EmbeddingDimensions == MemoryAiOptions.SupportedEmbeddingDimensions;
            }, $"MemoryAi:EmbeddingDimensions must be {MemoryAiOptions.SupportedEmbeddingDimensions} because the database column is vector({MemoryAiOptions.SupportedEmbeddingDimensions}).")
            .ValidateOnStart();

        services.AddDbContext<MemoryDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<MemoryDatabaseConnectionRuntime>().DataSource;

            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(MemoryDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure();
                npgsqlOptions.UseVector();
            });
        });

        services.AddScoped<IMemoryStore, MemoryStore>();
        services.AddHostedService<MemoryIngestionHostedService>();
        services.AddHostedService<MemoryRetentionHostedService>();
        AddMemoryAi(services, configuration);
        AddExternalHttpTools(services, configuration);
        AddToolsGateway(services, configuration);

        return services;
    }

    private static void AddToolsGateway(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ToolsOptions>()
            .Bind(configuration.GetSection(ToolsOptions.SectionName))
            .PostConfigure<ToolsConnectionRuntime>((options, runtime) => runtime.ApplyTo(options));

        var searchProvider = configuration[$"{ToolsOptions.SectionName}:Web:SearchProvider"] ?? "SearXNG";
        if (!string.IsNullOrWhiteSpace(searchProvider)
            && !string.Equals(searchProvider, "SearXNG", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported Tools:Web:SearchProvider '{searchProvider}'. Supported: SearXNG.");
        }

        services.AddSingleton<IHostAddressResolver, DnsHostAddressResolver>();
        services.AddScoped<IRawHttpFetcher, RawHttpFetcher>();
        services.AddScoped<IWebSearchProvider, SearXngWebSearchProvider>();
        services.AddSingleton<ISearXngReachabilityProbe, HttpSearXngReachabilityProbe>();

        services.AddHttpClient(SearXngWebSearchProvider.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient(HttpSearXngReachabilityProbe.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(2);
        });

        services.AddHttpClient(RawHttpFetcher.HttpClientName, (serviceProvider, client) =>
        {
            client.Timeout = serviceProvider.GetRequiredService<IOptions<ToolsOptions>>().Value.Web.Fetch.Timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(RawHttpFetcher.UserAgent);
        }).ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var timeout = serviceProvider.GetRequiredService<IOptions<ToolsOptions>>().Value.Web.Fetch.Timeout;
            return PublicNetworkSockets.Create(timeout);
        });
    }

    private static void AddExternalHttpTools(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExternalToolsOptions>()
            .Bind(configuration.GetSection(ExternalToolsOptions.SectionName));

        services.AddHttpClient(HttpTool.HttpClientName, client =>
        {
            client.Timeout = HttpTool.RequestTimeout;
        });

        var definitions = configuration
            .GetSection($"{ExternalToolsOptions.SectionName}:Tools")
            .Get<List<ExternalToolDefinition>>() ?? [];

        foreach (var definition in definitions)
        {
            _ = HttpTool.BuildDefinition(definition, out _, out _);
            var snapshot = definition;
            services.AddSingleton<ITool>(serviceProvider =>
            {
                var client = serviceProvider
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient(HttpTool.HttpClientName);
                return HttpTool.Create(snapshot, client);
            });
        }
    }

    private static void AddMemoryAi(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{MemoryAiOptions.SectionName}:Provider"] ?? "Ollama";

        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            AddMemoryAiHttpClients(services);
            services.AddScoped<IMemoryExtractor, OpenAiMemoryExtractor>();
            services.AddScoped<IEmbeddingProvider, OpenAiEmbeddingProvider>();
            services.AddScoped<IContradictionDetector, OpenAiContradictionDetector>();
            services.AddScoped<IChatCompletionProvider, OpenAiChatCompletionProvider>();
            services.AddScoped<IAiRuntimeProbe, OpenAiAiRuntimeProbe>();
            services.AddScoped<IAiModelCatalog, OpenAiAiModelCatalog>();
            return;
        }

        if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            AddMemoryAiHttpClients(services);
            services.AddScoped<IMemoryExtractor, OllamaMemoryExtractor>();
            services.AddScoped<IEmbeddingProvider, OllamaEmbeddingProvider>();
            services.AddScoped<IContradictionDetector, OllamaContradictionDetector>();
            services.AddScoped<IChatCompletionProvider, OllamaChatCompletionProvider>();
            services.AddScoped<IAiRuntimeProbe, OllamaAiRuntimeProbe>();
            services.AddScoped<IAiModelCatalog, OllamaAiModelCatalog>();
            return;
        }

        services.AddSingleton<IMemoryExtractor, NullMemoryExtractor>();
        services.AddSingleton<IEmbeddingProvider, NullEmbeddingProvider>();
        services.AddSingleton<IContradictionDetector, NullContradictionDetector>();
        services.AddSingleton<IChatCompletionProvider, NullChatCompletionProvider>();
        services.AddSingleton<IAiRuntimeProbe, NullAiRuntimeProbe>();
        services.AddSingleton<IAiModelCatalog, NullAiModelCatalog>();
    }

    private static void AddMemoryAiHttpClients(IServiceCollection services)
    {
        services.AddTransient<MemoryAiRequestHandler>();
        services.AddHttpClient("MemoryAi", (serviceProvider, client) =>
        {
            ConfigureMemoryAiClient(client, TimeSpan.FromSeconds(120));
        }).AddHttpMessageHandler<MemoryAiRequestHandler>();
        services.AddHttpClient("MemoryAiStream", (serviceProvider, client) =>
        {
            ConfigureMemoryAiClient(client, TimeSpan.FromMinutes(10));
        }).AddHttpMessageHandler<MemoryAiRequestHandler>();
        services.AddHttpClient("MemoryAiProbe", (serviceProvider, client) =>
        {
            ConfigureMemoryAiClient(client, TimeSpan.FromSeconds(2));
        }).AddHttpMessageHandler<MemoryAiRequestHandler>();
    }

    private static void ConfigureMemoryAiClient(HttpClient client, TimeSpan timeout)
    {
        client.BaseAddress = new Uri(MemoryAiRequestHandler.PlaceholderBase);
        client.Timeout = timeout;
    }

    private static void ConfigureMemoryAiOptions(
        MemoryAiOptions options,
        MemoryPolicyRuntime policyRuntime,
        MemoryAiConnectionRuntime connectionRuntime)
    {
        connectionRuntime.ApplyTo(options);
        NormalizeMemoryAiOptions(options);
        var policy = policyRuntime.Override ?? MemoryPolicyPresets.Normalize(options.Policy);
        MemoryPolicyPresets.Apply(options, policy);
    }

    private static void NormalizeMemoryAiOptions(MemoryAiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            options.Provider = "None";
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            options.BaseUrl = "http://localhost:11434";
        }

        if (string.IsNullOrWhiteSpace(options.ChatModel))
        {
            options.ChatModel = "llama3.1";
        }

        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            options.EmbeddingModel = "nomic-embed-text";
        }

        if (options.EmbeddingDimensions <= 0)
        {
            options.EmbeddingDimensions = MemoryAiOptions.DefaultEmbeddingDimensions;
        }

        if (options.RetentionBatchSize <= 0)
        {
            options.RetentionBatchSize = 50;
        }
        else if (options.RetentionBatchSize > 200)
        {
            options.RetentionBatchSize = 200;
        }

        if (options.RetentionPollSeconds < 30)
        {
            options.RetentionPollSeconds = 30;
        }

        if (options.StaleCandidateAgeDays < 0)
        {
            options.StaleCandidateAgeDays = 0;
        }

        if (options.CompletedIngestionJobAgeDays < 0)
        {
            options.CompletedIngestionJobAgeDays = 0;
        }
    }

    private static string ResolveDataDirectory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var configured = configuration["NEMORYN_DATA_DIR"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;
    }
}
