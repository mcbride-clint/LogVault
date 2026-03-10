using LogVault.Api.Middleware;
using LogVault.Domain.Models;
using LogVault.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace LogVault.Api.Endpoints;

public static class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingest").RequireAuthorization("CanIngest");

        group.MapPost("/serilog", async (
            HttpContext ctx,
            ILogIngestionService ingestion,
            CancellationToken ct) =>
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.Body))
                body = await reader.ReadToEndAsync(ct);

            var context = BuildIngestContext(ctx);
            var count = await ingestion.IngestSerilogBatchAsync(body, context, ct);
            return Results.Accepted("/api/ingest/serilog", new { accepted = count });
        }).WithName("IngestSerilog").WithTags("Ingestion");

        group.MapPost("/clef", async (
            HttpContext ctx,
            ILogIngestionService ingestion,
            CancellationToken ct) =>
        {
            var context = BuildIngestContext(ctx);
            var count = await ingestion.IngestClefStreamAsync(ctx.Request.Body, context, ct);
            return Results.Accepted("/api/ingest/clef", new { accepted = count });
        }).WithName("IngestClef").WithTags("Ingestion");

        group.MapPost("/file", async (
            HttpContext ctx,
            [FromForm] IFormFile file,
            [FromForm] string format,
            [FromForm] string? application,
            [FromForm] string? environment,
            ILogIngestionService ingestion,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<LogFileFormat>(format, true, out var fmt))
                return Results.BadRequest(new { error = $"Unknown format '{format}'. Use: IIS, Clef, SerilogJson, PlainText" });

            using var stream = file.OpenReadStream();
            var context = BuildIngestContext(ctx) with
            {
                ExplicitApplication = application,
                ExplicitEnvironment = environment
            };

            var count = await ingestion.IngestFileAsync(stream, fmt, context, ct);
            return Results.Accepted("/api/ingest/file", new { accepted = count });
        }).WithName("IngestFile").WithTags("Ingestion").DisableAntiforgery();

        return app;
    }

    internal static IngestContext BuildIngestContext(HttpContext ctx) => new(
        XApplicationName: ctx.Request.Headers["X-Application-Name"].FirstOrDefault(),
        XEnvironment: ctx.Request.Headers["X-Environment"].FirstOrDefault(),
        TraceParentHeader: ctx.Request.Headers["traceparent"].FirstOrDefault(),
        ApiKeyDefaultApp: ctx.Items["ApiKeyDefaultApp"] as string,
        ExplicitApplication: null,
        ExplicitEnvironment: null);
}
