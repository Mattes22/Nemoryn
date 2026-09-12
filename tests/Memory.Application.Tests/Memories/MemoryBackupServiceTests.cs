namespace Memory.Application.Tests.Memories;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.Memories;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Conversations;
using Memory.Domain.Memories;
using MemoryEntity = Memory.Domain.Memories.Memory;

public sealed class MemoryBackupServiceTests
{
    [Fact]
    public async Task Export_includes_active_memories_candidates_conflicts_and_audit_metadata()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-home", "Home");
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User lives in Brno.",
            MemoryType.Fact,
            0.9m,
            0.95m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        memory.SetEmbedding(CreateEmbedding());
        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User maybe likes jazz.",
            MemoryType.Preference,
            0.4m,
            0.5m);
        candidate.AddEvidence(0.4m, 0.5m);
        var conflict = new MemoryConflict(
            conversation.OwnerId,
            conversation.Id,
            candidate.Id,
            memory.Id,
            0.8m,
            "City vs music preference.");
        store.AddConversation(conversation);
        store.AddMemory(memory);
        store.AddMemoryCandidate(candidate);
        store.AddMemoryConflict(conflict);
        store.AddMemoryAuditLog(MemoryAuditLog.ForMemory(
            MemoryAuditAction.Created,
            memory,
            MemoryAuditActorKind.User,
            "Pinned identity fact."));

        var document = await CreateService(store, MemoryPolicyKind.Conservative).ExportAsync("matej");

        Assert.Equal(MemoryProfileExport.FormatVersion, document.Format);
        Assert.Equal("matej", document.OwnerId);
        Assert.Equal(MemoryPolicyKind.Conservative, document.Policy);
        Assert.Contains(document.Conversations, item => item.ExternalId == "chat-home" && item.Title == "Home");
        var exportedMemory = Assert.Single(document.Memories);
        Assert.Equal(memory.Content, exportedMemory.Content);
        Assert.Equal(memory.Fingerprint, exportedMemory.Fingerprint);
        Assert.NotNull(exportedMemory.Embedding);
        Assert.Equal(768, exportedMemory.Embedding.Length);
        var exportedCandidate = Assert.Single(document.Candidates);
        Assert.Equal(1, exportedCandidate.EvidenceCount);
        var exportedConflict = Assert.Single(document.Conflicts);
        Assert.Equal(candidate.Fingerprint, exportedConflict.CandidateFingerprint);
        Assert.Equal(memory.Fingerprint, exportedConflict.ConflictingMemoryFingerprint);
        Assert.Equal(1, document.Audit.ExportedCount);
        Assert.Contains(document.Audit.ByAction, item => item.Action == MemoryAuditAction.Created && item.Count == 1);
        Assert.NotNull(document.Audit.LastOccurredAt);
        Assert.Single(document.Audit.Recent);
    }

    [Fact]
    public async Task Import_dry_run_does_not_write()
    {
        var store = SeedProfile();
        var document = await CreateService(store).ExportAsync("matej");
        var empty = new FakeMemoryStore();

        var result = await CreateService(empty).ImportAsync(
            "matej",
            new MemoryImportRequest(document, DryRun: true, SkipDuplicates: true, PinExplicit: false));

        Assert.True(result.DryRun);
        Assert.Equal(1, result.Memories.Created);
        Assert.Equal(1, result.Candidates.Created);
        Assert.Equal(1, result.Conflicts.Created);
        Assert.Equal(1, result.Conversations.Created);
        Assert.Empty(empty.Memories);
        Assert.Empty(empty.Candidates);
        Assert.Empty(empty.Conflicts);
        Assert.Empty(empty.Conversations);
        Assert.Empty(empty.AuditLogs);
    }

    [Fact]
    public async Task Import_skip_duplicates_skips_existing_fingerprints()
    {
        var store = SeedProfile();
        var document = await CreateService(store).ExportAsync("matej");

        var first = await CreateService(store).ImportAsync(
            "matej",
            new MemoryImportRequest(document, DryRun: false, SkipDuplicates: true, PinExplicit: false));
        var second = await CreateService(store).ImportAsync(
            "matej",
            new MemoryImportRequest(document, DryRun: false, SkipDuplicates: true, PinExplicit: false));

        Assert.Equal(0, first.Memories.Created);
        Assert.Equal(1, first.Memories.Skipped);
        Assert.Equal(1, second.Memories.Skipped);
        Assert.Equal(1, store.Memories.Count(memory => memory.OwnerId == "matej" && memory.Status == MemoryStatus.Active));
        Assert.Equal(1, store.Candidates.Count(candidate => candidate.Status == MemoryCandidateStatus.Pending));
    }

    [Fact]
    public async Task Import_without_skip_duplicates_reports_existing_fingerprints_as_failed()
    {
        var store = SeedProfile();
        var document = await CreateService(store).ExportAsync("matej");

        var result = await CreateService(store).ImportAsync(
            "matej",
            new MemoryImportRequest(document, DryRun: false, SkipDuplicates: false, PinExplicit: false));

        Assert.Equal(1, result.Memories.Failed);
        Assert.Equal(1, result.Candidates.Failed);
        Assert.Contains(result.Issues, issue => issue.Kind == "memory");
        Assert.Single(store.Memories);
    }

    [Fact]
    public async Task Import_pin_explicit_pins_explicit_memories()
    {
        var source = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-1");
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User name is Matej.",
            MemoryType.Fact,
            0.7m,
            0.8m,
            origin: MemoryOrigin.Explicit,
            isPinned: false);
        source.AddConversation(conversation);
        source.AddMemory(memory);
        var document = await CreateService(source).ExportAsync("matej");
        var target = new FakeMemoryStore();

        var result = await CreateService(target).ImportAsync(
            "matej",
            new MemoryImportRequest(document, DryRun: false, SkipDuplicates: true, PinExplicit: true));

        Assert.Equal(1, result.Memories.Created);
        var imported = Assert.Single(target.Memories);
        Assert.True(imported.IsPinned);
        Assert.Equal(MemoryOrigin.Explicit, imported.Origin);
    }

    [Fact]
    public async Task Import_remaps_owner_and_creates_conversations_by_external_id()
    {
        var source = SeedProfile();
        var document = await CreateService(source).ExportAsync("matej");
        var target = new FakeMemoryStore();

        var result = await CreateService(target).ImportAsync(
            "jane",
            new MemoryImportRequest(document, DryRun: false, SkipDuplicates: true, PinExplicit: false));

        Assert.Equal("jane", result.OwnerId);
        Assert.Equal("matej", result.SourceOwnerId);
        Assert.Equal(1, result.Conversations.Created);
        Assert.Equal(1, result.Memories.Created);
        var conversation = Assert.Single(target.Conversations);
        Assert.Equal("jane", conversation.OwnerId);
        Assert.Equal("chat-home", conversation.ExternalId);
        var memory = Assert.Single(target.Memories);
        Assert.Equal("jane", memory.OwnerId);
        Assert.Equal(conversation.Id, memory.ConversationId);
        Assert.NotEqual(document.Memories[0].Id, memory.Id);
        Assert.Equal(
            MemoryFingerprint.Compute("jane", memory.Scope, memory.Type, memory.Content),
            memory.Fingerprint);
        Assert.Single(target.Conflicts);
        Assert.Contains(target.AuditLogs, entry =>
            entry.Action == MemoryAuditAction.Created
            && entry.Reason == "Imported from memory profile backup.");
    }

    [Fact]
    public async Task Import_reuses_existing_conversation_with_same_external_id()
    {
        var source = SeedProfile();
        var document = await CreateService(source).ExportAsync("matej");
        var target = new FakeMemoryStore();
        var existing = new Conversation("matej", "chat-home", "Already there");
        target.AddConversation(existing);

        var result = await CreateService(target).ImportAsync(
            "matej",
            new MemoryImportRequest(document, DryRun: false, SkipDuplicates: true, PinExplicit: false));

        Assert.Equal(0, result.Conversations.Created);
        Assert.Equal(1, result.Conversations.Skipped);
        Assert.Equal(existing.Id, Assert.Single(target.Memories).ConversationId);
        Assert.Single(target.Conversations);
    }

    [Fact]
    public async Task Import_rejects_unknown_format()
    {
        var document = new MemoryProfileExport(
            "memory.profile.v0",
            DateTimeOffset.UtcNow,
            "matej",
            MemoryPolicyKind.Balanced,
            [],
            [],
            [],
            [],
            new MemoryExportAuditMetadata(0, [], null, []));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(new FakeMemoryStore()).ImportAsync(
                "matej",
                new MemoryImportRequest(document, DryRun: true)));

        Assert.Contains("Unsupported memory profile format", exception.Message);
    }

    private static FakeMemoryStore SeedProfile()
    {
        var store = new FakeMemoryStore();
        var conversation = new Conversation("matej", "chat-home", "Home");
        var memory = new MemoryEntity(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User lives in Brno.",
            MemoryType.Fact,
            0.9m,
            0.95m,
            origin: MemoryOrigin.Explicit,
            isPinned: true);
        var candidate = new MemoryCandidate(
            conversation.OwnerId,
            conversation.Id,
            MemoryScope.User,
            "User maybe likes jazz.",
            MemoryType.Preference,
            0.4m,
            0.5m);
        var conflict = new MemoryConflict(
            conversation.OwnerId,
            conversation.Id,
            candidate.Id,
            memory.Id,
            0.8m,
            "Needs review.");
        store.AddConversation(conversation);
        store.AddMemory(memory);
        store.AddMemoryCandidate(candidate);
        store.AddMemoryConflict(conflict);
        return store;
    }

    private static MemoryBackupService CreateService(
        FakeMemoryStore store,
        MemoryPolicyKind policy = MemoryPolicyKind.Balanced)
    {
        return new MemoryBackupService(
            store,
            new StubPolicyService(policy),
            Options.Create(new MemoryAiOptions
            {
                EmbeddingDimensions = MemoryAiOptions.DefaultEmbeddingDimensions,
                Policy = policy
            }));
    }

    private static float[] CreateEmbedding()
    {
        return Enumerable.Repeat(0.01f, MemoryAiOptions.DefaultEmbeddingDimensions).ToArray();
    }

    private sealed class StubPolicyService(MemoryPolicyKind policy) : IMemoryPolicyService
    {
        public MemoryPolicyResponse Get() => MemoryPolicyPresets.Describe(policy);

        public MemoryPolicyResponse Set(MemoryPolicyKind next) => MemoryPolicyPresets.Describe(next);
    }
}
