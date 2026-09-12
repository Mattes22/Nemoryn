namespace Memory.Application.Tests.Configuration;

using Memory.Application.Configuration;

public sealed class MemoryPolicyPresetsTests
{
    [Fact]
    public void Conservative_is_stricter_than_balanced_on_staging_and_recall()
    {
        var conservative = MemoryPolicyPresets.ThresholdsFor(MemoryPolicyKind.Conservative);
        var balanced = MemoryPolicyPresets.ThresholdsFor(MemoryPolicyKind.Balanced);
        var aggressive = MemoryPolicyPresets.ThresholdsFor(MemoryPolicyKind.Aggressive);

        Assert.True(conservative.AutoPromoteConfidence > balanced.AutoPromoteConfidence);
        Assert.True(balanced.AutoPromoteConfidence > aggressive.AutoPromoteConfidence);
        Assert.True(conservative.AutoPromoteEvidenceCount > balanced.AutoPromoteEvidenceCount);
        Assert.True(balanced.AutoPromoteEvidenceCount > aggressive.AutoPromoteEvidenceCount);
        Assert.True(conservative.MinPersistConfidence > aggressive.MinPersistConfidence);
        Assert.True(conservative.MinRelevantSimilarity > balanced.MinRelevantSimilarity);
        Assert.True(balanced.MinRelevantSimilarity > aggressive.MinRelevantSimilarity);
        Assert.True(conservative.ContradictionConfidenceThreshold < aggressive.ContradictionConfidenceThreshold);
        Assert.True(conservative.MaxRelevantMemories < aggressive.MaxRelevantMemories);
    }

    [Fact]
    public void Apply_overwrites_threshold_fields_from_the_profile()
    {
        var options = new MemoryAiOptions
        {
            Policy = MemoryPolicyKind.Aggressive,
            AutoPromoteEvidenceCount = 9,
            MinRelevantScore = 0.99d
        };

        MemoryPolicyPresets.Apply(options, MemoryPolicyKind.Conservative);

        Assert.Equal(MemoryPolicyKind.Conservative, options.Policy);
        Assert.Equal(3, options.AutoPromoteEvidenceCount);
        Assert.Equal(0.78d, options.MinRelevantScore);
        Assert.Equal(0.65m, options.MinPersistConfidence);
    }

    [Fact]
    public void Describe_explains_staging_and_recall_impact()
    {
        var conservative = MemoryPolicyPresets.Describe(MemoryPolicyKind.Conservative);
        var aggressive = MemoryPolicyPresets.Describe(MemoryPolicyKind.Aggressive);

        Assert.Contains(conservative.StagingEffects, effect => effect.Contains("Auto-promote", StringComparison.Ordinal));
        Assert.Contains(conservative.RecallEffects, effect => effect.Contains("Core", StringComparison.Ordinal));
        Assert.Contains(aggressive.StagingEffects, effect => effect.Contains("jedné evidence", StringComparison.Ordinal));
        Assert.Equal(3, conservative.Available.Count);
    }
}
