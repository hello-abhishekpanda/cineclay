using System.Net.Http.Headers;
using System.Text.Json;
using Cineclay.Shared;
namespace Cineclay.Api;

public sealed class TmdbImporter(HttpClient client, IConfiguration config, MovieStore store)
{
    async Task<JsonDocument> Get(string path, CancellationToken ct)
    {
        var token = config["TMDB_TOKEN"]; if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("TMDB_TOKEN is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.themoviedb.org/3/" + path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, ct); response.EnsureSuccessStatusCode(); return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static IEnumerable<JsonElement> A(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];
    public async Task<Movie> Movie(int id, CancellationToken ct)
    {
        using var doc = await Get($"movie/{id}?append_to_response=credits,videos,watch/providers", ct); var r = doc.RootElement;
        if (!DateOnly.TryParse(S(r, "release_date"), out var date) || date.Year < 1990) throw new ArgumentException("Only movies dated 1990 onward can be imported.");
        var m = new Movie { Id = id, Title = S(r, "title"), Year = date.Year, Summary = S(r, "overview"), Genres = A(r, "genres").Select(g => S(g, "name")).ToArray(), Runtime = r.TryGetProperty("runtime", out var runtime) && runtime.ValueKind == JsonValueKind.Number ? runtime.GetInt32() : null, Source = "TMDB", Score = r.TryGetProperty("vote_average", out var score) ? score.GetDouble() : null };
        var code = S(r, "original_language"); try { if (code != "") m.Languages = [System.Globalization.CultureInfo.GetCultureInfo(code).EnglishName]; } catch (System.Globalization.CultureNotFoundException) { }
        var poster = S(r, "poster_path"); m.Poster = string.IsNullOrWhiteSpace(poster) ? "" : "https://image.tmdb.org/t/p/w500" + poster;
        if (r.TryGetProperty("credits", out var credits)) { m.Cast = A(credits, "cast").Take(20).Select(c => S(c, "name")).ToArray(); m.Director = string.Join(", ", A(credits, "crew").Where(c => S(c, "job") == "Director").Select(c => S(c, "name"))); }
        if (r.TryGetProperty("videos", out var videos)) m.Trailer = A(videos, "results").Where(v => S(v, "site") == "YouTube" && S(v, "type") == "Trailer" && v.TryGetProperty("official", out var official) && official.ValueKind == JsonValueKind.True).Select(v => S(v, "key")).FirstOrDefault() ?? "";
        if (r.TryGetProperty("watch/providers", out var watch) && watch.TryGetProperty("results", out var results)) foreach (var country in results.EnumerateObject())
            {
                var link = S(country.Value, "link"); if (!MovieUrl(link)) continue;
                foreach (var type in new[] { "flatrate", "rent", "buy", "free", "ads" }) foreach (var p in A(country.Value, type)) m.Providers.Add(new() { Name = S(p, "provider_name"), Region = country.Name, Type = type, Url = link, CheckedAt = DateTime.UtcNow.ToString("yyyy-MM-dd") });
            }
        // Keep human-maintained direct links and editorial review across metadata refreshes.
        var old = await store.Get(m.Id, ct); if (old != null) { m.Providers.AddRange(old.Providers.Where(p => p.Type == "manual")); m.Review = old.Review; m.Featured = old.Featured; }
        if (m.Cast.Length == 0) m.Cast = ["Cast not yet announced"]; if (m.Genres.Length == 0) m.Genres = ["Uncategorized"]; if (string.IsNullOrWhiteSpace(m.Summary)) m.Summary = "Synopsis not yet announced.";
        var error = m.Validate(); if (error != null) throw new ArgumentException(error); await store.Upsert(m, ct); return m;
    }
    static bool MovieUrl(string u) => Cineclay.Shared.Movie.Https(u);
    public async Task<ImportResult> Year(int year, int page, CancellationToken ct)
    {
        using var doc = await Get($"discover/movie?primary_release_year={year}&page={page}&include_adult=false&sort_by=popularity.desc", ct); var failed = new List<int>(); var count = 0;
        foreach (var r in A(doc.RootElement, "results")) { var id = r.GetProperty("id").GetInt32(); try { await Movie(id, ct); count++; } catch (HttpRequestException) { failed.Add(id); } catch (ArgumentException) { failed.Add(id); } await Task.Delay(125, ct); }
        return new(count, failed.ToArray(), page, doc.RootElement.GetProperty("total_pages").GetInt32());
    }
}
