using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Cineclay.Shared;

namespace Cineclay.Api;

public sealed class TmdbImporter(HttpClient client, IConfiguration config, MovieStore store)
{
    static string StringValue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    static IEnumerable<JsonElement> ArrayValue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    public async Task<Movie> Movie(int id, CancellationToken cancellationToken)
    {
        using var document = await Get(
            $"movie/{id}?append_to_response=credits,videos,watch/providers",
            cancellationToken);
        var root = document.RootElement;

        if (!DateOnly.TryParse(StringValue(root, "release_date"), out var date) || date.Year < 1990)
        {
            throw new ArgumentException("Only movies dated 1990 onward can be imported.");
        }

        var movie = new Movie
        {
            Id = id,
            Title = StringValue(root, "title"),
            Year = date.Year,
            Summary = StringValue(root, "overview"),
            Genres = ArrayValue(root, "genres").Select(genre => StringValue(genre, "name")).ToArray(),
            Runtime = root.TryGetProperty("runtime", out var runtime)
                && runtime.ValueKind == JsonValueKind.Number
                    ? runtime.GetInt32()
                    : null,
            Source = "TMDB",
            Score = root.TryGetProperty("vote_average", out var score) ? score.GetDouble() : null
        };

        var languageCode = StringValue(root, "original_language");
        try
        {
            if (languageCode != "")
            {
                movie.Languages = [CultureInfo.GetCultureInfo(languageCode).EnglishName];
            }
        }
        catch (CultureNotFoundException)
        {
            // Preserve an empty language list when TMDB returns an unknown culture code.
        }

        var poster = StringValue(root, "poster_path");
        movie.Poster = string.IsNullOrWhiteSpace(poster)
            ? ""
            : $"https://image.tmdb.org/t/p/w500{poster}";

        if (root.TryGetProperty("credits", out var credits))
        {
            movie.Cast = ArrayValue(credits, "cast")
                .Take(20)
                .Select(castMember => StringValue(castMember, "name"))
                .ToArray();
            movie.Director = string.Join(
                ", ",
                ArrayValue(credits, "crew")
                    .Where(crewMember => StringValue(crewMember, "job") == "Director")
                    .Select(crewMember => StringValue(crewMember, "name")));
        }

        if (root.TryGetProperty("videos", out var videos))
        {
            movie.Trailer = ArrayValue(videos, "results")
                .Where(video =>
                    StringValue(video, "site") == "YouTube"
                    && StringValue(video, "type") == "Trailer"
                    && video.TryGetProperty("official", out var official)
                    && official.ValueKind == JsonValueKind.True)
                .Select(video => StringValue(video, "key"))
                .FirstOrDefault() ?? "";
        }

        if (root.TryGetProperty("watch/providers", out var watch)
            && watch.TryGetProperty("results", out var results))
        {
            foreach (var country in results.EnumerateObject())
            {
                var link = StringValue(country.Value, "link");
                if (!MovieUrl(link))
                {
                    continue;
                }

                foreach (var type in new[] { "flatrate", "rent", "buy", "free", "ads" })
                {
                    foreach (var provider in ArrayValue(country.Value, type))
                    {
                        movie.Providers.Add(new WatchProvider
                        {
                            Name = StringValue(provider, "provider_name"),
                            Region = country.Name,
                            Type = type,
                            Url = link,
                            CheckedAt = DateTime.UtcNow.ToString("yyyy-MM-dd")
                        });
                    }
                }
            }
        }

        // Keep human-maintained direct links and editorial review across metadata refreshes.
        var existing = await store.Get(movie.Id, cancellationToken);
        if (existing is not null)
        {
            movie.Providers.AddRange(existing.Providers.Where(provider => provider.Type == "manual"));
            movie.Review = existing.Review;
            movie.Featured = existing.Featured;
        }

        if (movie.Cast.Length == 0)
        {
            movie.Cast = ["Cast not yet announced"];
        }

        if (movie.Genres.Length == 0)
        {
            movie.Genres = ["Uncategorized"];
        }

        if (string.IsNullOrWhiteSpace(movie.Summary))
        {
            movie.Summary = "Synopsis not yet announced.";
        }

        var error = movie.Validate();
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        await store.Upsert(movie, cancellationToken);
        return movie;
    }

    public async Task<ImportResult> Year(int year, int page, CancellationToken cancellationToken)
    {
        using var document = await Get(
            $"discover/movie?primary_release_year={year}&page={page}&include_adult=false&sort_by=popularity.desc",
            cancellationToken);
        var failed = new List<int>();
        var imported = 0;

        foreach (var result in ArrayValue(document.RootElement, "results"))
        {
            var id = result.GetProperty("id").GetInt32();
            try
            {
                await Movie(id, cancellationToken);
                imported++;
            }
            catch (HttpRequestException)
            {
                failed.Add(id);
            }
            catch (ArgumentException)
            {
                failed.Add(id);
            }

            await Task.Delay(125, cancellationToken);
        }

        return new ImportResult(
            imported,
            failed.ToArray(),
            page,
            document.RootElement.GetProperty("total_pages").GetInt32());
    }

    async Task<JsonDocument> Get(string path, CancellationToken cancellationToken)
    {
        var token = config["TMDB_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("TMDB_TOKEN is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.themoviedb.org/3/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    static bool MovieUrl(string url) => Cineclay.Shared.Movie.Https(url);
}
