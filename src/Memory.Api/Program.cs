using System.Text.Json.Serialization;
using Memory.Api;
using Memory.Api.OpenAi;
using Memory.Application;
using Memory.Application.Agent;
using Memory.Application.Configuration;
using Memory.Application.Context;
using Memory.Application.Conversations;
using Memory.Application.Ingestion;
using Memory.Application.Memories;
using Memory.Application.Owners;
using Memory.Application.Runtime;
using Memory.Application.Tools;
using Memory.Infrastructure;
using Memory.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<MemoryExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddMemoryApplication();
builder.Services.AddMemoryInfrastructure(builder.Configuration);
builder.Services.Configure<OpenAiCompatibleOptions>(
    builder.Configuration.GetSection(OpenAiCompatibleOptions.SectionName));
builder.Services.AddSingleton<OpenAiApiKeyFilter>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    var urls = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
    if (urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
    {
        app.UseHttpsRedirection();
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }))
    .WithName("GetHealth");

app.MapGet("/health/database", async (MemoryDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { Status = "Healthy" })
        : Results.Problem("Database connection failed.");
})
    .WithName("GetDatabaseHealth");

app.MapGet("/runtime/status", async (
    IRuntimeStatusService runtimeStatusService,
    CancellationToken cancellationToken) =>
{
    var status = await runtimeStatusService.GetAsync(cancellationToken);

    return Results.Ok(status);
})
    .WithName("GetRuntimeStatus");

app.MapGet("/runtime/ai-connection", async (
    IMemoryAiConnectionService connectionService,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await connectionService.GetAsync(cancellationToken));
})
    .WithName("GetRuntimeAiConnection");

app.MapGet("/owners", async (
    IOwnerDirectoryService ownerDirectoryService,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await ownerDirectoryService.ListAsync(cancellationToken));
})
    .WithName("ListOwners");

app.MapPut("/runtime/ai-connection", async (
    MemoryAiConnectionRequest request,
    IMemoryAiConnectionService connectionService,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await connectionService.SetAsync(request, cancellationToken));
})
    .WithName("SetRuntimeAiConnection");

app.MapGet("/runtime/database-connection", async (
    IMemoryDatabaseConnectionService connectionService,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await connectionService.GetAsync(cancellationToken));
})
    .WithName("GetRuntimeDatabaseConnection");

app.MapPut("/runtime/database-connection", async (
    MemoryDatabaseConnectionRequest request,
    IMemoryDatabaseConnectionService connectionService,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await connectionService.SetAsync(request, cancellationToken));
})
    .WithName("SetRuntimeDatabaseConnection");

app.MapGet("/tools", (IToolRegistry toolRegistry) => Results.Ok(toolRegistry.Definitions))
    .WithName("GetTools");

app.MapGet("/tool-permission-profiles", () => Results.Ok(ToolPermissionCatalog.All))
    .WithName("GetToolPermissionProfiles");

app.MapPost("/conversations", async (
    CreateConversationRequest request,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversation = await conversationService.CreateAsync(request, cancellationToken);

    return Results.Created($"/conversations/{conversation.Id}", conversation);
})
    .WithName("CreateConversation");

app.MapPut("/conversations/by-external-id/{externalId}", async (
    string externalId,
    UpsertConversationRequest request,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var result = await conversationService.UpsertByExternalIdAsync(externalId, request, cancellationToken);

    return result.Created
        ? Results.Created($"/conversations/{result.Conversation.Id}", result.Conversation)
        : Results.Ok(result.Conversation);
})
    .WithName("UpsertConversationByExternalId");

app.MapGet("/conversations/{conversationId:guid}", async (
    Guid conversationId,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversation = await conversationService.GetAsync(conversationId, cancellationToken);

    return conversation is null ? Results.NotFound() : Results.Ok(conversation);
})
    .WithName("GetConversation");

app.MapGet("/owners/{ownerId}/conversations", async (
    string ownerId,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversations = await conversationService.ListForOwnerAsync(ownerId, cancellationToken);

    return Results.Ok(conversations);
})
    .WithName("GetOwnerConversations");

