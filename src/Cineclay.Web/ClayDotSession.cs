using Cineclay.Shared;
namespace Cineclay.Web;

public sealed class ClayDotSession { public event Action<Movie>? Requested; public void Open(Movie movie) => Requested?.Invoke(movie); }
