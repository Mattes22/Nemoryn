namespace Memory.Application.Tests.Agent;

using Memory.Application.Agent;
using Memory.Application.Configuration;
using Memory.Application.Context;
using Memory.Application.Tests.Fakes;
using Memory.Domain.Memories;

public sealed class AgentSystemPromptBuilderTests
{
    [Fact]
    public void Empty_memories_still_emit_contract_and_forbid_invented_facts()
    {
        var prompt = CreateBuilder().Build([], []);

        Assert.Equal(AgentSystemPromptBuilder.ContractVersion, prompt.ContractVersion);
        Assert.Equal(MemoryPolicyKind.Balanced, prompt.Policy);
        Assert.Contains(AgentSystemPromptBuilder.ContractVersion, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.NoUnlistedFacts, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.ToolMemoriesAreEvidence, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.WebResultsAreEvidence, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.HideInternalStructure, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.LatestUserWins, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.DoNotClaimWrite, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.NoMemoriesListed, prompt.Text);
        Assert.Contains("search_memories", prompt.Text);
        Assert.DoesNotContain("<stable_memory>", prompt.Text);
        Assert.DoesNotContain("<relevant_memory>", prompt.Text);
    }

    [Fact]
    public void Prompt_hides_internal_blocks_and_forbids_fake_saves()
    {
        var prompt = CreateBuilder().Build([PinnedExplicit()], []);

        Assert.Contains(AgentSystemPromptBuilder.HideInternalStructure, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.DoNotClaimWrite, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.UseSilently, prompt.Text);
        Assert.Contains("<stable_memory>", prompt.Text);
        Assert.Contains("- [User/Fact/pinned/explicit] User name is Matej.", prompt.Text);
    }

    [Fact]
    public void Pinned_and_explicit_outrank_inferred_and_latest_user_wins()
    {
        var prompt = CreateBuilder().Build([PinnedExplicit()], [InferredPreference()]);

        Assert.Contains(AgentSystemPromptBuilder.PinnedOutranksInferred, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.LatestUserWins, prompt.Text);
        Assert.Contains("<relevant_memory>", prompt.Text);
        Assert.Contains("- [User/Preference/inferred] User prefers Czech.", prompt.Text);
    }

    [Theory]
    [InlineData(MemoryPolicyKind.Conservative)]
    [InlineData(MemoryPolicyKind.Balanced)]
    [InlineData(MemoryPolicyKind.Aggressive)]
    public void Policy_stance_is_embedded_in_the_contract(MemoryPolicyKind policy)
    {
        var prompt = CreateBuilder(policy).Build([], []);
        var stance = AgentSystemPromptBuilder.StanceFor(policy);

        Assert.Equal(policy, prompt.Policy);
        Assert.Contains($"Recall stance ({policy}): {stance}", prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.NoUnlistedFacts, prompt.Text);
        Assert.Contains(AgentSystemPromptBuilder.ToolMemoriesAreEvidence, prompt.Text);
    }

    [Fact]
    public void Conservative_and_aggressive_stances_differ()
    {
        var conservative = CreateBuilder(MemoryPolicyKind.Conservative).Build([], []);
        var aggressive = CreateBuilder(MemoryPolicyKind.Aggressive).Build([], []);

        Assert.Contains("Use memory only when it clearly answers the turn", conservative.Text);
        Assert.Contains("broader retrieved set", aggressive.Text);
        Assert.DoesNotContain("broader retrieved set", conservative.Text);
        Assert.DoesNotContain("Use memory only when it clearly answers the turn", aggressive.Text);
    }

    private static AgentSystemPromptBuilder CreateBuilder(
        MemoryPolicyKind policy = MemoryPolicyKind.Balanced)
    {
        return new AgentSystemPromptBuilder(new FakeMemoryPolicyService(policy));
    }

    private static MemoryContextItem PinnedExplicit()
    {
        return new MemoryContextItem(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "User name is Matej.",
            MemoryType.Fact,
            MemoryScope.User,
            MemoryOrigin.Explicit,
            true,
            0.95m,
            0.95m,
            null,
            1d,
            "Core",
            "pinned",
            null,
            null,
            null);
    }

    private static MemoryContextItem InferredPreference()
    {
        return new MemoryContextItem(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "User prefers Czech.",
            MemoryType.Preference,
            MemoryScope.User,
            MemoryOrigin.Inferred,
            false,
            0.7m,
            0.8m,
            0.84d,
            0.8d,
            "Relevant",
            "similarity",
            null,
            null,
            null);
    }
}
