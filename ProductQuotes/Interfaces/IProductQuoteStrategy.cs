using ProductQuotes.Models;

namespace ProductQuotes.Interfaces;

public interface IProductQuoteStrategy
{
    Task<List<ProductQuoteDto>> GetProductQuotes(
        string provider,
        string productName,
        string country    = "mx",
        string language   = "es",
        int    pageNumber = 1,
        int    pageSize   = 10);
}
