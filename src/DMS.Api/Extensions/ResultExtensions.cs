using DMS.Application.Common;
using DMS.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Extensions;

public static class ResultExtensions
{
    private static readonly Dictionary<string, int> ErrorStatusCodes = new()
    {
        [DomainErrors.File.NotFound.Code]          = StatusCodes.Status404NotFound,
        [DomainErrors.File.Forbidden.Code]         = StatusCodes.Status403Forbidden,
        [DomainErrors.File.TooLarge.Code]          = StatusCodes.Status413RequestEntityTooLarge,
        [DomainErrors.File.Empty.Code]             = StatusCodes.Status400BadRequest,
        [DomainErrors.User.NotFound.Code]          = StatusCodes.Status404NotFound,
        [DomainErrors.User.EmailAlreadyExists.Code]= StatusCodes.Status409Conflict,
        [DomainErrors.User.InvalidCredentials.Code]= StatusCodes.Status401Unauthorized,
        [DomainErrors.User.Suspended.Code]         = StatusCodes.Status403Forbidden,
        [DomainErrors.Token.Invalid.Code]          = StatusCodes.Status401Unauthorized,
        [DomainErrors.Token.Expired.Code]          = StatusCodes.Status401Unauthorized,
    };

    public static IActionResult ToProblemResult(this Result result, ControllerBase controller)
    {
        var statusCode = ErrorStatusCodes.GetValueOrDefault(result.Error.Code, StatusCodes.Status500InternalServerError);

        return controller.Problem(
            detail: result.Error.Description,
            title: result.Error.Code,
            statusCode: statusCode);
    }
}
