using Ardalis.Result;
using Microsoft.AspNetCore.Http;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

/// <summary>
/// BE-P-05: Unified Result→HTTP mapping.
/// Permanents (403/422/409/404) signal to peers that retry is futile.
/// Transient infra failures return 503 so peers can retry.
/// </summary>
public static class ResultToHttp
{
    public static Microsoft.AspNetCore.Http.IResult Map<T>(Result<T> result, Func<T, Microsoft.AspNetCore.Http.IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is not null ? onSuccess(result.Value) : Results.Ok(result.Value);

        return result.Status switch
        {
            ResultStatus.NotFound => Results.NotFound(result.Errors),
            ResultStatus.Forbidden => Results.Forbid(),
            ResultStatus.Invalid => Results.UnprocessableEntity(result.ValidationErrors),
            ResultStatus.Conflict => Results.Conflict(result.Errors),
            ResultStatus.Unavailable => Results.Problem(detail: string.Join(", ", result.Errors), statusCode: 503),
            _ => Results.BadRequest(result.Errors)
        };
    }

    public static Microsoft.AspNetCore.Http.IResult Map(Result result, Func<Microsoft.AspNetCore.Http.IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is not null ? onSuccess() : Results.Ok();

        return result.Status switch
        {
            ResultStatus.NotFound => Results.NotFound(result.Errors),
            ResultStatus.Forbidden => Results.Forbid(),
            ResultStatus.Invalid => Results.UnprocessableEntity(result.ValidationErrors),
            ResultStatus.Conflict => Results.Conflict(result.Errors),
            ResultStatus.Unavailable => Results.Problem(detail: string.Join(", ", result.Errors), statusCode: 503),
            _ => Results.BadRequest(result.Errors)
        };
    }
}
