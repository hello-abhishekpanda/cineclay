using Cineclay.Shared;
namespace Cineclay.Web;
public sealed class Visitor(IHttpContextAccessor context){public string Id {get;}=context.HttpContext?.Items["visitor"]?.ToString()??throw new InvalidOperationException("Visitor context missing.");}
public sealed class CatalogClient(HttpClient http){
 public async Task<MoviePage> List(
    string q = "",
    string year = "",
    string genre = "",
    string sort = "curated",
    int page = 1,
    int size = 24,
    string mediaType = "",
    string language = "")
{
    var parameters = new List<string>
    {
        $"sort={Uri.EscapeDataString(sort)}",
        $"page={page}",
        $"pageSize={size}"
    };

    void Add(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(
                $"{name}={Uri.EscapeDataString(value)}");
        }
    }

    Add("q", q);
    Add("year", year);
    Add("genre", genre);
    Add("mediaType", mediaType);
    Add("language", language);

    var url = $"/api/movies?{string.Join("&", parameters)}";

    return await http.GetFromJsonAsync<MoviePage>(url)
        ?? new MoviePage([], 0, page, size);
}
 public async Task<List<Movie>> Spotlight(string mediaType="")=>await http.GetFromJsonAsync<List<Movie>>($"/api/spotlight?mediaType={mediaType}")??[];
 public async Task<string> ImportTv(int id){using var response=await http.PostAsync($"/api/admin/import/tvmaze/{id}",null);await Check(response);return await response.Content.ReadAsStringAsync();}
 public async Task<List<Movie>> AssistantCatalog()=>await http.GetFromJsonAsync<List<Movie>>("/api/claydot/catalog")??[];
 public async Task<CatalogStats> Stats()=>await http.GetFromJsonAsync<CatalogStats>("/api/stats")??new(0,0,0);
 public async Task Save(Movie m){using var r=await http.PutAsJsonAsync($"/api/admin/movies/{m.Id}",m);await Check(r);}
 public async Task Delete(long id){using var r=await http.DeleteAsync($"/api/admin/movies/{id}");await Check(r);}
 public async Task<string> Import(int value,bool year,int page){using var r=await http.PostAsync(year?$"/api/admin/import/year/{value}?page={page}":$"/api/admin/import/movie/{value}",null);await Check(r);return await r.Content.ReadAsStringAsync();}
 public async Task<List<long>> Saved(string owner)=>await http.GetFromJsonAsync<List<long>>($"/api/admin/watchlist/{owner}")??[];
 public async Task Watch(string owner,long id,bool save){using var r=await http.PutAsync($"/api/admin/watchlist/{owner}/{id}?saved={save}",null);await Check(r);}
 public async Task<List<MovieReview>> Reviews(string owner,long id)=>await http.GetFromJsonAsync<List<MovieReview>>($"/api/admin/reviews/{owner}/{id}")??[];
 public async Task Review(string owner,long id,ReviewInput review){using var r=await http.PostAsJsonAsync($"/api/admin/reviews/{owner}/{id}",review);await Check(r);}
 static async Task Check(HttpResponseMessage r){if(!r.IsSuccessStatusCode)throw new HttpRequestException($"Request failed ({(int)r.StatusCode}): {await r.Content.ReadAsStringAsync()}");}
}
