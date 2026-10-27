using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Cineclay.Api;
using Cineclay.Shared;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Movies")
    ?? throw new InvalidOperationException("ConnectionStrings__Movies is required.");
var key = builder.Configuration["ADMIN_API_KEY"]
    ?? throw new InvalidOperationException("ADMIN_API_KEY is required.");

if (key.Length < 32)
{
    throw new InvalidOperationException("ADMIN_API_KEY must have at least 32 characters.");
}

builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton<MovieStore>();
builder.Services.AddSingleton<AlbumStore>();
builder.Services.AddHttpClient<TmdbImporter>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<PublicImporter>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("imports", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api/admin"))
    {
        var supplied = context.Request.Headers["X-Admin-Key"].ToString();
        var matches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
            SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        if (!matches)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "up" }));
app.MapGet("/health/ready", async (NpgsqlDataSource database, CancellationToken cancellationToken) =>
{
    await using var command = database.CreateCommand("SELECT 1");
    await command.ExecuteScalarAsync(cancellationToken);
    return Results.Ok(new { status = "ready" });
});
app.MapGet("/api/movies", async (
    string? q,
    int? year,
    string? genre,
    string? sort,
    int? page,
    int? pageSize,
    string? mediaType,
    string? language,
    MovieStore database,
    CancellationToken cancellationToken) =>
{
    var query = q?.Trim();
    if (query?.Length > 200)
    {
        query = query[..200];
    }

    return Results.Ok(await database.List(
        query,
        year,
        genre,
        sort ?? "curated",
        Math.Clamp(page ?? 1, 1, 100000),
        Math.Clamp(pageSize ?? 24, 1, 100),
        mediaType,
        language,
        cancellationToken));
});
app.MapGet("/api/movies/{id:long}", async (long id, MovieStore database, CancellationToken cancellationToken) =>
    await database.Get(id, cancellationToken) is { } movie ? Results.Ok(movie) : Results.NotFound());
app.MapGet("/api/stats", (MovieStore database, CancellationToken cancellationToken) =>
    database.Stats(cancellationToken));
app.MapGet("/api/spotlight", (string? mediaType, MovieStore database, CancellationToken cancellationToken) =>
    database.Spotlight(mediaType, cancellationToken));
app.MapGet("/api/claydot/catalog", (MovieStore database, CancellationToken cancellationToken) =>
    database.Enriched(cancellationToken));
app.MapGet("/api/albums", async (
    string? q,
    string? language,
    int? year,
    bool? playable,
    int? page,
    int? pageSize,
    AlbumStore database,
    CancellationToken cancellationToken) => Results.Ok(await database.List(
        q,
        language,
        year,
        playable ?? false,
        Math.Clamp(page ?? 1, 1, 100000),
        Math.Clamp(pageSize ?? 24, 1, 100),
        cancellationToken)));

