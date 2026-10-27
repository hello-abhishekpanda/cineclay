using System.ComponentModel.DataAnnotations;
namespace Cineclay.Shared;
public sealed class Movie {
 public long Id {get;set;}
 [RegularExpression("^(movie|tv)$")] public string MediaType {get;set;}="movie";
 public string MetadataStatus {get;set;}="enriched";
 public string? SourceUrl {get;set;}
 public string? ReleaseDate {get;set;}
 public string? ExternalId {get;set;}
 [Required,MaxLength(20)] public string[] Languages {get;set;}=[];
 public string? LanguageSource {get;set;}
 [Required,StringLength(200,MinimumLength=1)] public string Title {get;set;}="";
 [Range(1990,9999)] public int Year {get;set;}=2026;
 [Required,MinLength(1),MaxLength(20)] public string[] Genres {get;set;}=[];
 [Required,MinLength(1),MaxLength(100)] public string[] Cast {get;set;}=[];
 [StringLength(300)] public string Director {get;set;}="";
 [Range(0,1500)] public int? Runtime {get;set;}
 [StringLength(2000)] public string Poster {get;set;}="";
 [Required,StringLength(10000)] public string Summary {get;set;}="";
 [RegularExpression("^[a-zA-Z0-9_-]{11}$|^$")] public string Trailer {get;set;}="";
 [Range(0,10)] public double? Score {get;set;}
 public List<WatchProvider> Providers {get;set;}=[];
 public string Source {get;set;}="Manual";
 public DateTimeOffset? UpdatedAt {get;set;}
 public bool Featured {get;set;}
 public string? Review {get;set;}
 public string? Validate(){
  var errors=new List<ValidationResult>();if(!Validator.TryValidateObject(this,new ValidationContext(this),errors,true))return string.Join("; ",errors.Select(x=>x.ErrorMessage));
  if(Languages.Any(l=>string.IsNullOrWhiteSpace(l)||l.Length>80))return "Languages must be nonblank names up to 80 characters.";
  if(Genres.Any(string.IsNullOrWhiteSpace)||Cast.Any(string.IsNullOrWhiteSpace))return "Genres and cast cannot be blank.";
  if(!string.IsNullOrEmpty(Poster)&&!Poster.StartsWith("/posters/")&&!Https(Poster))return "Poster must be an HTTPS URL.";
  if(Providers.Count>5000||Providers.Any(p=>string.IsNullOrWhiteSpace(p.Name)||!Https(p.Url)||!(System.Text.RegularExpressions.Regex.IsMatch(p.Region,"^[A-Z]{2}$")||p.Region=="GLOBAL"&&p.Type=="official-title")))return "Providers need a name, HTTPS URL and two-letter region.";
  return null;
 }
 public static bool Https(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme=="https"&&string.IsNullOrEmpty(u.UserInfo);
}
public sealed class WatchProvider {
 public string Name {get;set;}="";
 public string Region {get;set;}="IN";
 public string Url {get;set;}="";
 public string Type {get;set;}="subscription";
 public string? CheckedAt {get;set;}
}
public record MoviePage(List<Movie> Items,int Total,int Page,int PageSize);
public record CatalogStats(int Movies,int Years,int Genres,int TvSeries=0,int Total=0);
public record ImportResult(int Imported,int[] FailedIds,int Page,int TotalPages);
public sealed class ReviewInput {
 [Range(1,5)] public int Rating {get;set;}=5;
 [Required,StringLength(2000,MinimumLength=1)] public string Text {get;set;}="";
}
public record MovieReview(Guid Id,long MovieId,int Rating,string Text,DateTimeOffset CreatedAt);
