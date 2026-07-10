using ProductQuotes.Models;

namespace ProductQuotes.Interfaces;

public interface IProductQuoteServices
{
    Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country = "mx", string language = "es", int pageNumber = 1, int pageSize = 10);
}
