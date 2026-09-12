namespace Memory.Infrastructure.Persistence;

using Memory.Application.Runtime;
using Npgsql;
using Pgvector;

internal sealed class MemoryDatabaseConnectionRuntime : IDisposable, IAsyncDisposable
{
    private readonly object gate = new();
    private readonly List<NpgsqlDataSource> retired = [];
    private NpgsqlDataSource dataSource;
    private bool disposed;

    public MemoryDatabaseConnectionRuntime(MemoryDatabaseConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings;
        dataSource = Build(settings);
    }

    public MemoryDatabaseConnectionSettings Current { get; private set; }

    public NpgsqlDataSource DataSource
    {
        get
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return dataSource;
            }
        }
    }

    public static MemoryDatabaseConnectionRuntime Create(
        IMemoryDatabaseConnectionStore store,
        string fallbackConnectionString)
    {
        ArgumentNullException.ThrowIfNull(store);

        var settings = store.Load() ?? MemoryDatabaseConnectionString.Parse(fallbackConnectionString);
        return new MemoryDatabaseConnectionRuntime(settings);
    }

    public void Apply(MemoryDatabaseConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var next = Build(settings);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            retired.Add(dataSource);
            dataSource = next;
            Current = settings;
        }
    }

    public void Dispose()
    {
        List<NpgsqlDataSource> toDispose;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            toDispose = TakeAllSources();
        }

        foreach (var source in toDispose)
        {
            source.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<NpgsqlDataSource> toDispose;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            toDispose = TakeAllSources();
        }

        foreach (var source in toDispose)
        {
            await source.DisposeAsync();
        }
    }

    private List<NpgsqlDataSource> TakeAllSources()
    {
        var sources = new List<NpgsqlDataSource>(retired.Count + 1) { dataSource };
        sources.AddRange(retired);
        retired.Clear();
        return sources;
    }

    private static NpgsqlDataSource Build(MemoryDatabaseConnectionSettings settings)
    {
        var builder = new NpgsqlDataSourceBuilder(MemoryDatabaseConnectionString.Build(settings));
        builder.UseVector();
        return builder.Build();
    }
}
