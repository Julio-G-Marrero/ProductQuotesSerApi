namespace ProductQuotes;

public interface IProductQuoteProvider
{
    Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country, string language, int pageNumber, int pageSize);
}
