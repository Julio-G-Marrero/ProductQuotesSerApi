using Microsoft.Extensions.DependencyInjection;

namespace ProductQuotes;

public static class DependencyContainer
{
    public static IServiceCollection AddProductQuotes(this IServiceCollection services)
    {
        services.AddScoped<IProductQuoteProvider, SerpApiProductQuoteProvider>();
        return services;
    }
}