app.MapGet("/conversations/{conversationId:guid}/messages", async (
    Guid conversationId,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var messages = await conversationService.GetMessagesAsync(conversationId, cancellationToken: cancellationToken);

    return Results.Ok(messages);
})
    .WithName("GetConversationMessages");

app.MapGet("/owners/{ownerId}/ingestion-jobs", async (
    string ownerId,
    int? take,
    IIngestionJobService ingestionJobService,
    CancellationToken cancellationToken) =>
{
    var jobs = await ingestionJobService.GetForOwnerAsync(ownerId, take, cancellationToken);

    return Results.Ok(jobs);
})
    .WithName("GetOwnerIngestionJobs");

app.MapGet("/conversations/{conversationId:guid}/ingestion-jobs", async (
    Guid conversationId,
    int? take,
    IIngestionJobService ingestionJobService,
    CancellationToken cancellationToken) =>
{
    var jobs = await ingestionJobService.GetForConversationAsync(conversationId, take, cancellationToken);

    return Results.Ok(jobs);
})
    .WithName("GetConversationIngestionJobs");

app.MapPost("/conversations/{conversationId:guid}/messages", async (
    Guid conversationId,
    AddMessageRequest request,
    IConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var message = await conversationService.AddMessageAsync(conversationId, request, cancellationToken);

    return Results.Created($"/conversations/{conversationId}/messages/{message.Id}", message);
})
    .WithName("AddConversationMessage");

app.MapGet("/conversations/{conversationId:guid}/memories", async (
    Guid conversationId,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memories = await memoryService.GetActiveForConversationAsync(conversationId, cancellationToken);

    return Results.Ok(memories);
})
    .WithName("GetConversationMemories");

app.MapGet("/owners/{ownerId}/memories", async (
    string ownerId,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memories = await memoryService.GetActiveForOwnerAsync(ownerId, cancellationToken);

    return Results.Ok(memories);
})
    .WithName("GetOwnerMemories");

app.MapPost("/owners/{ownerId}/memories/cleanup-preview", async (
    string ownerId,
    IMemoryCleanupService memoryCleanupService,
    CancellationToken cancellationToken) =>
{
    var preview = await memoryCleanupService.PreviewAsync(ownerId, cancellationToken);

    return Results.Ok(preview);
})
    .WithName("PreviewOwnerMemoryCleanup");

app.MapPost("/owners/{ownerId}/memories/cleanup-apply", async (
    string ownerId,
    MemoryCleanupApplyRequest request,
    IMemoryCleanupService memoryCleanupService,
    CancellationToken cancellationToken) =>
{
    var result = await memoryCleanupService.ApplyAsync(ownerId, request, cancellationToken);

    return Results.Ok(result);
})
    .WithName("ApplyOwnerMemoryCleanup");

app.MapGet("/owners/{ownerId}/memory-candidates", async (
    string ownerId,
    IMemoryCandidateService memoryCandidateService,
    CancellationToken cancellationToken) =>
{
    var candidates = await memoryCandidateService.GetPendingForOwnerAsync(ownerId, cancellationToken);

    return Results.Ok(candidates);
})
    .WithName("GetOwnerMemoryCandidates");

app.MapGet("/owners/{ownerId}/memory-conflicts", async (
    string ownerId,
    IMemoryConflictService memoryConflictService,
    CancellationToken cancellationToken) =>
{
    var conflicts = await memoryConflictService.GetPendingForOwnerAsync(ownerId, cancellationToken);

    return Results.Ok(conflicts);
})
    .WithName("GetOwnerMemoryConflicts");

app.MapGet("/owners/{ownerId}/memory-audit", async (
    string ownerId,
    Guid? memoryId,
    Guid? conversationId,
    int? take,
    IMemoryAuditService memoryAuditService,
    CancellationToken cancellationToken) =>
{
    var entries = await memoryAuditService.GetForOwnerAsync(
        ownerId,
        memoryId,
        conversationId,
        take ?? 50,
        cancellationToken);

    return Results.Ok(entries);
})
    .WithName("GetOwnerMemoryAudit");

