namespace Memory.Application.Tests.Fakes;

using Memory.Application.Tools;

internal static class TestToolRuntime
{
    public static ToolRuntime Create(
        IToolRegistry registry,
        FakeMemoryStore? store = null,
        TimeProvider? time = null)
    {
        return new ToolRuntime(
            registry,
            new ToolAuditService(store ?? new FakeMemoryStore(), time ?? TimeProvider.System));
    }

    public static ToolRuntime Create(
        IEnumerable<ITool> tools,
        FakeMemoryStore? store = null,
        TimeProvider? time = null)
    {
        return Create(new ToolRegistry(tools), store, time);
    }
}
