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

public static class DependencyInjection
{
    public static IServiceCollection AddMemoryApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITool, GetTimeTool>();
        services.AddScoped<ITool, SearchMemoriesTool>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddScoped<IToolAuditService, ToolAuditService>();
        services.AddScoped<IToolRuntime, ToolRuntime>();
        services.AddSingleton<MemoryPolicyRuntime>();
        services.AddSingleton<MemoryAiConnectionRuntime>();
        services.AddSingleton<IMemoryPolicyService, MemoryPolicyService>();
        services.AddScoped<IChatModelResolver, ChatModelResolver>();
        services.AddScoped<IMemoryAiConnectionService, MemoryAiConnectionService>();
        services.AddScoped<IMemoryDatabaseConnectionService, MemoryDatabaseConnectionService>();
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

        return services;
    }
}
