using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nidhi.Api.Authentication;

public static class AuthProblems
{
    public static ProblemDetails Create(HttpContext context, int status, string code, string detail,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://api.nidhi.lk/errors/{code}", Title = detail, Detail = detail,
            Status = status, Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["timestampUtc"] = DateTime.UtcNow;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        if (errors is not null) problem.Extensions["errors"] = errors;
        return problem;
    }

    public static string CodeForStatus(int status) => status switch
    {
        400 => "VALIDATION_ERROR",
        401 => "AUTHENTICATION_REQUIRED",
        403 => "FORBIDDEN",
        404 => "RESOURCE_NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED",
        415 => "UNSUPPORTED_MEDIA_TYPE",
        429 => "RATE_LIMITED",
        _ => "INTERNAL_ERROR"
    };

    public static void CompleteFrameworkProblem(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (problem.Extensions.ContainsKey("code")) return;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        var standard = Create(context.HttpContext, status, CodeForStatus(status), problem.Title ?? "The request could not be completed.");
        problem.Type = standard.Type;
        problem.Instance = standard.Instance;
        problem.Detail ??= standard.Detail;
        foreach (var extension in standard.Extensions) problem.Extensions[extension.Key] = extension.Value;
    }

    public static Task WriteAsync(HttpContext context, int status, string code, string detail) =>
        Results.Problem(Create(context, status, code, detail)).ExecuteAsync(context);
}
