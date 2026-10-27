using Cineclay.Shared;
using Npgsql;

namespace Cineclay.Api;

public sealed class AlbumStore(NpgsqlDataSource db)
{
    public async Task Initialize(CancellationToken cancellationToken)
    {
        await using var count = db.CreateCommand("SELECT count(*) FROM albums");
        if ((long)(await count.ExecuteScalarAsync(cancellationToken))! != 0)
        {
            return;
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "albums.seed.json");
        var albums = System.Text.Json.JsonSerializer.Deserialize<List<Album>>(
            await File.ReadAllTextAsync(seedPath, cancellationToken),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? [];

        await UpsertMany(albums, cancellationToken);
    }

    public async Task<AlbumPage> List(
        string? query,
        string? language,
        int? year,
        bool playableOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string where = "(@query = '' OR a.search_vector @@ websearch_to_tsquery('simple', @query) "
            + "OR lower(a.title) LIKE @prefix ESCAPE '\\' OR EXISTS (SELECT 1 FROM songs s "
            + "WHERE s.album_id = a.id AND s.search_vector @@ websearch_to_tsquery('simple', @query))) "
            + "AND (@language = '' OR a.language = @language) AND (@year = 0 OR a.release_year = @year) "
            + "AND (NOT @playable OR EXISTS (SELECT 1 FROM songs preview WHERE preview.album_id = a.id))";

        await using var command = db.CreateCommand($"""
            WITH filtered AS (
                SELECT a.*
                FROM albums a
                WHERE {where}
                ORDER BY EXISTS (SELECT 1 FROM songs preview WHERE preview.album_id = a.id) DESC,
                         a.release_year DESC, a.title
                LIMIT @page_size OFFSET @offset
            )
            SELECT a.id, a.title, a.release_year, a.language, a.artist, a.cover_url, a.source_url,
                   s.youtube_video_id, s.title, s.artist, s.thumbnail_url, s.published_at
            FROM filtered a
            LEFT JOIN songs s ON s.album_id = a.id
            ORDER BY a.release_year DESC, a.title, s.title
            """);
        AddFilters(command, query, language, year, playableOnly);
        command.Parameters.AddWithValue("page_size", pageSize);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);

        var albums = new Dictionary<long, Album>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt64(0);
                if (!albums.TryGetValue(id, out var album))
                {
                    album = new Album
                    {
                        Id = id,
                        Title = reader.GetString(1),
                        Year = reader.GetInt32(2),
                        Language = reader.GetString(3),
                        Artist = reader.GetString(4),
                        Cover = reader.GetString(5),
                        SourceUrl = reader.GetString(6)
                    };
                    albums[id] = album;
                }

                if (!reader.IsDBNull(7))
                {
                    album.Songs.Add(new Song
                    {
                        YouTubeVideoId = reader.GetString(7),
                        Title = reader.GetString(8),
                        Artist = reader.GetString(9),
                        Thumbnail = reader.GetString(10),
                        PublishedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)
                    });
                }
            }
        }

        await using var count = db.CreateCommand($"SELECT count(*) FROM albums a WHERE {where}");
        AddFilters(count, query, language, year, playableOnly);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));
        return new AlbumPage(albums.Values.ToList(), total, page, pageSize);
    }

    public async Task Upsert(Album album, CancellationToken cancellationToken)
    {
        await UpsertMany([album], cancellationToken);
    }

    public async Task UpsertMany(IReadOnlyCollection<Album> albums, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var album in albums)
        {
            await Upsert(album, connection, transaction, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    static async Task Upsert(
        Album album,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using (var command = new NpgsqlCommand("""
            INSERT INTO albums(id, title, release_year, language, artist, cover_url, source_url)
            VALUES(@id, @title, @year, @language, @artist, @cover, @source)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                release_year = excluded.release_year,
                language = excluded.language,
                artist = excluded.artist,
                cover_url = excluded.cover_url,
                source_url = excluded.source_url,
                updated_at = now()
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("id", album.Id);
            command.Parameters.AddWithValue("title", album.Title.Trim());
            command.Parameters.AddWithValue("year", album.Year);
            command.Parameters.AddWithValue("language", album.Language.Trim());
            command.Parameters.AddWithValue("artist", album.Artist.Trim());
            command.Parameters.AddWithValue("cover", album.Cover);
            command.Parameters.AddWithValue("source", album.SourceUrl);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var delete = new NpgsqlCommand(
            "DELETE FROM songs WHERE album_id = @album",
            connection,
            transaction))
        {
            delete.Parameters.AddWithValue("album", album.Id);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var song in album.Songs)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO songs(youtube_video_id, album_id, title, artist, thumbnail_url, published_at)
                VALUES(@video, @album, @title, @artist, @thumbnail, @published)
                ON CONFLICT(youtube_video_id) DO UPDATE SET
                    album_id = excluded.album_id,
                    title = excluded.title,
                    artist = excluded.artist,
                    thumbnail_url = excluded.thumbnail_url,
                    published_at = excluded.published_at
                """, connection, transaction);
            command.Parameters.AddWithValue("video", song.YouTubeVideoId);
            command.Parameters.AddWithValue("album", album.Id);
            command.Parameters.AddWithValue("title", song.Title.Trim());
            command.Parameters.AddWithValue("artist", song.Artist.Trim());
            command.Parameters.AddWithValue("thumbnail", song.Thumbnail);
            command.Parameters.AddWithValue("published", (object?)song.PublishedAt ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    static void AddFilters(
        NpgsqlCommand command,
        string? query,
        string? language,
        int? year,
        bool playableOnly)
    {
        var normalizedQuery = query?.Trim() ?? "";
        command.Parameters.AddWithValue("query", normalizedQuery);
        command.Parameters.AddWithValue(
            "prefix",
            $"{normalizedQuery.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%");
        command.Parameters.AddWithValue("language", language?.Trim() ?? "");
        command.Parameters.AddWithValue("year", year ?? 0);
        command.Parameters.AddWithValue("playable", playableOnly);
    }
}
