using Microsoft.Extensions.DependencyInjection;
using ProductQuotes.Interfaces;
using ProductQuotes.Services;

namespace ProductQuotes;

public static class DependencyContainer
{
    public static IServiceCollection AddProductQuotes(this IServiceCollection services)
    {
        services.AddScoped<IProductQuoteServices, SerpApiProductQuoteService>();
        return services;
    }
}
