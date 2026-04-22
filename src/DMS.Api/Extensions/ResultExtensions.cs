using DMS.Application.Common;
using DMS.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Extensions;

public static class ResultExtensions
{
    private static readonly Dictionary<string, int> ErrorStatusCodes = new()
    {
        [DomainErrors.File.NotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.File.Forbidden.Code] = StatusCodes.Status403Forbidden,
        [DomainErrors.File.TooLarge.Code] = StatusCodes.Status413RequestEntityTooLarge,
        [DomainErrors.File.Empty.Code] = StatusCodes.Status400BadRequest,
        [DomainErrors.User.IdMissing.Code] = StatusCodes.Status400BadRequest,
    };

    public static IActionResult ToProblemResult(this Result result, ControllerBase controller)
    {
        var statusCode = ErrorStatusCodes.GetValueOrDefault(result.Error.Code, StatusCodes.Status500InternalServerError);

        var problem = new ProblemDetails
        {
            Title = result.Error.Code,
            Detail = result.Error.Description,
            Status = statusCode
        };

        return controller.Problem(
            detail: problem.Detail,
            title: problem.Title,
            statusCode: problem.Status);
    }
}
