namespace FIAP.CloudGames.Catalog.API.Data.Search
{
    public class SearchContracts
    {
        public enum SortBy
        { Popularity, Metacritic, Recent }

        public record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int Size, long Total);
    }
}