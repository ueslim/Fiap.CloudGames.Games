namespace FIAP.CloudGames.Catalog.API.Models
{
    public class ProductLog
    {
        public ProductLog(string id, string name)
        {
            Id = id;
            Name = name;
            this.DataProduct = DateTime.Now;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime DataProduct { get; set; }
    }
}