app.MapGet("/owners/{ownerId}/tool-audit", async (
    string ownerId,
    Guid? conversationId,
    int? take,
    IToolAuditService toolAuditService,
    CancellationToken cancellationToken) =>
{
    var entries = await toolAuditService.GetForOwnerAsync(
        ownerId,
        conversationId,
        take ?? 50,
        cancellationToken);

    return Results.Ok(entries);
})
    .WithName("GetOwnerToolAudit");

app.MapGet("/owners/{ownerId}/memory-export", async (
    string ownerId,
    IMemoryBackupService memoryBackupService,
    CancellationToken cancellationToken) =>
{
    var document = await memoryBackupService.ExportAsync(ownerId, cancellationToken);

    return Results.Ok(document);
})
    .WithName("ExportOwnerMemoryProfile");

app.MapPost("/owners/{ownerId}/memory-import", async (
    string ownerId,
    MemoryImportRequest request,
    IMemoryBackupService memoryBackupService,
    CancellationToken cancellationToken) =>
{
    var result = await memoryBackupService.ImportAsync(ownerId, request, cancellationToken);

    return Results.Ok(result);
})
    .WithName("ImportOwnerMemoryProfile");

app.MapGet("/owners/{ownerId}/memory-review", async (
    string ownerId,
    Guid? conversationId,
    IMemoryReviewService memoryReviewService,
    CancellationToken cancellationToken) =>
{
    var review = await memoryReviewService.GetAsync(ownerId, conversationId, cancellationToken);

    return Results.Ok(review);
})
    .WithName("GetOwnerMemoryReview");

app.MapPost("/memory-retention", async (
    IMemoryRetentionService memoryRetentionService,
    CancellationToken cancellationToken) =>
{
    var result = await memoryRetentionService.ApplyAsync(cancellationToken);

    return Results.Ok(result);
})
    .WithName("ApplyMemoryRetention");

app.MapGet("/memory-policy", (IMemoryPolicyService memoryPolicyService) =>
{
    return Results.Ok(memoryPolicyService.Get());
})
    .WithName("GetMemoryPolicy");

app.MapPut("/memory-policy", (
    SetMemoryPolicyRequest request,
    IMemoryPolicyService memoryPolicyService) =>
{
    var policy = memoryPolicyService.Set(request.Policy);

    return Results.Ok(policy);
})
    .WithName("SetMemoryPolicy");

app.MapGet("/conversations/{conversationId:guid}/memory-candidates", async (
    Guid conversationId,
    IMemoryCandidateService memoryCandidateService,
    CancellationToken cancellationToken) =>
{
    var candidates = await memoryCandidateService.GetPendingForConversationAsync(conversationId, cancellationToken);

    return Results.Ok(candidates);
})
    .WithName("GetConversationMemoryCandidates");

app.MapGet("/conversations/{conversationId:guid}/memory-conflicts", async (
    Guid conversationId,
    IMemoryConflictService memoryConflictService,
    CancellationToken cancellationToken) =>
{
    var conflicts = await memoryConflictService.GetPendingForConversationAsync(conversationId, cancellationToken);

    return Results.Ok(conflicts);
})
    .WithName("GetConversationMemoryConflicts");

app.MapPost("/conversations/{conversationId:guid}/memories/search", async (
    Guid conversationId,
    SearchMemoriesRequest request,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var matches = await memoryService.SearchAsync(conversationId, request, cancellationToken);

    return Results.Ok(matches);
})
    .WithName("SearchConversationMemories");

app.MapPost("/conversations/{conversationId:guid}/memories/evaluate-recall", async (
    Guid conversationId,
    EvaluateMemoryRecallRequest request,
    IMemoryRecallEvaluationService memoryRecallEvaluationService,
    CancellationToken cancellationToken) =>
{
    var evaluation = await memoryRecallEvaluationService.EvaluateAsync(
        conversationId,
        request,
        cancellationToken);

    return Results.Ok(evaluation);
})
    .WithName("EvaluateConversationMemoryRecall");

