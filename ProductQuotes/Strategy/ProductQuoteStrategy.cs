using Microsoft.Extensions.DependencyInjection;
using ProductQuotes.Interfaces;
using ProductQuotes.Models;
using ProductQuotes.Services;

namespace ProductQuotes.Strategy;

/// <summary>
/// Orquestador del patrón Strategy. Recibe la clave del proveedor como string,
/// verifica que exista, y resuelve la implementación concreta desde el contenedor de DI.
/// </summary>
public sealed class ProductQuoteStrategy : IProductQuoteStrategy
{
    private readonly IServiceProvider _serviceProvider;

    public ProductQuoteStrategy(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <param name="provider">Clave del proveedor. Usar la constante Key de cada servicio.</param>
    /// <exception cref="ArgumentException">Si no existe ningún proveedor registrado con esa clave.</exception>
    public Task<List<ProductQuoteDto>> GetProductQuotes(
        string provider,
        string productName,
        string country    = "mx",
        string language   = "es",
        int    pageNumber = 1,
        int    pageSize   = 10)
    {
        var strategy = _serviceProvider.GetKeyedService<IProductQuoteServices>(provider);

        if (strategy is null)
            throw new ArgumentException(
                $"No provider registered with key '{provider}'. " +
                $"Use one of the available Key constants (e.g. {nameof(SerpApiProductQuoteService)}.Key).",
                nameof(provider));

        return strategy.GetProductQuotes(productName, country, language, pageNumber, pageSize);
    }
}
