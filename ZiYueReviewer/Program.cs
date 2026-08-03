using System.IO;
using ZiYueReviewer.Models;
using ZiYueReviewer.Services;

namespace ZiYueReviewer;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (!DatabaseService.TryLoadConfig(out Models.DatabaseConfig config, out string error, out string baseDir))
        {
            Console.WriteLine(error);
            return;
        }

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5177");

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<DatabaseService>();

        WebApplication app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/api/pending", async (DatabaseService db, CancellationToken ct) =>
            Results.Json(await db.GetPendingAsync(ct)));

        app.MapGet("/api/published", async (DatabaseService db, CancellationToken ct) =>
            Results.Json(await db.GetPublishedAsync(ct)));

        app.MapGet("/api/queue", async (ulong? id, DatabaseService db, CancellationToken ct) =>
        {
            if (id is null) return Results.BadRequest("缺少 id 参数");
            return Results.Json(await db.GetUserQueueAsync(id.Value, ct));
        });

        app.MapGet("/api/review-records", async (DatabaseService db, CancellationToken ct) =>
            Results.Json(await db.GetAllReviewRecordsAsync(ct)));

        app.MapPost("/api/review", async (DatabaseService db, ReviewRequest request, CancellationToken ct) =>
        {
            try
            {
                await db.ReviewAsync(request, ct);
                return Results.Ok();
            }
            catch (Exception e)
            {
                return Results.Problem(e.Message, statusCode: 500);
            }
        });

        app.MapGet("/queue", () => Results.File(Path.Combine(app.Environment.WebRootPath, "queue.html"), "text/html; charset=utf-8"));

        app.MapGet("/api/file", async (string path, HttpContext ctx) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return Results.NotFound();

                string full = Path.IsPathFullyQualified(path)
                    ? path
                    : Path.GetFullPath(Path.Combine(baseDir, path));

                if (!File.Exists(full)) return Results.NotFound();
                return Results.File(await File.ReadAllBytesAsync(full));
            }
            catch
            {
                return Results.NotFound();
            }
        });

        Console.WriteLine("子悦云瓶审核 WebUI：http://localhost:5177");
        await app.RunAsync();
    }
}
