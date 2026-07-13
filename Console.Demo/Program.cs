using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductQuotes;
using ProductQuotes.Interfaces;
using ProductQuotes.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProductQuotes();
var host = builder.Build();

var strategy = host.Services.GetRequiredService<IProductQuoteStrategy>();

Console.WriteLine("=== Provider: Static ===");
var staticProducts = await strategy.GetProductQuotes(StaticProductQuoteService.Key, "facia delantera");
foreach (var p in staticProducts)
{
    Console.WriteLine($"  [{p.Store}] {p.ProductName} — ${p.Price:N2} MXN");
    Console.WriteLine($"  {p.Url}");
    Console.WriteLine();
}

Console.WriteLine("=== Provider: SerpApi ===");
var serpApiProducts = await strategy.GetProductQuotes(SerpApiProductQuoteService.Key, "facia delantera", pageSize: 3);
foreach (var p in serpApiProducts)
{
    Console.WriteLine($"  [{p.Store}] {p.ProductName} — ${p.Price:N2} MXN");
    Console.WriteLine($"  Url: {p.Url}");
    Console.WriteLine($"  ImageUrl: {p.ImageUrl}");
    Console.WriteLine($"  ImmersiveProductPageToken: {(string.IsNullOrEmpty(p.ImmersiveProductPageToken) ? "(vacío)" : p.ImmersiveProductPageToken[..Math.Min(30, p.ImmersiveProductPageToken.Length)] + "...")}");
    Console.WriteLine();
}

Console.WriteLine("=== Provider: Aldo Autopartes ===");
try
{
    var aldoProducts = await strategy.GetProductQuotes(AldoAutopartesProductQuoteService.Key, "Chevrolet|Aveo|2018|Cofre", pageSize: 10);
    Console.WriteLine($"  ({aldoProducts.Count} resultados)");
    foreach (var p in aldoProducts)
    {
        Console.WriteLine($"  [{p.Store}] {p.ProductName} — costo real: ${p.Price:N2} MXN");
        Console.WriteLine($"  Url: {p.Url}");
        Console.WriteLine($"  ImageUrl: {p.ImageUrl}");
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  ERROR: {ex.GetType().Name}: {ex.Message}");
}
