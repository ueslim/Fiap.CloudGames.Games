using FIAP.CloudGames.Core.DomainObjects;

namespace FIAP.CloudGames.Catalog.API.Models
{
    public class Product : Entity, IAggregateRoot
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }
        public decimal Value { get; set; }
        public DateTime DateRegister { get; set; }
        public string Image { get; set; }
        public int StockQuantity { get; set; }

   
        public string Genre { get; set; }          
        public string Platform { get; set; }       
        public string[] Tags { get; set; } = [];   
        public decimal? Metacritic { get; set; }   
        public double? UserRating { get; set; }    
        public DateTime? ReleaseDate { get; set; }
        public long PopularityScore { get; set; }  
        public long Sales { get; set; }            
        public long Views { get; set; }            

        public void DecrementStock(int quantity)
        {
            if (StockQuantity >= quantity)
                StockQuantity -= quantity;
        }

        public bool IsAvailable(int quantity)
        {
            return Active && StockQuantity >= quantity;
        }
    }
}