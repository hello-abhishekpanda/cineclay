using Cineclay.Shared;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
namespace Cineclay.Api;

public sealed class MovieStore(NpgsqlDataSource db)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task Initialize(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "schema.sql"), ct)); await cmd.ExecuteNonQueryAsync(ct);
        await using var count = db.CreateCommand("SELECT count(*) FROM movies");
        if ((long)(await count.ExecuteScalarAsync(ct))! == 0)
        {
            var seed = JsonSerializer.Deserialize<List<Movie>>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seed.json"), ct), Json)!;
            foreach (var movie in seed) await Upsert(movie, ct);
        }
    }
    public async Task<MoviePage> List(string? q, int? year, string? genre, string sort, int page, int size, string? mediaType, string? language, CancellationToken ct)
    {
        var order = sort switch { "new" => "release_year DESC,id", "old" => "release_year,id", "title" => "title,id", "rating" => "(payload->>'score')::float DESC NULLS LAST,id", _ => "(payload->>'featured')::boolean DESC,id" };
        var where = "deleted_at IS NULL AND (@q='' OR payload::text ILIKE @pattern) AND (@year=0 OR release_year=@year) AND (@genre='' OR @genre=ANY(genres)) AND (@type='' OR COALESCE(payload->>'mediaType','movie')=@type) AND (@language='' OR COALESCE(payload->'languages','[]'::jsonb) ? @language)";
        await using var cmd = db.CreateCommand($"SELECT payload FROM movies WHERE {where} ORDER BY {order} LIMIT @size OFFSET @offset");
        AddFilters(cmd, q, year, genre, mediaType, language); cmd.Parameters.AddWithValue("size", size); cmd.Parameters.AddWithValue("offset", (page - 1) * size);
        var list = new List<Movie>(); await using (var reader = await cmd.ExecuteReaderAsync(ct)) { while (await reader.ReadAsync(ct)) list.Add(JsonSerializer.Deserialize<Movie>(reader.GetString(0), Json)!); }
        await using var count = db.CreateCommand($"SELECT count(*) FROM movies WHERE {where}"); AddFilters(count, q, year, genre, mediaType, language);
        return new(list, Convert.ToInt32(await count.ExecuteScalarAsync(ct)), page, size);
    }
    static void AddFilters(NpgsqlCommand cmd, string? q, int? year, string? genre, string? mediaType, string? language) { cmd.Parameters.AddWithValue("q", q ?? ""); cmd.Parameters.AddWithValue("pattern", "%" + (q ?? "").Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%"); cmd.Parameters.AddWithValue("year", year ?? 0); cmd.Parameters.AddWithValue("genre", genre ?? ""); cmd.Parameters.AddWithValue("type", mediaType ?? ""); cmd.Parameters.AddWithValue("language", language ?? ""); }
    public async Task<Movie?> Get(long id, CancellationToken ct) { await using var cmd = db.CreateCommand("SELECT payload FROM movies WHERE id=@id AND deleted_at IS NULL"); cmd.Parameters.AddWithValue("id", id); var s = await cmd.ExecuteScalarAsync(ct); return s is string json ? JsonSerializer.Deserialize<Movie>(json, Json) : null; }
    public async Task Upsert(Movie m, CancellationToken ct)
    {
        m.UpdatedAt = DateTimeOffset.UtcNow; await using var conn = await db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO movies(id,title,release_year,genres,payload) VALUES(@id,@title,@year,@genres,@data) ON CONFLICT(id) DO UPDATE SET title=excluded.title,release_year=excluded.release_year,genres=excluded.genres,payload=excluded.payload,updated_at=now(),deleted_at=NULL", conn, tx);
        cmd.Parameters.AddWithValue("id", m.Id); cmd.Parameters.AddWithValue("title", m.Title); cmd.Parameters.AddWithValue("year", m.Year); cmd.Parameters.AddWithValue("genres", m.Genres); cmd.Parameters.AddWithValue("data", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(m, Json)); await cmd.ExecuteNonQueryAsync(ct);
        await using var log = new NpgsqlCommand("INSERT INTO audit_log(action,movie_id) VALUES('upsert',@id)", conn, tx); log.Parameters.AddWithValue("id", m.Id); await log.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<bool> Delete(long id, CancellationToken ct) { await using var cmd = db.CreateCommand("WITH removed AS (UPDATE movies SET deleted_at=now() WHERE id=@id AND deleted_at IS NULL RETURNING id) INSERT INTO audit_log(action,movie_id) SELECT 'soft_delete',id FROM removed"); cmd.Parameters.AddWithValue("id", id); return await cmd.ExecuteNonQueryAsync(ct) > 0; }
    public async Task<CatalogStats> Stats(CancellationToken ct) { await using var cmd = db.CreateCommand("SELECT count(*) FILTER(WHERE COALESCE(payload->>'mediaType','movie')='movie'),count(DISTINCT release_year),(SELECT count(DISTINCT genre) FROM movies,unnest(genres) genre WHERE deleted_at IS NULL),count(*) FILTER(WHERE payload->>'mediaType'='tv'),count(*),(SELECT count(*) FROM albums),(SELECT count(*) FROM songs) FROM movies WHERE deleted_at IS NULL"); await using var reader = await cmd.ExecuteReaderAsync(ct); await reader.ReadAsync(ct); return new((int)reader.GetInt64(0), (int)reader.GetInt64(1), (int)reader.GetInt64(2), (int)reader.GetInt64(3), (int)reader.GetInt64(4), (int)reader.GetInt64(5), (int)reader.GetInt64(6)); }
    public async Task<List<Movie>> Spotlight(string? type, CancellationToken ct) { await using var cmd = db.CreateCommand("SELECT payload FROM movies WHERE deleted_at IS NULL AND COALESCE(payload->>'poster','')<>'' AND COALESCE(payload->>'metadataStatus','enriched')<>'index-only' AND (@type='' OR COALESCE(payload->>'mediaType','movie')=@type) ORDER BY release_year DESC,(payload->>'score')::float DESC NULLS LAST,id LIMIT 20"); cmd.Parameters.AddWithValue("type", type ?? ""); var list = new List<Movie>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) list.Add(JsonSerializer.Deserialize<Movie>(reader.GetString(0), Json)!); return list; }
    public async Task<List<Movie>> Enriched(CancellationToken ct) { await using var cmd = db.CreateCommand("SELECT payload FROM movies WHERE deleted_at IS NULL AND COALESCE(payload->>'metadataStatus','enriched')<>'index-only' ORDER BY release_year DESC LIMIT 1000"); var result = new List<Movie>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(JsonSerializer.Deserialize<Movie>(reader.GetString(0), Json)!); return result; }
    public async Task<List<long>> Watchlist(string owner, CancellationToken ct) { await using var cmd = db.CreateCommand("SELECT movie_id FROM watchlist w JOIN movies m ON m.id=w.movie_id WHERE owner_id=@owner AND m.deleted_at IS NULL"); cmd.Parameters.AddWithValue("owner", owner); var ids = new List<long>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) ids.Add(r.GetInt64(0)); return ids; }
    public async Task SetWatch(string owner, long id, bool saved, CancellationToken ct) { await using var cmd = db.CreateCommand(saved ? "INSERT INTO watchlist(owner_id,movie_id) VALUES(@owner,@id) ON CONFLICT DO NOTHING" : "DELETE FROM watchlist WHERE owner_id=@owner AND movie_id=@id"); cmd.Parameters.AddWithValue("owner", owner); cmd.Parameters.AddWithValue("id", id); await cmd.ExecuteNonQueryAsync(ct); }
    public async Task<List<MovieReview>> Reviews(long id, string owner, CancellationToken ct) { await using var cmd = db.CreateCommand("SELECT id,movie_id,rating,text,created_at FROM reviews WHERE movie_id=@id AND owner_id=@owner ORDER BY created_at DESC"); cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("owner", owner); var list = new List<MovieReview>(); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new(r.GetGuid(0), r.GetInt64(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4))); return list; }
    public async Task Review(long id, string owner, ReviewInput input, CancellationToken ct) { await using var cmd = db.CreateCommand("INSERT INTO reviews(id,movie_id,owner_id,rating,text) VALUES(@rid,@id,@owner,@rating,@text)"); cmd.Parameters.AddWithValue("rid", Guid.NewGuid()); cmd.Parameters.AddWithValue("id", id); cmd.Parameters.AddWithValue("owner", owner); cmd.Parameters.AddWithValue("rating", input.Rating); cmd.Parameters.AddWithValue("text", input.Text.Trim()); await cmd.ExecuteNonQueryAsync(ct); }
}
