using FIAP.CloudGames.Catalog.API.Configuration;

namespace FIAP.CloudGames.Catalog.API.Configuration
{
    public interface IElasticSettings
    {
        string Uri { get; set; }
        string Username { get; set; }
        string Password { get; set; }
    }

    public class ElasticSettings : IElasticSettings
    {
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    // Cloud
    //public interface IElasticSettings
    //{
    //    string ApiKey { get; set; }
    //    string CloudId { get; set; }
    //}

    //public class ElasticSettings : IElasticSettings
    //{
    //    public string ApiKey { get; set; }
    //    public string CloudId { get; set; }
    //}
}
