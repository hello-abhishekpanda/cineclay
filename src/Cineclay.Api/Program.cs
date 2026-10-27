using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Cineclay.Api;
using Cineclay.Shared;
using Npgsql;
var builder=WebApplication.CreateBuilder(args);
var connection=builder.Configuration.GetConnectionString("Movies")??throw new InvalidOperationException("ConnectionStrings__Movies is required.");
var key=builder.Configuration["ADMIN_API_KEY"]??throw new InvalidOperationException("ADMIN_API_KEY is required.");
if(key.Length<32)throw new InvalidOperationException("ADMIN_API_KEY must have at least 32 characters.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));builder.Services.AddSingleton<MovieStore>();builder.Services.AddHttpClient<TmdbImporter>(c=>c.Timeout=TimeSpan.FromSeconds(30));builder.Services.AddHttpClient<PublicImporter>(c=>c.Timeout=TimeSpan.FromSeconds(30));builder.Services.AddProblemDetails();builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddFixedWindowLimiter("imports",x=>{x.PermitLimit=10;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;});});
var app=builder.Build();app.UseExceptionHandler();app.UseRateLimiter();
app.Use(async(ctx,next)=>{ctx.Response.Headers["X-Content-Type-Options"]="nosniff";if(ctx.Request.Path.StartsWithSegments("/api/admin")){
 var supplied=ctx.Request.Headers["X-Admin-Key"].ToString();if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),SHA256.HashData(Encoding.UTF8.GetBytes(key)))){ctx.Response.StatusCode=401;return;}
 }await next();});
app.MapGet("/health",()=>Results.Ok(new{status="up"}));
app.MapGet("/health/ready",async(NpgsqlDataSource db,CancellationToken ct)=>{await using var cmd=db.CreateCommand("SELECT 1");await cmd.ExecuteScalarAsync(ct);return Results.Ok(new{status="ready"});});
app.MapGet("/api/movies",async(string? q,int? year,string? genre,string? sort,int? page,int? pageSize,string? mediaType,string? language,MovieStore db,CancellationToken ct)=>Results.Ok(await db.List(q?.Trim()[..Math.Min(q.Trim().Length,200)],year,genre,sort??"curated",Math.Clamp(page??1,1,100000),Math.Clamp(pageSize??24,1,100),mediaType,language,ct)));
app.MapGet("/api/movies/{id:long}",async(long id,MovieStore db,CancellationToken ct)=>await db.Get(id,ct) is {} movie?Results.Ok(movie):Results.NotFound());
app.MapGet("/api/stats",async(MovieStore db,CancellationToken ct)=>await db.Stats(ct));
app.MapGet("/api/spotlight",async(string? mediaType,string? language,MovieStore db,CancellationToken ct)=>await db.Spotlight(mediaType,ct));
app.MapGet("/api/claydot/catalog",async(MovieStore db,CancellationToken ct)=>await db.Enriched(ct));
var admin=app.MapGroup("/api/admin");
admin.MapPut("/movies/{id:long}",async(long id,Movie movie,MovieStore db,CancellationToken ct)=>{movie.Id=id;var error=movie.Validate();if(id<=0||error!=null)return Results.BadRequest(new{error=error??"Positive ID required."});await db.Upsert(movie,ct);return Results.Ok(movie);});
admin.MapDelete("/movies/{id:long}",async(long id,MovieStore db,CancellationToken ct)=>await db.Delete(id,ct)?Results.NoContent():Results.NotFound());
admin.MapPost("/import/movie/{id:int}",async(int id,TmdbImporter tmdb,CancellationToken ct)=>{try{return Results.Ok(await tmdb.Movie(id,ct));}catch(InvalidOperationException){return Results.Problem("Set TMDB_TOKEN on the API server to enable imports.",statusCode:503);}catch(ArgumentException e){return Results.BadRequest(new{error=e.Message});}catch(HttpRequestException){return Results.Problem("TMDB could not complete the request. Check the token, ID and provider service.",statusCode:502);}}).RequireRateLimiting("imports");
admin.MapPost("/import/year/{year:int}",async(int year,int? page,TmdbImporter tmdb,CancellationToken ct)=>{if(year<1990||year>9999||page is <1 or >500)return Results.BadRequest(new{error="Year must be 1990–9999; page must be 1–500."});try{return Results.Ok(await tmdb.Year(year,page??1,ct));}catch(InvalidOperationException){return Results.Problem("Set TMDB_TOKEN on the API server to enable imports.",statusCode:503);}catch(HttpRequestException){return Results.Problem("TMDB import failed. Retry the same page safely.",statusCode:502);}}).RequireRateLimiting("imports");
admin.MapPost("/import/tvmaze/{id:int}",async(int id,PublicImporter source,CancellationToken ct)=>{try{return Results.Ok(await source.Tv(id,ct));}catch(ArgumentException e){return Results.BadRequest(new{error=e.Message});}catch(HttpRequestException){return Results.Problem("TVmaze could not complete the request.",statusCode:502);}}).RequireRateLimiting("imports");
// Personal data API is trusted-server only. The Blazor server supplies a cookie-derived pseudonymous owner ID.
admin.MapGet("/watchlist/{owner}",async(string owner,MovieStore db,CancellationToken ct)=>await db.Watchlist(owner,ct));
admin.MapPut("/watchlist/{owner}/{id:long}",async(string owner,long id,bool saved,MovieStore db,CancellationToken ct)=>{if(await db.Get(id,ct)==null)return Results.NotFound();await db.SetWatch(owner,id,saved,ct);return Results.NoContent();});
admin.MapGet("/reviews/{owner}/{id:long}",async(string owner,long id,MovieStore db,CancellationToken ct)=>await db.Reviews(id,owner,ct));
admin.MapPost("/reviews/{owner}/{id:long}",async(string owner,long id,ReviewInput review,MovieStore db,CancellationToken ct)=>{var errors=new List<ValidationResult>();if(!Validator.TryValidateObject(review,new ValidationContext(review),errors,true)||string.IsNullOrWhiteSpace(review.Text))return Results.BadRequest(errors.Select(e=>e.ErrorMessage));if(await db.Get(id,ct)==null)return Results.NotFound();await db.Review(id,owner,review,ct);return Results.NoContent();});
await app.Services.GetRequiredService<MovieStore>().Initialize(CancellationToken.None);
app.Run();
