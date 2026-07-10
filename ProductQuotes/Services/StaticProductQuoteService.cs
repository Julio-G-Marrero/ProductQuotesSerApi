using ProductQuotes.Interfaces;
using ProductQuotes.Models;

namespace ProductQuotes.Services;

/// <summary>
/// Implementación estática de <see cref="IProductQuoteServices"/> que devuelve productos
/// hardcodeados. No requiere API key ni conexión a Internet.
/// Útil para pruebas, demostraciones y desarrollo local.
/// </summary>
public sealed class StaticProductQuoteService : IProductQuoteServices
{
    public const string Key = "static";

    private static readonly IReadOnlyList<ProductQuoteDto> _catalog =
    [
        new("Facia Delantera Nissan Versa 2015-2019",   "MercadoLibre",        1_250.00m, "https://static.ejemplo.com/p/1"),
        new("Facia Delantera Tsuru GS III",             "Refaccionaria López",   890.00m, "https://static.ejemplo.com/p/2"),
        new("Bumper Delantero Jetta A4 Clásico",        "Walmart México",       2_100.00m, "https://static.ejemplo.com/p/3"),
        new("Facia Delantera Aveo 2012-2017",           "Amazon México",        1_750.00m, "https://static.ejemplo.com/p/4"),
        new("Fascia Delantera Spark GT 2011-2016",      "MercadoLibre",           980.00m, "https://static.ejemplo.com/p/5"),
        new("Bumper Delantero Corolla 2014-2018",       "Coppel",               3_200.00m, "https://static.ejemplo.com/p/6"),
        new("Facia Delantera Sentra B16 2013-2019",     "Amazon México",        1_400.00m, "https://static.ejemplo.com/p/7"),
        new("Fascia Delantera Cavalier 2018-2022",      "Walmart México",       2_450.00m, "https://static.ejemplo.com/p/8"),
        new("Bumper Delantero Escape 2013-2016",        "MercadoLibre",         1_900.00m, "https://static.ejemplo.com/p/9"),
        new("Facia Delantera Ranger XL 2020-2023",      "Autozone MX",          4_100.00m, "https://static.ejemplo.com/p/10"),
        new("Fascia Delantera HRV 2016-2021",           "Amazon México",        2_800.00m, "https://static.ejemplo.com/p/11"),
        new("Bumper Delantero CX-5 2017-2022",          "MercadoLibre",         3_500.00m, "https://static.ejemplo.com/p/12"),
    ];

    public Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country = "mx", string language = "es",
        int pageNumber = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("productName no puede estar vacío.", nameof(productName));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "pageNumber debe ser >= 1.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize debe ser >= 1.");

        var page = _catalog
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(page);
    }
}
