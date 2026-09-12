namespace Memory.Api.OpenAi;

using Microsoft.Extensions.Options;
using Memory.Application.Configuration;
using Memory.Application.OpenAiCompatible;

internal sealed class OpenAiApiKeyFilter(IOptions<OpenAiCompatibleOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var required = options.Value.ApiKey?.Trim();
        if (string.IsNullOrEmpty(required))
        {
            return await next(context);
        }

        if (!OpenAiBearerAuthentication.TryGetBearerToken(
                context.HttpContext.Request.Headers.Authorization.ToString(),
                out var token)
            || !OpenAiBearerAuthentication.FixedEquals(token, required))
        {
            return Results.Json(
                new OpenAiErrorResponse(new OpenAiError("Invalid API key.", "invalid_request_error")),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }
}
