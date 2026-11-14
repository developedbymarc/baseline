using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using backend.Data;
using backend.Responses;
using backend.Services.Missions;

namespace backend.Controllers;

public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        (int status, string code, string message)? mapped = context.Exception switch
        {
            ApiProblem p => (p.Status, p.Code, p.Message),
            BaselineLockedException e => (409, "BASELINE_LOCKED", e.Message),
            FormatException e => (400, "BAD_FORMAT", e.Message),
            ArgumentOutOfRangeException e => (400, "OUT_OF_RANGE", e.Message),
            _ => null,
        };
        if (mapped is not { } m) return;
        context.Result = new ObjectResult(ApiResponse.Failed(m.message, m.code)) { StatusCode = m.status };
        context.ExceptionHandled = true;
    }
}