var admin = app.MapGroup("/api/admin");
admin.MapPut("/movies/{id:long}", async (
    long id,
    Movie movie,
    MovieStore database,
    CancellationToken cancellationToken) =>
{
    movie.Id = id;
    var error = movie.Validate();
    if (id <= 0 || error is not null)
    {
        return Results.BadRequest(new { error = error ?? "Positive ID required." });
    }

    await database.Upsert(movie, cancellationToken);
    return Results.Ok(movie);
});
admin.MapDelete("/movies/{id:long}", async (
    long id,
    MovieStore database,
    CancellationToken cancellationToken) =>
    await database.Delete(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
admin.MapPut("/albums/{id:long}", async (
    long id,
    Album album,
    AlbumStore database,
    CancellationToken cancellationToken) =>
{
    album.Id = id;
    var error = album.Validate();
    if (id <= 0 || error is not null)
    {
        return Results.BadRequest(new { error = error ?? "Positive ID required." });
    }

    await database.Upsert(album, cancellationToken);
    return Results.Ok(album);
});
admin.MapPost("/albums/import", async (
    List<Album> albums,
    AlbumStore database,
    CancellationToken cancellationToken) =>
{
    if (albums.Count is < 1 or > 1000)
    {
        return Results.BadRequest(new { error = "Import batches must contain 1–1,000 albums." });
    }

    for (var index = 0; index < albums.Count; index++)
    {
        var error = albums[index].Validate();
        if (albums[index].Id <= 0 || error is not null)
        {
            return Results.BadRequest(new { index, error = error ?? "Positive ID required." });
        }
    }

    await database.UpsertMany(albums, cancellationToken);
    return Results.Ok(new { imported = albums.Count });
}).RequireRateLimiting("imports");
admin.MapPost("/import/movie/{id:int}", async (
    int id,
    TmdbImporter tmdb,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await tmdb.Movie(id, cancellationToken));
    }
    catch (InvalidOperationException)
    {
        return Results.Problem("Set TMDB_TOKEN on the API server to enable imports.", statusCode: 503);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException)
    {
        return Results.Problem(
            "TMDB could not complete the request. Check the token, ID and provider service.",
            statusCode: 502);
    }
}).RequireRateLimiting("imports");
admin.MapPost("/import/year/{year:int}", async (
    int year,
    int? page,
    TmdbImporter tmdb,
    CancellationToken cancellationToken) =>
{
    if (year < 1990 || year > DateTime.UtcNow.Year || page is < 1 or > 500)
    {
        return Results.BadRequest(new
        {
            error = $"Year must be 1990–{DateTime.UtcNow.Year}; page must be 1–500."
        });
    }

    try
    {
        return Results.Ok(await tmdb.Year(year, page ?? 1, cancellationToken));
    }
    catch (InvalidOperationException)
    {
        return Results.Problem("Set TMDB_TOKEN on the API server to enable imports.", statusCode: 503);
    }
    catch (HttpRequestException)
    {
        return Results.Problem("TMDB import failed. Retry the same page safely.", statusCode: 502);
    }
}).RequireRateLimiting("imports");
admin.MapPost("/import/tvmaze/{id:int}", async (
    int id,
    PublicImporter source,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await source.Tv(id, cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException)
    {
        return Results.Problem("TVmaze could not complete the request.", statusCode: 502);
    }
}).RequireRateLimiting("imports");

// Personal data endpoints are trusted-server only. The Blazor server supplies a cookie-derived owner ID.
admin.MapGet("/watchlist/{owner}", (string owner, MovieStore database, CancellationToken cancellationToken) =>
    database.Watchlist(owner, cancellationToken));
admin.MapPut("/watchlist/{owner}/{id:long}", async (
    string owner,
    long id,
    bool saved,
    MovieStore database,
    CancellationToken cancellationToken) =>
{
    if (await database.Get(id, cancellationToken) is null)
    {
        return Results.NotFound();
    }

    await database.SetWatch(owner, id, saved, cancellationToken);
    return Results.NoContent();
});
admin.MapGet("/reviews/{owner}/{id:long}", (
    string owner,
    long id,
    MovieStore database,
    CancellationToken cancellationToken) => database.Reviews(id, owner, cancellationToken));
admin.MapPost("/reviews/{owner}/{id:long}", async (
    string owner,
    long id,
    ReviewInput review,
    MovieStore database,
    CancellationToken cancellationToken) =>
{
    var errors = new List<ValidationResult>();
    if (!Validator.TryValidateObject(review, new ValidationContext(review), errors, true)
        || string.IsNullOrWhiteSpace(review.Text))
    {
        return Results.BadRequest(errors.Select(error => error.ErrorMessage));
    }

    if (await database.Get(id, cancellationToken) is null)
    {
        return Results.NotFound();
    }

    await database.Review(id, owner, review, cancellationToken);
    return Results.NoContent();
});

await app.Services.GetRequiredService<MovieStore>().Initialize(CancellationToken.None);
await app.Services.GetRequiredService<AlbumStore>().Initialize(CancellationToken.None);
app.Run();
