using System.Text.Json;
using LibraryApi.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _log;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> log) => _log = log;

    // ყველა შეცდომა -> ProblemDetails
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        int status;
        string detail;

        if (ex is AppException app)
        {
            status = app.StatusCode;
            detail = app.Message;
            _log.LogWarning("{Status}: {Message}", status, detail);
        }
        else if (ex is DbUpdateConcurrencyException)
        {
            status = StatusCodes.Status409Conflict;
            detail = "მონაცემები შეიცვალა სხვა მომხმარებლის მიერ, სცადეთ თავიდან";
            _log.LogWarning(ex, "Concurrency conflict");
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            detail = "სერვერზე მოხდა მოულოდნელი შეცდომა";
            _log.LogError(ex, "Unhandled exception");
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = detail,
            Detail = detail,
            Instance = ctx.Request.Path
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json", ct);
        return true;
    }
}
