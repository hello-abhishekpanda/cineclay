using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cineclay.Shared;
namespace Cineclay.Api;

public sealed class PublicImporter(HttpClient client, MovieStore store)
{
    static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public async Task<Movie> Tv(int id, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("Positive TVmaze ID required.");
        using var response = await client.GetAsync($"https://api.tvmaze.com/shows/{id}?embed=cast", ct); response.EnsureSuccessStatusCode(); using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var x = doc.RootElement;
        if (!DateOnly.TryParse(S(x, "premiered"), out var date) || date.Year < 1990) throw new ArgumentException("Series premiere must be dated 1990 onward.");
        var m = new Movie { Id = 2000000000L + id, ExternalId = id.ToString(), MediaType = "tv", Languages = S(x, "language") == "" ? [] : [S(x, "language")], LanguageSource = "TVmaze original language", Title = S(x, "name"), Year = date.Year, ReleaseDate = date.ToString("yyyy-MM-dd"), Source = "TVmaze", SourceUrl = S(x, "url"), Summary = WebUtility.HtmlDecode(Regex.Replace(S(x, "summary"), "<[^>]+>", " ")).Trim(), Genres = x.GetProperty("genres").EnumerateArray().Select(g => g.GetString()!).ToArray(), Cast = x.GetProperty("_embedded").GetProperty("cast").EnumerateArray().Take(20).Select(c => S(c.GetProperty("person"), "name")).ToArray() };
        if (m.Genres.Length == 0) m.Genres = ["Uncategorized"]; if (m.Cast.Length == 0) m.Cast = ["Not indexed"]; if (m.Summary == "") m.Summary = "Synopsis not available.";
        if (x.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object) m.Poster = S(image, "original");
        if (x.TryGetProperty("rating", out var rating) && rating.TryGetProperty("average", out var average) && average.ValueKind == JsonValueKind.Number) m.Score = average.GetDouble();
        var site = S(x, "officialSite"); var channel = x.TryGetProperty("webChannel", out var web) && web.ValueKind == JsonValueKind.Object ? web : x.TryGetProperty("network", out var network) ? network : default; if (Movie.Https(site) && channel.ValueKind == JsonValueKind.Object) m.Providers.Add(new() { Name = S(channel, "name"), Url = site, Region = "GLOBAL", Type = "official-title", CheckedAt = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        var old = await store.Get(m.Id, ct); if (old != null) { m.Trailer = old.Trailer; m.Providers.AddRange(old.Providers.Where(p => p.Type == "manual")); m.Review = old.Review; }
        var error = m.Validate(); if (error != null) throw new ArgumentException(error); await store.Upsert(m, ct); return m;
    }
}
