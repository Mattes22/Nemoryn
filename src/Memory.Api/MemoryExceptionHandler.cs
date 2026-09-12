namespace Memory.Api;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Memory.Application.Exceptions;
using Npgsql;

internal sealed class MemoryExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var mapped = Map(exception);
        if (mapped is null)
        {
            return false;
        }

        var (statusCode, title, detail) = mapped.Value;
        httpContext.Response.StatusCode = statusCode;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });

        return true;
    }

    private static (int StatusCode, string Title, string Detail)? Map(Exception exception)
    {
        return exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message),
            ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DbUpdateException dbUpdate when IsUniqueViolation(dbUpdate) =>
                (StatusCodes.Status409Conflict, "Conflict", "A resource with the same unique key already exists."),
            _ => null
        };
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
