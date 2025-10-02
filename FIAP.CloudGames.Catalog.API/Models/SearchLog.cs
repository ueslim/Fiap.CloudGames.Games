namespace FIAP.CloudGames.Catalog.API.Models
{
    public class SearchLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Action { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
