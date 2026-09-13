namespace Memory.Application;

using Microsoft.Extensions.DependencyInjection;
using Memory.Application.Agent;
using Memory.Application.Configuration;
using Memory.Application.OpenAiCompatible;
using Memory.Application.Context;
using Memory.Application.Conversations;
using Memory.Application.Ingestion;
using Memory.Application.Memories;
using Memory.Application.Abstractions.AI;
using Memory.Application.Owners;
using Memory.Application.Runtime;
using Memory.Application.Tools;
using Memory.Application.ToolsGateway;

public static class DependencyInjection
{
    public static IServiceCollection AddMemoryApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Memory.Application.Tools.ITool, GetTimeTool>();
        services.AddScoped<Memory.Application.Tools.ITool, SearchMemoriesTool>();
        services.AddScoped<Memory.Application.Tools.ITool, WebSearchAgentTool>();
        services.AddScoped<Memory.Application.Tools.ITool, WebFetchAgentTool>();
        services.AddScoped<Memory.Application.Tools.IToolRegistry, Memory.Application.Tools.ToolRegistry>();
        services.AddScoped<IToolAuditService, ToolAuditService>();
        services.AddScoped<IToolRuntime, ToolRuntime>();
        services.AddSingleton<MemoryPolicyRuntime>();
        services.AddSingleton<MemoryAiConnectionRuntime>();
        services.AddSingleton<IMemoryPolicyService, MemoryPolicyService>();
        services.AddScoped<IChatModelResolver, ChatModelResolver>();
        services.AddScoped<IMemoryAiConnectionService, MemoryAiConnectionService>();
        services.AddScoped<IMemoryDatabaseConnectionService, MemoryDatabaseConnectionService>();
        services.AddScoped<IToolsConnectionService, ToolsConnectionService>();
        services.AddSingleton<ToolsConnectionRuntime>();
        services.AddSingleton<IRuntimeWorkerPulse, RuntimeWorkerPulse>();
        services.AddSingleton<AgentSystemPromptBuilder>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IOwnerDirectoryService, OwnerDirectoryService>();
        services.AddScoped<IContextBuilder, ContextBuilder>();
        services.AddScoped<IAgentChatService, AgentChatService>();
        services.AddScoped<IOpenAiCompatibleChatService, OpenAiCompatibleChatService>();
        services.AddScoped<IIngestionJobService, IngestionJobService>();
        services.AddScoped<IAgentMemoryService, AgentMemoryService>();
        services.AddScoped<IMemoryCandidateService, MemoryCandidateService>();
        services.AddScoped<IMemoryCleanupService, MemoryCleanupService>();
        services.AddScoped<IMemoryConflictService, MemoryConflictService>();
        services.AddScoped<IMemoryAuditService, MemoryAuditService>();
        services.AddScoped<IMemoryReviewService, MemoryReviewService>();
        services.AddScoped<IMemoryRetentionService, MemoryRetentionService>();
        services.AddScoped<IMemoryRecallEvaluationService, MemoryRecallEvaluationService>();
        services.AddScoped<IMemoryBackupService, MemoryBackupService>();
        services.AddScoped<IRuntimeStatusService, RuntimeStatusService>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IMemoryIngestionService, MemoryIngestionService>();
        services.AddNemorynToolsGateway();

        return services;
    }
}
