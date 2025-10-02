using FIAP.CloudGames.Catalog.API.Data.Search;
using FIAP.CloudGames.WebAPI.Core.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CloudGames.Catalog.API.Controllers
{
    [AllowAnonymous]
    [Route("catalog/search")]
    public class SearchController : MainController
    {
        private readonly IProductSearchService _search;

        public SearchController(IProductSearchService search)
        {
            _search = search;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? platform, [FromQuery] string? genre, [FromQuery] string? tags, [FromQuery] int page = 1, [FromQuery] int size = 20)
        {
            var tagsArr = string.IsNullOrWhiteSpace(tags) ? Array.Empty<string>() : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = await _search.SearchAsync(q ?? "", platform, genre, tagsArr, page, size);
            return Ok(result);
        }

        [HttpPost("recommendations")]
        public async Task<IActionResult> Recommendations([FromBody] RecommendRequest req)
        {
            var userId = User?.Identity?.Name ?? "anonymous";
            var result = await _search.RecommendForUserAsync(userId, req.LikedGenres ?? [], req.LikedTags ?? [], req.Platform, req.Size <= 0 ? 12 : req.Size);
            return Ok(result);
        }

        [HttpGet("metrics/popular")]
        public async Task<IActionResult> PopularMetrics()
        {
            var res = await _search.PopularMetricsAsync();
            return Ok(new
            {
                topByGenre = res.TopByGenre.Select(x => new { genre = x.Genre, count = x.Count }),
                topByPlatform = res.TopByPlatform.Select(x => new { platform = x.Platform, count = x.Count }),
                topGamesOverall = res.TopGamesOverall.Select(x => new { name = x.Game, popularity = x.Popularity })
            });
        }

        public record RecommendRequest(IEnumerable<string>? LikedGenres, IEnumerable<string>? LikedTags, string? Platform, int Size);
    }
}
