using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductQuotes;
using ProductQuotes.Interfaces;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProductQuotes();

var host = builder.Build();
var quote = host.Services.GetRequiredService<IProductQuoteServices>();

var products = await quote.GetProductQuotes("facia delantera");

foreach (var product in products)
{
    Console.WriteLine(product.Url);
    Console.WriteLine(product.ProductName);
    Console.WriteLine(product.Price);
}