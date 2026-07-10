using Microsoft.Extensions.DependencyInjection;
using ProductQuotes.Interfaces;
using ProductQuotes.Services;
using ProductQuotes.Strategy;

namespace ProductQuotes;

public static class DependencyContainer
{
    public static IServiceCollection AddProductQuotes(this IServiceCollection services)
    {
        services.AddKeyedScoped<IProductQuoteServices, SerpApiProductQuoteService>(SerpApiProductQuoteService.Key);
        services.AddKeyedScoped<IProductQuoteServices, StaticProductQuoteService>(StaticProductQuoteService.Key);

        services.AddScoped<IProductQuoteStrategy, ProductQuoteStrategy>();

        return services;
    }
}