app.MapPost("/conversations/{conversationId:guid}/context", async (
    Guid conversationId,
    BuildMemoryContextRequest request,
    IContextBuilder contextBuilder,
    CancellationToken cancellationToken) =>
{
    var context = await contextBuilder.BuildAsync(conversationId, request, cancellationToken);

    return Results.Ok(context);
})
    .WithName("BuildConversationContext");

app.MapPost("/conversations/{conversationId:guid}/agent/prepare", async (
    Guid conversationId,
    PrepareAgentTurnRequest request,
    IAgentMemoryService agentMemoryService,
    CancellationToken cancellationToken) =>
{
    var prepared = await agentMemoryService.PrepareTurnAsync(conversationId, request, cancellationToken);

    return Results.Ok(prepared);
})
    .WithName("PrepareAgentTurn");

app.MapPost("/conversations/{conversationId:guid}/agent/chat", async (
    Guid conversationId,
    AgentChatRequest request,
    IAgentChatService agentChatService,
    CancellationToken cancellationToken) =>
{
    var response = await agentChatService.ChatAsync(conversationId, request, cancellationToken);

    return Results.Ok(response);
})
    .WithName("AgentChat");

app.MapOpenAiCompatibleEndpoints();

app.MapPost("/memories", async (
    CreateMemoryRequest request,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryService.CreateAsync(request, cancellationToken);

    return Results.Created($"/memories/{memory.Id}", memory);
})
    .WithName("CreateMemory");

app.MapPost("/memory-candidates/{candidateId:guid}/promote", async (
    Guid candidateId,
    PromoteMemoryCandidateRequest request,
    IMemoryCandidateService memoryCandidateService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryCandidateService.PromoteAsync(candidateId, request, cancellationToken);

    return Results.Created($"/memories/{memory.Id}", memory);
})
    .WithName("PromoteMemoryCandidate");

app.MapPost("/memory-candidates/{candidateId:guid}/reject", async (
    Guid candidateId,
    IMemoryCandidateService memoryCandidateService,
    CancellationToken cancellationToken) =>
{
    var candidate = await memoryCandidateService.RejectAsync(candidateId, cancellationToken);

    return Results.Ok(candidate);
})
    .WithName("RejectMemoryCandidate");

app.MapPost("/memory-conflicts/{conflictId:guid}/accept-candidate", async (
    Guid conflictId,
    IMemoryConflictService memoryConflictService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryConflictService.AcceptCandidateAsync(conflictId, cancellationToken);

    return Results.Created($"/memories/{memory.Id}", memory);
})
    .WithName("AcceptMemoryConflictCandidate");

app.MapPost("/memory-conflicts/{conflictId:guid}/keep-existing", async (
    Guid conflictId,
    IMemoryConflictService memoryConflictService,
    CancellationToken cancellationToken) =>
{
    var conflict = await memoryConflictService.KeepExistingAsync(conflictId, cancellationToken);

    return Results.Ok(conflict);
})
    .WithName("KeepExistingMemoryConflict");

app.MapPost("/memories/{memoryId:guid}/pin", async (
    Guid memoryId,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryService.PinAsync(memoryId, cancellationToken);

    return Results.Ok(memory);
})
    .WithName("PinMemory");

app.MapPost("/memories/{memoryId:guid}/unpin", async (
    Guid memoryId,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryService.UnpinAsync(memoryId, cancellationToken);

    return Results.Ok(memory);
})
    .WithName("UnpinMemory");

app.MapPut("/memories/{memoryId:guid}", async (
    Guid memoryId,
    CorrectMemoryRequest request,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryService.CorrectAsync(memoryId, request, cancellationToken);

    return Results.Ok(memory);
})
    .WithName("CorrectMemory");

app.MapDelete("/memories/{memoryId:guid}", async (
    Guid memoryId,
    IMemoryService memoryService,
    CancellationToken cancellationToken) =>
{
    var memory = await memoryService.ForgetAsync(memoryId, cancellationToken);

    return Results.Ok(memory);
})
    .WithName("ForgetMemory");

app.MapFallbackToFile("index.html");

await app.StartAsync();
await MemorySchema.ApplyPendingMigrationsAsync(app.Services);
await app.WaitForShutdownAsync();
