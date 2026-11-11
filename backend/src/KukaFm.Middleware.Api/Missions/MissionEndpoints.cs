using System.Diagnostics;
using KukaFm.Middleware.Api.Data;

namespace KukaFm.Middleware.Api.Missions;

public static class MissionEndpoints
{
    public static void MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var missions = app.MapGroup("/api/missions");

        missions.MapGet("", async (string? state, bool? includeHistory, MissionService service, CancellationToken ct) =>
            MissionService.TryParseState(state, out var filter)
                ? Results.Ok(await service.ListAsync(filter, includeHistory ?? false, ct))
                : TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["state"] = ["state must be 'active' or 'finished'."],
                }));

        missions.MapGet("{missionCode}/history", async (
            string missionCode, int? page, int? pageSize, MissionService service, CancellationToken ct) =>
        {
            int p = page ?? Paging.DefaultPage, size = pageSize ?? Paging.MaxPageSize;
            if (Paging.Validate(p, size) is { } errors)
                return TypedResults.ValidationProblem(errors);

            return await service.ListHistoryAsync(missionCode, p, size, ct) is { } history
                ? Results.Ok(history)
                : NotFound(missionCode);
        });

        missions.MapPost("", async (DispatchMissionRequest request, MissionService service, CancellationToken ct) =>
        {
            var result = await service.DispatchAsync(request, ct);
            return result.Outcome == MissionOutcome.Success
                ? Results.Created((string?)null, result.Mission)
                : Problem(result);
        });

        missions.MapPost("{missionCode}/cancel", async (string missionCode, MissionService service, CancellationToken ct) =>
        {
            var result = await service.CancelAsync(missionCode, ct);
            return result.Outcome == MissionOutcome.Success ? Results.Ok() : Problem(result);
        });
    }

    private static IResult NotFound(string missionCode) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Mission not found",
            detail: $"Mission {missionCode} does not exist.");

    private static IResult Problem(MissionResult result) => result.Outcome switch
    {
        MissionOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
        MissionOutcome.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Mission not found", detail: result.Detail),
        MissionOutcome.NotCancelable => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Mission not cancelable", detail: result.Detail),
        MissionOutcome.KukaError => TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "KUKA FM error", detail: result.Detail),
        MissionOutcome.NotStored => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Mission not stored", detail: result.Detail),
        MissionOutcome.NotReady => TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service not ready", detail: result.Detail),
        _ => throw new UnreachableException($"No HTTP mapping for {result.Outcome}"),
    };
}
