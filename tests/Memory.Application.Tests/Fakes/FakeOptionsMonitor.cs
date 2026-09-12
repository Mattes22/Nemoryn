namespace Memory.Application.Tests.Fakes;

using Microsoft.Extensions.Options;

internal sealed class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    where T : class
{
    public T CurrentValue { get; set; } = currentValue;

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
