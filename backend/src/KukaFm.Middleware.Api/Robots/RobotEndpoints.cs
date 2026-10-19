using Microsoft.AspNetCore.Http.HttpResults;

namespace KukaFm.Middleware.Api.Robots;

public static class RobotEndpoints
{
    public static void MapRobotEndpoints(this IEndpointRouteBuilder app)
    {
        var robots = app.MapGroup("/api/robots");

        robots.MapGet("", (RobotService service, CancellationToken ct) => service.ListAsync(ct));

        robots.MapGet("{id:long}/history", async Task<Results<Ok<List<RobotHistoryDto>>, ProblemHttpResult>> (
            long id, RobotService service, CancellationToken ct) =>
            await service.ListHistoryAsync(id, ct) is { } history
                ? TypedResults.Ok(history)
                : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Robot not found",
                    detail: $"Robot with id {id} does not exist."));
    }
}
