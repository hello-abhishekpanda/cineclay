using Cineclay.Shared;

var count = 0;

void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception(name);
    }

    count++;
}

Movie Film() => new()
{
    Id = 1,
    Title = "Test",
    Summary = "Synopsis",
    Year = 1990,
    Genres = ["Drama"],
    Cast = ["Actor", "Actress"]
};

var movie = Film();
Check(movie.Validate() is null, "Valid movie");
movie.Year = 1989;
Check(movie.Validate() is not null, "Reject pre-1990");
movie.Year = DateTime.UtcNow.Year + 1;
Check(movie.Validate() is not null, "Reject future year");
movie.Year = DateTime.UtcNow.Year;
movie.Poster = "javascript:alert(1)";
Check(movie.Validate() is not null, "Reject unsafe poster scheme");
movie.Poster = "http://example.com/a.jpg";
Check(movie.Validate() is not null, "Require HTTPS");
movie.Poster = "/posters/dune.jpg";
Check(movie.Validate() is null, "Allow bundled artwork");
movie.Trailer = "../../unsafe";
Check(movie.Validate() is not null, "Reject unsafe trailer ID");
movie.Trailer = "Way9Dexny3w";
Check(movie.Validate() is null, "Allow video ID");
movie.Providers.Add(new WatchProvider { Name = "Example", Url = "https://example.com/title/1", Region = "IN" });
Check(movie.Validate() is null, "Allow direct OTT link");
movie.Providers[0].Region = "India";
Check(movie.Validate() is not null, "Validate country code");
movie.Providers[0].Region = "IN";
movie.Providers[0].Url = "https://user:pass@example.com";
Check(movie.Validate() is not null, "Reject credentials in URLs");
movie = Film();
movie.Cast = [""];
Check(movie.Validate() is not null, "Reject empty cast names");
movie = Film();
movie.MediaType = "tv";
movie.Providers =
[
    new WatchProvider
    {
        Name = "Netflix",
        Region = "GLOBAL",
        Type = "official-title",
        Url = "https://www.netflix.com/title/81231974"
    }
];
Check(movie.Validate() is null, "Global official title page accepted");
movie.Providers[0].Type = "subscription";
Check(movie.Validate() is not null, "Global link must not claim regional subscription availability");
movie = Film();
movie.MediaType = "episode";
Check(movie.Validate() is not null, "Unsupported media type rejected");
movie = Film();
movie.Languages = ["Hindi", "Tamil"];
Check(movie.Validate() is null, "Multiple language tags accepted");
movie.Languages = [""];
Check(movie.Validate() is not null, "Blank language tag rejected");

var album = new Album
{
    Id = 1,
    Title = "Odia Songs of 2022",
    Language = "Odia",
    Year = 2022,
    Cover = "https://i.ytimg.com/vi/I6Bs8xOwr4g/hqdefault.jpg",
    SourceUrl = "https://www.youtube.com/watch?v=I6Bs8xOwr4g",
    Songs =
    [
        new Song
        {
            Title = "Saami Saami",
            Artist = "Aseema Panda",
            YouTubeVideoId = "I6Bs8xOwr4g"
        }
    ]
};
Check(album.Validate() is null, "Valid album");
album.Year = DateTime.UtcNow.Year + 1;
Check(album.Validate() is not null, "Reject future album year");
album.Year = 2024;
album.Cover = "";
album.SourceUrl = "https://www.youtube.com/results?search_query=Odia+album";
album.Songs = [];
Check(album.Validate() is null, "Allow metadata album with YouTube search link");
album.ExternalId = "not-a-musicbrainz-id";
Check(album.Validate() is not null, "Reject invalid MusicBrainz release ID");

Console.WriteLine($"{count} model and URL validation checks passed.");
