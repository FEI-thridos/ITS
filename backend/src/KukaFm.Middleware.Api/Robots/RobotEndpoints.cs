using KukaFm.Middleware.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KukaFm.Middleware.Api.Robots;

public static class RobotEndpoints
{
    public static void MapRobotEndpoints(this IEndpointRouteBuilder app)
    {
        var robots = app.MapGroup("/api/robots");

        robots.MapGet("", (bool? includeHistory, RobotService service, CancellationToken ct) =>
            service.ListAsync(includeHistory ?? false, ct));

        robots.MapGet("{id:long}/history", async Task<Results<Ok<PagedResult<RobotHistoryDto>>, ValidationProblem, ProblemHttpResult>> (
            long id, int? page, int? pageSize, RobotService service, CancellationToken ct) =>
        {
            int p = page ?? Paging.DefaultPage, size = pageSize ?? Paging.MaxPageSize;
            if (Paging.Validate(p, size) is { } errors)
                return TypedResults.ValidationProblem(errors);

            return await service.ListHistoryAsync(id, p, size, ct) is { } history
                ? TypedResults.Ok(history)
                : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Robot not found",
                    detail: $"Robot with id {id} does not exist.");
        });
    }
}
