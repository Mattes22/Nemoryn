namespace Memory.Application.Tests.OpenAiCompatible;

using Memory.Application.OpenAiCompatible;

public sealed class OpenAiBearerAuthenticationTests
{
    [Theory]
    [InlineData("Bearer secret", "secret")]
    [InlineData("bearer secret", "secret")]
    [InlineData("Bearer  secret  ", "secret")]
    public void TryGetBearerToken_reads_the_token(string authorization, string expected)
    {
        Assert.True(OpenAiBearerAuthentication.TryGetBearerToken(authorization, out var token));
        Assert.Equal(expected, token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic secret")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    public void TryGetBearerToken_rejects_invalid_headers(string? authorization)
    {
        Assert.False(OpenAiBearerAuthentication.TryGetBearerToken(authorization, out var token));
        Assert.Equal(string.Empty, token);
    }

    [Fact]
    public void FixedEquals_matches_the_same_value()
    {
        Assert.True(OpenAiBearerAuthentication.FixedEquals("secret", "secret"));
        Assert.False(OpenAiBearerAuthentication.FixedEquals("secret", "Secret"));
        Assert.False(OpenAiBearerAuthentication.FixedEquals("secret", "secrets"));
    }
}
